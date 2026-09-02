# 正式地图编辑器与棋盘锚点规格 v1

## 资产边界

- 地图 Prefab 只保存环境地形、装饰建筑、灯光、后处理组件和空间锚点，不保存战斗规则。
- 多个关卡可以引用同一地图；第一版全部正式关卡共用 `PF_BaseFormalBattleMap`。
- 棋盘尺寸、控制区、战斗建筑、波次、目标和授权继续由关卡 JSON 与纯 C# Domain 管理。
- `EnvironmentRoot` 下的对象统一进入 Unity `Ignore Raycast` 层；正式战斗启动时递归禁用所有环境 Collider，禁止干扰棋盘点击、单位移动或战斗判定。

## 编辑器工作流

1. 通过 `Company War-RE/地图编辑器` 打开工具。
2. 输入地图名称与 Gizmos 预览行列数，选择“创建地图 Prefab”。
3. 工具生成 `EnvironmentRoot`、基础地面、`Lighting` 分组和 `BattleBoardAnchor`，并保存到 `Assets/CompanyWarRE/Content/Maps`。
4. 通过“打开 Prefab 编辑环境”进入 Prefab Mode，直接摆放地形、装饰、灯光与 Volume/Post Process 组件。
5. `BattleBoardAnchor` 可以平移和旋转；禁止非等比缩放。自定义 Inspector 会显示错误并提供等比修复。
6. Scene Gizmos 以锚点为坐标系绘制小格、每 3 格控制块边界和前进方向，不生成临时 GameObject。

## 运行时坐标

- 棋盘自身中心与 `BattleBoardAnchor` 局部原点重合。
- 对 `columns × rows` 棋盘，小格中心坐标为：
  - `x = column - 1 - (columns - 1) / 2`
  - `z = row - 1 - (rows - 1) / 2`
- 棋盘、单位、战斗建筑和反馈均作为锚点后代，使用同一局部坐标转换。
- 摄像机焦点、平移边界、旋转和缩放以锚点坐标系计算，因此地图和锚点可以任意平移、旋转。
- `FormalBattle` 显式绑定共用基础地图；Resources 加载和旧程序化环境仅作为缺失引用时的安全回退。

## 第一版验收

- 基础地图 Prefab 无 Missing Script，并包含 `EnvironmentRoot` 与 `BattleBoardAnchor`。
- 18×30 棋盘的首末列中心分别为 `-8.5` 和 `8.5`，外边界分别为 `-9` 和 `9`。
- 移动或旋转锚点后，棋盘、单位、反馈和摄像机整体保持对齐。
- 环境 Collider 不参与运行时射线或物理碰撞。
- 锚点非等比缩放会被编辑器和运行时校验报告。
