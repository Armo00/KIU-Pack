# 远征二号 / Yuanzheng-2 — v0.8

## 2026-09-25：VAB 不显示的加载修复

v0.8 硬件材质使用带法线槽的 Shader，却漏绑 `_BumpMap`。已在 KSP 复现：`PartLoader.ReplaceTextures` 抛出 `NullReferenceException`，导致整个 Part 编译失败、VAB 中不显示。硬件和发动机转接盘现显式绑定 4×4 公共平坦法线；金箔与银箔仍使用原褶皱法线。几何、节点、碰撞、RF 和 RCS 配置未改。

本机后台原生对照：`yz2-v08-load01` 修复前失败；`yz2-v08-load02` 修复后载入远征二号及两台 YF-50E，4 个模型 Renderer 均正常，截图和保存的 craft 已核对。此轮验证加载与 VAB 显示，没有重新验证飞行性能。证据在 `local_workspace/yuanzheng-2/diagnostics/20260925-load`。

另一台电脑至少同步以下运行文件，再重启 KSP：本目录的 `Assets/Yuanzheng2.mu`、新增 `Assets/YZ2_HardwareNormal.png`、`UpperStage/part.cfg`，以及 `GameData/KIU/Common/KIU_Common_texture/Shared_FlatNormal.v3.5.2.png`。建议同步整个当前 Yuanzheng2 目录和公共库，避免混用版本。下面保留造型修订时的历史说明；原来的“未启动 KSP”仅指当时的建模阶段。本轮未提交 Git。


安装目录：`GameData/KIU/KIU_Chinese_Launch_Vehicle_pack/Parts/Yuanzheng/Yuanzheng2`。Part ID：`KCLV_Yuanzheng2`。部件无需测试控制 DLL。安装时清除旧的 `Parts/Yuanzheng-2` 副本，避免重复 Part。

## 模型与节点

主体直径 3.8 m，四球罐、四载荷挂点，两台主发动机另装。顶部中央节点 Y=0.075 m、朝上；底部中央节点 Y=-1.54 m、朝下。底部节点随下移的 RCS 留出空间，旧载具若保留原节点位置应重新装配。

金色上法兰整体低于白色平台顶面 8 mm，消除原来的共面重叠。金箔侧裙从 Y=-0.006 延伸到 -1.120 m，覆盖球罐完整 1.53 m 高度的 72.8%。四组五喷口 RCS 直接安装在金色侧壁下部，中心半径 2.0 m、Y=-0.95 m。65 mm 短箱形底座通过曲面加强板、穿透式紧固件及内侧背板连接竖向加强梁，再传至上下承力环；金箔不承受推力。含喷口最大外廓约 4.216 m，主体直径仍为 3.8 m，装入整流罩时按喷口外廓留空间。顶部增加中央检修盖、四组服务盖板及封盖接口、面板接缝和紧固件，细节最高 24 mm，低于 +75 mm 安装节点。每个 Renderer 仅有一个材质和一个 submesh。

engine1/engine2 各安装既有 KCLV_YF50E。现有默认发动机每台 0.05 t，接口与发动机自身均未更改；这是暂代 YF-50D 的装配，不代表已核实的真机接口。

## 质量与 RF 分配

| 项目 | 数值 |
| --- | --- |
| 本体干重 | 1.1 t |
| 默认双 YF-50E | 合计 0.1 t |
| 整级干重 | 1.2 t |
| 默认推进剂／整级湿重 | 6.2 t／7.4 t |
| UDMH/NTO 满容量／整级湿重 | 7.2 t／8.4 t |
| RF 总净容积 | 6278.011266 L |
| 默认已分配容积 | 5406.065256 L，全部加满 |
| 默认未分配容积 | 871.946009 L |

按 CZ-7 一级的方式：UDMH `maxAmount = 39.61111111111111%`，NTO `maxAmount = 46.5%`，两者 `amount = Full`。两种资源默认分别是 2486.790018 L、2919.275238 L。它们各自的资源条为满，其余 13.8889% 总容积尚未分配。

要装 7.2 t UDMH/NTO，应在 RF 编辑器中把容积分配调整至 46%／54%，再加满；仅点击 Fill 不会扩大已分配容量。7.2 t 是该混合物的容量换算，不是任何燃料的统一质量上限。切换其他燃料时密度和总质量会改变。

四只球罐的几何、容积不变。7.2 t 保留原先的较紧内部预算：径向 8 mm、构件 2%、气枕 3%，NTO 两罐余约 24.2 L；此为游戏模型空间估算。干重按用户设定更新，不代表重新完成真实压力容器结构设计。

非 RF：保留 6.12 t LF/Ox + 0.08 t MonoPropellant，总湿重同为 7.4 t。RF 本体质量由 ModuleFuelTanks 按体积计入，RCS 的 ModuleEngineConfigs 使用 origMass=-1，避免重复修改本体质量。原有 Advanced YF-50E 每台增重 0.01 t，换用后整级增重 0.02 t。

## RCS 配置

| 配置 | 体积比 | 单喷口推力 | 真空／海平面 Isp |
| --- | --- | --- | --- |
| UDMH/NTO（默认） | 0.46／0.54 | 100 N | 290／100 s |
| MMH/NTO | 0.499／0.501 | 100 N | 310／110 s |
| Hydrazine | 1 | 80 N | 240／85 s |

这是暂定游戏参数。默认 RCS 与主发动机共用 UDMH/NTO。选择其他 RCS 配置后，需要在 RF 里为 MMH 或 Hydrazine 分配并加注燃料；RCS 的选择不会更改两台现有主发动机的燃料配置。资源从本 Part 取用。6000 EC 保持 unmanaged。

现有 YF-50E 在 RF 下仍受推进剂沉底与点火次数限制，未改全项目发动机配置。

## 材质与兼容性

金箔与银箔改为独立的平铺 UV，1.50 m 重复周期，直接复用公共 2K 法线，避免整级 1K 图集压缩掉细褶皱。三个专用 DDS 仍为 1024²：Color/Specular 为无照明色板，Normal 为模型加载占位。MODEL 将 Normal 重映射到公共 `KIU/Common/KIU_Common_texture/Shared_CrumpledFoil_NormalAG.v1.0.0`。必须随公共库一同安装。

共享法线由已有 CC0 Foil002 的 RGB +Y 法线以 0.70 强度转换为 KSP A/G 编码，源文件保持原样。Default 使用金属高光，Matte 仍由 B9 切换；保留 Waterfall、RemoteTech、TweakScale、VABO、TUFX 和中英本地化。RO 补丁不代表完整 RO/RP-1 生涯平衡。

共 25,156 三角面（本体 24,772 + 转接盘 384），相对 v0.7 净增加 1,204 面。4 个 Renderer、3 个材质，每个 Renderer 单材质/单 submesh。新细节复用已有 1K 专用色板与公共法线；本轮没有增加或修改贴图。金箔细节来自法线，不靠细分网格。

## 本轮证据

本轮完成实际 MU/DDS 的 Blender 回读渲染与配置检查。20 个喷口，3,060 条 15° 扩散角采样射线未发现本体遮挡；48 组正反控制分配仍可实现六自由度。检查的是本部件，不包含外接载荷、发动机及整流罩。

四组短底座取代外露六杆支架。31 种喷口开关组合、每喷口 100 N 的安装面合力上限约 261.3 N，合力矩上限约 12.15 N·m。这是接口静载估算，不代表接头、材料或发射振动强度验证；旧六杆矩阵结果不适用于本结构。

顶部细节与安装点间有余量，球罐、金箔覆盖高度、连接节点、质量和 RF 配置沿用 v0.7。未启动 KSP、未同步游戏测试目录。最新 HTML 报告、可编辑 Blender 和记录位于 `local_workspace/yuanzheng-2`，v0.7 报告及源场景保留供对照；未提交 Git。
