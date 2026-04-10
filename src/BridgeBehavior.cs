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
                AIInfluenceWriter.UpdateNpcFile(kv.Value.HeroStringId, desc, kv.Value.KingdomId, debug);
                updatedCount++;
            }

            if (debug)
                LogMessage($"NPC files checked: {updatedCount} lords", false);

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
                    AIInfluenceWriter.UpdateNpcFile(kv.Value.HeroStringId, desc, kv.Value.KingdomId, debug);
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
                            ? $"{faction.LeaderClanName}氏族领导的\u201c{name}\u201d正式成立，以\u201c{faction.DemandDescription}\u201d为诉求，{faction.MemberCount}个氏族响应。王国内部政治裂痕加深。"
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
                            ? $"\u201c{name}\u201d已宣告解散，{prevFaction.LeaderClanName}氏族领导的反叛运动暂告平息。"
                            : $"The \"{name}\" has disbanded. The rebellion led by Clan {prevFaction.LeaderClanName} has been quelled.";

                        AIInfluenceWriter.WriteDynamicEvent(title, desc,
                            prevFaction.KingdomId,
                            prevFaction.MemberClanIds,
                            campaignDays);
                    }
                }
            }

            // Detect civil war outbreak
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
                            ? $"\u201c{faction.FactionName}\u201d向{kv.Value.RulerName}发出最后通牒，要求\u201c{faction.DemandDescription}\u201d。若被拒绝，内战将不可避免。"
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
