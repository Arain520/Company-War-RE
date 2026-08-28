# 生产级基础设施规格 v1

## 资源后端决策

当前项目已安装 QFramework ResKit 与 AudioKit，未安装 Addressables，也没有 Addressables catalog、group 或 label。正式资源目前由场景序列化直引与 `Resources/CowLegacy` 保证启动完整性。

本阶段采用以下边界：

- 启动场景、正式战斗基础 UI 和当前关卡配置继续使用序列化直引，避免在首屏引入远程目录依赖。
- 可更新内容使用 ResKit；调用方必须显式提供 bundle owner，禁止同一资源同时进入 Addressables 与 ResKit 清单。
- 未打包或编辑器迁移期资源允许异步 Resources fallback，并在结果中报告实际 Backend。
- Addressables 暂不安装。未来只有在需要 Unity Cloud Content Delivery、Addressables 分析器或团队已经建立 Addressables 发布流水线时，才新增独立适配器；上层不改接口。
- ResKit 和 StreamingAssets 不是存档后端，也不承载 Domain 数据。

## 已实现基础设施

### 异步资源与场景

- `IProductionAssetProvider` 隔离具体资源后端。
- `ResKitWithResourcesFallbackProvider` 优先异步 ResKit，失败后异步 Resources fallback，并统一返回成功、Backend 和错误。
- `ProductionSceneLoader` 对 bundle 场景使用 ResKit，对内置场景使用 `SceneManager.LoadSceneAsync`。
- `BattleSliceController` 暴露 `LoadProductionAssetAsync` 与 `LoadProductionSceneAsync`，并在销毁时释放 loader 引用。

### 对象池与渲染分配

- 正式战斗的 `BattleSliceCombatantView` 使用有上限的组件池；死亡、切关和离开场景时回收。
- CombatantView 复用已有几何体、材质和导入模型实例，重新绑定 Actor 时只重置运行状态。
- 18×30 战场的 540 个 Cell 不再各自创建材质，改为单一共享材质加 `MaterialPropertyBlock`。
- 池默认最多保留 256 个战斗表现对象，超限对象直接释放。

### 音频

- `FormalAudioService` 以存档中的 Master/BGM/SFX/UI/Voice 五层设置驱动 AudioKit。
- BGM、Voice 映射 AudioKit 独立通道；SFX 与 UI 共享 AudioKit Sound 通道，但通过服务入口分别应用层音量与静音。
- 正式存档加载时自动应用音频设置，`SaveAudioSetting` 成功后立即刷新 AudioKit。
- 当前资源批次没有可确认的正式音乐/音效事件表，因此不虚构音频文件绑定；后续只需向服务传入审计批准的 AudioClip。

### 性能基线

- `BattleRuntimePerformanceMonitor` 每秒记录平均/最差帧耗时、托管内存增量、格子数、活动与池中战斗表现数。
- 当前建议预算：平均帧时间不高于 33.34ms、单次采样托管增长不高于 256KB、格子不高于 900、活动表现不高于 256。
- 预算超限可在组件上启用日志警告；正式验收仍需在目标构建而非仅编辑器中采样。

## 验收

1. EditMode 运行 `ProductionInfrastructureTests`，确认池复用、资源路由和性能预算判定通过。
2. 运行 FormalBattle L02，观察 `BattleRuntimePerformanceMonitor.Latest`；18×30 应报告 540 个格子。
3. 长时间运行敌人波次，确认死亡单位从 ActiveCount 转入 AvailableCount，CreatedCount 不随每波无限增长。
4. 重开或切换 L02-L05，确认池复用且画面、血条、反馈无残留。
5. 修改保存音频设置后重启，确认 AudioKit 的 Music/Voice/Sound 设置同步。
6. 在建立 ResKit bundle 后，用显式 bundle name 加载测试资产和场景，确认结果 Backend 为 ResKit；移除 bundle 时确认允许的 Resources fallback 生效。
