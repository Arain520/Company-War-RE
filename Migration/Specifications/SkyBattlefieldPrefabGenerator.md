# 云上战场 Prefab 生成器

入口：`Company War-RE > 云上战场 Prefab 生成器`。

默认地图：`Assets/CompanyWarRE/Content/Maps/SkyBattlefield/PF_SkyBattlefield.prefab`。

## 使用

1. 输入关卡的逻辑列数、行数。每个 3×3 控制区生成一根战斗柱，18×30 对应 6×10 根柱；不足 3 格的边缘控制区也会生成一根柱。
2. 调整柱高范围、随机种子、视觉倍率、柱间距、远景塔数量和主云层高度，点击“生成新地图 Prefab”。相同种子生成相同高度和环境布局；每次生成保存到独立路径，保留已有地图。
3. 点击“创建场景预览”查看临时柱阵，或打开 Prefab 手工调整外围环境。预览可 Undo，根对象为 EditorOnly，进入运行模式会关闭；预览中的动态柱阵不保存到地图。
4. 在战斗场景中选择 BattleSliceController，点击“应用到当前场景战斗控制器”，然后保存场景。若当前场景只有一个控制器，可直接应用；不会修改关卡的逻辑尺寸。

外围环境是固定布局，因此不同尺寸的战场应生成对应地图。地图的运行时柱阵仍以实际关卡尺寸为准，不会被生成器的预览尺寸锁死。地图及 BattleBoardAnchor 保持等比缩放。

## 保存内容

- 主地图保存桥梁、平台、机房、天线、机械设备、中远景塔群、环形连廊、三层云海、局部云团、冷暖方向光和 URP 后处理。
- 配套资源目录保存云材质、建筑材质、VolumeProfile 与主柱视觉 Prefab。后处理组件作为 Profile 子资源保存，运行时没有临时资源引用丢失的问题。
- 主柱视觉 Prefab 引用 Content 中的 SkyPillar FBX；外围使用 SkyArchitecture FBX。实例通过外层 Transform 拼装，保留 FBX 自带轴向校正。
- 原模型和共享材质保持原样。生成的建筑材质使用独立的 URP 光照与高度雾 Shader，支持金属、发光、投影和距离雾。
- 环境位于 Ignore Raycast 层，没有碰撞体；运行时战斗柱保留用于选取的简化碰撞体。

## 运行时

SkyBattlefieldSettings 将地图的视觉参数传给 FormalBattleBoardView。现有 SeededRandomBattlePillarHeightProvider 提供真实柱高，现有坐标映射器驱动格子、角色和移动插值；不新增高地攻击或移动规则。模型名义甲板安装面为 Y=16，顶边饰条为 Y=16.05；定位使用安装面，柱身向云下延伸时不移动甲板锚点。

CloudAbyssEnvironmentView 遇到已烘焙云层时只设置氛围参数，不重建环境，不隐藏新建筑。CloudSeaURP 同时支持正交与透视相机的软交界深度计算。新地图使用 48° 俯角、-32° 方位的初始构图；其他地图保留原有镜头参数。

## 验证

`Company War-RE > 验证并渲染云上战场` 会在临时场景中加载默认 Prefab，检查资源引用、碰撞体、重复生成、真实高度、模型朝向、甲板/角色锚点对齐和静态环境复用，然后输出 `Migration/Artifacts/SkyBattlefield/SkyBattlefield_Unity.png` 与 `verification.txt`。渲染同步等待 Shader 编译，验证结束关闭临时场景并恢复之前的场景。

EditMode 测试：`PillarBoardPresentationTests` 和 `SkyBattlefieldPresentationTests`，结果保存在 `Migration/Artifacts/SkyBattlefield/editmode-results.xml`。

当前云海使用分层平面与云团卡片，非体积云。远景塔使用共享网格和材质，没有独立 LOD；可通过远景塔数量控制开销。
