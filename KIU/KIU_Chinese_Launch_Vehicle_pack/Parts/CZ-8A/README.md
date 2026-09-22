# CZ-8A · 3.35 米低温上面级

本目录是这组三个零件的正式运行文件与后续维护入口。直接修改本目录并随 `KIU` 使用，不再生成独立发布包。未经用户明确要求，不执行 Git 提交或推送。

## 零件与目录

| 内部 Part 名称 | 配置与模型目录 | 功能 |
|---|---|---|
| `KCLV_CZ8A_Stage_2` | `Tanks` | 3.35 米燃料箱；中国航天／空白涂装 |
| `KCLV_CZ8A_Engine_Mount` | `EngineMount` | 独立双 YF-75 支架，含原有 12 个 RCS 喷口及姿控推进剂 |
| `KCLV_CZ8A_Interstage` | `Interstage` | 3.35 米直筒级间段，顶部连接处分离 |

三件均采用 `rescaleFactor = 1`，在有／无 RealFuels 时都保持实际 3.35 米。发动机沿用现有 `KCLV_YF75`，配合当前完整 KIU 的真实尺寸配置使用；YF-75 若另行缩小，管口将不再匹配。

`Textures` 保存该组零件的专用结构图和法线，燃料箱与支架共用其中一套。各模型旁的 `Ref_*.png` 是小型加载占位图，`MODEL/texture` 将它们替换为真实贴图。MU 内部网格名沿用经过验证的模型，文件名与零件名采用 CZ8A 命名。

公共资产直接引用 `KIU/Common/KIU_Common_texture`：

- `Shared_CASC.v3.3.1.dds`
- `Shared_ChinaFlag.v3.3.1.dds`
- `Shared_White.v3.3.0.dds`
- `Shared_ChinaSpaceText.v1.0.0.png`

公共图像内容、压缩格式与分辨率保持不变。本目录不复制公共国旗、标志或字形的完整图片。公共目录由用户维护，制作新贴图前先查其 `catalog.json`。

## 配置组织

所有 CZ-8A 专用适配只在本目录的 `Compatibility` 维护，本地化只在 `Localization/zh-cn.cfg` 与 `Localization/en_us.cfg` 维护；不追加到 KCLV 顶层或 `KIU/Common` 的适配／本地化文件。

| 子目录 | 行为 |
|---|---|
| `B9PartSwitch` | 只切换燃料箱涂装；支架始终为独立 Part |
| `Stock` | 没有 B9 时使用 `ModulePartVariants` 切换涂装 |
| `RealFuels` | 箱内液氢／液氧，支架内联氨；保留已验证的容量与 RCS 参数 |
| `RO` | 真实尺寸及 `RSSROConfig` 标记 |
| `RP-1` | 可见性、现代科技节点、按模型尺寸开模、支架 RCS 标签 |
| `Tweakscale` | 复用 KCLVStack，基准直径统一为 3.35 米 |
| `VABO` | 燃料箱、引擎支架、级间分类；复用 `size3_35` |
| `Waterfall` | 支架 RCS 复用 KIU 的 NewRCS 模板，保留声音并移除重复原版粒子 |

每条补丁限定新零件名称并带相应 `NEEDS` 条件。现有全局缩放／RO／RP-1 补丁的设置与新件一致；MechJeb 的全局补丁要求 `ModuleCommand`，这三件均不具备。发动机喷焰继续由原 YF-75 的适配管理。三个零件没有天线、发射台或新 PBR 材质，因此不为 RemoteTech、KerbalKonstructs、Textures Unlimited 等生成无作用的补丁。

RP-1 科技节点按 KCLV 现有 CZ-8 的结构件规则放入 `orbitalRocketry2019`。基础配置均已包含 `cost/entryCost`；RealFuels/RP-1 储箱成本沿用项目现有计算方式。容量、干质量和费用是原型的玩法估算，不代表真实 CZ-8A 硬件参数或已完成 RP-1 生涯平衡验证。

## 装配与测试件迁移

1. 燃料箱下挂点接支架上挂点。
2. 支架的两个偏心挂点各装一台原尺寸 YF-75；第二台绕自身纵轴转 180°，使四处供给管口相接。
3. 支架中央向下的挂点接级间段上挂点。

[示例飞船](CraftFiles/CZ8A_UpperStage.craft) 继承 v1.0.3 的正确位置和发动机朝向，已迁移零件名；其保存资源与模块来自 RealFuels 环境，需要 KIU、ModuleManager、B9PartSwitch、RealFuels 及其依赖，发动机特效使用 Waterfall。该示例用于装配参考，并非完整发射火箭。

旧测试文件使用的内部名称与现在不同：

| 原测试件名 | 正式件名 |
|---|---|
| `KCLV_CZ335_CryoTank` | `KCLV_CZ8A_Stage_2` |
| `KCLV_CZ335_EngineMount` | `KCLV_CZ8A_Engine_Mount` |
| `KCLV_CZ335_Interstage` | `KCLV_CZ8A_Interstage` |

craft/save 中 KSP 通常将下划线保存为点号，例如 `KCLV.CZ8A.Stage.2`；完整的 `part`、`link`、`attN` 引用需一起迁移，涂装模块 ID 由 `CZ335Livery` 改为 `CZ8ALivery`。本次只更新随项目提供的示例，未修改用户存档。安装正式目录时移出旧 `GameData/KIU_CZ335` 测试目录，以免编辑器出现两套零件；先备份仍使用测试件的飞船。

## 来源、验证与维护

来源为 `MODELS/CZ-335_Cryogenic/releases/v1.0.3` 中经过管线修复的模型。只改 MU 末尾纹理名称表；几何、三角形顺序、法线、切线、材质参数、碰撞体、RCS 和发动机挂点字节全部保留。专用贴图也逐字节复用。没有重新缩放支架或引擎，没有分离小固推。

从仓库根目录执行：

```powershell
python MODELS/CZ-335_Cryogenic/tools/validate_cz8a.py
python tools/validate_common_textures.py
```

离线证据位于 `MODELS/CZ-335_Cryogenic/audit/cz8a-integration/validation.json`，包括完整 MU 字节回归、引用闭包、双语键、示例装配、公共图片哈希及可选依赖条件覆盖。条件覆盖是静态检查，不是运行 ModuleManager。

本次整合尚未进行原生 KSP 复测：测试安装由 CZ 家族任务占用，未修改该游戏目录或启动 KSP。历史 v1.0.3 的 260 项原生断言只证明原测试目录中的模型与静态装配，不能代替新路径、纹理重映射及新增适配的原生验收。RO、RP-1、TweakScale、VABOrganizer 的完整组合也未在本机实测。飞行、发动机摇摆时的管路运动、分离动力学及性能仍不在已验收范围。

后续更新以本目录为准。`MODELS/CZ-335_Cryogenic` 保留 Blender 源场景、历史冻结版本、一次性迁移工具和验证记录；不重新运行历史打包脚本。原始及派生美术资产沿用项目 CC BY-NC-SA 4.0，MU 工具的 GPL 声明保留在其原工具目录。
