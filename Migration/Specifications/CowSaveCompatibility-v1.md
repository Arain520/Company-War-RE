# Cow 存档兼容规格 v1

## 审计结论

Cow 没有已接入业务流程的完整 JSON、二进制或加密文件存档。真实玩家持久化仅包括：

- `CompanyWar.LastLevel`：最高到达关卡，默认 `L00`。
- `CompanyWar.LevelStars.{LevelId}`：逐关最高星级，默认 `0`，胜利时写入 `1..3`。
- 五个音频层 `Master/BGM/SFX/UI/Voice` 的音量与静音，共 10 个 PlayerPrefs 键，默认音量 `1.0`、静音 `0`。

Cow 的 `SaveUtil` 虽声明 `{persistentDataPath}/Saves/Slot_{n}/{key}.sav|json`、JsonUtility 和可选加密，但全项目没有业务调用者。它只能作为未采用的框架能力记录，禁止臆造为旧存档格式。StreamingAssets 中的 JSON 是只读游戏配置，不是用户存档。

授权点、授权成长、部署列表、战斗资源与突击分仅存在于运行时内存；目标存档中的这些字段均标为目标新增。

## 版本与升级

- v1：Cow 可迁移的战役进度、星级和音频设置。
- v2：新增授权点与部署列表。v1 升级时使用 `0/空列表`。
- v3：新增资源与分数。v2 升级时使用 `0/0`。
- 缺失非关键 section 使用安全默认并产生 Warning；未知版本、非法活动关卡、非法星级、负数成长/经济数据产生明确错误。
- 未知 JSON 字段被忽略，以支持前向扩展；损坏 JSON 返回 `InvalidJson`。

## 导入行为

1. 旧输入是 `CowLegacyPreferenceSnapshotDto` 或 `ILegacyPreferenceReader`，不是虚构的 Cow JSON 文件。
2. `LastLevel` 按调用方提供的目标关卡顺序映射解锁范围；未知关卡退回第一个目标关卡并报告 Warning。
3. 星级大于 0 表示 Cow 确实记录过胜利，因此映射为 Completed；星级被限制在 0..3。
4. 音量限制在 0..1；Mute 仅在旧值等于 1 时为 true。
5. 当前目标存档存在时只尝试读取它。若其损坏或版本未知，禁止自动改用旧数据覆盖，必须由用户处理或显式恢复。

## 存储行为

- `ISaveDocumentStore` 可替换；默认 `AtomicLocalSaveDocumentStore` 只接受调用方给出的绝对路径。
- Unity 适配器默认路径为 `{Application.persistentDataPath}/CompanyWarRE/save.json`，备份目录为同根 `Backups`；Domain/Application 不引用 Unity 路径。
- 写入前若目标存在，先创建 `文件名.UTC时间戳.GUID.bak` 唯一备份。
- 新文档先写同目录临时文件并 Flush，再使用同卷 `File.Replace`；首次写入使用同目录 Move。
- 写入失败删除临时文件并从备份自动回滚；显式恢复前先验证备份 JSON，恢复操作也会备份被替换的当前存档。
- 不依赖 ResKit 或 StreamingAssets。

## 已知风险

1. Cow 的 Unity 标识为 `Tripeaks Studio / DotMatrixAgreement`，目标为 `DefaultCompany / Company War-RE`。两者 PlayerPrefs 域不同；目标进程不能假定直接看到 Cow 键。迁移器支持外部提取后的快照 DTO，或在拥有旧 PlayerPrefs 上下文的平台入口中注入 reader。
2. `LastLevel` 同时可能在关卡进入时写入且不立即 `PlayerPrefs.Save`，其磁盘值受 Unity 正常退出/后续 Save 时机影响。
3. 音量 setter 没有显式 `PlayerPrefs.Save`，同样存在异常退出时最后修改丢失风险。
4. `LastLevel` 表示最高到达而非完成；完成状态只能由星级大于 0 推导。
5. Cow 未保存授权、部署、资源与分数，无法恢复某场战斗的精确中途状态。

## 人工验收

1. 使用包含 `LastLevel=L04`、L02 三星和自定义音量的快照导入空目标路径，确认生成 v3 JSON。
2. 在正式流程初始化时应用导入结果，确认 L02 完成、L02-L04 解锁、L05 锁定。
3. 修改授权点、部署列表、资源和分数，保存后重启并确认 v3 往返一致。
4. 再次保存，确认 Backups 产生唯一 `.bak`；显式恢复后内容回到上一版，同时当前版也被备份。
5. 将当前 JSON 人为损坏，确认加载报告 `InvalidJson` 且文件未被旧数据覆盖。
6. 将 Version 改为未知值，确认报告 `UnsupportedVersion` 且文件保持不变。
7. 在 Unity Test Runner 的 EditMode 运行 `SaveGameDomainTests`、`SaveGameApplicationTests`、`SaveCompatibilityTests`、`SaveArchitectureBoundaryTests`，确认 19 个参数化测试实例通过。
