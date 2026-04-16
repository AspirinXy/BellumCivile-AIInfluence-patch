# BellumCivile 模组完整功能说明

> 本文档基于 `BellumCivile.dll` 反编译代码整理，用于查阅游戏中各系统如何运作。
> 版本参照：`G:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/BellumCivile/bin/Win64_Shipping_Client/BellumCivile.dll`

---

## 目录

1. [核心概念](#1-核心概念)
2. [意识形态派系系统](#2-意识形态派系系统)
3. [派系叛乱机制（完整 4 阶段）](#3-派系叛乱机制完整-4-阶段)
4. [内战结算与处决规则](#4-内战结算与处决规则)
5. [代理战争（阴谋）](#5-代理战争阴谋)
6. [文化化氏族继承](#6-文化化氏族继承)
7. [国王选举重构](#7-国王选举重构)
8. [封地与政策投票](#8-封地与政策投票)
9. [婚姻嫁妆](#9-婚姻嫁妆)
10. [驱逐氏族机制](#10-驱逐氏族机制)
11. [安抚机制](#11-安抚机制)
12. [争议事件](#12-争议事件)
13. [战友情谊](#13-战友情谊)
14. [王朝继承人](#14-王朝继承人)
15. [意识形态冲击事件](#15-意识形态冲击事件)
16. [第三方介入](#16-第三方介入)
17. [原版覆盖清单](#17-原版覆盖清单)

---

## 1. 核心概念

BellumCivile 把卡拉迪亚的王国政治从"简单外交"改造为**两层政治生态**：

- **永久层（意识形态派系）**：每个王国天生有 4 个意识形态派系——保皇派、贵族派、军国派、平民派——氏族根据特质自动归属，不会消失。
- **动态层（叛乱派系）**：当意识形态派系积怨到顶时，会演化为具体的反王联盟、独立派、封地重分派等"行动派系"，发动内战。

**关键：没有白和平**（`BlockWhitePeacePatch`）。内战一旦开打必须分出胜负——领袖被俘、被杀或王国领土被完全吞并。

---

## 2. 意识形态派系系统

### 4 大派系

| 派系 | 中文名 | 核心诉求 | 偏好特质 |
|---|---|---|---|
| Royalists | 保皇派 | 效忠王朝正统，反对僭主 | Honor、低 Calculating |
| Aristocrats | 贵族派 | 维护封建特权，反对平民化 | 高阶氏族、Calculating |
| Militarists | 军国派 | 主张对外战争、军事荣耀 | Valor、Merciful/Cruel 混合 |
| Populists | 平民派 | 减负减税、限制贵族特权 | Generosity、低阶氏族 |

### 每派系属性

| 字段 | 含义 |
|---|---|
| `Members` | 派系成员氏族列表 |
| `Leader` | 派系领袖氏族（影响力最高的成员） |
| `LoyalClan` | **仅保皇派**：要效忠的氏族（默认=王室） |
| `Mood`（-100 到 100） | 情绪。< -20 表示不满可能叛乱 |
| `Discontent`（0-100） | 不满累积条，100 触发最后通牒 |
| `Type` | FactionType 枚举 |

### 支持的政策（每派系）

保皇派：神圣王权、皇家特权、帝国城镇、王室义务、皇家专员、御林军、土地先占权、国家专卖、行政官、货币贬值、王室赎金权、皇家海军特权、龙骨什一税

贵族派：元老院、领主枢密会、贵族扈从、农奴制、封建继承、城堡特许、道路通行税、土地税、司法官

军国派：元帅府、军事勋章、退伍土地分配、战争税、军械库法令、海军合练、海防御令、兄弟舰队、海盗赦免、劫掠战利品

平民派：债务豁免、平民护民官、平民议会、陪审制、公民权、自治郡县、放牧权、民兵加强、狩猎权、地方法官、海洋福祉

投票时派系成员按**意识形态**优先投票，而非原版的"什么对我有利"。

---

## 3. 派系叛乱机制（完整 4 阶段）

### 阶段 1：不满积累（Discontent 0 → 100）

**每日检查**（每个派系每天）：

```
若 FactionPower > KingdomPower × 权力阈值（最低 0.4）：
    Discontent += 2 × (FactionPower ÷ 阈值)
否则：
    Discontent -= 1
```

弱势派系永远不会叛乱。派系权力 = 成员氏族实力总和。

### 阶段 2：最后通牒（Discontent = 100）

自动触发 `TriggerUltimatum`。

- **玩家是国王** → 弹窗让玩家选择"接受"或"拒绝"
- **AI 国王** → 按概率自动决定

**AI 接受概率公式**：

- 派系权力 < 王国忠诚权力 → **强制拒绝**（开战）
- 否则：
  - 基础 10%
  - 每 50% 额外权力优势 +10%（上限累计 +50%）
  - 王国正对外战争 +10%
  - 国王 Calculating +1/+2 → +5% / +10%
  - 国王 Calculating -1/-2 → -5% / -10%
  - 国王 Valor +1/+2 → -5% / -10%（勇武更愿硬刚）
  - 国王 Mercy +1/+2 → +5% / +10%
  - Clamp(0, 100%)

掷骰决定 AcceptDemands 或 RefuseDemandsAndRebel。

### 阶段 3a：和平化解（AcceptDemands）

根据派系诉求（`FactionType` 决定）：

| 类型 | 结果 |
|---|---|
| **Abdication**（贵族/军国/平民派发动） | 国王召开王位选举 `KingSelectionKingdomDecision`，可能失位 |
| **InstallRuler**（保皇派发动） | 直接把王位让给派系拥立的氏族 |
| **Independence** | 派系及成员集体脱离，建立独立王国（按文化命名）：<br>• Vlandia → Duchy of ...<br>• Battania → Chiefdom of ...<br>• Sturgia → Principality of ...<br>• Nord/Sturgia北 → Jarldom of ...<br>• Empire → Despotate of ...<br>• Aserai → Emirate of ...<br>• Khuzait → ... Horde<br>• 其他 → ... Confederacy |
| **FiefRedistribution** | 强制把封地从派系外氏族划给派系氏族 |

消息："{王国}接受{派系}诉求，避免了内战"。

### 阶段 3b：开战（RefuseDemandsAndRebel）

1. 新建叛军王国，ID = `{原王国}_rebels_{领袖氏族}`
2. 派系领袖成为叛军统治氏族
3. 派系全体成员氏族换旗到叛军
4. **自动宣战**：叛军 vs 原王国
5. 锁定外部介入一段时间（见第 16 节）

消息："领地碎裂。{领袖}已在公开反抗{王国}"。

### 阶段 4：内战结束

详见第 4 节。

---

## 4. 内战结算与处决规则

### 触发条件（`CivilWarResolutionBehavior`）

监听两个事件：

- **`HeroPrisonerTaken`**：
  - 叛军领袖被俘 → `ResolveLiegeVictory`（国王胜）
  - 国王被俘 → `ResolveRebelVictory`（叛军胜）
- **`HeroKilled`**：领袖之一被杀也会触发结算

### `ApplyPostWarConsequences` 对每个败方氏族逐个判定

**先判特赦（先于处决）**

| 修正因素 | 加权 |
|---|---|
| 基础 | 10% |
| 胜方 Mercy ≥ +1 | +30% |
| 胜方 Mercy ≥ +2 | 额外 +20% |
| 胜方 Mercy ≤ -1 | -20% |
| 胜方 Generosity ≥ +1 | +20% |
| Clamp | 0 - 100% |

特赦命中 → 氏族被宽恕、保留领地、强制加入胜方王国。

**未特赦则判处决**

| 胜方 Mercy | 处决率 |
|---|---|
| +2 | 20% |
| +1 | 30% |
| 0 | 50%（基础）|
| -1 | 70% |
| -2 | 80% |

命中则对氏族领袖调用 `KillCharacterAction.ApplyByExecution`。

**消息**："以王室之名，{胜方国王}处决了{败方领袖} {原因}"
- 原因-旧王："以巩固新得到的权力"
- 原因-叛徒："以叛国之罪"

### 处决后氏族的后续

| 情况 | 结果 |
|---|---|
| 领袖被处决 | **氏族不灭**。原版 Bannerlord 继承机制接管：配偶/成年子女/兄弟姐妹顺位接班 |
| 其他家族成员 | 全部存活，保留头衔、关系、部队，随氏族迁移 |
| 整个家族血统全灭 | `OnClanDestroyed` 触发，氏族从游戏移除 |
| 保皇派的拥立氏族全灭 | `HandleClanDestroyed` 将派系 LoyalClan 回退为派系领袖氏族 |

### 领地处置（与处决独立判定）

**领地剥夺概率**：基础 50%，由胜方 **Generosity** 修正：
- Gen +2 → 20% / +1 → 30% / 0 → 50% / -1 → 70% / -2 → 80%

命中：剥夺败方氏族**最富裕**的领地，交给胜方的非王族氏族。

**剥夺后结局分三种**：
- 氏族完全无领地 → 流放到同文化王国（若无则无国流亡）
- 氏族还有其他领地 → 强制加入胜方王国
- 未剥夺 → 同上强制加入

### 战后冲击（`ApplyTribunalShocks`）

胜方王国的意识形态派系 Mood 会根据结算结果变化：
- 有处决 → 贵族派不满（同侪被处决的警告）
- 有宽恕 → 平民派情绪回暖
- 有流放 → 各派系反应不一
- 旧王被处决 → 保皇派 Mood 大跌

---

## 5. 代理战争（阴谋）

### 触发与目标选择

**每周 tick**（`ProxyWarBehavior.EvaluateEspionageTarget`）每个非玩家、非小派系王国都会检查：

1. 施动方冷却未过 → 跳过
2. 遍历 `Kingdom.All`（**固定顺序，无随机化**），找第一个满足条件的目标：
   - 不是盟友、不是小派系、非自己
   - `flag2`（是否攻击）判定：
     - 国王 Calculating ≥ 1 或 Honor ≤ -1 → 总是攻击
     - Honor ≥ 1 或 Mercy ≥ 1 → 关系 < -40 才攻击
     - 否则 → 已开战或关系 < -20 攻击

> **副作用**：因为无随机化，目标顺序固定，**北帝国在 Kingdom.All 中靠前且开局与多国低关系**，容易被多方并发针对。冷却只对施动方生效，对目标方无保护。

### ActionType 分级（0-9）

若目标已有叛军派系 → 随机 5-9（内战支援型）
否则 → 随机 0-4（一般骚扰型）

| ID | 中文 | 效果 |
|---|---|---|
| 0 | 资助土匪 | 首都附近大量土匪团，扰乱商路 |
| 1 | 煽动农民暴动 | 目标最脆弱城市忠诚度暴跌 |
| 2 | 宫廷丑闻 | 国王影响力大幅下降 |
| 3 | 抹黑政治派系 | 派系 Mood 恶化 |
| 4 | 资助异见 | 加速氏族叛变路径 |
| 5 | 外国军事顾问 | 叛军全部部队 +40 凝聚力 |
| 6 | 战争金库 | 叛军领袖获得 75,000 第纳尔 |
| 7 | 收买雇佣兵合同 | 最多 2 个雇佣氏族叛变到叛军 |
| 8 | 远征雇佣兵 | 叛军首领获得 50 名精锐 |
| 9 | 策反叛逃 | 从原王国挑对国王关系最差的忠诚氏族，整体叛逃到叛军 |

### 发现 Roll

`RollForDiscovery(leader, victim, actionType)` 判定是否暴露。
- 被发现 → `ApplyDiscoveryFallout`：外交危机、关系大跌、冷却 10 天
- 未发现 → `ApplyEspionageEffects`：效果生效，施动方 +10 关系（若有具体受害人、actionType ≥ 5）、冷却 10 天
- 中止（目标无资源可操作）→ 金币退回

### 成本

`GetCostForAction(actionType)` 决定金币消耗，国王必须有 `cost + 100,000` 才会行动。

---

## 6. 文化化氏族继承

原版 Bannerlord 继承规则单一（最近男性血亲）。BC 按**文化**给 9 种不同继承法（`DynamicSuccessionModel`）：

| 文化 | 继承法 | 规则 |
|---|---|---|
| Vlandia | 长子继承（Primogeniture） | 最年长成年男性子嗣 |
| Sturgia | 兄终弟及（Rota） | 氏族最年长在世男性成员 |
| Khuzait | 幼子继承（Ultimogeniture） | 最年幼成年男性子嗣 |
| Aserai | 舒拉协商（Shura） | 氏族成员年龄/影响力综合评分 |
| Battania | 塔尼斯特（Tanistry） | 氏族成员战技/氏族关系综合 |
| Empire（3国） | 拉丁式 | 长子优先，女性次之 |
| 其他/混合 | 默认逻辑 | 影响力 + 年龄加权 |

影响：氏族领袖继承、王位继承候选人。

---

## 7. 国王选举重构

`KingSelectionAIPatch` + `ElectionArchetypes` 替换原版"经济考量"为三种**候选人原型**：

### 三原型

| 原型 | 判定条件 |
|---|---|
| **DynasticHeir（王朝继承人）** | 氏族有王室血缘，特质倾向 Honor/Mercy |
| **MilitaryChallenger（军事挑战者）** | Valor、Calculating 高，军功多，派系=军国派领袖 |
| **WealthyAdministrator（富有行政者）** | 影响力 + 金库 + 领地总和高，派系=贵族派领袖 |

### 投票逻辑

每个投票氏族按以下加权给每候选人打分：

1. **派系认同**：同派系候选人大幅加分，保皇派对王室候选人额外加分
2. **意识形态匹配**：候选人原型 vs 投票者派系偏好
3. **个人恩怨**：关系/战友情谊/敌对历史
4. **原版经济考量**：少量权重（不再是主导）

`MarriageDowryPatch` 还会把候选人的联姻网络纳入考虑。

### 相关常量

- `KingSelectDefectionPenalty` = 150（王位失利方可能叛变）
- `KingSelectDefectionRelThreshold` = -50（叛变触发阈值）

---

## 8. 封地与政策投票

### 封地投票（`FiefVoteAIPatch`）

替代原版"谁最需要/最有贡献"，改为**派系恩怨驱动**：

- **派系惩罚**：对敌对派系候选氏族打负分
- **反建制加成**：非保皇派给反派候选氏族加分 `FiefAntiEstablishmentRally = 40`
- **暴君奖励**：若国王声名暴虐，反王派系额外加分 `FiefTyrantGreedBonus = 100`、`FiefTyrantDishonorBonus = 100`、`FiefTyrantCalcBonus = 50`
- **怨气加成**：之前被剥夺的氏族下次优先
- **原版经济权重**：最低

### 封地结算（`FiefVoteResolutionPatch`）

决票时记录 `_recentFiefAwards` 和 `_recentFiefSnubs`（5 天冷却），影响之后派系 Mood。

### 政策投票（`PolicyVoteAIPatch` / `PolicyVoteResolutionPatch`）

按 `IdeologyPolicyRoster` 支持列表投票：
- 派系支持列表里的政策 → 同派系 100% 赞成
- 列表外政策 → 按原版经济逻辑 + 少量派系偏好

---

## 9. 婚姻嫁妆

`MarriageDowryPatch` 改为按**氏族等级差**动态计算：

- 女方氏族等级 = 男方 → 标准嫁妆
- 女方低于男方 **1 级** → 嫁妆 +30%
- 女方低于男方 **2 级及以上** → 嫁妆 +60% ~ +100%
- 女方高于男方 → 嫁妆大幅减少（男方"娶进豪门"）

影响婚姻决策：低阶氏族与高阶氏族联姻代价大，AI 更倾向同阶联姻。

---

## 10. 驱逐氏族机制

### 决策路径（`ExpelClanDecisionPatch`）

国王可**召集投票**驱逐某氏族，或**绕过投票用诏令**（消耗大量影响力）。

### AI 投票逻辑（`ExpulsionVoteAIPatch`）

`ExpelRebelBaseChance = 0.3`，各修正：

| 因素 | 加权 |
|---|---|
| 暴君加成 `ExpelTyrantBonus` | +1.0 |
| 被驱逐者 Honor 高 | -0.15 / -0.08 |
| 被驱逐者名誉扫地高/中 | +0.15 / +0.08 |
| 被驱逐者 Valor 高/中 | +0.2 / +0.1 |
| 被驱逐者 Cowardice 高/中 | -0.2 / -0.1 |
| 被驱逐者 Hothead 高/中 | +0.15 / +0.08 |
| Calculating 权力门槛 `CalcPowerGate` | 0.5 |

### 选举冷却

`_expelElectionCooldowns` = 24 天（每王国）。

---

## 11. 安抚机制

`AppeasementBehavior`：国王可花费影响力/金币让不满派系 Mood 回升。

- 贵族派：发放特权、减税
- 军国派：宣战、发放军衔
- 平民派：赦免、减债
- 保皇派：确认拥立氏族、王室仪式

每次安抚降低目标派系 Mood 压力，延缓 Discontent 增长。AI 国王会在 Discontent > 70 时优先考虑安抚。

**`_pacifiedClans`**：被单独安抚的氏族有冷却期（约 30 天内不会参与叛乱）。

---

## 12. 争议事件

`ControversyBehavior` 随机触发政治丑闻：

- **宫廷丑闻**：某氏族成员被曝私德问题，涉及者影响力 -50，Mood -10
- **贪腐指控**：某领地官员贪污，相关氏族被质疑
- **继承危机**：无子嗣的氏族继承权争议

影响：触发派系 Mood 变化，可能激化叛乱进度。

---

## 13. 战友情谊

`ComradesInArmsBehavior`：

- **并肩作战**：同军队/军团参战累积 `Comrade` 分，影响关系 +5 ~ +15
- **共同胜利**：氏族实力提升时，军团领袖与成员关系 +10
- **共同战败**：责任归咎——指挥者与军团成员关系 -20
- **战友反叛**：若老战友加入叛军且自己在忠王方 → 个人挣扎 Mood -20，可能投敌

战友情谊是影响**投票、叛变、军团凝聚**的重要隐藏层。

---

## 14. 王朝继承人

`DynasticHeirBehavior` 识别每个王国的王朝合法继承人：

- 每个王室家族按文化继承法（第 6 节）计算合法继承人
- 继承人有隐藏分数，决定 `DynasticHeir` 原型在国王选举中的权重
- 若现任国王非合法继承人 → 保皇派 LoyalClan 可能反水

---

## 15. 意识形态冲击事件

`IdeologyEventShockBehavior`：当**外部大事**发生时批量调整派系 Mood：

- 某国王被处决 → 所有王国保皇派 Mood -20
- 某王国战败失地 → 该国军国派 Mood -15
- 某王国大获全胜 → 该国军国派 Mood +15
- 某王国通过平民政策 → 该国贵族派 -10、平民派 +10
- 内战结束时的 `ApplyTribunalShocks` 也走这一层

---

## 16. 第三方介入

`CivilWarInterventionBehavior`：

- 内战开战时对叛军王国 `ApplyLock`：一段时间内锁定外国王国宣战（避免雪崩式群殴）
- 锁定期过后，第三国根据**外交利益 + 意识形态共鸣**决定站队：
  - 同文化/派系 → 倾向支援叛军
  - 敌对 → 倾向支援旧王
- `_protectedRebelKingdoms`：某些条件下叛军进入保护期（天数可配）

`CivilWarDiplomacyModel` 重写内战期间的外交关系计算，避免原版"内战不影响外交"的 bug。

---

## 17. 原版覆盖清单

| 补丁类 | 覆盖原版机制 |
|---|---|
| `BlockVanillaAnnexPatch` | 阻止原版自动吞并附庸国 |
| `BlockVanillaPolicyPatch` | 阻止原版政策投票流程（走 BC 的） |
| `BlockWhitePeacePatch` | 阻止白和平（内战必有胜负） |
| `KingdomAnnexButtonPatch` | UI 入口：吞并按钮 |
| `KingdomExpelButtonPatch` | UI 入口：驱逐氏族按钮 |
| `KingdomPolicyButtonPatch` | UI 入口：政策按钮 |
| `EncyclopediaFactionPagePatch` | 百科中的派系页面 |

---

## 附录 A：关键类速查

| 类 | 作用 |
|---|---|
| `IdeologyBehavior` | 意识形态系统主控（派系、Mood、叛乱触发） |
| `FactionManagerBehavior` | 派系成员管理、加入/离开、议程 |
| `FactionObject` | 单个派系的数据对象（Members、Leader、Mood、Discontent） |
| `AppeasementBehavior` | 安抚机制 |
| `ControversyBehavior` | 争议事件 |
| `ProxyWarBehavior` | 代理战争 |
| `CivilWarInterventionBehavior` | 第三方介入内战 |
| `CivilWarResolutionBehavior` | 内战胜负判定 |
| `ComradesInArmsBehavior` | 战友情谊 |
| `DynasticHeirBehavior` | 王朝继承人 |
| `IdeologyEventShockBehavior` | 意识形态冲击 |
| `CivilWarDiplomacyModel` | 内战外交 |
| `DynamicArmyManagementModel` | 动态军队管理（Feudal Apathy） |
| `DynamicSuccessionModel` | 文化继承法 |

---

## 附录 B：常见问题

**Q: 内战最快结束方法？**
A: 俘获或击杀另一方领袖。即使只是俘虏带进任一军队监狱就立即触发结算。

**Q: 为什么我的国王总被叛乱？**
A: 检查四个派系 Mood。若多派系 < -20，Discontent 高，最快应对：
1. 立即 `Appeasement` 压低最高不满派系
2. 触发 `Controversy` 转移注意力
3. 对外宣战（打外战让国内派系团结，+10% Accept）

**Q: 领袖被处决是否灭族？**
A: **不会**。只处决领袖一人，氏族继承机制（第 6 节）让继承人接班。仅当整个血统灭绝才灭族。

**Q: 叛军赢了会怎样？**
A: 叛军王国继承被推翻的王国头衔，原王朝被清算（见第 4 节处决规则）。但叛军氏族池通常较小，后期可能被邻国侵蚀。

**Q: 白和平为什么用不了？**
A: BC 用 `BlockWhitePeacePatch` 明确禁用了内战白和平。设计目的是强制政治斗争有明确胜负，避免无限拖延。

**Q: 代理战争为什么老是打北帝国？**
A: BC 的目标选择按 `Kingdom.All` 固定顺序取第一个匹配目标，北帝国在列表前段且与多国关系差。这是 BC 源码问题，未做随机化。

**Q: 玩家可以主动发动代理战争吗？**
A: 可以。派有 Roguery 技能的同伴出执行秘密任务，分 10 种行动（见第 5 节），有被发现风险——被发现会爆发外交危机。
