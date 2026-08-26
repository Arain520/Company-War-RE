# 正式战斗资源迁移 v1

## 批次范围

- 从 Cow 只读选择 U01、E01、E06、E07 四个视觉 Prefab，以及它们的四个材质和四个模型依赖。
- 导入前将 26 个源文件（13 个资产与各自 `.meta`）打包到目标项目快照，并完成解压、逐文件 SHA-256 复验。
- 保留 Cow 的 Prefab、材质和模型 GUID；不复制任何完整 Assets、Packages 或 ProjectSettings 目录。
- Batch 05 对 Cow 的全部 75 个 Prefab 建立递归依赖闭包，共 158 个资产、316 个资产/`.meta` 文件，先生成可恢复 ZIP 并逐文件复验，再迁移 71 个尚未存在的 Prefab；Batch 01 的 4 个同 GUID Prefab 直接复用，禁止制造 GUID 冲突。
- 迁移闭包包含 38 个材质、38 个模型、Cow UI 字体和 Shader；34 个新增 Built-in 材质在目标副本中升级为 URP/Lit，Cow 源文件不修改。

## 运行时边界

- `BattleSliceVisualCatalog` 位于 Presentation，仅把模板 ID 解析为显示 Prefab。
- Domain 不引用 Unity、Prefab 或资源系统；战斗判定仍只依赖纯 C# 快照。
- 正式场景优先使用序列化引用；Batch 05 的兼容资源通过 `Resources/CowLegacy` 作为可替换回退，不把 Addressables、ResKit、Resources 或 StreamingAssets 定为唯一最终方案。
- U02–U24 与 E02–E15 可按模板 ID 解析新增 Cow Prefab；Cow 没有对应 Prefab 的 U25–U36 继续使用程序化回退，规则运行不受影响。
- 导入实例只按包含真实几何的非零 Renderer Bounds 自动统一尺度，忽略旧 Prefab 中无 Mesh 的空 Renderer。移动单位水平尺寸为 0.95（小于相邻格心间距 1.0），九格建筑适配 3×3 占地（2.65）。移动单位 Transform 对齐三维几何中心后整体上移半高以保持贴地；建筑仍以底部中心对齐格心，避免旧 Prefab Pivot 或空 Bounds 造成视觉偏移、缩小和出格。

## 序列化兼容

- Catalog 保留 Cow `VisualMapping` 的 `unitMappings`、`enemyMappings`、`unitId`、`enemyId`、`prefab`、`material`、`model` 字段名，使后续 Editor 转换可无损逐字段传递。
- 75 个 Prefab 中只有 MenuPanel、LevelSelectPanel、BattlePanel、FloatingTextItem 引用项目旧脚本；目标为四者保留原 `.cs.meta` GUID、类型名和字段，并以兼容组件转接新流程，禁止 Missing Script。
- 全部序列化映射见 `PrefabMigrationBatch05/PrefabSerializationCompatibility.csv`；源 `VisualMapping.asset` 仍不直接作为运行时权威，避免恢复旧框架边界。
- 当前没有字段重命名，因此不滥用 `FormerlySerializedAs`；后续真实改名时必须先添加该属性。类型或命名空间迁移时仅在基类兼容的前提下使用 `MovedFrom`。

## URP 材质处理

- Cow 材质使用 Built-in Standard；目标项目固定使用 URP 14.0.12。
- 目标副本保留材质 GUID和旧 `_Color`/`_Glossiness` 数据，同时添加 `_BaseColor`/`_Smoothness` 并切换到 URP/Lit。
- 这是一项目标侧兼容升级，Cow 原材质未修改。

## 验收门槛

1. 75 个 Cow Prefab 在总清单中逐项出现，全部源文件具备可恢复快照和 SHA-256。
2. Prefab 无 Missing Script，递归依赖都能被 AssetDatabase 解析，四个旧脚本 GUID 解析到目标兼容组件。
3. 所有迁移材质非空且使用 URP Shader。
4. FormalBattle 场景能解析 U01–U24 与 E01–E15 的 Cow Prefab；U25–U36 明确使用回退几何体。
5. 现有 Domain、Application、Infrastructure 测试保持通过。
