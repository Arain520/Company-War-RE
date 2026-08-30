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
- U02–U24 与 E02–E15 可按模板 ID 解析 Cow Prefab；U25–U36 按 Cow `VisualMapping` 的真实结构直接解析原 FBX（Cow 的 `prefab` 与 `model` 字段均指向该 FBX），不制造无依据的包装 Prefab。
- 导入实例只按包含真实几何的非零 Renderer Bounds 自动统一尺度，忽略旧 Prefab 中无 Mesh 的空 Renderer。移动单位水平尺寸为 0.95（小于相邻格心间距 1.0），九格建筑适配 3×3 占地（2.65）。移动单位 Transform 对齐三维几何中心后整体上移半高以保持贴地；建筑仍以底部中心对齐格心，避免旧 Prefab Pivot 或空 Bounds 造成视觉偏移、缩小和出格。
- Ally 保留 Cow 映射中的基础旋转，Enemy 在此基础上绕 Y 轴旋转 180 度；每次改变朝向后必须按 Renderer Bounds 再次把几何中心（建筑为底部中心）对齐格心，禁止绕旧 FBX 远端 Pivot 旋转导致模型离开棋盘。缩放、中心点与阵营朝向都由 Presentation 统一处理，不进入 Domain。
- Domain 战斗快照仅公开 `Effect` 和隐身、治疗、转化、驱离、重击能力标签。Presentation 用这些只读标签显示能力环与状态文字；Support/Terrain 部署使用独立的瞬时范围反馈，不改变规则判定。
- U21/U33 治疗、E14 击杀回血、U32 转化和 U27 驱离由 Domain 分别发出 `Heal`、`Conversion`、`Pushback` 事件；Presentation 只消费事件生成反馈，不通过血量或位置差值猜测规则结果。
- 普通攻击消费既有 `Attack` 事件生成短寿命轨迹；Ally/Enemy 使用不同颜色，重击额外显示命中范围提示。瞬时反馈池设置 96 个硬上限，长波次中禁止无限积累 GameObject。
- Cow 棋盘不是 FBX 或静态 Prefab，而是 `CompanyWarGrid3DRenderer` 运行时生成的 Cube 网格。目标 `PF_FormalBattleBoard` 迁移其真实视觉契约：0.92 小格填充率、控制块边框和 `Battle3D.unity` 实际覆盖的 owned/polluted/enemy/empty 状态色。Cow 逻辑小格间距为 0.4，目标为 1.0，因此 0.04 高度、-0.008 表面偏移与 0.025 边框按 2.5 倍归一化为 0.1、-0.02 与 0.0625，保持视觉比例而不破坏目标坐标体系。目标仍由 Presentation 根据每关行列数动态生成，不把棋盘状态写入资源或 Domain。

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
4. FormalBattle 场景能解析 U01–U36 与 E01–E15 的 Cow 视觉；U25–U36 必须保持原 FBX GUID 绑定。
5. 现有 Domain、Application、Infrastructure 测试保持通过。
6. Ally/Enemy 朝向相差 180 度；隐身、治疗、转化、驱离、重击和 Support/Terrain 部署在正式战斗中存在可辨识反馈。
