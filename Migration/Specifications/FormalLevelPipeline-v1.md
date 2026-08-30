# 正式关卡配置管线 v1

状态：Cow L00–L20 与 L_ENDLESS 共 22 关已纳入目标兼容管线。

## 源数据与恢复

- 目标逐文件迁入 Cow `StreamingAssets/CompanyWar/Configs/Levels` 的 22 份 JSON 及原 `.meta`，不复制完整 Assets、Packages 或 ProjectSettings。
- 同批保留 Cow `SpawnSchedules.json`、`EndlessModeConfig.json`、`LevelArchives.json` 及 `.meta`。
- 目标副本位于 `Assets/CompanyWarRE/Resources/CompanyWarRE/Configs`，运行时仍允许序列化 TextAsset 覆盖；Resources 不是不可替换的唯一最终后端。
- 可恢复快照位于 `Migration/Baseline/Cow/20260830-all-level-configs`，包含 50 个源文件、SHA-256 清单与 ZIP；解压逐文件复验必须为 PASS。

## Cow 旧格式兼容

Cow 关卡是无版本的平面 JSON。Infrastructure 在内存中补齐目标 v1 包装，不改写原文件：

- `SchemaVersion = 1`；
- `CoordinateSpace = MacroControlBlock3x3`；
- 初始控制 2 个大格行，初始资源 10；
- 固定产出间隔 5 秒，传讯基站间隔 3 秒；
- 默认单位/敌人为 U01/E01；
- 环境采用 Cow 运行时默认 `Cow.DefaultIndustrial`、装饰环 5、密度 0.65、种子 1001；
- L00–L20 使用摧毁敌方建筑目标；L_ENDLESS 使用无尽生存目标且不要求初始建筑。

一个 Cow 大格转换为目标 3×3 小格。`C×R` 大格地图统一变为 `(C×3)×(R×3)`；建筑坐标转换为对应九格区域中心，禁止逐关自行计算。

## 已知 Cow 配置缺口

L08–L20 与 L_ENDLESS 引用了 `DLC1Stage1`–`DLC1Stage7`，但 Cow 的 `SpawnSchedules.json` 只定义 Stage1–Stage4 与 Finale。目标不篡改源文件，在 Infrastructure 内明确使用以下兼容别名：

| DLC 阶段 | Cow 已存在的调度 |
| --- | --- |
| DLC1Stage1 | Stage1 |
| DLC1Stage2 | Stage2 |
| DLC1Stage3 | Stage3 |
| DLC1Stage4 | Stage4 |
| DLC1Stage5 | Finale |
| DLC1Stage6 | Stage4 |
| DLC1Stage7 | Finale |

这是目标兼容回退，不宣称是 Cow 原始 DLC 调度数据。L_ENDLESS 的循环阶段、幕墙增援和逐阶段强化仍需独立 Domain 规格；当前仅保证配置可加载和棋盘可运行。

## 棋盘 Prefab

- `PF_FormalBattleBoard` 是正式棋盘 Presentation 根 Prefab。
- Prefab 持有 Grid、Combatants、Feedback 三类运行时容器；格子数量、建筑和环境完全来自当前关卡配置。
- `BattleSliceController` 优先使用序列化 Prefab，缺省通过可替换的 Resources 回退加载；找不到资源时才创建明确命名的运行时回退根。
- 棋盘 Prefab 不保存 Domain 状态，不引用 Cow 旧逻辑。

## 验收

1. 22 个关卡 ID、尺寸、阶段数量、建筑数量、目标分数与 Cow JSON 一致。
2. 所有大格坐标合法且无重复建筑格；目标副本与 Cow SHA-256 一致。
3. 批量验证菜单 `Company War-RE > Migration > Validate All Cow Formal Levels` 返回 22/22 成功。
4. 棋盘 Prefab 无 Missing Script，切换不同尺寸关卡时重新创建正确数量的格子。
5. Domain 保持纯 C#；解析与 Cow 缺口兼容只位于 Infrastructure；QFramework 只负责用例流程。
