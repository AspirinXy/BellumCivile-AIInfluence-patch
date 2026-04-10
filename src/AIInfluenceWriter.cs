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

        public static void WriteDynamicEvent(string title, string description,
            string kingdomId, List<string> characterIds, float campaignDays)
        {
            WriteDynamicEvent(title, description,
                new List<string> { kingdomId }, characterIds,
                campaignDays, 7, false);
        }

        public static void WriteDynamicEvent(string title, string description,
            List<string> kingdomIds, List<string> characterIds,
            float campaignDays, int importance, bool requiresDiplomaticAnalysis)
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
                    ["id"] = Guid.NewGuid().ToString(),
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
                    ["kingdoms_involved"] = new JArray(kingdomIds.ToArray()),
                    ["characters_involved"] = new JArray(characterIds.ToArray()),
                    ["importance"] = importance,
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
                    ["requires_diplomatic_analysis"] = requiresDiplomaticAnalysis,
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
