# 正式游戏流程 v1

## 范围

- `FormalBattle` 启动后进入主菜单；正式关卡顺序固定为 L00–L20，最后附加 L_ENDLESS。
- 新存档初始只解锁 L00。胜利记录完成状态并解锁下一关，失败不解锁；结算可重开、进入已解锁下一关、打开关卡选择或返回主菜单。
- 战斗中 `Esc` 在运行与暂停之间切换。暂停、菜单、关卡选择、授权选择和结算期间均停止 Domain 时间推进。
- 关卡解锁、当前关卡和完成状态通过正式存档会话保存；旧存档恢复时按完整关卡目录合并，不删除已存在进度。

## 授权成长

- 初始授权点为 6，初始部署列表为 U01、U08、U09，与 Cow Prototype3D 初始化一致。
- 使用 Cow 三阶段配置：I/II/III 的基础需求分别为 7/10/15，候选数量继续由纯 C# `AuthorizationProgression` 约束为 4/3/2。
- U09 产生的完整授权点通过 Application 模型转交 Domain；达到需求后战斗进入授权选择，接受一个候选后加入部署列表。
- Presentation 只发送 QFramework Command/Query；关卡解锁、暂停状态和授权规则不依赖 Unity 类型。

## UI 与流程边界

- `FormalBattle` 运行时创建正式 Canvas/EventSystem，并实例化 Cow 的 `Menu`、`LevelSelectPanel`、`BattlePanel`；IMGUI 默认关闭，只保留配置失败和调试后备用途。
- 兼容组件保留旧脚本 GUID/类型，并补齐 Cow Prefab 当前缺少的开始、暂停、重开、返回、结算、授权候选与部署列表控件。
- Cow `Default SDF` 保留字体和材质 GUID，但旧 TMP Shader 改为引用目标工程提交的 TMP Essential Resources，禁止再次整体复制旧 TMP 包。
- Cow UI 仅作为 Presentation 视觉与序列化来源，不恢复 YFan Procedure 或旧 Domain 依赖。
- 关卡选择兼容组件运行时从 Cow 按钮模板生成 22 个按钮的六列网格，避免旧六按钮 Prefab 限制完整战役。

## 验收

- `FormalCampaignProgressionTests`：锁定、胜利解锁、失败重开、暂停。
- `BattleSliceApplicationTests.FormalFlowCommands_UnlockNextLevelAndRemainBehindQFrameworkBoundary`：QFramework 用例边界。
- `FormalLevelConfigurationPipelineTests`：正式关卡初始授权状态与部署列表。
- `BattleSliceSceneTests.FormalBattleScene_WiresCowUguiPrefabsAndRuntimeBootstrap`：正式场景必须引用三套 Cow UGUI Prefab 与运行时装载器。
- `BattleSliceSceneTests.CowTmpShader_UsesInstalledTargetEssentialResources`：Cow 字体 Shader 必须使用目标 TMP include。
- `MissingScriptTests.TargetPrefabs_HaveNoMissingScripts`：Cow UI 兼容组件不得出现 Missing Script。
