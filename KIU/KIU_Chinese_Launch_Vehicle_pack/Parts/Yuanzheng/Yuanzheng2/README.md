# 远征二号 / Yuanzheng-2 — v0.10

载荷支架侧壁改为 24 个交错圆角三角减重孔，约占展开参数域 61.7%；连续上下安装环、轴向与斜向连接筋，孔角约 47 mm。保持 200 kg、高度 0.72 m 和节点位置，新增底部分离器。直接更新 KIU，没有新增贴图或运行插件。

默认分级只释放 bottom，支架随载荷离开上面级；top 默认不参与分级，右键“分离顶部载荷”或动作组可单独释放。如果手动把两端都启用分级，会在同一分级同时触发。旧 craft 可能保留旧的开关，更新后建议重新安装支架或核对两端分级设置。

本轮验证：KSP 1.12.3 后台实测通过：新支架正确加载、质量 200 kg；一次分级只触发底部分离。真实存档确认本体与两台发动机保留为 3 个部件，支架与测试载荷组成另一个 2 部件载具，顶部分离器仍为未触发。游戏正常退出、恢复设置且未抢前台。

造型参考承力路径，未声称完成真实有限元／拓扑优化。以下为整级参数和上一版底盖说明。

安装：`GameData/KIU/KIU_Chinese_Launch_Vehicle_pack/Parts/Yuanzheng/Yuanzheng2`。

| 部件 | Part ID | 干重 |
| --- | --- | --- |
| 本体 | `KCLV_Yuanzheng2` | 1.1 t |
| 载荷支架 | `KCLV_Yuanzheng2_PayloadBracket` | 0.2 t |

底盖遮蔽约 70.6% 投影，中央双发动机通道和四个观察口保持开放；计入原本体质量预算。原 RCS、球罐和发动机接口不移动。底部中央节点缩至 size 0，Y=-1.54 m 不变；engine1/engine2 位于 X=±0.42、Y=-1.355 m。

中央 top 节点从 Y=0.075 改为平台安装面 Y=0；四个 payload 节点从 0.075 改为垫块表面 0.05 m。新支架 bottom=0、top=0.72 m，底环外径 3.51 m、顶环外径 2.55 m。上下两端均有独立分离器；默认只有底部参与分级，顶部通过右键或动作组释放。旧 craft 的装配位置可能需要重新安装载荷以应用新节点。

两台默认 YF-50E 装齐后的整级干重仍为 1.2 t；额外装载荷支架后为 1.4 t。RF 总净容积仍为 6278.011266 L，默认为已分配并加满的 6.2 t UDMH/NTO，余 871.946009 L 未分配；重新分配全部容积可达 7.2 t。含支架默认／最大湿重为 7.6／8.6 t，未含载荷。RCS 保留 UDMH/NTO、MMH/NTO、Hydrazine 三套配置。

新支架兼容 B9 Default / Matte、TweakScale、中英本地化和 VAB Organizer 的 payload_adapters 分类。TweakScale 名义尺寸同本体为 3.8 m，两者须同步缩放。没有给纯结构支架增加 RF 储箱、RemoteTech 或 Waterfall 模块。RO 补丁仍不声明已完成完整 RO/RP-1 平衡。

本体 27,796 三角面／5 Renderer，支架 6,464 三角面／1 Renderer。每 Renderer 单材质／单 submesh。保留全部法线槽显式绑定，避免旧 v0.8 的 PartLoader.ReplaceTextures 空槽错误。共享依赖：Shared_FlatNormal.v3.5.2、Shared_CrumpledFoil_NormalAG.v1.0.0、Shared_MatteSpec.v1.0.0，均位于 GameData/KIU/Common/KIU_Common_texture。

离线检查：原有运行网格与 RCS 变换保留；新底盖、支架与球罐无穿插；两台现有 YF-50E 不穿底盖；新增底盖未遮挡 960 条 RCS 采样射线。支架接触面已按实际平台、垫块及顶环核对。几何采样不代表真实热流或结构强度分析。

上一轮 v0.9 记录：KSP 1.12.3 后台验证通过，本体、新支架及双 YF-50E 共四个部件载入 VAB 并保存；新支架实测 200 kg，Default / Matte 切换及恢复生效，全部六个 YZ2 Renderer 正常。飞行场景取得顶部及底盖外观截图，未进行点火、载荷分离或完整任务性能复测。游戏正常退出、设置恢复且未抢前台。

源场景：`local_workspace/yuanzheng-2/source/Yuanzheng2_v0.10.blend`。
报告：`local_workspace/yuanzheng-2/Report.html`。
本轮证据：`local_workspace/yuanzheng-2/revisions/v0.10`；上一轮：`local_workspace/yuanzheng-2/revisions/v0.9`。历史报告和 v0.8 备份保留于工作目录。

请同步完整 Yuanzheng2 目录与公共依赖，避免遗漏新支架模型或混用旧配置。本轮未提交或推送 Git。
