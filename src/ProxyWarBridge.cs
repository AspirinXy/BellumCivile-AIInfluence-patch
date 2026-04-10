using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BellumCivile.Behaviors;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace BellumCivileAIInfluencePatch
{
    /// <summary>
    /// Harmony patches to intercept BellumCivile proxy war events
    /// and bridge them to AIInfluence via dynamic_events.json.
    /// </summary>
    public static class ProxyWarBridge
    {
        private static readonly string[] ActionNamesCn =
        {
            "资助土匪",       // 0 Fund Highwaymen
            "煽动农民暴动",   // 1 Incite Peasant Revolt
            "宫廷丑闻",       // 2 Court Scandal
            "抹黑政治派系",   // 3 Smear Campaign
            "资助异见分子",   // 4 Fund Dissent
            "外国军事顾问",   // 5 Foreign Advisors
            "战争金库",       // 6 War Chest
            "收买雇佣兵合同", // 7 Buy Out Contracts
            "远征雇佣兵",     // 8 Expeditionary Mercenaries
            "策反叛逃"        // 9 Orchestrate Defection
        };

        private static readonly string[] ActionNamesEn =
        {
            "funding highwaymen",
            "inciting a peasant revolt",
            "orchestrating a court scandal",
            "running a smear campaign",
            "funding dissent",
            "sending foreign military advisors",
            "funneling a war chest",
            "buying out mercenary contracts",
            "smuggling expeditionary mercenaries",
            "orchestrating a defection"
        };

        private static readonly string[] ActionDescCn =
        {
            "在{0}境内资助匪帮，大量土匪在其首都附近出没，商路遭到严重破坏",
            "在{0}最脆弱的城市煽动农民暴动，城市忠诚度急剧下降",
            "在{0}宫廷散布丑闻，国王的政治影响力遭到严重削弱",
            "在{0}境内散布针对某政治派系的恶意谣言，派系情绪急剧恶化",
            "在{0}境内收买氏族，加速其走向叛变的道路",
            "向{0}的叛军派遣军事顾问，叛军部队凝聚力大幅提升",
            "向{0}的叛军秘密输送75000第纳尔的战争资金",
            "收买{0}的雇佣兵氏族，使其背弃效忠对象",
            "向{0}的叛军首领偷运50名精锐士兵",
            "收买{0}的一个忠诚氏族叛逃加入叛军"
        };

        private static readonly string[] ActionDescEn =
        {
            "funded highwaymen near the capital of {0}, severely disrupting trade routes with massive bandit parties",
            "incited a peasant revolt in the most vulnerable city of {0}, devastating its loyalty",
            "orchestrated a court scandal in {0}, severely damaging the ruler's political influence",
            "ran a smear campaign against a political faction in {0}, causing outrage and faction unrest",
            "funded dissent among clans in {0}, rapidly accelerating their path to treason",
            "sent foreign military advisors to rebels in {0}, boosting rebel army cohesion by 40",
            "secretly funneled 75,000 denars into the rebel war chest in {0}",
            "bribed mercenary clans to abandon {0}, severing their contracts",
            "smuggled 50 elite troops into the rebel leader's forces in {0}",
            "orchestrated the defection of a loyalist clan to the rebels in {0}"
        };

        public static string GetActionName(int actionType, bool cn)
        {
            if (actionType < 0 || actionType > 9) return cn ? "未知行动" : "unknown action";
            return cn ? ActionNamesCn[actionType] : ActionNamesEn[actionType];
        }

        public static string GetActionDescription(int actionType, bool cn, string targetKingdomName)
        {
            if (actionType < 0 || actionType > 9)
                return cn ? "在敌国执行了秘密破坏行动" : "carried out a covert operation against a foreign realm";
            string template = cn ? ActionDescCn[actionType] : ActionDescEn[actionType];
            return string.Format(template, targetKingdomName);
        }
    }

    /// <summary>
    /// Patch ApplyDiscoveryFallout — fires when ANY proxy war (player or AI) is discovered.
    /// </summary>
    [HarmonyPatch]
    public static class ProxyWarDiscoveryPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ProxyWarBehavior),
                "ApplyDiscoveryFallout",
                new[] { typeof(Kingdom), typeof(Kingdom), typeof(int), typeof(Hero) });
        }

        static void Postfix(Kingdom actingKingdom, Kingdom targetKingdom, int actionType)
        {
            try
            {
                if (actingKingdom == null || targetKingdom == null) return;

                var settings = Settings.BellumCivileAIInfluencePatchSettings.Instance;
                if (settings != null && !settings.EnableBridge) return;

                var lang = settings?.GetPatchLanguage() ?? PatchLanguage.Auto;
                bool cn = DescriptionTemplates.ShouldUseChinese(lang);

                string actingName = actingKingdom.Name?.ToString() ?? actingKingdom.StringId;
                string targetName = targetKingdom.Name?.ToString() ?? targetKingdom.StringId;
                string actingRulerName = actingKingdom.RulingClan?.Leader?.Name?.ToString() ?? "Unknown";
                string targetRulerName = targetKingdom.RulingClan?.Leader?.Name?.ToString() ?? "Unknown";

                string actionName = ProxyWarBridge.GetActionName(actionType, cn);
                string actionDesc = ProxyWarBridge.GetActionDescription(actionType, cn, targetName);

                string title = cn
                    ? $"阴谋败露：{actingName}{actionName}"
                    : $"Plot Exposed: {actingName} caught {ProxyWarBridge.GetActionName(actionType, false)}";

                string description = cn
                    ? $"{actingName}的{actingRulerName}被{targetName}的{targetRulerName}揭露{actionDesc}。这一阴谋的曝光引发了严重的外交危机，两国关系急剧恶化。"
                    : $"{actingRulerName} of {actingName} has been exposed by {targetRulerName} of {targetName} for {actionDesc}. The exposure of this plot has caused a severe diplomatic crisis, with relations between the two kingdoms deteriorating sharply.";

                // DeepSeek expansion
                if (settings?.EnableDeepSeek ?? false)
                {
                    string kingdomContext = cn
                        ? $"{actingName}与{targetName}之间的秘密代理战争"
                        : $"A covert proxy war between {actingName} and {targetName}";
                    description = DeepSeekClient.ExpandDescription(description, title, kingdomContext, cn);
                }

                var characterIds = new List<string>();
                if (actingKingdom.RulingClan?.Leader != null)
                    characterIds.Add(actingKingdom.RulingClan.Leader.StringId);
                if (targetKingdom.RulingClan?.Leader != null)
                    characterIds.Add(targetKingdom.RulingClan.Leader.StringId);

                float campaignDays = (float)CampaignTime.Now.ToDays;

                var kingdomIds = new List<string>
                {
                    actingKingdom.StringId,
                    targetKingdom.StringId
                };

                AIInfluenceWriter.WriteDynamicEvent(
                    title, description,
                    kingdomIds,
                    characterIds,
                    campaignDays,
                    8,
                    true);

                // Show reload banner
                ProxyWarNotifier.ShowReloadBanner(cn);

                if (settings?.DebugLog ?? false)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[BC-AI Bridge] Proxy war discovery event written: {actingName} → {targetName}",
                        Colors.Cyan));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[BC-AI Bridge ERROR] ProxyWarDiscoveryPatch: " + ex.Message, Colors.Red));
            }
        }
    }

    /// <summary>
    /// Patch OnDailyTick to detect player mission completions (success/failure/execution).
    /// We capture the state of _playerMissionReturnDates before and after to detect changes.
    /// </summary>
    [HarmonyPatch]
    public static class ProxyWarPlayerMissionPatch
    {
        private static HashSet<string> _previousMissionHeroes = new HashSet<string>();

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ProxyWarBehavior), "OnDailyTick");
        }

        static void Prefix(ProxyWarBehavior __instance)
        {
            try
            {
                var returnDates = AccessTools.Field(typeof(ProxyWarBehavior), "_playerMissionReturnDates")
                    ?.GetValue(__instance) as Dictionary<string, CampaignTime>;
                var actions = AccessTools.Field(typeof(ProxyWarBehavior), "_playerMissionActions")
                    ?.GetValue(__instance) as Dictionary<string, int>;
                var targets = AccessTools.Field(typeof(ProxyWarBehavior), "_playerMissionTargets")
                    ?.GetValue(__instance) as Dictionary<string, string>;

                if (returnDates == null) return;

                // Store heroes whose missions are about to expire (return date is past)
                _previousMissionHeroes.Clear();
                foreach (var kvp in returnDates)
                {
                    var returnDate = kvp.Value;
                    if (returnDate.IsPast)
                    {
                        _previousMissionHeroes.Add(kvp.Key);
                    }
                }
            }
            catch { }
        }

        static void Postfix(ProxyWarBehavior __instance)
        {
            try
            {
                if (_previousMissionHeroes.Count == 0) return;

                var settings = Settings.BellumCivileAIInfluencePatchSettings.Instance;
                if (settings != null && !settings.EnableBridge) return;

                // After OnDailyTick, the completed missions have been removed from the dictionaries.
                // We check if any hero from our captured set is now missing (= mission resolved).
                var returnDates = AccessTools.Field(typeof(ProxyWarBehavior), "_playerMissionReturnDates")
                    ?.GetValue(__instance) as Dictionary<string, CampaignTime>;

                if (returnDates == null) return;

                // Note: BC's OnDailyTick already shows InformationManager messages for results
                // (green for success, red for execution, yellow for escape/abort).
                // The discovery case is handled by ProxyWarDiscoveryPatch.
                // For successful (undetected) missions, we write a more subtle event.

                // We don't write events for undetected successes to dynamic_events
                // because the whole point is that nobody knows about it.
                // The discovery patch handles the interesting case.
            }
            catch { }
        }
    }

    /// <summary>
    /// Patch AI espionage (EvaluateEspionageTarget) to capture successful undetected AI missions.
    /// Even though these are secret, we can optionally write a "rumors" event.
    /// </summary>
    [HarmonyPatch]
    public static class ProxyWarAISuccessPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ProxyWarBehavior),
                "ApplyEspionageEffects",
                new[]
                {
                    typeof(Kingdom), typeof(Kingdom), typeof(int),
                    typeof(bool).MakeByRefType(), typeof(TaleWorlds.Localization.TextObject).MakeByRefType(),
                    typeof(Hero).MakeByRefType(), typeof(Kingdom).MakeByRefType()
                });
        }

        static void Postfix(Kingdom actingKingdom, Kingdom targetKingdom, int actionType,
            bool abortMission)
        {
            try
            {
                // Only write events for successful (non-aborted) missions
                if (abortMission) return;
                if (actingKingdom == null || targetKingdom == null) return;

                var settings = Settings.BellumCivileAIInfluencePatchSettings.Instance;
                if (settings != null && !settings.EnableBridge) return;

                // Check if this is an AI mission (not player)
                // Player missions also call ApplyEspionageEffects, but the player kingdom is Clan.PlayerClan.Kingdom
                bool isPlayerMission = actingKingdom == Clan.PlayerClan?.Kingdom &&
                                       actingKingdom.RulingClan == Clan.PlayerClan;

                var lang = settings?.GetPatchLanguage() ?? PatchLanguage.Auto;
                bool cn = DescriptionTemplates.ShouldUseChinese(lang);

                string targetName = targetKingdom.Name?.ToString() ?? targetKingdom.StringId;
                string actionName = ProxyWarBridge.GetActionName(actionType, cn);

                if (isPlayerMission)
                {
                    // Player's successful covert mission — write a subtle "consequences" event
                    string title = cn
                        ? $"神秘事件：{targetName}遭受不明破坏"
                        : $"Mysterious Incident in {targetName}";

                    string description = cn
                        ? $"{targetName}近日遭受了一系列不明来源的破坏活动（{actionName}），朝堂震动，民间议论纷纷。官方尚未查明幕后黑手，但有传言暗示这可能是外国势力的手笔。"
                        : $"{targetName} has recently suffered mysterious disruptions ({ProxyWarBridge.GetActionName(actionType, false)}). The court is shaken and rumors abound. Officials have yet to identify the perpetrator, though whispers suggest foreign involvement.";

                    // DeepSeek expansion
                    if (settings?.EnableDeepSeek ?? false)
                    {
                        string kingdomContext = cn
                            ? $"{targetName}遭受的神秘破坏事件"
                            : $"Mysterious disruption in {targetName}";
                        description = DeepSeekClient.ExpandDescription(description, title, kingdomContext, cn);
                    }

                    var characterIds = new List<string>();
                    if (targetKingdom.RulingClan?.Leader != null)
                        characterIds.Add(targetKingdom.RulingClan.Leader.StringId);

                    float campaignDays = (float)CampaignTime.Now.ToDays;

                    AIInfluenceWriter.WriteDynamicEvent(
                        title, description,
                        new List<string> { targetKingdom.StringId },
                        characterIds,
                        campaignDays,
                        6,
                        true);

                    ProxyWarNotifier.ShowReloadBanner(cn);
                }
                // AI successful undetected missions — no event (it's a secret)
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[BC-AI Bridge ERROR] ProxyWarAISuccessPatch: " + ex.Message, Colors.Red));
            }
        }
    }

    /// <summary>
    /// Handles showing the reload banner notification to the player.
    /// </summary>
    public static class ProxyWarNotifier
    {
        private static CampaignTime _lastBannerTime;
        private static bool _bannerShownThisSession;

        public static void ShowReloadBanner(bool cn)
        {
            // Throttle: don't spam banners, show at most once per 3 game days
            if (_bannerShownThisSession && _lastBannerTime != null)
            {
                float daysSinceLast = (float)(CampaignTime.Now - _lastBannerTime).ToDays;
                if (daysSinceLast < 3f) return;
            }

            string message = cn
                ? "新的动态事件已生成，建议保存并重新加载存档以使AI感知最新事件"
                : "New dynamic event created. Save and reload to let AI perceive the latest events";

            // Top-center banner notification
            MBInformationManager.AddQuickInformation(
                new TextObject("{=BC_AI_ReloadBanner}" + message),
                0,
                null,
                null,
                "event:/ui/notification/quest_update");

            // Also log to message area for persistence
            InformationManager.DisplayMessage(
                new InformationMessage("[BC-AI Bridge] " + message, Colors.Yellow));

            _lastBannerTime = CampaignTime.Now;
            _bannerShownThisSession = true;
        }
    }
}
