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
            "资助土匪",
            "煽动暴动",
            "宫廷丑事",
            "流言中伤",
            "策动异见",
            "外邦客将",
            "金流暗渡",
            "收买旌旗",
            "奇兵暗援",
            "策反反侧"
        };

        private static readonly string[] ActionNamesEn =
        {
            "the patronage of highwaymen",
            "the incitement of a city revolt",
            "a whispered court scandal",
            "slanderous rumor-mongering",
            "the stirring of dissident clans",
            "foreign men-at-arms to the rebels",
            "a clandestine war-chest",
            "the purchase of sworn banners",
            "hidden companies of elite men",
            "treason sown among loyal houses"
        };

        // Short, epic, 4-char titles per actionType (for Discovery when DeepSeek disabled).
        private static readonly string[] ActionTitlesCn =
        {
            "匪踪蔽路",
            "民怨倒悬",
            "宫闱余烬",
            "流言蚀心",
            "潜流涌动",
            "客将南渡",
            "金流暗渡",
            "旌旗易主",
            "奇兵暗集",
            "人心反侧"
        };

        private static readonly string[] ActionTitlesEn =
        {
            "Roads of Shadow",
            "The City in Tumult",
            "Ashes of the Court",
            "A Slander Most Foul",
            "The Hidden Tide",
            "Foreign Banners",
            "Gold Passed in Night",
            "Banners Bought and Sold",
            "Steel in Silence",
            "Hearts Turned Aside"
        };

        // Subjects are kingdom rulers, framed as the acting crown's deed against the target crown.
        // Classical register; no percentages, no modern metrics.
        private static readonly string[] ActionDescCn =
        {
            "遣匪潜伏于{0}之商道，驿路阻绝，货殖之利断然",
            "挑唆{0}民邑起事，城中鼎沸，民心涣散",
            "散布丑事于{0}宫禁之中，国主威名陡落，朝堂侧目",
            "使流言毁谤{0}之一党，该派愤懑翻涌，士气颓靡",
            "收买{0}不臣之族，煽其走向背主之途",
            "遣异邦军士入{0}叛军营中，军心大振",
            "暗输七万五千第纳尔之资，以饱{0}叛军之粮秣兵械",
            "倾金贿{0}之雇佣氏族，使其背旧主而附叛军麾下",
            "走私五十精卒入{0}叛军首领帐下",
            "策动{0}一世家大族脱离王座，投身叛军"
        };

        private static readonly string[] ActionDescEn =
        {
            "loosed brigands upon the trade-roads of {0}, that its caravans might not pass",
            "stirred the meanest streets of {0} to tumult, turning hearth against lord",
            "spread whispered scandal through the court of {0}, that the sovereign's name might be darkened",
            "sowed slander against a faction within {0}, curdling their resolve to ash",
            "bought the ear of discontented houses in {0}, quickening their march toward treason",
            "sent sworn swords from abroad into the rebel camps of {0}, stiffening their line",
            "slid seventy-five thousand denars into the rebel war-chest within {0}",
            "turned mercenary banners in {0} with gold, that they should abandon their oath",
            "spirited fifty picked men into the rebel leader's host in {0}",
            "drew a loyal house of {0} from its crown, that it might stand with the rebels"
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

        public static string GetActionEpicTitle(int actionType, bool cn)
        {
            if (actionType < 0 || actionType > 9) return cn ? "秘谋暗涌" : "Shadows and Schemes";
            return cn ? ActionTitlesCn[actionType] : ActionTitlesEn[actionType];
        }

        // Symptom descriptions for "mysterious incident" events (undetected AI success).
        // These describe what is seen from the target's perspective, without attribution.
        private static readonly string[] MysterySymptomCn =
        {
            "商道之上忽见大股匪徒出没，驿路阻绝，货殖受挫",
            "一座要城民心大乱，街巷鼎沸，旌旗飘摇",
            "宫禁之内忽传丑事，国主威名蒙尘，百官相顾失色",
            "朝中一党士气骤颓，流言蜚语如潮水涌动",
            "境内数家氏族暗动不安，似有异志之火潜燃",
            "国中叛军旌旗整肃，军容一振，似得外邦暗助",
            "国中叛军府库忽丰，粮秣兵械一时不缺",
            "国中雇佣之旗忽转方向，旧盟顿成陌路",
            "国中叛军首领麾下精卒骤增，锋芒毕现",
            "国中一世家豪族弃王而去，投身叛军阵中"
        };

        private static readonly string[] MysterySymptomEn =
        {
            "bands of brigands have risen upon the trade-roads, and the caravans are waylaid",
            "a great city has fallen to tumult, its streets restless and its banners unsure",
            "whispered scandal drifts through the palace halls, and the crown's name is darkened",
            "a faction at court has lost its resolve, rumors coursing through them like poison",
            "within the realm, certain houses stir in uneasy concert, as if drawn by a hidden hand",
            "the rebels' ranks have stiffened as though foreign swords walk among them",
            "the rebel coffers have grown heavy, supplied by no known merchant",
            "mercenary banners within the realm have turned, old oaths now forsworn",
            "unlooked-for companies of picked men have appeared in the rebel captain's host",
            "a great house has slipped from the crown's side to stand with the rebels"
        };

        public static string GetMysterySymptom(int actionType, bool cn)
        {
            if (actionType < 0 || actionType > 9)
                return cn ? "境内忽生异变，莫测其源" : "a strange disturbance whose source is unknown";
            return cn ? MysterySymptomCn[actionType] : MysterySymptomEn[actionType];
        }

        /// <summary>
        /// Per-actionType event-type classification (aligned with AIInfluence enum).
        /// 0 bandits→economic, 1 city revolt→social, 2/3 court/ideology→political,
        /// 4 dissident network→social, 5-9 rebel support→political.
        /// </summary>
        public static string GetActionEventType(int actionType)
        {
            switch (actionType)
            {
                case 0: return "economic";
                case 1: return "social";
                case 4: return "social";
                default: return "political";
            }
        }

        /// <summary>
        /// Per-actionType importance for "plot exposed" (discovery) events.
        /// 0-4 random 2-5, 5/6/8=7, 7/9=8.
        /// </summary>
        public static int RollDiscoveryImportance(int actionType)
        {
            if (actionType >= 0 && actionType <= 4)
                return TaleWorlds.Core.MBRandom.RandomInt(2, 6); // inclusive-exclusive: 2..5
            if (actionType == 7 || actionType == 9) return 8;
            return 7; // 5, 6, 8
        }

        /// <summary>
        /// Per-actionType importance for "mysterious incident" (undetected AI success) events.
        /// These are always a step lower than their discovery counterpart.
        /// </summary>
        public static int RollAISuccessImportance(int actionType)
        {
            if (actionType >= 0 && actionType <= 4)
                return TaleWorlds.Core.MBRandom.RandomInt(2, 5); // 2..4
            if (actionType == 7 || actionType == 9) return 6;
            return 5;
        }

        /// <summary>
        /// Per AIInfluence events-generator rules, low-level harassment (importance &lt; 5)
        /// should rarely escalate to formal diplomatic response. Significant events
        /// (importance ≥ 5) almost always do.
        ///   importance ≤ 2: 0.05
        ///   importance 3:   0.10
        ///   importance 4:   0.20
        ///   importance 5:   0.55
        ///   importance 6:   0.70
        ///   importance 7:   0.85
        ///   importance ≥ 8: 0.95
        /// </summary>
        public static bool RollAllowsDiplomaticResponse(int importance)
        {
            float p;
            if (importance <= 2) p = 0.05f;
            else if (importance == 3) p = 0.10f;
            else if (importance == 4) p = 0.20f;
            else if (importance == 5) p = 0.55f;
            else if (importance == 6) p = 0.70f;
            else if (importance == 7) p = 0.85f;
            else p = 0.95f;
            return TaleWorlds.Core.MBRandom.RandomFloat < p;
        }

        /// <summary>
        /// Minimum distance between any two settlements of the two kingdoms,
        /// measured in Calradia map units (Position2D). Calradia spans roughly
        /// 1000 units east-west. A typical kingdom's interior is ~150-200 units.
        /// Returns float.MaxValue if either kingdom has no settlements.
        /// </summary>
        public static float GetMinKingdomDistance(Kingdom a, Kingdom b)
        {
            try
            {
                if (a == null || b == null) return float.MaxValue;
                float min = float.MaxValue;
                foreach (var sA in a.Settlements)
                {
                    foreach (var sB in b.Settlements)
                    {
                        float d = sA.GatePosition.Distance(sB.GatePosition);
                        if (d < min) min = d;
                    }
                }
                return min;
            }
            catch
            {
                return float.MaxValue;
            }
        }

        /// <summary>
        /// AIInfluence geography rule: ancient-era realms do not engage in
        /// petty cross-continental meddling. Dampen importance when acting
        /// and target kingdoms are geographically remote.
        ///   dist &lt; 200 (adjacent or near): no change
        ///   dist 200-400 (distant neighbors): −1
        ///   dist ≥ 400 (across the continent): −2
        /// Returns the geography tier (0/1/2) for downstream narrative framing.
        /// </summary>
        public static int ApplyGeographyDamping(ref int importance, Kingdom acting, Kingdom target)
        {
            float dist = GetMinKingdomDistance(acting, target);
            int tier;
            int delta;
            if (dist < 200f) { tier = 0; delta = 0; }
            else if (dist < 400f) { tier = 1; delta = -1; }
            else { tier = 2; delta = -2; }

            importance += delta;
            if (importance < 2) importance = 2;
            return tier;
        }

        public static string GetGeographyPrefix(int geoTier, bool cn)
        {
            switch (geoTier)
            {
                case 1:
                    return cn ? "山川阻隔，风声辗转而至——" : "Through distant lands the tidings came at last — ";
                case 2:
                    return cn ? "海天相隔，此谋跨数国而来，令人难以尽信——" : "Across half a continent this scheme is said to reach, scarce to be believed — ";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Maps importance 2..9 linearly onto expiration 10..84 days.
        /// </summary>
        public static int ComputeExpirationDays(int importance)
        {
            int i = importance;
            if (i < 2) i = 2;
            if (i > 9) i = 9;
            int days = (int)(10 + (i - 2) * 74f / 7f);
            if (days < 10) days = 10;
            if (days > 84) days = 84;
            return days;
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

                string actionDesc = ProxyWarBridge.GetActionDescription(actionType, cn, targetName);
                string epicTitle = ProxyWarBridge.GetActionEpicTitle(actionType, cn);
                string gameDate = DescriptionTemplates.FormatGameDate(cn);

                int importance = ProxyWarBridge.RollDiscoveryImportance(actionType);
                int geoTier = ProxyWarBridge.ApplyGeographyDamping(ref importance, actingKingdom, targetKingdom);
                string geoPrefix = ProxyWarBridge.GetGeographyPrefix(geoTier, cn);

                string title = epicTitle;

                string description = cn
                    ? $"【{gameDate}】{geoPrefix}{targetRulerName}震怒：{actingName}之{actingRulerName}行秘谋，{actionDesc}。阴事既泄，两国之谊如薄冰骤裂，朝堂之上，群臣相顾失色。"
                    : $"[{gameDate}] {geoPrefix}{targetRulerName} stands wrathful. The sovereign of {actingName}, {actingRulerName}, {actionDesc} — and the deed has come to light. Between the two crowns, the thread of amity snaps like brittle ice, and the halls fall to uneasy silence.";

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

                string eventType = ProxyWarBridge.GetActionEventType(actionType);
                bool allowsDiplomatic = ProxyWarBridge.RollAllowsDiplomaticResponse(importance);
                int expirationDays = ProxyWarBridge.ComputeExpirationDays(importance);

                // Write event immediately with original text (no blocking)
                string eventId = AIInfluenceWriter.WriteDynamicEvent(
                    title, description,
                    kingdomIds,
                    characterIds,
                    campaignDays,
                    importance,
                    true,
                    eventType,
                    allowsDiplomatic,
                    expirationDays);

                // DeepSeek expansion in background — updates the event file when done
                if ((settings?.EnableDeepSeek ?? false) && eventId != null)
                {
                    string kingdomContext = cn
                        ? $"{actingName}与{targetName}之间的秘密代理战争"
                        : $"A covert proxy war between {actingName} and {targetName}";
                    DeepSeekClient.ExpandAndUpdateEventAsync(eventId, description, title, kingdomContext, cn);
                }

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
                string targetRulerName = targetKingdom.RulingClan?.Leader?.Name?.ToString()
                    ?? (cn ? "国主" : "the sovereign");

                if (isPlayerMission)
                {
                    string gameDate = DescriptionTemplates.FormatGameDate(cn);
                    string epicTitle = ProxyWarBridge.GetActionEpicTitle(actionType, cn);

                    string title = epicTitle;

                    string symptom = ProxyWarBridge.GetMysterySymptom(actionType, cn);
                    string description = cn
                        ? $"【{gameDate}】{targetRulerName}座下忽生异象——{symptom}。朝堂震动，百官无言以对，民间流言四起，或谓此乃异邦暗手，或谓天谴将至。幕后之人，尚不可辨。"
                        : $"[{gameDate}] Strange misfortunes beset {targetRulerName} — {symptom}. The court is shaken, the common folk whisper of foreign hands or heavenly portents, and none can name the author of the deed.";

                    var characterIds = new List<string>();
                    if (targetKingdom.RulingClan?.Leader != null)
                        characterIds.Add(targetKingdom.RulingClan.Leader.StringId);

                    float campaignDays = (float)CampaignTime.Now.ToDays;

                    int importance = ProxyWarBridge.RollAISuccessImportance(actionType);
                    string eventType = ProxyWarBridge.GetActionEventType(actionType);
                    // Undetected = attribution unknown; diplomatic response unlikely,
                    // and even less likely than the exposed-plot case.
                    bool allowsDiplomatic = ProxyWarBridge.RollAllowsDiplomaticResponse(importance - 2);
                    int expirationDays = ProxyWarBridge.ComputeExpirationDays(importance);

                    // Write event immediately with original text (no blocking)
                    string eventId = AIInfluenceWriter.WriteDynamicEvent(
                        title, description,
                        new List<string> { targetKingdom.StringId },
                        characterIds,
                        campaignDays,
                        importance,
                        true,
                        eventType,
                        allowsDiplomatic,
                        expirationDays);

                    // DeepSeek expansion in background
                    if ((settings?.EnableDeepSeek ?? false) && eventId != null)
                    {
                        string kingdomContext = cn
                            ? $"{targetName}遭受的神秘破坏事件"
                            : $"Mysterious disruption in {targetName}";
                        DeepSeekClient.ExpandAndUpdateEventAsync(eventId, description, title, kingdomContext, cn);
                    }

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

            // Top-center banner notification (extraTimeInMs controls how long it stays visible)
            MBInformationManager.AddQuickInformation(
                new TextObject("{=BC_AI_ReloadBanner}" + message),
                4000,
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
