# 远征二号 / Yuanzheng-2 — v0.11.0

2026-10-22 完整优化版，基于 v0.10.0。模型、材质与专用配置已直接更新 KIU。KSP 1.12.3 下已分别完成原版 TweakScale 2.4.8.9 与 Rescaled 3.3.2.1＋RF＋B9 的定向验收；中文标题和菜单、Waterfall、RemoteTech 模块及 VAB Organizer 分类均有实际记录。报告：`local_workspace/yuanzheng-2/optimization-20261022/Report.html`。

安装目录：`GameData/KIU/KIU_Chinese_Launch_Vehicle_pack/Parts/Yuanzheng/Yuanzheng2`。请同步整个部件目录、当前 KIU/Plugins/ConfigurableTank.dll 以及所引用的公共纹理，避免只复制 MU。

| 部件 | Part ID | 干重 |
| --- | --- | --- |
| 3.8 m 本体 | KCLV_Yuanzheng2 | 1.1 t |
| 载荷支架 | KCLV_Yuanzheng2_PayloadBracket | 0.2 t |
| 两台外接发动机 | KCLV_YF50E × 2 | 0.05 t × 2（默认或 YF-50E_YZ2） |

两台发动机装齐后干重 1.2 t；加支架为 1.4 t。默认推进剂 6.2 t，含支架湿重 7.6 t；最大 7.2 t 推进剂时为 8.6 t，未计载荷与外接设备。既有 Advanced 发动机配置每台增加 10 kg，不能仍按上述 1.2 t 计算。

## 安装与分离

本体 top 在平台基准 Y=0，四个 payload 节点位于垫块面 Y=0.05。它们不是整个模型的最高处：球冠约达 +0.215 m。宽平底载荷应优先接载荷支架的 top（+0.72 m），并检查实际载荷包络，不要把载荷平底直接压进球冠。中央 bottom 在 -1.54 m，size 0；engine1/engine2 在 X=±0.42、Y=-1.355 m，各安装一台现有 YF-50E，暂代 YF-50D。

支架默认分级只释放 bottom，使支架随载荷离开本体。top 默认不参与分级，通过右键“分离顶部载荷”或动作组独立释放。两端都启用分级会同时触发。旧 craft 保留旧节点与分级状态时，应重新安装相关零件并核对开关。

支架保留 24 个圆角三角孔，连续上下环、孔边唇口及分离组件。24 个凸碰撞分区近似侧壁与安装环，中央贯通；小减重孔不是逐孔贯通的物理碰撞孔。底盖保留中央双发动机通道和四个观察口。造型不代表真实有限元或拓扑优化结果。

## RealFuels 与任务配置

RF 净容量为 6278.011266 L。默认 UDMH/NTO 已分配容积分别为 2486.790018 L / 2919.275238 L，均加满，合计 6.2 t；871.946009 L 保持未分配。按 UDMH/NTO = 46%/54% 重新分配整个容量可装 7.2 t。最大容量的实体内部余量很紧：现行壁面、内部占用、气相预算假设下，NTO 余量约 24.2 L；这是游戏模型预算，不是压力容器设计认证。

RCS 默认与主发动机共用 UDMH/NTO。切换 MMH/NTO 或 Hydrazine 后，需另行在 RF 燃料箱中分配对应燃料；主发动机配方不会跟随 RCS 切换。不要把“已分配但未加满”误认为默认的未分配空间。

需要多次点火时，两台现有 YF-50E 都选择新增的 `YF-50E_YZ2` RF 配置：20 次点火、2–6.5 kN、每台 50 kg、UDMH/NTO 体积比 0.46/0.54。每次点火仍检查推进剂沉底和 1 EC 点火能量；先用 RCS 稳定推进剂再点火。通用默认配置不变。这是暂用现有发动机的模组任务适配，不是实测 YF-50D 数据，也不代表完整 RO/RP-1 平衡。

## 电源与通信

本体有 6000 EC，没有发电器。Command 0.025 EC/s 加 RemoteTech 常开天线 0.001 EC/s，仅按两项静态负载约 64.1 小时；未包含点火、其他设备、发报、休眠及其他模组影响。内置 RT 全向距离为 5 Mm，实际可控性还取决于地面站、遮挡和网络。

较长任务可外装现有设备。示例规划使用两块 OX-4L 太阳能板和 HG-5 天线，增加 105 kg：太阳能板各标称 1.64 EC/s，实际随照明与距离变化；HG-5 在 RT 下为 20 Mm 定向天线，激活耗电 0.55 EC/s，需选择目标并建立链路。展开后不可收回的太阳能板需留出整流罩、RCS 和载荷分离空间。示例已验证两板完全展开、实际发电和 HG-5 激活：一次受光采样约 1.25 EC/s，另一块被遮挡，电池从 3000 EC 回升。没有把天线激活等同完整通信链路或任意距离通信。

## 表面、缩放与依赖

Default / Matte 只切换制造件：顶面硬件、底盖、发动机接口和独立支架。金箔/银箔保留自身反射与折痕。新公共贴图：Shared_CrumpledFoil_GoldSpec.v1.0.0、Shared_CrumpledFoil_SilverSpec.v1.0.0（各 1024²）、Shared_CrumpledFoil_NormalAG.v1.1.0（2048²）。另需 Shared_FlatNormal.v3.5.2、Shared_MatteSpec.v1.0.0，统一位于 GameData/KIU/Common/KIU_Common_texture。专用纹理为 1024²；4×4 PNG 仅为 MODEL 公共贴图重映射入口，不是最终箔材贴图。

本体和支架的 TweakScale 名义尺寸均为 3.8 m，装配时保持一致。原版 TweakScale 2.4.x 的 RF+B9 兼容使用当前 ConfigurableTank.dll 中的显式视觉开关放行；依赖环境已有 Harmony 2。YZ2 不添加 ConfigurableTank 模块，RF 与 TweakScale 仍各自负责容量/质量/缩放。真正改变燃料、体积、质量或节点的 B9 开关不会被放行。两版均通过 0.75／1.25／1 比例下的质量、容量、加注比例、节点、价格及存读档检查。当前 DLL 还为这两个 Part 显式启用 `KIURestoreScaledNodes`：修复原版飞行重载后的节点回退，调用原版节点计算而不移动零件。ConfigurableTank 自身的动态节点和功能性 B9 节点切换不使用此修复。RCS 保留所选 RF 配置的推力参数，不据缩放测试宣称各尺寸的姿控任务性能已重新平衡。

本轮本体 31,678 三角面 / 5 Renderer / 54 碰撞体，支架 8,192 / 1 / 24；每 Renderer 单材质、单 submesh。球罐包络和全部 20 个 RCS 变换保持不变，底盖及新细节已做离线间隙/喷流采样。本轮实际触发了六自由度 12 组 RCS 输入，40 层 Waterfall 在测试并集中全部启用；两种替代 RCS 燃料也实际消耗。双发动机两次点火及关机、顶部手动释放、底部分级和飞行存档重载均已核验。仍不代表完整轨道任务、热流或真实结构强度认证。

中英本地化、Waterfall、RemoteTech 和 VAB Organizer 配置保留；支架归入 payload_adapters，不添加无用途的 RF、RT 或 Waterfall 模块。历史 v0.9/v0.10 实测与报告保持不变。运行资源于2026-10-23按用户统一授权提交0f41ca1并推送。

## 已保存的示例与来源

`KIU/CraftFiles/Non-RO/KCLV/KIU-Yuanzheng-2 RF.craft` 是本体＋支架＋双 YF-50E；`KIU-Yuanzheng-2 RF Services.craft` 再加两块 OX-4L 和 HG-5。均需 RealFuels，使用 YF-50E_YZ2；没有测试占位载荷。复制到所选存档的 Ships/VAB 后装载，实际载荷接支架 top。两份来自原生保存，已在另一版 TweakScale 中交叉载入；不代表完整 RO/RP-1 方案。

可编辑完整源归档：`KIU-Model/Yuanzheng-2/versions/v0.11.0/Yuanzheng2_v0.11.0.blend`；图像打包，附带独立 MU 导出工具和保留接口的导出配置。模型归档0817f37已于2026-10-23推送。ConfigurableTank本地兼容修复a5e63ee与远端塔段性能修复合并为6a5ea73，重新编译并通过12项保护检查；前文原生验收针对当时DLL，未据合并编译宣称完整重测了远征二号与塔架。测试环境已恢复原有依赖与语言。
