# 云上建筑柱 · SkyPillar A

根据参考图制作的单根可复用建筑柱：深灰分段外墙、四角加强筋、浅灰九宫格甲板、倒角钢框、金色护角及暖色状态灯。未包含参考图的桥梁、角色、云海。

## 交付

- `SkyPillar_A.blend`：Blender 4.5 源文件。`SkyPillar_Editable` 集合保留分件及倒角修改器；`Studio_Preview_Only` 是展示相机、灯光和地面，不包含在 FBX 中。
- `SkyPillar_Hero.png`、`SkyPillar_Detail.png`：整体及柱顶渲染。
- Unity 项目内 `Assets/CompanyWarRE/Content/Environment/SkyPillar/SM_SkyPillar_A.fbx`：合并网格，7 个材质槽，带 UV0；旁边的 Materials 文件夹提供 URP/Lit 材质，FBX meta 配置显式材质映射。
- `build_pillar.py`：可重复生成源文件、模型、材质和预览。重新运行会覆盖本目录的上述生成文件及对应 Unity 资源。

## 尺寸和使用

- 米制，名义尺寸 4 × 4 × 16 米，含护角实际包围盒约 4.04 × 4.04 × 16.05 米。
- 原点为底部中心，Unity 导入后 Y 朝上。主要柱顶表面高 16 米，边缘压板高 16.05 米。
- 网格 20,576 三角面、10,640 个顶点。用于精细近景；大量远景实例建议另做 LOD。
- 在 Unity Project 中将 FBX 拖入场景即可摆放。未替换现有战场、地图或游戏柱子逻辑。
- 需要碰撞时可加 BoxCollider，Center = (0, 8, 0)，Size = (4, 16, 4)。该简化碰撞体刻意忽略小型装饰。未自动添加碰撞体、Prefab 或 LOD。
- Unity 中不依赖 Blender 程序材质节点或外部贴图；实际灯光效果由场景灯光、反射与后处理决定。灯条泛光需要场景启用 Bloom。
- 已进行 Blender 渲染和 FBX 回读检查；没有在 Unity 编辑器中验证最终显示。

## 重建

在项目根目录用 PowerShell 执行：

```powershell
& 'D:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background --python 'Art\SkyPillar\build_pillar.py'
```
