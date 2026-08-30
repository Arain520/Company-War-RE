# 正式存档接线规格 v1

## 生命周期接线

- `FormalBattle` 的 `BattleSliceController.Awake` 初始化正式流程后，使用平台持久化路径启动 `FormalSaveSession`。
- 当前 v3 存档存在时优先加载；不存在时尝试读取真实 Cow PlayerPrefs；Cow 数据也不存在时建立新 v3 存档。
- 加载成功后恢复正式战役的活动关卡、解锁和完成状态，再配置活动关卡。
- Cow 的授权点、授权申请结果和部署列表属于当前关卡战斗状态；每次打开、切换或重开关卡都从该关 JSON 的 `InitialAuthorizationPoints` 与 `InitialDeployments` 重新建立，禁止跨关合并。
- v3 DTO 继续保留 `Growth` 字段以兼容读取旧文件；若读到旧版本曾错误持久化的授权数据，启动时通过原子写入和备份清零，不影响战役、经济和设置。
- 接受授权只更新当前 Domain 战斗实例，不触发存档写入。
- 战斗进入胜利或失败状态且结果首次被正式流程接受时，保存战役、剩余资源和突击分；`Growth` 固定为空。
- `SaveAudioSetting` 是正式设置 UI 的保存入口，逐层保存 Master/BGM/SFX/UI/Voice 的音量和静音。

## 数据保护

- 损坏 JSON、未知版本或读取错误会令本次 `FormalSaveSession` 进入只读保护。
- 只读保护期间正式战斗仍可运行，但任何自动保存都不会覆盖问题文件。
- 首次存档、自动保存、设置保存继续使用 `AtomicLocalSaveDocumentStore` 的临时文件、Flush、备份和回滚规则。
- 当前存档路径通过 `BattleSliceController.SavePath` 暴露；状态通过 `SaveStatus` 和 `IsSaveWritable` 暴露。

## 自动化验收

在 Unity Test Runner 的 EditMode 运行：

- `FormalSaveSessionTests`：首次建档、现有存档加载、旧授权数据清理、逐关默认授权重载、损坏存档保护、胜负/经济/设置持久化。
- `SaveGameDomainTests`：版本升级与数据验证。
- `SaveGameApplicationTests`：QFramework 命令边界和正式流程恢复。
- `SaveCompatibilityTests`：JSON 往返、备份、回滚和恢复。
- `BattleSliceSceneTests.FormalBattleController_ExposesCompleteFormalFlowUseCases`：正式场景控制器公开存档入口和诊断状态。

## 人工验收

1. 备份并移走 `{persistentDataPath}/CompanyWarRE` 后运行 FormalBattle，确认生成 `save.json`。
2. 完成 L02，确认 JSON 中 L02 为 Completed、Stars 至少为 1，L03 解锁；再次保存后 Backups 出现 `.bak`。
3. 在 L02 申请一个单位后退出并重新进入，确认活动关卡和解锁状态恢复，但授权点与部署列表回到 L02 配置默认值；切换 L03 时只出现 L03 默认部署。
4. 调用设置 UI 的 `SaveAudioSetting` 后重启，确认音量和静音仍一致。
5. 人为损坏 `save.json` 后重新运行，确认 `IsSaveWritable=false`、Console 有只读保护警告，损坏文件保持不变。
