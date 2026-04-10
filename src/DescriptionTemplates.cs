using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.MountAndBlade;

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
                sb.Append($"你是{clan.ClanName}氏族的{(clan.IsClanLeader ? "族长" : "成员")}。");

                if (clan.IdeologyFactionType != null)
                {
                    string typeName = GetFactionTypeName(clan.IdeologyFactionType, cn);
                    sb.Append($"你的氏族属于{typeName}，");
                    string moodDesc = GetMoodDescription(clan.IdeologyMood, cn);
                    sb.Append($"派系情绪{moodDesc}。");
                }
                else
                {
                    sb.Append("你目前未加入任何意识形态派系。");
                }

                if (clan.RebelFactionName != null)
                {
                    sb.Append($"你的氏族参与了\u201c{clan.RebelFactionName}\u201d反叛运动（{GetFactionTypeName(clan.RebelFactionType, cn)}）。");
                }

                sb.Append($"你的氏族叛乱倾向为{clan.RebellionScore:F0}/100");
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
                sb.Append($"[Your Political Stance] You are {(clan.IsClanLeader ? "the leader" : "a member")} of Clan {clan.ClanName}. ");

                if (clan.IdeologyFactionType != null)
                {
                    string typeName = GetFactionTypeName(clan.IdeologyFactionType, cn);
                    sb.Append($"Your clan belongs to the {typeName}. ");
                    string moodDesc = GetMoodDescription(clan.IdeologyMood, cn);
                    sb.Append($"Faction mood: {moodDesc}. ");
                }
                else
                {
                    sb.Append("You are not aligned with any ideological faction. ");
                }

                if (clan.RebelFactionName != null)
                {
                    sb.Append($"Your clan is part of the \"{clan.RebelFactionName}\" rebellion ({GetFactionTypeName(clan.RebelFactionType, cn)}). ");
                }

                sb.Append($"Your clan's rebellious intent is {clan.RebellionScore:F0}/100");
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
                        sb.Append($"{kd.KingdomName}内战——{rf.LeaderClanName}氏族以\u201c{rf.DemandDescription}\u201d为由率{rf.MemberCount}个氏族起兵反叛，这是政治派系斗争引发的内战；");
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
