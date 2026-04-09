# BellumCivile ↔ AIInfluence Bridge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bridge BellumCivile faction/civil-war data into AIInfluence's JSON-based knowledge system so NPC dialogues reflect the political landscape.

**Architecture:** A CampaignBehavior reads BellumCivile's public API for faction state, converts it to Chinese/English narrative descriptions, and writes them to AIInfluence's `world_info.json` and NPC JSON files. Delta comparison minimizes file I/O.

**Tech Stack:** C# / .NET Framework 4.7.2 / HarmonyLib / MCMv5 / Newtonsoft.Json / Bannerlord Modding API

---

## File Map

| File | Responsibility |
|------|---------------|
| `BellumCivileAIInfluencePatch.csproj` | Modify: add BellumCivile.dll, Newtonsoft.Json references |
| `SubModule.xml` | Modify: add BellumCivile + AIInfluence dependencies |
| `src/BellumCivileAIInfluencePatchSubModule.cs` | Modify: register BridgeBehavior in OnGameStart |
| `src/Settings/BellumCivileAIInfluencePatchSettings.cs` | Modify: real MCM settings |
| `src/PoliticalSnapshot.cs` | Create: data classes for caching political state |
| `src/BCDataReader.cs` | Create: read BellumCivile data via public API |
| `src/DescriptionTemplates.cs` | Create: CN/EN narrative template strings |
| `src/AIInfluenceWriter.cs` | Create: write world_info.json and NPC JSON files |
| `src/BridgeBehavior.cs` | Create: orchestrate sync logic (daily tick + event detection) |

---

### Task 1: Project Setup — Dependencies and SubModule

**Files:**
- Modify: `BellumCivileAIInfluencePatch.csproj`
- Modify: `SubModule.xml`

- [ ] **Step 1: Add BellumCivile and Newtonsoft.Json references to csproj**

Add these references inside the existing `<ItemGroup>`:

```xml
    <Reference Include="BellumCivile">
      <HintPath>$(BANNERLORD_PATH)\Modules\BellumCivile\bin\Win64_Shipping_Client\BellumCivile.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="Newtonsoft.Json">
      <HintPath>$(BANNERLORD_PATH)\bin\Win64_Shipping_Client\Newtonsoft.Json.dll</HintPath>
      <Private>False</Private>
    </Reference>
```

Note: We do NOT reference AIInfluence.dll — its DLL is obfuscated and we only interact via JSON files.

- [ ] **Step 2: Update SubModule.xml with dependency declarations**

Replace the `<DependedModules>` block with:

```xml
  <DependedModules>
    <DependedModule Id="Native"/>
    <DependedModule Id="SandBoxCore"/>
    <DependedModule Id="Sandbox"/>
    <DependedModule Id="Bannerlord.Harmony"/>
    <DependedModule Id="Bannerlord.MBOptionScreen"/>
    <DependedModule Id="BellumCivile"/>
    <DependedModule Id="AIInfluence"/>
  </DependedModules>
```

This ensures our mod loads after both BellumCivile and AIInfluence.

- [ ] **Step 3: Verify project compiles**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug`
Expected: Build succeeded (warnings OK, zero errors)

- [ ] **Step 4: Commit**

```bash
git add BellumCivileAIInfluencePatch.csproj SubModule.xml
git commit -m "chore: add BellumCivile and Newtonsoft.Json dependencies"
```

---

### Task 2: Political Snapshot Data Classes

**Files:**
- Create: `src/PoliticalSnapshot.cs`

- [ ] **Step 1: Create PoliticalSnapshot.cs**

```csharp
using System.Collections.Generic;

namespace BellumCivileAIInfluencePatch
{
    public class ClanPoliticalData
    {
        public string ClanId { get; set; }
        public string ClanName { get; set; }
        public string KingdomId { get; set; }
        public string IdeologyFactionType { get; set; }   // "Royalists"/"Militarists"/etc or null
        public string RebelFactionName { get; set; }       // rebel faction name or null
        public string RebelFactionType { get; set; }       // "Independence"/"Abdication"/etc or null
        public float RebellionScore { get; set; }
        public int RelationWithRuler { get; set; }
        public int OwnedFiefs { get; set; }
        public int DesiredFiefs { get; set; }
        public float IdeologyMood { get; set; }

        public bool ContentEquals(ClanPoliticalData other)
        {
            if (other == null) return false;
            return ClanId == other.ClanId
                && KingdomId == other.KingdomId
                && IdeologyFactionType == other.IdeologyFactionType
                && RebelFactionName == other.RebelFactionName
                && RebelFactionType == other.RebelFactionType
                && System.Math.Abs(RebellionScore - other.RebellionScore) < 1f
                && RelationWithRuler == other.RelationWithRuler
                && OwnedFiefs == other.OwnedFiefs
                && DesiredFiefs == other.DesiredFiefs
                && System.Math.Abs(IdeologyMood - other.IdeologyMood) < 1f;
        }
    }

    public class FactionSnapshotData
    {
        public string FactionName { get; set; }
        public string FactionType { get; set; }            // enum name
        public bool IsIdeology { get; set; }
        public string LeaderClanId { get; set; }
        public string LeaderClanName { get; set; }
        public List<string> MemberClanIds { get; set; } = new List<string>();
        public int MemberCount { get; set; }
        public float Discontent { get; set; }
        public float Mood { get; set; }
        public float FactionPower { get; set; }
        public float LoyalistPower { get; set; }
        public string KingdomId { get; set; }
        public string DemandDescription { get; set; }
    }

    public class KingdomPoliticalData
    {
        public string KingdomId { get; set; }
        public string KingdomName { get; set; }
        public string RulerName { get; set; }
        public List<FactionSnapshotData> Factions { get; set; } = new List<FactionSnapshotData>();
        public bool HasActiveCivilWar { get; set; }

        public bool ContentEquals(KingdomPoliticalData other)
        {
            if (other == null) return false;
            if (KingdomId != other.KingdomId) return false;
            if (RulerName != other.RulerName) return false;
            if (HasActiveCivilWar != other.HasActiveCivilWar) return false;
            if (Factions.Count != other.Factions.Count) return false;
            for (int i = 0; i < Factions.Count; i++)
            {
                var a = Factions[i];
                var b = other.Factions[i];
                if (a.FactionName != b.FactionName) return false;
                if (a.MemberCount != b.MemberCount) return false;
                if (System.Math.Abs(a.Discontent - b.Discontent) >= 1f) return false;
                if (System.Math.Abs(a.Mood - b.Mood) >= 1f) return false;
                if (System.Math.Abs(a.FactionPower - b.FactionPower) >= 1f) return false;
            }
            return true;
        }
    }

    public class PoliticalSnapshot
    {
        public Dictionary<string, ClanPoliticalData> ClanData { get; set; } = new Dictionary<string, ClanPoliticalData>();
        public Dictionary<string, KingdomPoliticalData> KingdomData { get; set; } = new Dictionary<string, KingdomPoliticalData>();
        public List<string> ActiveFactionNames { get; set; } = new List<string>();
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/PoliticalSnapshot.cs
git commit -m "feat: add political snapshot data classes for delta comparison"
```

---

### Task 3: BellumCivile Data Reader

**Files:**
- Create: `src/BCDataReader.cs`

- [ ] **Step 1: Create BCDataReader.cs**

This class reads BellumCivile's public API. Since `FactionObject`, `FactionManagerBehavior`, and `IdeologyBehavior` all have public properties/methods, we call them directly (no reflection needed).

```csharp
using System.Collections.Generic;
using System.Linq;
using BellumCivile;
using BellumCivile.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BellumCivileAIInfluencePatch
{
    public static class BCDataReader
    {
        private static FactionManagerBehavior _factionManager;
        private static IdeologyBehavior _ideologyBehavior;

        public static bool Initialize()
        {
            _factionManager = Campaign.Current?.GetCampaignBehavior<FactionManagerBehavior>();
            _ideologyBehavior = Campaign.Current?.GetCampaignBehavior<IdeologyBehavior>();
            return _factionManager != null;
        }

        public static PoliticalSnapshot TakeSnapshot()
        {
            var snapshot = new PoliticalSnapshot();

            if (_factionManager == null) return snapshot;

            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;

                var kingdomData = BuildKingdomData(kingdom);
                snapshot.KingdomData[kingdom.StringId] = kingdomData;

                foreach (var faction in kingdomData.Factions)
                {
                    if (!snapshot.ActiveFactionNames.Contains(faction.FactionName))
                        snapshot.ActiveFactionNames.Add(faction.FactionName);
                }

                foreach (var clan in kingdom.Clans)
                {
                    if (clan.IsEliminated || clan.IsUnderMercenaryService || clan.IsMinorFaction)
                        continue;
                    if (clan.Leader == null || clan.Leader.IsDead) continue;

                    var clanData = BuildClanData(clan, kingdom);
                    snapshot.ClanData[clan.StringId] = clanData;
                }
            }

            return snapshot;
        }

        private static KingdomPoliticalData BuildKingdomData(Kingdom kingdom)
        {
            var data = new KingdomPoliticalData
            {
                KingdomId = kingdom.StringId,
                KingdomName = kingdom.Name?.ToString() ?? kingdom.StringId,
                RulerName = kingdom.RulingClan?.Leader?.Name?.ToString() ?? "Unknown"
            };

            var factions = _factionManager.GetFactionsInKingdom(kingdom);
            foreach (var faction in factions)
            {
                var fData = new FactionSnapshotData
                {
                    FactionName = faction.Name,
                    FactionType = faction.Type.ToString(),
                    IsIdeology = faction.IsIdeology,
                    LeaderClanId = faction.Leader?.StringId,
                    LeaderClanName = faction.Leader?.Name?.ToString(),
                    MemberCount = faction.Members?.Count ?? 0,
                    MemberClanIds = faction.Members?.Where(m => m != null && !m.IsEliminated)
                        .Select(m => m.StringId).ToList() ?? new List<string>(),
                    Discontent = faction.Discontent,
                    Mood = faction.Mood,
                    FactionPower = faction.CalculateFactionPower(),
                    LoyalistPower = faction.CalculateLoyalistPower(),
                    KingdomId = kingdom.StringId,
                    DemandDescription = faction.GetDemandDescription()
                };
                data.Factions.Add(fData);
            }

            // detect active civil war: rebel kingdoms at war with this kingdom
            data.HasActiveCivilWar = Kingdom.All.Any(k =>
                !k.IsEliminated && k != kingdom &&
                k.StringId.Contains("_rebels_") &&
                k.IsAtWarWith(kingdom));

            return data;
        }

        private static ClanPoliticalData BuildClanData(Clan clan, Kingdom kingdom)
        {
            var data = new ClanPoliticalData
            {
                ClanId = clan.StringId,
                ClanName = clan.Name?.ToString() ?? clan.StringId,
                KingdomId = kingdom.StringId,
                OwnedFiefs = clan.Fiefs?.Count ?? 0,
                DesiredFiefs = FactionObject.CalculateDesiredFiefs(clan)
            };

            // relation with ruler
            if (kingdom.RulingClan?.Leader != null && clan.Leader != null)
            {
                data.RelationWithRuler = clan.Leader.GetRelation(kingdom.RulingClan.Leader);
            }

            // ideology faction
            var ideoFaction = _factionManager.GetIdeologicalFaction(clan);
            if (ideoFaction != null)
            {
                data.IdeologyFactionType = ideoFaction.Type.ToString();
                data.IdeologyMood = ideoFaction.Mood;
            }

            // rebel faction
            var rebelFaction = _factionManager.GetRebelFaction(clan);
            if (rebelFaction != null)
            {
                data.RebelFactionName = rebelFaction.Name;
                data.RebelFactionType = rebelFaction.Type.ToString();
            }

            // rebellion score
            if (_ideologyBehavior != null)
            {
                data.RebellionScore = _ideologyBehavior.CalculateRebellionScore(clan);
            }

            return data;
        }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/BCDataReader.cs
git commit -m "feat: add BCDataReader to extract BellumCivile political state"
```

---

### Task 4: Description Templates (CN/EN)

**Files:**
- Create: `src/DescriptionTemplates.cs`

- [ ] **Step 1: Create DescriptionTemplates.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BellumCivileAIInfluencePatch
{
    public enum PatchLanguage
    {
        Auto,
        Chinese,
        English
    }

    public static class DescriptionTemplates
    {
        public static string BuildKingdomDescription(KingdomPoliticalData kingdom, PatchLanguage lang)
        {
            bool cn = ShouldUseChinese(lang);
            var sb = new StringBuilder();

            if (cn)
            {
                sb.Append($"{kingdom.KingdomName}王国政治格局：");
                if (kingdom.Factions.Count == 0)
                {
                    sb.Append("当前国内政局平稳，无活跃的政治派系。");
                    return sb.ToString();
                }
                sb.Append("王国内存在以下派系——");
            }
            else
            {
                sb.Append($"Political landscape of {kingdom.KingdomName}: ");
                if (kingdom.Factions.Count == 0)
                {
                    sb.Append("The kingdom is currently stable with no active political factions.");
                    return sb.ToString();
                }
                sb.Append("The following factions are active — ");
            }

            foreach (var faction in kingdom.Factions)
            {
                sb.Append(BuildFactionSegment(faction, cn));
            }

            if (kingdom.HasActiveCivilWar)
            {
                sb.Append(cn
                    ? "该王国目前正处于内战状态。"
                    : "The kingdom is currently in a state of civil war.");
            }

            return sb.ToString();
        }

        private static string BuildFactionSegment(FactionSnapshotData f, bool cn)
        {
            var sb = new StringBuilder();
            string typeName = GetFactionTypeName(f.FactionType, cn);

            if (cn)
            {
                sb.Append($"【{typeName}】由{f.LeaderClanName}氏族领导，{f.MemberCount}个氏族参与，");
                sb.Append($"主张：{f.DemandDescription}");

                if (f.IsIdeology)
                {
                    string rival = GetRivalFactionName(f.FactionType, cn);
                    sb.Append($"，与{rival}对立");
                    string moodDesc = GetMoodDescription(f.Mood, cn);
                    sb.Append($"，当前情绪{moodDesc}（{f.Mood:F0}）");
                }
                else
                {
                    sb.Append($"，不满度{f.Discontent:F0}%");
                    if (f.Discontent >= 80) sb.Append("，一触即发");
                    else if (f.Discontent >= 50) sb.Append("，局势紧张");
                    float totalPower = f.FactionPower + f.LoyalistPower;
                    if (totalPower > 0)
                    {
                        float factionPct = f.FactionPower / totalPower * 100f;
                        sb.Append($"，力量对比：派系{factionPct:F0}%对忠诚者{100 - factionPct:F0}%");
                    }
                }
                sb.Append("；");
            }
            else
            {
                sb.Append($"[{typeName}] led by Clan {f.LeaderClanName}, {f.MemberCount} clans involved, ");
                sb.Append($"demands: {f.DemandDescription}");

                if (f.IsIdeology)
                {
                    string rival = GetRivalFactionName(f.FactionType, cn);
                    sb.Append($", rivals: {rival}");
                    string moodDesc = GetMoodDescription(f.Mood, cn);
                    sb.Append($", current mood: {moodDesc} ({f.Mood:F0})");
                }
                else
                {
                    sb.Append($", discontent: {f.Discontent:F0}%");
                    if (f.Discontent >= 80) sb.Append(" — on the brink of revolt");
                    else if (f.Discontent >= 50) sb.Append(" — tensions rising");
                    float totalPower = f.FactionPower + f.LoyalistPower;
                    if (totalPower > 0)
                    {
                        float factionPct = f.FactionPower / totalPower * 100f;
                        sb.Append($", power balance: faction {factionPct:F0}% vs loyalists {100 - factionPct:F0}%");
                    }
                }
                sb.Append("; ");
            }

            return sb.ToString();
        }

        public static string BuildClanDescription(ClanPoliticalData clan, PatchLanguage lang)
        {
            bool cn = ShouldUseChinese(lang);
            var sb = new StringBuilder();

            if (cn)
            {
                sb.AppendLine("[BC_POLITICAL_START]");
                sb.Append("【你的政治立场】");

                if (clan.IdeologyFactionType != null)
                {
                    string typeName = GetFactionTypeName(clan.IdeologyFactionType, cn);
                    sb.Append($"你是{typeName}成员，");
                    string moodDesc = GetMoodDescription(clan.IdeologyMood, cn);
                    sb.Append($"派系情绪{moodDesc}。");
                }
                else
                {
                    sb.Append("你目前未加入任何意识形态派系。");
                }

                if (clan.RebelFactionName != null)
                {
                    sb.Append($"你参与了"{clan.RebelFactionName}"反叛运动（{GetFactionTypeName(clan.RebelFactionType, cn)}）。");
                }

                sb.Append($"你的叛乱倾向为{clan.RebellionScore:F0}/100");
                if (clan.RebellionScore >= 80) sb.Append("，极度不满");
                else if (clan.RebellionScore >= 50) sb.Append("，心怀不满");
                else if (clan.RebellionScore < 30) sb.Append("，比较安分");
                sb.Append("。");

                sb.Append($"你与国王的关系为{clan.RelationWithRuler:+#;-#;0}。");
                sb.Append($"领地状况：拥有{clan.OwnedFiefs}块，期望{clan.DesiredFiefs}块");
                if (clan.OwnedFiefs < clan.DesiredFiefs)
                    sb.Append("，领地不足令你不满");
                sb.Append("。");

                sb.AppendLine();
                sb.Append("[BC_POLITICAL_END]");
            }
            else
            {
                sb.AppendLine("[BC_POLITICAL_START]");
                sb.Append("[Your Political Stance] ");

                if (clan.IdeologyFactionType != null)
                {
                    string typeName = GetFactionTypeName(clan.IdeologyFactionType, cn);
                    sb.Append($"You are a member of the {typeName}. ");
                    string moodDesc = GetMoodDescription(clan.IdeologyMood, cn);
                    sb.Append($"Faction mood: {moodDesc}. ");
                }
                else
                {
                    sb.Append("You are not aligned with any ideological faction. ");
                }

                if (clan.RebelFactionName != null)
                {
                    sb.Append($"You are part of the \"{clan.RebelFactionName}\" rebellion ({GetFactionTypeName(clan.RebelFactionType, cn)}). ");
                }

                sb.Append($"Your rebellious intent is {clan.RebellionScore:F0}/100");
                if (clan.RebellionScore >= 80) sb.Append(" — deeply discontented");
                else if (clan.RebellionScore >= 50) sb.Append(" — harboring grievances");
                else if (clan.RebellionScore < 30) sb.Append(" — relatively content");
                sb.Append(". ");

                sb.Append($"Your relationship with the ruler: {clan.RelationWithRuler:+#;-#;0}. ");
                sb.Append($"Fiefs: own {clan.OwnedFiefs}, desire {clan.DesiredFiefs}");
                if (clan.OwnedFiefs < clan.DesiredFiefs)
                    sb.Append(" — the shortage breeds resentment");
                sb.Append(". ");

                sb.AppendLine();
                sb.Append("[BC_POLITICAL_END]");
            }

            return sb.ToString();
        }

        public static string BuildCrossKingdomCivilWarSummary(
            Dictionary<string, KingdomPoliticalData> kingdoms, PatchLanguage lang)
        {
            bool cn = ShouldUseChinese(lang);
            var warKingdoms = kingdoms.Values.Where(k => k.HasActiveCivilWar).ToList();

            if (warKingdoms.Count == 0)
            {
                return cn
                    ? "当前卡拉迪亚各王国内部局势平稳，无活跃内战。"
                    : "All kingdoms in Calradia are currently free of civil war.";
            }

            var sb = new StringBuilder();
            sb.Append(cn
                ? "当前卡拉迪亚内战局势："
                : "Current civil wars across Calradia: ");

            foreach (var kd in warKingdoms)
            {
                var rebelFactions = kd.Factions.Where(f => !f.IsIdeology).ToList();
                foreach (var rf in rebelFactions)
                {
                    if (cn)
                    {
                        sb.Append($"{kd.KingdomName}内战——{rf.LeaderClanName}氏族以"{rf.DemandDescription}"为由率{rf.MemberCount}个氏族起兵反叛，这是政治派系斗争引发的内战；");
                    }
                    else
                    {
                        sb.Append($"{kd.KingdomName} civil war — Clan {rf.LeaderClanName} leads {rf.MemberCount} clans in rebellion demanding \"{rf.DemandDescription}\", a civil war born of factional politics; ");
                    }
                }
            }

            return sb.ToString();
        }

        private static string GetFactionTypeName(string factionType, bool cn)
        {
            if (cn)
            {
                switch (factionType)
                {
                    case "Royalists": return "保王派";
                    case "Militarists": return "军国派";
                    case "Aristocrats": return "贵族派";
                    case "Populists": return "平民派";
                    case "Independence": return "独立运动";
                    case "Abdication": return "推翻统治";
                    case "InstallRuler": return "夺取王位";
                    case "FiefRedistribution": return "领地重分";
                    default: return factionType;
                }
            }
            else
            {
                switch (factionType)
                {
                    case "Royalists": return "Royalists";
                    case "Militarists": return "Militarists";
                    case "Aristocrats": return "Aristocrats";
                    case "Populists": return "Populists";
                    case "Independence": return "Independence Movement";
                    case "Abdication": return "Overthrow Ruler";
                    case "InstallRuler": return "Claim the Throne";
                    case "FiefRedistribution": return "Fief Redistribution";
                    default: return factionType;
                }
            }
        }

        private static string GetRivalFactionName(string factionType, bool cn)
        {
            if (cn)
            {
                switch (factionType)
                {
                    case "Royalists": return "贵族派";
                    case "Aristocrats": return "保王派";
                    case "Militarists": return "平民派";
                    case "Populists": return "军国派";
                    default: return "";
                }
            }
            else
            {
                switch (factionType)
                {
                    case "Royalists": return "Aristocrats";
                    case "Aristocrats": return "Royalists";
                    case "Militarists": return "Populists";
                    case "Populists": return "Militarists";
                    default: return "";
                }
            }
        }

        private static string GetMoodDescription(float mood, bool cn)
        {
            if (cn)
            {
                if (mood > 20) return "忠诚";
                if (mood > 0) return "满意";
                if (mood >= -20) return "中立";
                if (mood >= -50) return "不满";
                return "愤怒";
            }
            else
            {
                if (mood > 20) return "Loyal";
                if (mood > 0) return "Content";
                if (mood >= -20) return "Neutral";
                if (mood >= -50) return "Unhappy";
                return "Furious";
            }
        }

        public static bool ShouldUseChinese(PatchLanguage lang)
        {
            if (lang == PatchLanguage.Chinese) return true;
            if (lang == PatchLanguage.English) return false;
            // Auto: detect game language
            try
            {
                string gameLang = TaleWorlds.Engine.Utilities.GetLocalOutputPath();
                // Fallback: check BannerlordConfig
                string configLang = BannerlordConfig.Language;
                return configLang != null && (configLang.Contains("中文") || configLang.Contains("Chinese"));
            }
            catch
            {
                return false;
            }
        }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/DescriptionTemplates.cs
git commit -m "feat: add CN/EN description templates for faction narratives"
```

---

### Task 5: AIInfluence JSON Writer

**Files:**
- Create: `src/AIInfluenceWriter.cs`

- [ ] **Step 1: Create AIInfluenceWriter.cs**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BellumCivileAIInfluencePatch
{
    public static class AIInfluenceWriter
    {
        private static string _aiModulePath;
        private static string _saveDataPath;

        public static bool Initialize()
        {
            try
            {
                _aiModulePath = Path.Combine(
                    BasePath.Name, "Modules", "AIInfluence");

                if (!Directory.Exists(_aiModulePath))
                {
                    LogError("AIInfluence module directory not found: " + _aiModulePath);
                    return false;
                }

                // Find save_data subfolder (single save)
                string saveDataRoot = Path.Combine(_aiModulePath, "save_data");
                if (Directory.Exists(saveDataRoot))
                {
                    var subdirs = Directory.GetDirectories(saveDataRoot);
                    if (subdirs.Length == 1)
                    {
                        _saveDataPath = subdirs[0];
                    }
                    else if (subdirs.Length > 1)
                    {
                        // Take most recently modified
                        _saveDataPath = subdirs.OrderByDescending(d =>
                            Directory.GetLastWriteTime(d)).First();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError("AIInfluenceWriter.Initialize failed: " + ex.Message);
                return false;
            }
        }

        public static void UpdateWorldInfo(
            Dictionary<string, KingdomPoliticalData> kingdoms,
            string civilWarSummary,
            PatchLanguage lang)
        {
            string worldInfoPath = Path.Combine(_aiModulePath, "world_info.json");
            if (!File.Exists(worldInfoPath)) return;

            try
            {
                string json = File.ReadAllText(worldInfoPath);
                var entries = JsonConvert.DeserializeObject<List<JObject>>(json) ?? new List<JObject>();

                // Remove old BC entries
                entries.RemoveAll(e =>
                {
                    string id = e["id"]?.ToString();
                    return id != null && id.StartsWith("bc_kingdom_") || id == "bc_crossking_civil_wars";
                });

                // Add kingdom entries
                foreach (var kv in kingdoms)
                {
                    string desc = DescriptionTemplates.BuildKingdomDescription(kv.Value, lang);
                    var entry = new JObject
                    {
                        ["id"] = "bc_kingdom_" + kv.Key,
                        ["description"] = desc,
                        ["usageChance"] = 100,
                        ["applicableNPCs"] = new JArray("lords", "faction_leaders"),
                        ["category"] = "world"
                    };
                    entries.Add(entry);
                }

                // Add civil war summary
                var cwEntry = new JObject
                {
                    ["id"] = "bc_crossking_civil_wars",
                    ["description"] = civilWarSummary,
                    ["usageChance"] = 100,
                    ["applicableNPCs"] = new JArray("lords", "faction_leaders"),
                    ["category"] = "world"
                };
                entries.Add(cwEntry);

                string output = JsonConvert.SerializeObject(entries, Formatting.Indented);
                File.WriteAllText(worldInfoPath, output);
            }
            catch (Exception ex)
            {
                LogError("UpdateWorldInfo failed: " + ex.Message);
            }
        }

        public static void UpdateNpcFile(string clanStringId, string clanDescription,
            string kingdomId, bool enableDebugLog)
        {
            if (_saveDataPath == null) return;

            string npcFilePath = FindNpcFile(clanStringId);
            if (npcFilePath == null) return; // File doesn't exist yet — skip

            try
            {
                string json = File.ReadAllText(npcFilePath);
                var npc = JsonConvert.DeserializeObject<JObject>(json);
                if (npc == null) return;

                // Update CharacterDescription with political block
                string charDesc = npc["CharacterDescription"]?.ToString() ?? "";
                charDesc = InjectPoliticalBlock(charDesc, clanDescription);
                npc["CharacterDescription"] = charDesc;

                // Ensure KnownInfo contains the kingdom BC id
                var knownInfo = npc["KnownInfo"] as JArray ?? new JArray();
                string kingdomInfoId = "bc_kingdom_" + kingdomId;
                string civilWarId = "bc_crossking_civil_wars";

                if (!knownInfo.Any(t => t.ToString() == kingdomInfoId))
                    knownInfo.Add(kingdomInfoId);
                if (!knownInfo.Any(t => t.ToString() == civilWarId))
                    knownInfo.Add(civilWarId);

                npc["KnownInfo"] = knownInfo;

                string output = JsonConvert.SerializeObject(npc, Formatting.Indented);
                File.WriteAllText(npcFilePath, output);

                if (enableDebugLog)
                    Log($"Updated NPC file: {Path.GetFileName(npcFilePath)}");
            }
            catch (Exception ex)
            {
                LogError($"UpdateNpcFile failed for {clanStringId}: {ex.Message}");
            }
        }

        public static void WriteDynamicEvent(string title, string description,
            string kingdomId, List<string> characterIds, float campaignDays)
        {
            if (_saveDataPath == null) return;
            string eventsPath = Path.Combine(_saveDataPath, "dynamic_events.json");

            try
            {
                List<JObject> events;
                if (File.Exists(eventsPath))
                {
                    string json = File.ReadAllText(eventsPath);
                    events = JsonConvert.DeserializeObject<List<JObject>>(json) ?? new List<JObject>();
                }
                else
                {
                    events = new List<JObject>();
                }

                var newEvent = new JObject
                {
                    ["id"] = "bc_event_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                    ["type"] = "political",
                    ["title"] = title,
                    ["description"] = description,
                    ["event_history"] = new JArray
                    {
                        new JObject
                        {
                            ["campaign_days"] = campaignDays,
                            ["description"] = description,
                            ["update_reason"] = "Initial Event",
                            ["days_since_creation"] = 0,
                            ["economic_effects"] = new JArray()
                        }
                    },
                    ["player_involved"] = false,
                    ["kingdoms_involved"] = new JArray(kingdomId),
                    ["characters_involved"] = new JArray(characterIds.ToArray()),
                    ["importance"] = 7,
                    ["spread_speed"] = "fast",
                    ["allows_diplomatic_response"] = true,
                    ["applicable_npcs"] = new JArray("lords", "faction_leaders", "merchants"),
                    ["economic_effects"] = new JArray(),
                    ["creation_time"] = DateTime.Now.ToString("o"),
                    ["creation_campaign_days"] = campaignDays,
                    ["expiration_time"] = DateTime.Now.AddDays(84).ToString("o"),
                    ["expiration_campaign_days"] = campaignDays + 84,
                    ["participating_kingdoms"] = new JArray(),
                    ["kingdom_statements"] = new JArray(),
                    ["requires_diplomatic_analysis"] = false,
                    ["diplomatic_rounds"] = 0,
                    ["statements_at_round_start"] = 0,
                    ["next_analysis_attempt_days"] = 0.0,
                    ["next_statement_attempt_days"] = new JObject(),
                    ["failed_statement_attempts"] = new JObject()
                };

                events.Add(newEvent);

                string output = JsonConvert.SerializeObject(events, Formatting.Indented);
                File.WriteAllText(eventsPath, output);
            }
            catch (Exception ex)
            {
                LogError("WriteDynamicEvent failed: " + ex.Message);
            }
        }

        private static string FindNpcFile(string clanStringId)
        {
            if (_saveDataPath == null) return null;

            try
            {
                // NPC files are named like "Name (string_id).json"
                // The string_id in filename is the Hero's string_id, not the Clan's
                // We need to search for files containing the lord's string_id
                // For clan leaders, the hero string_id is usually like "lord_X_Y"
                var files = Directory.GetFiles(_saveDataPath, "*.json");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Contains("(" + clanStringId + ")") ||
                        fileName.Contains("(" + clanStringId + " "))
                    {
                        return file;
                    }
                }

                // Fallback: search by clan leader's hero string id
                // The clan string_id and hero string_id may differ
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string InjectPoliticalBlock(string charDesc, string politicalBlock)
        {
            const string pattern = @"\[BC_POLITICAL_START\][\s\S]*?\[BC_POLITICAL_END\]";
            if (Regex.IsMatch(charDesc, pattern))
            {
                return Regex.Replace(charDesc, pattern, politicalBlock);
            }
            else
            {
                return charDesc + "\n\n" + politicalBlock;
            }
        }

        public static void RefreshSaveDataPath()
        {
            string saveDataRoot = Path.Combine(_aiModulePath, "save_data");
            if (Directory.Exists(saveDataRoot))
            {
                var subdirs = Directory.GetDirectories(saveDataRoot);
                if (subdirs.Length >= 1)
                {
                    _saveDataPath = subdirs.OrderByDescending(d =>
                        Directory.GetLastWriteTime(d)).First();
                }
            }
        }

        private static void Log(string message)
        {
            InformationManager.DisplayMessage(
                new InformationMessage("[BC-AI Bridge] " + message));
        }

        private static void LogError(string message)
        {
            InformationManager.DisplayMessage(
                new InformationMessage("[BC-AI Bridge ERROR] " + message, Colors.Red));
        }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/AIInfluenceWriter.cs
git commit -m "feat: add AIInfluenceWriter for world_info.json and NPC JSON updates"
```

---

### Task 6: MCM Settings

**Files:**
- Modify: `src/Settings/BellumCivileAIInfluencePatchSettings.cs`

- [ ] **Step 1: Replace settings file content**

```csharp
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace BellumCivileAIInfluencePatch.Settings
{
    public class BellumCivileAIInfluencePatchSettings : AttributeGlobalSettings<BellumCivileAIInfluencePatchSettings>
    {
        public override string Id => "BellumCivileAIInfluencePatch";
        public override string DisplayName => "BellumCivile AI Influence Patch";
        public override string FolderName => "BellumCivileAIInfluencePatch";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Bridge", Order = 0, RequireRestart = false,
            HintText = "Enable/disable the BellumCivile to AIInfluence data bridge.")]
        [SettingPropertyGroup("General")]
        public bool EnableBridge { get; set; } = true;

        [SettingPropertyDropdown("Language", Order = 1, RequireRestart = false,
            HintText = "Language for political descriptions. Auto detects game language.")]
        [SettingPropertyGroup("General")]
        public MCM.Abstractions.Dropdown.DropdownDefault<string> Language { get; set; }
            = new MCM.Abstractions.Dropdown.DropdownDefault<string>(
                new[] { "Auto", "中文", "English" }, 0);

        [SettingPropertyBool("Write Dynamic Events", Order = 2, RequireRestart = false,
            HintText = "Attempt to write major political events to dynamic_events.json (experimental).")]
        [SettingPropertyGroup("General")]
        public bool WriteDynamicEvents { get; set; } = true;

        [SettingPropertyBool("Debug Log", Order = 3, RequireRestart = false,
            HintText = "Log sync details to game messages for debugging.")]
        [SettingPropertyGroup("Debug")]
        public bool DebugLog { get; set; } = false;

        public PatchLanguage GetPatchLanguage()
        {
            string selected = Language?.SelectedValue ?? "Auto";
            switch (selected)
            {
                case "中文": return PatchLanguage.Chinese;
                case "English": return PatchLanguage.English;
                default: return PatchLanguage.Auto;
            }
        }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/Settings/BellumCivileAIInfluencePatchSettings.cs
git commit -m "feat: implement MCM settings with language, bridge toggle, debug options"
```

---

### Task 7: Bridge Behavior — Core Orchestrator

**Files:**
- Create: `src/BridgeBehavior.cs`
- Modify: `src/BellumCivileAIInfluencePatchSubModule.cs`

- [ ] **Step 1: Create BridgeBehavior.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using BellumCivileAIInfluencePatch.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace BellumCivileAIInfluencePatch
{
    public class BridgeBehavior : CampaignBehaviorBase
    {
        private PoliticalSnapshot _previousSnapshot;
        private bool _initialized;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this,
                new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this,
                new Action(OnDailyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent data — we rebuild from BellumCivile state each session
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            var settings = BellumCivileAIInfluencePatchSettings.Instance;
            if (settings != null && !settings.EnableBridge) return;

            bool bcReady = BCDataReader.Initialize();
            bool aiReady = AIInfluenceWriter.Initialize();

            if (!bcReady)
            {
                LogMessage("BellumCivile not detected or not initialized. Bridge disabled.", true);
                return;
            }

            if (!aiReady)
            {
                LogMessage("AIInfluence file system not found. Bridge disabled.", true);
                return;
            }

            // Full sync on session start
            PerformFullSync();
            _initialized = true;
            LogMessage("Bridge initialized. Full sync completed.", false);
        }

        private void OnDailyTick()
        {
            if (!_initialized) return;

            var settings = BellumCivileAIInfluencePatchSettings.Instance;
            if (settings != null && !settings.EnableBridge) return;

            // Refresh save data path in case it changed
            AIInfluenceWriter.RefreshSaveDataPath();

            var currentSnapshot = BCDataReader.TakeSnapshot();

            if (_previousSnapshot != null)
            {
                DetectAndHandleMajorEvents(currentSnapshot, settings);
            }

            PerformDeltaSync(currentSnapshot, settings);

            _previousSnapshot = currentSnapshot;
        }

        private void PerformFullSync()
        {
            var settings = BellumCivileAIInfluencePatchSettings.Instance;
            var lang = settings?.GetPatchLanguage() ?? PatchLanguage.Auto;
            bool debug = settings?.DebugLog ?? false;

            var snapshot = BCDataReader.TakeSnapshot();

            // Update world_info.json
            string civilWarSummary = DescriptionTemplates.BuildCrossKingdomCivilWarSummary(
                snapshot.KingdomData, lang);
            AIInfluenceWriter.UpdateWorldInfo(snapshot.KingdomData, civilWarSummary, lang);

            if (debug)
                LogMessage($"world_info.json updated: {snapshot.KingdomData.Count} kingdoms", false);

            // Update all existing NPC files
            int updatedCount = 0;
            foreach (var kv in snapshot.ClanData)
            {
                string desc = DescriptionTemplates.BuildClanDescription(kv.Value, lang);
                AIInfluenceWriter.UpdateNpcFile(kv.Key, desc, kv.Value.KingdomId, debug);
                updatedCount++;
            }

            if (debug)
                LogMessage($"NPC files checked: {updatedCount} clans", false);

            _previousSnapshot = snapshot;
        }

        private void PerformDeltaSync(PoliticalSnapshot current,
            BellumCivileAIInfluencePatchSettings settings)
        {
            var lang = settings?.GetPatchLanguage() ?? PatchLanguage.Auto;
            bool debug = settings?.DebugLog ?? false;

            // Check kingdom-level changes
            bool kingdomChanged = false;
            foreach (var kv in current.KingdomData)
            {
                KingdomPoliticalData prev = null;
                _previousSnapshot?.KingdomData.TryGetValue(kv.Key, out prev);

                if (prev == null || !prev.ContentEquals(kv.Value))
                {
                    kingdomChanged = true;
                    break;
                }
            }

            if (kingdomChanged)
            {
                string civilWarSummary = DescriptionTemplates.BuildCrossKingdomCivilWarSummary(
                    current.KingdomData, lang);
                AIInfluenceWriter.UpdateWorldInfo(current.KingdomData, civilWarSummary, lang);

                if (debug)
                    LogMessage("world_info.json updated (kingdom data changed)", false);
            }

            // Check clan-level changes — only update changed NPCs
            int changedCount = 0;
            foreach (var kv in current.ClanData)
            {
                ClanPoliticalData prev = null;
                _previousSnapshot?.ClanData.TryGetValue(kv.Key, out prev);

                if (prev == null || !prev.ContentEquals(kv.Value))
                {
                    string desc = DescriptionTemplates.BuildClanDescription(kv.Value, lang);
                    AIInfluenceWriter.UpdateNpcFile(kv.Key, desc, kv.Value.KingdomId, debug);
                    changedCount++;
                }
            }

            if (debug && changedCount > 0)
                LogMessage($"Delta sync: {changedCount} NPC files updated", false);
        }

        private void DetectAndHandleMajorEvents(PoliticalSnapshot current,
            BellumCivileAIInfluencePatchSettings settings)
        {
            if (settings != null && !settings.WriteDynamicEvents) return;

            var lang = settings?.GetPatchLanguage() ?? PatchLanguage.Auto;
            bool cn = DescriptionTemplates.ShouldUseChinese(lang);
            float campaignDays = (float)CampaignTime.Now.ToDays;

            // Detect new factions
            foreach (string name in current.ActiveFactionNames)
            {
                if (!_previousSnapshot.ActiveFactionNames.Contains(name))
                {
                    var faction = current.KingdomData.Values
                        .SelectMany(k => k.Factions)
                        .FirstOrDefault(f => f.FactionName == name);

                    if (faction != null && !faction.IsIdeology)
                    {
                        string title = cn
                            ? $"新反叛派系成立：{name}"
                            : $"New Rebellion Formed: {name}";
                        string desc = cn
                            ? $"{faction.LeaderClanName}氏族领导的"{name}"正式成立，以"{faction.DemandDescription}"为诉求，{faction.MemberCount}个氏族响应。王国内部政治裂痕加深。"
                            : $"The \"{name}\" led by Clan {faction.LeaderClanName} has been formally established, demanding \"{faction.DemandDescription}\". {faction.MemberCount} clans have joined. Political fractures deepen.";

                        AIInfluenceWriter.WriteDynamicEvent(title, desc,
                            faction.KingdomId,
                            faction.MemberClanIds,
                            campaignDays);
                    }
                }
            }

            // Detect disbanded factions
            foreach (string name in _previousSnapshot.ActiveFactionNames)
            {
                if (!current.ActiveFactionNames.Contains(name))
                {
                    var prevFaction = _previousSnapshot.KingdomData.Values
                        .SelectMany(k => k.Factions)
                        .FirstOrDefault(f => f.FactionName == name);

                    if (prevFaction != null && !prevFaction.IsIdeology)
                    {
                        string title = cn
                            ? $"反叛派系解散：{name}"
                            : $"Rebellion Disbanded: {name}";
                        string desc = cn
                            ? $""{name}"已宣告解散，{prevFaction.LeaderClanName}氏族领导的反叛运动暂告平息。"
                            : $"The \"{name}\" has disbanded. The rebellion led by Clan {prevFaction.LeaderClanName} has been quelled.";

                        AIInfluenceWriter.WriteDynamicEvent(title, desc,
                            prevFaction.KingdomId,
                            prevFaction.MemberClanIds,
                            campaignDays);
                    }
                }
            }

            // Detect civil war outbreak (kingdom gained HasActiveCivilWar)
            foreach (var kv in current.KingdomData)
            {
                KingdomPoliticalData prev = null;
                _previousSnapshot.KingdomData.TryGetValue(kv.Key, out prev);

                if (kv.Value.HasActiveCivilWar && (prev == null || !prev.HasActiveCivilWar))
                {
                    string title = cn
                        ? $"{kv.Value.KingdomName}内战爆发"
                        : $"Civil War Erupts in {kv.Value.KingdomName}";
                    string desc = cn
                        ? $"{kv.Value.KingdomName}陷入内战！政治派系的长期矛盾终于演变为公开武装冲突，反叛者已举旗独立，王国面临分裂危机。"
                        : $"{kv.Value.KingdomName} descends into civil war! Long-simmering factional tensions have erupted into open armed conflict. Rebels have raised their banners, and the kingdom faces a crisis of fracture.";

                    var involvedChars = kv.Value.Factions
                        .Where(f => !f.IsIdeology)
                        .SelectMany(f => f.MemberClanIds)
                        .ToList();

                    AIInfluenceWriter.WriteDynamicEvent(title, desc,
                        kv.Key, involvedChars, campaignDays);
                }

                // Detect civil war ended
                if (!kv.Value.HasActiveCivilWar && prev != null && prev.HasActiveCivilWar)
                {
                    string title = cn
                        ? $"{kv.Value.KingdomName}内战结束"
                        : $"Civil War Ends in {kv.Value.KingdomName}";
                    string desc = cn
                        ? $"{kv.Value.KingdomName}的内战已经结束。无论胜负如何，王国的政治格局已被永久改变。"
                        : $"The civil war in {kv.Value.KingdomName} has ended. Regardless of the outcome, the political landscape has been permanently altered.";

                    AIInfluenceWriter.WriteDynamicEvent(title, desc,
                        kv.Key, new List<string>(), campaignDays);
                }
            }

            // Detect ultimatum (discontent jumped to 100)
            foreach (var kv in current.KingdomData)
            {
                KingdomPoliticalData prev = null;
                _previousSnapshot.KingdomData.TryGetValue(kv.Key, out prev);
                if (prev == null) continue;

                foreach (var faction in kv.Value.Factions.Where(f => !f.IsIdeology))
                {
                    var prevFaction = prev.Factions
                        .FirstOrDefault(f => f.FactionName == faction.FactionName);

                    if (prevFaction != null &&
                        faction.Discontent >= 100f &&
                        prevFaction.Discontent < 100f)
                    {
                        string title = cn
                            ? $"最后通牒：{faction.FactionName}"
                            : $"Ultimatum Issued: {faction.FactionName}";
                        string desc = cn
                            ? $""{faction.FactionName}"向{kv.Value.RulerName}发出最后通牒，要求"{faction.DemandDescription}"。若被拒绝，内战将不可避免。"
                            : $"The \"{faction.FactionName}\" has issued an ultimatum to {kv.Value.RulerName}, demanding \"{faction.DemandDescription}\". If refused, civil war is inevitable.";

                        AIInfluenceWriter.WriteDynamicEvent(title, desc,
                            kv.Key, faction.MemberClanIds, campaignDays);
                    }
                }
            }
        }

        private void LogMessage(string message, bool isError)
        {
            var color = isError ? Colors.Red : Colors.Cyan;
            InformationManager.DisplayMessage(
                new InformationMessage("[BC-AI Bridge] " + message, color));
        }
    }
}
```

- [ ] **Step 2: Update SubModule entry point**

Replace `src/BellumCivileAIInfluencePatchSubModule.cs` with:

```csharp
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BellumCivileAIInfluencePatch
{
    public class BellumCivileAIInfluencePatchSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            new Harmony("com.bellumcivileaiinfluencepatch").PatchAll(Assembly.GetExecutingAssembly());
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (gameStarter is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddBehavior(new BridgeBehavior());
            }
        }
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/BridgeBehavior.cs src/BellumCivileAIInfluencePatchSubModule.cs
git commit -m "feat: add BridgeBehavior orchestrator and wire up SubModule entry point"
```

---

### Task 8: NPC File Matching Fix — Use Hero StringId

**Files:**
- Modify: `src/BCDataReader.cs`
- Modify: `src/AIInfluenceWriter.cs`

AIInfluence NPC files use the Hero's `string_id` (e.g., `lord_2_3`), not the Clan's. We need to pass the hero string_id through the pipeline.

- [ ] **Step 1: Add HeroStringId to ClanPoliticalData**

In `src/PoliticalSnapshot.cs`, add this field to `ClanPoliticalData`:

```csharp
        public string HeroStringId { get; set; }        // clan leader's Hero.StringId for NPC file matching
```

- [ ] **Step 2: Populate HeroStringId in BCDataReader**

In `src/BCDataReader.cs`, inside `BuildClanData`, add after setting `DesiredFiefs`:

```csharp
            // Hero string_id for NPC file matching
            data.HeroStringId = clan.Leader?.StringId;
```

- [ ] **Step 3: Update AIInfluenceWriter.UpdateNpcFile to accept heroStringId**

In `src/AIInfluenceWriter.cs`, change the `UpdateNpcFile` signature and `FindNpcFile` call:

```csharp
        public static void UpdateNpcFile(string heroStringId, string clanDescription,
            string kingdomId, bool enableDebugLog)
        {
            if (_saveDataPath == null || heroStringId == null) return;

            string npcFilePath = FindNpcFile(heroStringId);
```

And update `FindNpcFile`:

```csharp
        private static string FindNpcFile(string heroStringId)
        {
            if (_saveDataPath == null) return null;

            try
            {
                var files = Directory.GetFiles(_saveDataPath, "*.json");
                string searchPattern = "(" + heroStringId + ")";
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Contains(searchPattern))
                    {
                        return file;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
```

- [ ] **Step 4: Update all UpdateNpcFile callers in BridgeBehavior**

In `src/BridgeBehavior.cs`, in `PerformFullSync`, change:

```csharp
                AIInfluenceWriter.UpdateNpcFile(kv.Value.HeroStringId, desc, kv.Value.KingdomId, debug);
```

In `PerformDeltaSync`, change:

```csharp
                    AIInfluenceWriter.UpdateNpcFile(kv.Value.HeroStringId, desc, kv.Value.KingdomId, debug);
```

- [ ] **Step 5: Verify build**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add src/PoliticalSnapshot.cs src/BCDataReader.cs src/AIInfluenceWriter.cs src/BridgeBehavior.cs
git commit -m "fix: use Hero StringId for NPC file matching instead of Clan id"
```

---

### Task 9: Final Build Verification and Cleanup

**Files:**
- Modify: `src/Patches/.gitkeep` (remove if empty)

- [ ] **Step 1: Clean build**

Run: `dotnet build BellumCivileAIInfluencePatch.csproj -c Debug --no-incremental`
Expected: Build succeeded, zero errors

- [ ] **Step 2: Verify output DLL exists**

Check that `$(BANNERLORD_PATH)\Modules\BellumCivileAIInfluencePatch\bin\Win64_Shipping_Client\BellumCivileAIInfluencePatch.dll` was created.

- [ ] **Step 3: Remove empty Patches directory gitkeep**

```bash
rm src/Patches/.gitkeep
rmdir src/Patches
```

(If Harmony patches are not needed for this mod, remove the empty directory)

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "chore: clean build verification, remove unused Patches directory"
```
