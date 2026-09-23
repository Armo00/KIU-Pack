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
- `Shared_MatteSpec.v1.0.0.png`（只用于哑光高光控制，不是外观图片）

公共图像内容、压缩格式与分辨率保持不变。本目录不复制公共国旗、标志或字形的完整图片。公共目录由用户维护，制作新贴图前先查其 `catalog.json`。

## 配置组织

所有 CZ-8A 专用适配只在本目录的 `Compatibility` 维护，本地化只在 `Localization/zh-cn.cfg` 与 `Localization/en_us.cfg` 维护；不追加到 KCLV 顶层或 `KIU/Common` 的适配／本地化文件。

| 子目录 | 行为 |
|---|---|
| `B9PartSwitch` | 燃料箱涂装与三件的 Default Finish／Matte Finish 独立切换；支架始终为独立 Part |
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

来源为 `local_workspace/CZ-335_Cryogenic/releases/v1.0.3` 中经过管线修复的模型。初次整合只修改 MU 末尾纹理名称表。2026-09-22 标识更新进一步调整燃料箱两侧的 12 个国旗、CASC 与中文字形贴面：放大、修正国旗比例并避开下方短管。除此之外的模型字节（包括结构、材质默认参数、碰撞体、RCS、供给管口和发动机挂点）全部保留，专用贴图逐字节复用。

2026-09-23 按用户要求删除燃料箱两侧的国旗对象，现有涂装只显示 CASC 与“中国航天”。两个标志和八个字形的位置、尺寸及其余模型字节不变。旧材质／纹理表与加载映射保留兼容，但已没有渲染国旗的网格；公共国旗文件继续供其他零件使用，不删除。离线证据位于 `local_workspace/CZ-335_Cryogenic/audit/remove-flags-20260923`，本次外观调整未启动 KSP 验证。

`SurfaceGloss` 与 `CZ8ALivery` 为两个独立的 B9 模块。Default 不覆盖材质参数，由 B9 恢复各材质原值；Matte 降低普通 Specular 的 `_SpecColor`、`_Shininess`，并为 Mapped Specular 指定公共零值 `_SpecMap`。透明标识贴图、主色图与法线不替换。没有 B9 时仍保留原版涂装切换和模型默认材质，不提供 Finish 菜单。

从仓库根目录执行：

```powershell
python local_workspace/CZ-335_Cryogenic/tools/validate_cz8a.py
python local_workspace/CZ-335_Cryogenic/tools/validate_surface_finish.py
python tools/validate_common_textures.py
```

离线证据位于 `local_workspace/CZ-335_Cryogenic/audit/cz8a-integration/validation.json` 与 `audit/markings-finish-20260922`，包括排除已声明贴面后的完整 MU 字节回归、贴面间隙、引用闭包、双语键、示例装配、公共图片哈希及可选依赖条件覆盖。条件覆盖是静态检查，不是运行 ModuleManager。

2026-09-22 在 KSP 1.12.3 / D3D11 中完成首轮原生检查：五个目标 Part 均成功加载；正式 CZ8A 路径与公共贴图绑定正确；燃料箱的 Default → Matte → Default、涂装独立性与材质覆盖共通过 325 项断言，Default 与恢复后截图字节一致。游戏不在前台时仍能输出图形截图。本轮没有 Deferred / TUFX。

首轮探针的保存字段读取错误已修正。随后 Texturing 的 Deferred / TUFX run05 完成了三件 CZ8A 与 YF-20 / YF-21 的材质参数、涂装／外壳变体、Default 恢复及保存重载检查：本任务五件共 8,172 项断言通过，零失败。证据为 `local_workspace/KCLV_RenderFix/audit/run05/results.txt`，本任务汇总见 `local_workspace/CZ-335_Cryogenic/audit/markings-finish-20260922/report.html`。

回归中发现原版外壳变体会重置 YF-20 / YF-21 的哑光参数。run05 使用另一任务提供的 `KIUSurfaceFinishSync`，在外壳切换后通知 B9 重建材质修改并重新应用当前 Finish；上述 8,172 项通过结论对应包含该插件的测试组合。该任务随后将其同步插件及补丁撤出 KIU，因此它们不包含在本次提交中。未安装对应附加修复时，YF-20 / YF-21 切换外壳后仍可能重置哑光参数，需要重新选择 Finish，不能将 run05 的外壳联动通过结论用于这种环境。CZ8A 三件的专用 Finish 配置全部留在本目录，它们不使用该原版外壳联动补丁。

RO、RP-1、TweakScale、VABOrganizer 的完整组合未实测；本次五件的飞行、发动机摇摆时的管路运动、分离动力学及性能也不在已验收范围。

后续更新以本目录为准。`local_workspace/CZ-335_Cryogenic` 保留 Blender 源场景、历史冻结版本、一次性迁移工具和验证记录；当前燃料箱源场景为 `source/current/CZ8A_Tank.blend`。不重新运行历史打包脚本。原始及派生美术资产沿用项目 CC BY-NC-SA 4.0，MU 工具的 GPL 声明保留在其原工具目录。
