using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ModTemplate
{
    /// <summary>
    /// 模组主入口类，继承 MBSubModuleBase。
    /// OnSubModuleLoad：游戏启动时自动调用，在此处初始化 Harmony 补丁。
    /// OnGameStart：每局游戏开始时调用，在此处注入 MissionBehavior 或其他逻辑。
    /// </summary>
    public class ModTemplateSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            new Harmony("com.modtemplate").PatchAll(Assembly.GetExecutingAssembly());
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            // 在此处添加游戏启动逻辑，例如注入 MissionBehavior：
            // if (gameStarter is CampaignGameStarter campaignStarter)
            //     campaignStarter.AddBehavior(new YourBehavior());
        }
    }
}
