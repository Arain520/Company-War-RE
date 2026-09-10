# 云上战场建筑模块

沿用 `SkyPillar_A` 主战场柱的深灰金属、浅灰平台、金色护角与暖光风格。本套提供参考图中的建筑配套件，主柱继续使用原有模型。

## 文件

- `SkyArchitecture_Kit.blend`：12 种模块的可编辑 Blender 源文件。每个集合对应一种建筑，根空物体用于移动整组分件。源文件将模块分开展示；导出的每个 FBX 已恢复为各自的局部原点。
- `SkyBattlefield_Assembly.blend`：主柱阵列、前方连接桥、屋顶设施与外围高塔的组合示例，打开后选择 `SkyBattlefield_Assembly` 场景。主柱使用实例共享网格。
- `SkyBattlefield_Assembly.png`：组合预览。
- `Architecture_Modules.png`：8 种较小模块的预览。
- Unity 模型目录：`Assets/CompanyWarRE/Content/Environment/SkyArchitecture/`。
- `asset_manifest.json`：各模型的三角面数、包围盒、材质和对齐基准。
- `assembly_layout.json`：组合示例中建筑的位置和旋转，采用 Blender Z 向上的坐标。
- `verification.json`：FBX 回读检查结果。

## 模块清单

| FBX 名称 | 用途 | 名义规格 |
| --- | --- | --- |
| SM_SkyBridge_8m | 带金色护栏、底部桁架的主桥 | 通行面 8 × 3 m |
| SM_SkyCatwalk_8m | 远景与检修用窄桥 | 通行面 8 × 1.25 m |
| SM_SkyLanding_4m | 桥端、柱顶连接平台 | 4 × 4 m，两端开放 |
| SM_SkyStairs_1m | 高差连接楼梯 | 长 3 m，宽 2.4 m，升高 1 m |
| SM_SkyCommandRoom | 白色发光观察室／指挥节点 | 约 3.25 × 3.25 × 2.64 m |
| SM_SkyServiceRoom | 白色机房，带爬梯和屋顶栏杆 | 约 2.9 × 2.6 m |
| SM_SkyAntenna | 双层三向通信天线 | 高约 4.55 m |
| SM_SkyMachinery | 双风机、管线与控制箱 | 底座 2.8 × 2.4 m |
| SM_SkyTower_Slim24m | 细长远景塔 | 塔身 2.9 × 2.9 × 24 m |
| SM_SkyTower_Monolith36m | 巨型远景塔 | 塔身 5.6 × 5.6 × 36 m |
| SM_SkyTower_Split28m | 双层阶梯冠部高塔 | 底部宽 3.8 m，主体高 28 m |
| SM_SkyRing_Quarter_R7m | 四分之一环形连廊，带内侧支撑杆 | 中线半径 7 m，宽 1.2 m |

尺寸不含个别护栏、天线及突出件；精确尺寸见 manifest。

## Unity 拼装

模型使用米制，Unity 导入后 Y 向上。12 个 FBX 共用原柱子目录中的 7 个 URP/Lit 材质，meta 已配置材质映射，因此移动资源时应通过 Unity Project 窗口操作并保留 meta。

1. 主战场阵营柱继续使用 `SkyPillar/SM_SkyPillar_A.fbx`，底部中心为原点，主要柱顶高 16 m。
2. 房间、天线和设备原点均在底部安装面；放在原柱顶时设置 Unity Y = 16。
3. 桥和端部平台原点在通行面中央，沿 X 连接。主桥两端 X = ±4，平台两端 X = ±2。以中心距 12 m 的两根柱子为例，在它们中间放一段 8 m 主桥，两柱顶部各放 4 m 平台即可连接。平台本体向下约 0.6 m；若覆在已有柱顶，建议平台通行面 Y = 16.65，桥也使用相同高度。
4. 楼梯原点在低端安装平面，沿 +X 上升 1 m；中心沿 X 对齐时需计入半长 1.5 m。
5. 远景塔原点在底部中心。冠部和天线可超出名义塔高。
6. 环形连廊原点在圆心的通行高度；四份绕 Unity Y 轴各旋转 90° 组成完整圆环。内缘半径 6.4 m，内侧支撑伸至约半径 2.8 m，适合搭配宽 5.6 m 的巨塔。

本交付为建筑静态模型和 Blender 组合示例，不包含角色、云海特效、导航、碰撞体、LOD 或战斗逻辑接入，也没有修改现有游戏场景。需要可行走表面时，桥和楼梯可使用简单盒体碰撞，环形连廊建议使用分段碰撞，避免单个凸碰撞体填满圆环中心。

已在 Blender 中检查预览，并逐个回读 FBX 验证网格、尺寸、UV、原点和闭合几何的表面朝向。尚未在 Unity 编辑器内验证最终显示与材质映射。

## 重建

在项目根目录执行：

```powershell
& 'D:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background --python 'Art\SkyArchitecture\build_architecture.py'
& 'D:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background --python 'Art\SkyArchitecture\verify_architecture.py'
```

重建依赖 `Art/SkyPillar` 中的原柱子源文件、几何函数及原有 Unity 材质，会覆盖本套生成的模型、源文件与预览。
