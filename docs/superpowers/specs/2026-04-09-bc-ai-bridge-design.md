# BellumCivile ↔ AIInfluence 兼容补丁设计文档

## 概述

本补丁让 AIInfluence 的 AI 对话系统感知 BellumCivile 的派系和内战状态。BellumCivile 在内存中管理派系数据，AIInfluence 通过 JSON 文件获取世界信息。补丁作为桥梁，将 BellumCivile 的政治数据转换为 AIInfluence 可消费的 JSON 格式。

## 架构

```
BellumCivile (内存)          Patch (桥接模组)              AIInfluence (文件)
┌──────────────────┐    ┌──────────────────────────┐    ┌───────────────────┐
│ FactionManager   │    │ BridgeBehavior           │    │                   │
│   - 派系列表      │←反射│   DailyTick:             │    │ world_info.json   │
│   - 成员/不满度   │    │     差量对比 → 更新文件   │──→│   (王国级派系概况)  │
│   - 心情/力量     │    │   事件检测:              │    │                   │
│                  │    │     重大变化 → 写入事件   │──→│ dynamic_events.json│
│ IdeologyBehavior │←反射│                          │    │   (重大政治事件)   │
│   - 叛乱分数     │    │ NPC文件管理:             │    │                   │
│   - 意识形态     │    │   CharacterDescription   │──→│ NPC (id).json     │
│                  │    │   标记块精确替换          │    │   (个人政治数据)   │
└──────────────────┘    └──────────────────────────┘    └───────────────────┘
```

## 数据分层

### 王国层（world_info.json）

每个王国一条记录，描述该王国整体政治格局。

```json
{
  "id": "bc_kingdom_vlandia",
  "description": "瓦兰迪亚王国政治格局：...",
  "usageChance": 100,
  "applicableNPCs": ["lords", "faction_leaders"],
  "category": "world"
}
```

- 9个王国 = 9条记录
- 约150字/条，包含：派系列表、各派系领导者和成员数、意识形态对立关系、力量对比、反叛派系不满度
- 按王国过滤：只有本王国 NPC 的 `KnownInfo` 中会拥有对应 ID

### 跨王国内战摘要（world_info.json）

一条全局记录，汇总当前所有王国正在发生的内战。

```json
{
  "id": "bc_crossking_civil_wars",
  "description": "当前卡拉迪亚各王国内战局势：...",
  "usageChance": 100,
  "applicableNPCs": ["lords", "faction_leaders"],
  "category": "world"
}
```

- 所有领主 NPC 的 `KnownInfo` 都包含此 ID
- 只记录已爆发的内战，不含酝酿中的派系
- 侧重政治背景（派系原因），而非战争事实本身（AIInfluence 已能感知战争关系）

### 个人层（NPC JSON CharacterDescription）

在每个领主 NPC 的 `CharacterDescription` 字段中追加标记块：

```
[BC_POLITICAL_START]
【你的政治立场】你是贵族派成员，主张维护贵族权利。你的叛乱倾向为35/100，远未到反叛的地步。你与国王德瓦兰特的关系为+12，领地需求基本满足（拥有2块，期望2块）。你不会轻易加入反叛运动，但你在封地分配和政策投票中会倾向支持贵族派的主张。
[BC_POLITICAL_END]
```

- 用 `[BC_POLITICAL_START]...[BC_POLITICAL_END]` 标记隔离，更新时正则匹配精确替换
- 不存在时追加到末尾，不破坏原有性格描述
- 约80-120字/NPC

### 重大事件（dynamic_events.json，尝试性）

重大政治事件写入 `dynamic_events.json`，格式匹配 AIInfluence 现有事件结构：

```json
{
  "id": "bc_event_{guid}",
  "type": "political",
  "title": "事件标题",
  "description": "事件描述",
  "event_history": [...],
  "player_involved": false,
  "kingdoms_involved": ["vlandia"],
  "characters_involved": ["lord_x_y"],
  "importance": 7,
  "spread_speed": "fast",
  "allows_diplomatic_response": true,
  "applicable_npcs": ["lords", "faction_leaders", "merchants"],
  "economic_effects": [],
  "creation_time": "ISO时间",
  "creation_campaign_days": 12345.0,
  "expiration_campaign_days": 12430.0,
  "participating_kingdoms": [],
  "kingdom_statements": [],
  "requires_diplomatic_analysis": false,
  "diplomatic_rounds": 0,
  "statements_at_round_start": 0,
  "next_analysis_attempt_days": 0.0,
  "next_statement_attempt_days": {},
  "failed_statement_attempts": {}
}
```

触发写入的重大事件：
- 反叛派系成立
- 反叛派系解散
- 最后通牒发出
- 内战爆发
- 内战结束（某方获胜）

此功能为尝试性质：`dynamic_events.json` 热重载未验证，MCM 中提供开关。如果测试无效则仅依赖 `world_info.json` 方案。

## 加载顺序

### SubModule.xml 依赖

```xml
<DependedModules>
    <DependedModule Id="Native"/>
    <DependedModule Id="SandBoxCore"/>
    <DependedModule Id="Sandbox"/>
    <DependedModule Id="Bannerlord.Harmony"/>
    <DependedModule Id="Bannerlord.MBOptionScreen"/>
    <DependedModule Id="BellumCivile"/>
    <DependedModule Id="AIInfluence"/>
</DependedModules>
```

### 初始化时序

| 阶段 | 时机 | 操作 |
|------|------|------|
| `OnSubModuleLoad` | 游戏启动 | 注册 Harmony 补丁（如有） |
| `OnGameStart` | 战役开始 | 注册 `BridgeBehavior` |
| `OnSessionLaunched` | 所有模组 Behavior 就绪 | 反射获取 BellumCivile 实例，首次全量同步 |

`OnSessionLaunched` 时 BellumCivile 的 `FactionManagerBehavior` 和 `IdeologyBehavior` 已全部注册完毕，数据可安全读取。

## 同步策略

### 触发时机

| 触发 | 时机 | 操作范围 |
|------|------|---------|
| 首次初始化 | `OnSessionLaunched` | 全量：更新 `world_info.json` 所有王国 + 遍历所有已存在的领主 NPC JSON |
| 每日定时 | `DailyTick` | 差量：对比内存缓存，只更新数据有变化的 NPC 和王国条目 |
| 重大事件 | 差量对比检测到 | 立即更新相关王国 + 相关 NPC + 尝试写入 `dynamic_events.json` |

### 差量对比机制

1. 内存中为每个 NPC 缓存上一次的政治数据快照（叛乱分数、派系归属、心情等）
2. 每日 `DailyTick` 时反射读取 BellumCivile 最新状态，与缓存对比
3. 只有数据变化的 NPC 才执行文件读写
4. 日常大约 5-10 个 NPC 发生变化

### 重大事件检测

通过快照对比检测：
- 派系列表变化（新增/消失）→ 派系创建/解散
- 派系成员列表变化 → 成员加入/离开
- 不满度从 <100 变为 >=100 → 最后通牒
- 王国列表变化（内战创建反叛王国）→ 内战爆发/结束

## 文件定位

| 文件 | 定位方式 |
|------|---------|
| `world_info.json` | `ModuleHelper.GetModuleFullPath("AIInfluence")` + `world_info.json` |
| `dynamic_events.json` | AIInfluence 模组路径 + `save_data/{唯一子文件夹}/dynamic_events.json` |
| NPC JSON | AIInfluence 模组路径 + `save_data/{唯一子文件夹}/` 下，通过文件名中的 `(string_id)` 匹配 |

- `save_data` 下扫描唯一子文件夹（用户只用一个存档）
- NPC JSON 文件不存在时跳过，不创建（AIInfluence 在首次对话时才生成）

## 语言支持

- 默认自动检测游戏语言（`BannerlordConfig.Language`）
- MCM 提供手动覆盖选项
- 支持中文和英文两套描述模板
- 模板内容硬编码在代码中，通过格式化字符串填充动态数据

## MCM 配置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| 启用桥接 | bool | true | 总开关 |
| 语言 | 下拉 | 自动（跟随游戏） | 自动/中文/English |
| 尝试写入动态事件 | bool | true | 是否向 `dynamic_events.json` 写入重大事件 |
| 调试日志 | bool | false | 记录每次同步的变更内容 |

## BellumCivile 数据读取（反射）

需要反射访问的核心类和字段：

### FactionManagerBehavior
- 派系列表（`List<FactionObject>`）
- 每个 FactionObject：名称、类型、领导者氏族、成员列表、不满度、心情、所属王国

### IdeologyBehavior
- 各氏族的叛乱分数（通过调用 `CalculateRebellionScore` 方法）
- 意识形态归属

### FactionObject 关键字段
- `_name`: 派系名称
- `_parentKingdom`: 所属王国
- `_leader`: 领导氏族
- `_members`: 成员氏族列表
- `_type`: 派系类型（FactionType 枚举）
- `_discontent`: 不满度 (0-100)
- `_mood`: 心情 (-100 to +100)
- `IsIdeology`: 是否为意识形态派系

### 其他数据来源（游戏原生API）
- 氏族与国王的关系：`clan.Leader.GetRelation(kingdom.Ruler)`
- 氏族领地数：`clan.Fiefs.Count()`
- 氏族等级：`clan.Tier`
- 王国定居点列表：`kingdom.Settlements`

## NPC 文件更新规则

1. 读取 NPC JSON 文件
2. 解析 `CharacterDescription` 字段
3. 正则匹配 `\[BC_POLITICAL_START\][\s\S]*?\[BC_POLITICAL_END\]`
4. 存在则替换，不存在则追加到字段末尾
5. 写回文件
6. 不修改 `KnownInfoUserEdited` 等标志位（我们不动 `KnownInfo` 以外的服务字段）
7. 对 `KnownInfo` 数组：确保包含对应王国的 `bc_kingdom_xxx` ID 和 `bc_crossking_civil_wars` ID

## 项目结构

```
BellumCivileAIInfluencePatch/
├── BellumCivileAIInfluencePatch.csproj
├── BellumCivileAIInfluencePatch.sln
├── SubModule.xml
├── src/
│   ├── BellumCivileAIInfluencePatchSubModule.cs   # 模组入口
│   ├── BridgeBehavior.cs                          # 核心桥接 CampaignBehavior
│   ├── BCDataReader.cs                            # 反射读取 BellumCivile 数据
│   ├── AIInfluenceWriter.cs                       # 写入 AIInfluence JSON 文件
│   ├── DescriptionTemplates.cs                    # 中英文描述模板
│   ├── PoliticalSnapshot.cs                       # 政治数据快照（用于差量对比）
│   ├── Settings/
│   │   └── BellumCivileAIInfluencePatchSettings.cs # MCM 配置
│   └── Patches/
│       └── .gitkeep
└── docs/
    └── superpowers/
        └── specs/
            └── 2026-04-09-bc-ai-bridge-design.md
```
