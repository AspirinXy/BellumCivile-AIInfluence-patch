using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BellumCivile;
using BellumCivile.Behaviors;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BellumCivileAIInfluencePatch
{
    /// <summary>
    /// Hooks IdeologyBehavior.TriggerGrandCoalition so whenever an ideology
    /// faction (Aristocrats/Militarists/Populists/Royalists) reaches its
    /// breaking point and forms a Grand Coalition against the king, we emit
    /// a dynamic event. This is a major political moment — a civil war is
    /// effectively starting.
    /// </summary>
    [HarmonyPatch]
    public static class FactionGrandCoalitionPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(IdeologyBehavior),
                "TriggerGrandCoalition");
        }

        static void Postfix(FactionObject triggeringFaction, Kingdom kingdom)
        {
            try
            {
                if (triggeringFaction == null || kingdom == null || kingdom.IsEliminated) return;

                var settings = Settings.BellumCivileAIInfluencePatchSettings.Instance;
                if (settings != null && !settings.EnableBridge) return;

                var lang = settings?.GetPatchLanguage() ?? PatchLanguage.Auto;
                bool cn = DescriptionTemplates.ShouldUseChinese(lang);

                string kingdomName = kingdom.Name?.ToString() ?? kingdom.StringId;
                string factionName = triggeringFaction.Name?.ToString() ?? triggeringFaction.Type.ToString();
                string factionTypeLabel = LocalizedFactionType(triggeringFaction.Type, cn);
                string rulerName = kingdom.RulingClan?.Leader?.Name?.ToString() ?? (cn ? "国王" : "the king");
                string rebelLeaderName = triggeringFaction.Leader?.Leader?.Name?.ToString()
                    ?? (cn ? "派系领袖" : "the faction leader");
                int memberCount = triggeringFaction.Members?.Count ?? 0;
                string gameDate = DescriptionTemplates.FormatGameDate(cn);

                string title = cn
                    ? $"王冠蒙尘"
                    : $"The Crown Beset";

                string description = cn
                    ? $"【{gameDate}】{rulerName}正面对一场惊动朝野的危局——{kingdomName}境内{factionTypeLabel}之怨积年，如今已汇成反王之潮。{memberCount}家氏族隐然附议于一位野心勃勃的挑战者，只待{rulerName}失德之举便倾国相向。王座虽未倾覆，圣火却已晦暗。"
                    : $"[{gameDate}] {rulerName} now faces a storm long in the brewing. Across {kingdomName}, grievances of the {factionTypeLabel} have knitted together into a challenge to the crown itself, with {memberCount} clans said to stand in silent accord with an ambitious pretender. The throne yet holds, but the sacred flame wavers.";

                var characterIds = new List<string>();
                if (kingdom.RulingClan?.Leader != null)
                    characterIds.Add(kingdom.RulingClan.Leader.StringId);
                if (triggeringFaction.Leader?.Leader != null
                    && triggeringFaction.Leader.Leader != kingdom.RulingClan?.Leader)
                    characterIds.Add(triggeringFaction.Leader.Leader.StringId);

                float campaignDays = (float)CampaignTime.Now.ToDays;

                // Grand Coalition = full-blown civil war trigger → highest non-max importance
                string eventId = AIInfluenceWriter.WriteDynamicEvent(
                    title, description,
                    new List<string> { kingdom.StringId },
                    characterIds,
                    campaignDays,
                    9,
                    true);

                if ((settings?.EnableDeepSeek ?? false) && eventId != null)
                {
                    string kingdomContext = cn
                        ? $"{kingdomName}内{factionName}派系发动的意识形态叛乱"
                        : $"An ideological rebellion by the {factionName} faction within {kingdomName}";
                    DeepSeekClient.ExpandAndUpdateEventAsync(
                        eventId, description, title, kingdomContext, cn);
                }

                ProxyWarNotifier.ShowReloadBanner(cn);

                if (settings?.DebugLog ?? false)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[BC-AI Bridge] Grand Coalition event written: {factionName} → {kingdomName}",
                        Colors.Cyan));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[BC-AI Bridge ERROR] FactionGrandCoalitionPatch: " + ex.Message, Colors.Red));
            }
        }

        private static string LocalizedFactionType(FactionType type, bool cn)
        {
            if (cn)
            {
                switch (type)
                {
                    case FactionType.Royalists: return "保皇派";
                    case FactionType.Militarists: return "军国派";
                    case FactionType.Aristocrats: return "贵族派";
                    case FactionType.Populists: return "平民派";
                    case FactionType.Abdication: return "退位派";
                    case FactionType.InstallRuler: return "拥立派";
                    default: return type.ToString();
                }
            }
            else
            {
                switch (type)
                {
                    case FactionType.Royalists: return "Royalists";
                    case FactionType.Militarists: return "Militarists";
                    case FactionType.Aristocrats: return "Aristocrats";
                    case FactionType.Populists: return "Populists";
                    default: return type.ToString();
                }
            }
        }
    }
}
