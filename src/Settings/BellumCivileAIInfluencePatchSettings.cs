using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace BellumCivileAIInfluencePatch.Settings
{
    /// <summary>
    /// MCM 游戏内配置面板。
    /// MCM 通过反射自动发现此类，无需手动注册。
    /// 必须实现四个抽象属性：Id, DisplayName, FolderName, FormatType。
    /// </summary>
    public class BellumCivileAIInfluencePatchSettings : AttributeGlobalSettings<BellumCivileAIInfluencePatchSettings>
    {
        public override string Id => "BellumCivileAIInfluencePatch";
        public override string DisplayName => "BellumCivile AI Influence Patch";
        public override string FolderName => "BellumCivileAIInfluencePatch";
        public override string FormatType => "json2";

        // 示例配置项，开发时替换或删除
        [SettingPropertyBool("启用功能", Order = 0, RequireRestart = false,
            HintText = "启用/禁用主要功能")]
        [SettingPropertyGroup("基础开关")]
        public bool EnableFeature { get; set; } = true;
    }
}
