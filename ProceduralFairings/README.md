# CZ-10B Procedural Fairings collision build

2026-10-23：用户授权直接发布并推送，随后补做 KSP 验证。当前为离线通过、原生待验收。

这是基于 KSP-RO **ProceduralFairings 6.8.0.0** 的修改版，保留原程序集名称和依赖，不是另一个 KIU DLL，也不是官方 PF 发布。

## 安装

1. 先安装完整官方 PF 6.8.0.0 及其依赖。
2. 将本目录的 `Plugins/ProceduralFairings.dll` 覆盖到游戏的 `GameData/ProceduralFairings/Plugins/ProceduralFairings.dll`。
3. 同时更新 `GameData/KIU`；CZ-10B 自己的 Compatibility/ProceduralFairings/ColliderOptimization.cfg 启用优化。

**不要把这个 DLL 另放进 KIU/Plugins，也不要同时加载两份 ProceduralFairings.dll。** 本目录只提供替换插件，不包含 PF 的完整零件、贴图或依赖。

## 改动和边界

- 默认维持上游行为；仅 CZ-10B PF 半罩选择新的凸分片模式。
- 当前参考形状每半罩951→30，完整两半1902→60。可见模型、UV、贴图、质量与分离力代码不变。
- 保留空的 nose_collider 方向参考及原 model/Hinge 父级；改形时更新碰撞网格，复制零件时复用层级及只读网格缓存。
- 普通无底部外扩的两半筒段／罩头适用；夹层、外扩、非凸轮廓或误差超限回退上游算法，可能恢复为数百个碰撞体。
- 参考罩形碰撞边界最大径向偏差约13.2 cm；碰撞几何比视觉外形粗。离线通过不等于分离卡顿已消失。

完整可编辑插件源在 `../KIU/CSScripts/ProceduralFairings/`。编译入口为本目录 `build.ps1`，需显式提供本机 KSP 程序集目录、ROUtils.dll 和 Roslyn csc.exe 路径。

恢复官方 PF DLL 并移除 CZ-10B 的 ColliderOptimization.cfg 即撤销本优化。正式更新不会携带 ColliderCount 或 KIUTestControl 测试插件。

上游：Alexey Volynskov（e-dog）、rsparkyc、PhineasFreak 和 KSP-RO contributors。
[源代码 v6.8.0.0](https://github.com/KSP-RO/ProceduralFairings/tree/v6.8.0.0)；上游源码声明 [CC-BY-4.0](https://creativecommons.org/licenses/by/4.0/legalcode)。原作者标记保留；本地修改为可选碰撞生成、对象缓存及清理逻辑。
