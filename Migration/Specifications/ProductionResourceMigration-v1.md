# 正式战斗资源迁移 v1

## 本批范围

- 从 Cow 只读选择 U01、E01、E06、E07 四个视觉 Prefab，以及它们的四个材质和四个模型依赖。
- 导入前将 26 个源文件（13 个资产与各自 `.meta`）打包到目标项目快照，并完成解压、逐文件 SHA-256 复验。
- 保留 Cow 的 Prefab、材质和模型 GUID；不复制任何完整 Assets、Packages 或 ProjectSettings 目录。
- 完整 U01–U36、E01–E15 映射记录在 `CombatVisualResourceMap.csv`，未迁移条目明确标记为 `Pending`。

## 运行时边界

- `BattleSliceVisualCatalog` 位于 Presentation，仅把模板 ID 解析为显示 Prefab。
- Domain 不引用 Unity、Prefab 或资源系统；战斗判定仍只依赖纯 C# 快照。
- 正式场景使用序列化直接引用，暂不把 Addressables、ResKit 或 StreamingAssets 定为唯一方案。
- 未迁移模板继续使用 `BattleSliceCombatantView` 的 Capsule/Cube 回退表现，规则运行不受影响。
- 导入实例按 Renderer Bounds 自动统一尺度：移动单位水平尺寸为 0.95（小于相邻格心间距 1.0），九格建筑适配 3×3 占地（2.65）。移动单位 Transform 对齐三维几何中心后整体上移半高以保持贴地；建筑仍以底部中心对齐格心，避免旧 Prefab Pivot 偏移造成视觉出格。

## 序列化兼容

- Catalog 保留 Cow `VisualMapping` 的 `unitMappings`、`enemyMappings`、`unitId`、`enemyId`、`prefab`、`material`、`model` 字段名，使后续 Editor 转换可无损逐字段传递。
- 本批 Prefab 不含 MonoBehaviour，因此不存在需要复用的 `.cs.meta`；原 Prefab/材质/模型 `.meta` 已全部保留。
- 没有把源 `VisualMapping.asset` 直接导入目标，因为它仍引用 47 个未迁移条目；直接导入会产生大量断引用。
- 当前没有字段重命名，因此不滥用 `FormerlySerializedAs`；后续真实改名时必须先添加该属性。类型或命名空间迁移时仅在基类兼容的前提下使用 `MovedFrom`。

## URP 材质处理

- Cow 材质使用 Built-in Standard；目标项目固定使用 URP 14.0.12。
- 目标副本保留材质 GUID和旧 `_Color`/`_Glossiness` 数据，同时添加 `_BaseColor`/`_Smoothness` 并切换到 URP/Lit。
- 这是一项目标侧兼容升级，Cow 原材质未修改。

## 验收门槛

1. 四个 Prefab、四个材质、四个模型的 GUID 与清单一致。
2. Prefab 无 Missing Script，递归依赖都能被 AssetDatabase 解析。
3. 所有 Renderer 材质非空且使用 URP Shader。
4. FormalBattle 场景能解析 U01、E01、E06、E07，其他模板仍能显示回退几何体。
5. 现有 Domain、Application、Infrastructure 测试保持通过。
