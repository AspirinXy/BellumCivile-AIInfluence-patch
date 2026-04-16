using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static string _ownModulePath;
        private static readonly object _fileLock = new object();

        /// <summary>
        /// Path to our own local event store: {OurMod}/bc_events_{saveFolderName}.json
        /// This file is ours alone — the main AIInfluence mod never touches it.
        /// </summary>
        private static string _localEventsPath;

        public static bool Initialize()
        {
            try
            {
                _aiModulePath = Path.Combine(
                    BasePath.Name, "Modules", "AIInfluence");

                _ownModulePath = Path.Combine(
                    BasePath.Name, "Modules", "BellumCivileAIInfluencePatch");

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

                RefreshLocalEventsPath();

                return true;
            }
            catch (Exception ex)
            {
                LogError("AIInfluenceWriter.Initialize failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Build the local events file path based on the current save_data subfolder name.
        /// </summary>
        private static void RefreshLocalEventsPath()
        {
            if (_saveDataPath == null || _ownModulePath == null)
            {
                _localEventsPath = null;
                return;
            }

            string saveFolderName = Path.GetFileName(_saveDataPath);
            _localEventsPath = Path.Combine(_ownModulePath, "bc_events_" + saveFolderName + ".json");
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
                    return id != null && (id.StartsWith("bc_kingdom_") || id == "bc_crossking_civil_wars");
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

        public static bool UpdateNpcFile(string heroStringId, string clanDescription,
            string kingdomId, bool enableDebugLog)
        {
            if (_saveDataPath == null || heroStringId == null) return false;

            string npcFilePath = FindNpcFile(heroStringId);
            if (npcFilePath == null) return false; // File doesn't exist yet — skip

            try
            {
                string json = File.ReadAllText(npcFilePath);
                var npc = JsonConvert.DeserializeObject<JObject>(json);
                if (npc == null) return false;

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

                return true;
            }
            catch (Exception ex)
            {
                LogError($"UpdateNpcFile failed for {heroStringId}: {ex.Message}");
                return false;
            }
        }

        public static string WriteDynamicEvent(string title, string description,
            string kingdomId, List<string> characterIds, float campaignDays)
        {
            return WriteDynamicEvent(title, description,
                new List<string> { kingdomId }, characterIds,
                campaignDays, 7, false);
        }

        public static string WriteDynamicEvent(string title, string description,
            List<string> kingdomIds, List<string> characterIds,
            float campaignDays, int importance, bool requiresDiplomaticAnalysis)
        {
            return WriteDynamicEvent(title, description, kingdomIds, characterIds,
                campaignDays, importance, requiresDiplomaticAnalysis,
                "political", true, 84);
        }

        /// <summary>
        /// Creates a new dynamic event and saves it to our LOCAL file only.
        /// The event will not appear in AIInfluence until the user clicks
        /// "Sync Dynamic Events" in MCM.
        /// </summary>
        public static string WriteDynamicEvent(string title, string description,
            List<string> kingdomIds, List<string> characterIds,
            float campaignDays, int importance, bool requiresDiplomaticAnalysis,
            string eventType, bool allowsDiplomaticResponse, int expirationDays)
        {
            if (_localEventsPath == null) return null;

            if (expirationDays < 10) expirationDays = 10;
            else if (expirationDays > 84) expirationDays = 84;

            lock (_fileLock)
            {
                try
                {
                    // Read our local store
                    List<JObject> events = ReadLocalEvents();

                    string eventId = Guid.NewGuid().ToString();
                    var newEvent = new JObject
                    {
                        ["id"] = eventId,
                        ["type"] = eventType ?? "political",
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
                        ["kingdoms_involved"] = new JArray(kingdomIds.ToArray()),
                        ["characters_involved"] = new JArray(characterIds.ToArray()),
                        ["importance"] = importance,
                        ["spread_speed"] = "normal",
                        ["allows_diplomatic_response"] = allowsDiplomaticResponse,
                        ["applicable_npcs"] = new JArray("lords", "faction_leaders", "companions", "merchants"),
                        ["economic_effects"] = new JArray(),
                        ["creation_time"] = DateTime.Now.ToString("o"),
                        ["creation_campaign_days"] = campaignDays,
                        ["expiration_time"] = DateTime.Now.AddDays(expirationDays).ToString("o"),
                        ["expiration_campaign_days"] = campaignDays + expirationDays,
                        ["participating_kingdoms"] = new JArray(kingdomIds.ToArray()),
                        ["kingdom_statements"] = new JArray(),
                        ["requires_diplomatic_analysis"] = requiresDiplomaticAnalysis,
                        ["diplomatic_rounds"] = 0,
                        ["statements_at_round_start"] = 0,
                        ["next_analysis_attempt_days"] = 0.0,
                        ["next_statement_attempt_days"] = new JObject(),
                        ["failed_statement_attempts"] = new JObject()
                    };

                    events.Add(newEvent);

                    // Prune expired events
                    PruneExpired(events, campaignDays);

                    WriteLocalEvents(events);
                    return eventId;
                }
                catch (Exception ex)
                {
                    LogError("WriteDynamicEvent failed: " + ex.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// Updates both title and description of an event. If newTitle is null/empty,
        /// only description is updated (graceful fallback when DeepSeek doesn't return a title).
        /// </summary>
        public static void UpdateEventTitleAndDescription(string eventId, string newTitle, string newDescription)
        {
            if (_localEventsPath == null || eventId == null) return;

            lock (_fileLock)
            {
                try
                {
                    List<JObject> events = ReadLocalEvents();
                    if (events.Count == 0) return;

                    var target = events.FirstOrDefault(e => e["id"]?.ToString() == eventId);
                    if (target == null) return;

                    if (!string.IsNullOrWhiteSpace(newTitle))
                        target["title"] = newTitle;

                    if (!string.IsNullOrWhiteSpace(newDescription))
                    {
                        target["description"] = newDescription;
                        var history = target["event_history"] as JArray;
                        if (history != null && history.Count > 0)
                            history[0]["description"] = newDescription;
                    }

                    WriteLocalEvents(events);
                }
                catch (Exception ex)
                {
                    LogError("UpdateEventTitleAndDescription failed: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Updates the description of an event in our LOCAL file.
        /// Called from background thread after DeepSeek expansion completes.
        /// </summary>
        public static void UpdateEventDescription(string eventId, string newDescription)
        {
            if (_localEventsPath == null || eventId == null) return;

            lock (_fileLock)
            {
                try
                {
                    List<JObject> events = ReadLocalEvents();
                    if (events.Count == 0) return;

                    var target = events.FirstOrDefault(e => e["id"]?.ToString() == eventId);
                    if (target == null) return;

                    target["description"] = newDescription;

                    // Also update the first event_history entry
                    var history = target["event_history"] as JArray;
                    if (history != null && history.Count > 0)
                    {
                        history[0]["description"] = newDescription;
                    }

                    WriteLocalEvents(events);
                }
                catch (Exception ex)
                {
                    LogError("UpdateEventDescription failed: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Syncs our locally stored events into AIInfluence's dynamic_events.json.
        /// Reads both files, merges by id (our events win on conflict), writes back.
        /// Returns (synced, total) counts.
        /// </summary>
        public static (int synced, int total) SyncDynamicEvents(float campaignDays)
        {
            if (_saveDataPath == null || _localEventsPath == null)
                return (0, 0);

            string targetPath = Path.Combine(_saveDataPath, "dynamic_events.json");

            lock (_fileLock)
            {
                try
                {
                    // Read our local events
                    List<JObject> ours = ReadLocalEvents();
                    PruneExpired(ours, campaignDays);

                    if (ours.Count == 0)
                        return (0, 0);

                    // Read AIInfluence's current dynamic_events.json
                    List<JObject> target;
                    if (File.Exists(targetPath))
                    {
                        string json = File.ReadAllText(targetPath);
                        target = JsonConvert.DeserializeObject<List<JObject>>(json) ?? new List<JObject>();
                    }
                    else
                    {
                        target = new List<JObject>();
                    }

                    // Build index of existing ids in target
                    var existingIds = new Dictionary<string, int>();
                    for (int i = 0; i < target.Count; i++)
                    {
                        string id = target[i]["id"]?.ToString();
                        if (id != null) existingIds[id] = i;
                    }

                    // Merge: add missing, update existing with our version
                    int syncedCount = 0;
                    foreach (var ev in ours)
                    {
                        string id = ev["id"]?.ToString();
                        if (id == null) continue;

                        if (existingIds.TryGetValue(id, out int idx))
                        {
                            // Already exists — update with our version (may have DeepSeek expansion)
                            target[idx] = (JObject)ev.DeepClone();
                        }
                        else
                        {
                            // Missing — add it
                            target.Add((JObject)ev.DeepClone());
                            syncedCount++;
                        }
                    }

                    // Write back
                    string output = JsonConvert.SerializeObject(target, Formatting.Indented);
                    File.WriteAllText(targetPath, output);

                    // Also save pruned local file
                    WriteLocalEvents(ours);

                    return (syncedCount, ours.Count);
                }
                catch (Exception ex)
                {
                    LogError("SyncDynamicEvents failed: " + ex.Message);
                    return (0, 0);
                }
            }
        }

        /// <summary>Returns the number of pending events in our local store.</summary>
        public static int GetPendingEventCount()
        {
            if (_localEventsPath == null) return 0;
            lock (_fileLock)
            {
                return ReadLocalEvents().Count;
            }
        }

        // ── Local file helpers ──────────────────────────────────────────

        private static List<JObject> ReadLocalEvents()
        {
            if (_localEventsPath != null && File.Exists(_localEventsPath))
            {
                string json = File.ReadAllText(_localEventsPath);
                return JsonConvert.DeserializeObject<List<JObject>>(json) ?? new List<JObject>();
            }
            return new List<JObject>();
        }

        private static void WriteLocalEvents(List<JObject> events)
        {
            if (_localEventsPath == null) return;
            string output = JsonConvert.SerializeObject(events, Formatting.Indented);
            File.WriteAllText(_localEventsPath, output);
        }

        private static void PruneExpired(List<JObject> events, float campaignDays)
        {
            events.RemoveAll(e =>
            {
                var expDays = e["expiration_campaign_days"];
                if (expDays != null && (expDays.Type == JTokenType.Float || expDays.Type == JTokenType.Integer))
                {
                    return (float)expDays < campaignDays;
                }
                return false;
            });
        }

        // ── NPC / other helpers ─────────────────────────────────────────

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

        private static string InjectPoliticalBlock(string charDesc, string politicalBlock)
        {
            // Use simple string search instead of regex to avoid catastrophic
            // backtracking when the end tag is missing from long descriptions.
            const string startTag = "[BC_POLITICAL_START]";
            const string endTag = "[BC_POLITICAL_END]";

            int startIdx = charDesc.IndexOf(startTag, StringComparison.Ordinal);
            if (startIdx >= 0)
            {
                int endIdx = charDesc.IndexOf(endTag, startIdx, StringComparison.Ordinal);
                if (endIdx >= 0)
                {
                    // Replace existing block (including both tags)
                    return charDesc.Substring(0, startIdx)
                        + politicalBlock
                        + charDesc.Substring(endIdx + endTag.Length);
                }
                else
                {
                    // Start tag found but no end tag — replace from start tag to end of string
                    return charDesc.Substring(0, startIdx) + politicalBlock;
                }
            }

            return charDesc + "\n\n" + politicalBlock;
        }

        public static void RefreshSaveDataPath()
        {
            if (_aiModulePath == null) return;
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
            RefreshLocalEventsPath();
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
