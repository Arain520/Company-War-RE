Blender 模型交付说明
====================

本目录中的所有文件均已检查为 Binary FBX，可直接导入 Blender。

Units/
- U01-U24：从当前游戏使用的 PF_Uxx Prefab 批量导出。
- U16 对应“亡命者”；旧工程中它引用了错误命名的 U15.obj，本次已按正确逻辑编号输出为 U16.fbx。
- U15 是阵地构建功能模块，仍保留其旧 Prefab 的 FBX 输出以便完整核对。

Enemies/
- E02-E05、E08-E15：旧工程原始 FBX 位于 Resources/CowLegacy/_Game/Resources/11111，已按游戏逻辑编号重命名。
- E01、E06、E07 原本已经有正确命名的 FBX，因此没有重复放入本次“缺少 FBX”交付目录。

验证结果
- 单位：24 个
- 敌人/敌方建筑：12 个
- 合计：36 个 Binary FBX

Blender 导入：文件 > 导入 > FBX (.fbx)
