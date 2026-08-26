# 正式游戏流程 v1

## 范围

- `FormalBattle` 启动后进入主菜单；正式关卡顺序固定为 L02、L03、L04、L05。
- 初始只解锁 L02。胜利记录完成状态并解锁下一关，失败不解锁；结算可重开、进入已解锁下一关、打开关卡选择或返回主菜单。
- 战斗中 `Esc` 在运行与暂停之间切换。暂停、菜单、关卡选择、授权选择和结算期间均停止 Domain 时间推进。
- 本批进度为运行期状态；磁盘存档与 Cow 旧存档升级仍属于后续存档兼容批次。

## 授权成长

- 初始授权点为 6，初始部署列表为 U01、U08、U09，与 Cow Prototype3D 初始化一致。
- 使用 Cow 三阶段配置：I/II/III 的基础需求分别为 7/10/15，候选数量继续由纯 C# `AuthorizationProgression` 约束为 4/3/2。
- U09 产生的完整授权点通过 Application 模型转交 Domain；达到需求后战斗进入授权选择，接受一个候选后加入部署列表。
- Presentation 只发送 QFramework Command/Query；关卡解锁、暂停状态和授权规则不依赖 Unity 类型。

## UI 与流程边界

- 当前正式流程具备目标侧可执行 IMGUI，保证在 Cow UI 进一步美术整理前可完整游玩。
- Cow 的 `Menu`、`LevelSelectPanel`、`BattlePanel` Prefab 与字段均已迁移；兼容组件保留旧脚本 GUID/类型，可将按钮与文本直接接入新流程。
- Cow UI 仅作为 Presentation 视觉与序列化来源，不恢复 YFan Procedure 或旧 Domain 依赖。

## 验收

- `FormalCampaignProgressionTests`：锁定、胜利解锁、失败重开、暂停。
- `BattleSliceApplicationTests.FormalFlowCommands_UnlockNextLevelAndRemainBehindQFrameworkBoundary`：QFramework 用例边界。
- `FormalLevelConfigurationPipelineTests`：正式关卡初始授权状态与部署列表。
- `MissingScriptTests.TargetPrefabs_HaveNoMissingScripts`：Cow UI 兼容组件不得出现 Missing Script。
