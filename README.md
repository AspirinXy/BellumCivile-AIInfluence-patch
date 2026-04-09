# bannerlord-mod-template

Mount & Blade II Bannerlord 模组开发模板。

**框架：** .NET Framework 4.7.2 · HarmonyLib 2.x · MCMv5

---

## 前置条件

设置系统环境变量 `BANNERLORD_PATH` 指向游戏根目录：

```
BANNERLORD_PATH=G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord
```

---

## 使用方法

1. 点击 GitHub 页面 **"Use this template"** → **"Create a new repository"**
2. 克隆新仓库到本地
3. 用 IDE **全局搜索替换**以下三个占位符：

   | 占位符 | 含义 | 替换示例 |
   |--------|------|----------|
   | `ModTemplate` | 命名空间、类名、Id | `RefinedCombat` |
   | `mod-template` | kebab-case 标识符 | `refined-combat` |
   | `com.modtemplate` | Harmony ID | `com.refinedcombat` |

4. 构建验证：

   ```bash
   dotnet build -c Debug
   ```

   DLL 自动输出到 `%BANNERLORD_PATH%\Modules\<ModName>\bin\Win64_Shipping_Client\`

---

## 目录结构

```
ModTemplate.csproj          # 项目文件，引用 BANNERLORD_PATH 下的游戏 DLL
SubModule.xml               # 模组声明（名称、版本、依赖）
src/
  ModTemplateSubModule.cs   # 模组入口，Harmony PatchAll
  Settings/
    ModTemplateSettings.cs  # MCM 游戏内配置面板
  Patches/                  # 存放 Harmony Patch 类
```

---

## 常见问题

**Q: 游戏启动报 "Cannot find: ModTemplate"**
A: `SubModule.xml` 的 `<DLLName>` 必须带 `.dll` 后缀，例如 `ModTemplate.dll`。

**Q: MCM 菜单看不到模组选项**
A: `ModTemplateSettings` 必须正确实现 `Id`、`DisplayName`、`FolderName`、`FormatType` 四个属性。

**Q: Harmony Patch 参数注入拿到全零默认值**
A: 游戏版本升级后参数名可能变化，改用 `object[] __args` 按索引取值。
