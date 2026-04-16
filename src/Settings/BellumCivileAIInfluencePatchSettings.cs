using System;
using System.Threading.Tasks;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

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
        public Dropdown<string> Language { get; set; }
            = new Dropdown<string>(
                new[] { "Auto", "中文", "English" }, 0);

        [SettingPropertyBool("Write Dynamic Events", Order = 2, RequireRestart = false,
            HintText = "Attempt to write major political events to dynamic_events.json (experimental).")]
        [SettingPropertyGroup("General")]
        public bool WriteDynamicEvents { get; set; } = true;

        [SettingPropertyBool("Enable DeepSeek Expansion", Order = 3, RequireRestart = false,
            HintText = "Use DeepSeek AI to expand brief event descriptions into immersive narratives.")]
        [SettingPropertyGroup("DeepSeek")]
        public bool EnableDeepSeek { get; set; } = false;

        [SettingPropertyText("API Key", Order = 4, RequireRestart = false,
            HintText = "DeepSeek API key. Get one from platform.deepseek.com")]
        [SettingPropertyGroup("DeepSeek")]
        public string DeepSeekApiKey { get; set; } = "sk-xxx";

        [SettingPropertyText("API URL", Order = 5, RequireRestart = false,
            HintText = "DeepSeek API endpoint URL. Default: https://api.deepseek.com/v1/chat/completions")]
        [SettingPropertyGroup("DeepSeek")]
        public string DeepSeekApiUrl { get; set; } = "https://api.deepseek.com/v1/chat/completions";

        [SettingPropertyText("Model", Order = 6, RequireRestart = false,
            HintText = "DeepSeek model name. Default: deepseek-chat")]
        [SettingPropertyGroup("DeepSeek")]
        public string DeepSeekModel { get; set; } = "deepseek-chat";

        private bool _testApiConnection;

        [SettingPropertyBool("Test API Connection", Order = 7, RequireRestart = false,
            HintText = "Toggle ON to test the DeepSeek API connection. Result will show as a banner message.")]
        [SettingPropertyGroup("DeepSeek")]
        public bool TestApiConnection
        {
            get => _testApiConnection;
            set
            {
                if (value && !_testApiConnection)
                {
                    _testApiConnection = true;
                    RunApiTest();
                    // Reset after a short delay
                    Task.Delay(500).ContinueWith(_ => _testApiConnection = false);
                }
                else
                {
                    _testApiConnection = value;
                }
            }
        }

        private void RunApiTest()
        {
            Task.Run(() =>
            {
                try
                {
                    string apiKey = DeepSeekApiKey;
                    if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "sk-xxx")
                    {
                        ShowTestResult(false, "API Key not configured");
                        return;
                    }

                    bool cn = GetPatchLanguage() == PatchLanguage.Chinese ||
                              (GetPatchLanguage() == PatchLanguage.Auto &&
                               DescriptionTemplates.ShouldUseChinese(PatchLanguage.Auto));

                    string result = DeepSeekClient.ExpandDescription(
                        "A test event for API connection verification.",
                        "API Test",
                        "Testing DeepSeek connection",
                        cn);

                    bool success = !string.IsNullOrWhiteSpace(result) &&
                                   result != "A test event for API connection verification.";
                    ShowTestResult(success, success ? "API response received" : "No expansion returned");
                }
                catch (Exception ex)
                {
                    ShowTestResult(false, ex.Message);
                }
            });
        }

        private void ShowTestResult(bool success, string detail)
        {
            string message = success
                ? $"[BC-AI Bridge] DeepSeek API test SUCCESS: {detail}"
                : $"[BC-AI Bridge] DeepSeek API test FAILED: {detail}";

            var color = success ? Colors.Green : Colors.Red;

            InformationManager.DisplayMessage(new InformationMessage(message, color));

            try
            {
                string bannerText = success
                    ? "DeepSeek API connection test successful!"
                    : $"DeepSeek API test failed: {detail}";
                MBInformationManager.AddQuickInformation(
                    new TextObject("{=BC_AI_TestResult}" + bannerText),
                    4000, null, null, "");
            }
            catch { }
        }

        private bool _syncEvents;

        [SettingPropertyBool("Sync Dynamic Events", Order = 8, RequireRestart = false,
            HintText = "Toggle ON to sync BellumCivile dynamic events into AIInfluence's dynamic_events.json. Events are stored locally first and only written to AIInfluence when you click this.")]
        [SettingPropertyGroup("General")]
        public bool SyncEvents
        {
            get => _syncEvents;
            set
            {
                if (value && !_syncEvents)
                {
                    _syncEvents = true;
                    RunSync();
                    Task.Delay(500).ContinueWith(_ => _syncEvents = false);
                }
                else
                {
                    _syncEvents = value;
                }
            }
        }

        private void RunSync()
        {
            try
            {
                float campaignDays = Campaign.Current != null
                    ? (float)CampaignTime.Now.ToDays
                    : 0f;

                var (synced, total) = AIInfluenceWriter.SyncDynamicEvents(campaignDays);

                bool cn = GetPatchLanguage() == PatchLanguage.Chinese ||
                          (GetPatchLanguage() == PatchLanguage.Auto &&
                           DescriptionTemplates.ShouldUseChinese(PatchLanguage.Auto));

                string message = cn
                    ? $"同步完成：{synced} 个新事件已写入，本地共 {total} 个事件"
                    : $"Sync complete: {synced} new events written, {total} total in local store";

                InformationManager.DisplayMessage(
                    new InformationMessage("[BC-AI Bridge] " + message, Colors.Green));

                MBInformationManager.AddQuickInformation(
                    new TextObject("{=BC_AI_SyncResult}" + message),
                    4000, null, null, "");
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("[BC-AI Bridge] Sync failed: " + ex.Message, Colors.Red));
            }
        }

        [SettingPropertyBool("Debug Log", Order = 9, RequireRestart = false,
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
