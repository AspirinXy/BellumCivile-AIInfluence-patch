using System;
using System.Threading.Tasks;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
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
                    0, null, null, "");
            }
            catch { }
        }

        [SettingPropertyBool("Debug Log", Order = 8, RequireRestart = false,
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
