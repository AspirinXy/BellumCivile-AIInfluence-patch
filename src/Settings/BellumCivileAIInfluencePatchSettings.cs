using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

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

        [SettingPropertyBool("Debug Log", Order = 7, RequireRestart = false,
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
