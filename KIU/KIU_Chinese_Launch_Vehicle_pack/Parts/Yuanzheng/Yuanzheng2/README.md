# Yuanzheng-2 / 远征二号

目录组织沿用 CZ-10B；整个文件夹保留在 `GameData/KIU/KIU_Chinese_Launch_Vehicle_pack/Parts/Yuanzheng/Yuanzheng2`。无需额外 DLL。

```text
Yuanzheng/Yuanzheng2/
  Assets/                 Yuanzheng2.mu、YZ2_Color.dds、YZ2_Normal.dds、YZ2_Specular.dds
  UpperStage/part.cfg      KCLV_Yuanzheng2
  Localization/           en_us.cfg、zh-cn.cfg
  Compatibility/
    B9PartSwitch/          Default / Matte Finish
    RealFuels/             UDMH/NTO 燃料与双组元 RCS
    RemoteTech/            通信与无人控制
    VABO/                  avionics 分组
    Waterfall/             20 个 RCS 喷口；主发动机复用现有适配
    Tweakscale/            KCLVStack，默认 3.8 m
    TUFX/                  KIU-Metal-Review-Editor / Flight 可选配置
    RO/                    撤销全局通配补丁的未验证 RO 标记
```

## 装配与质量

本体直径 3.8 m，比例 1:1。`engine1`、`engine2` 各安装一台已有 `KCLV_YF50E`；本体不包含主发动机。新增两块低面数转接安装盘，将较小的 YF-50E 安装端接到原预留安装环。发动机节点中心距 0.84 m，仍是临时适配尺寸，不代表已核实的 YF-50D 真机接口。顶部 `payload1`—`payload4` 为四个载荷挂点，`top`/`bottom` 为中央堆叠接口。外接载荷分离器、转接段另装；本体不自动抛弃载荷。

| 项目 | 默认环境 | RealFuels |
| --- | --- | --- |
| 本体干重 | 1.700 t | 1.700 t |
| 两台默认 YF-50E | 0.100 t | 0.100 t |
| 默认主推进剂 | LF 550.8 + Ox 673.2 单位，共 6.120 t | UDMH 2486.790017962 L + NTO 2919.275238477 L，共 6.200 t |
| RCS 推进剂 | MonoPropellant 20 单位，共 0.080 t | 与主发动机共用上述 UDMH/NTO；体积配比同为 0.46:0.54 |
| 电量 | 6000 EC | 6000 EC；RF unmanaged resource |
| 整级干重／默认湿重 | 1.800 / 8.000 t | 1.800 / 8.000 t |
| 最大容量／整级最大湿重 | 默认装载不变 | 7.200 t / 9.000 t；UDMH 2887.885182149 L + NTO 3390.126083393 L |

上述质量不含载荷及外接适配器。默认环境的主推进剂/RCS 分账、100 N/喷口、EC 容量和 Isp 是暂定游戏参数。RF 净容量上限 6278.011265542 L，默认装量 5406.065256439 L；`amount` 与 `maxAmount` 分开设置。RF 按容积计算本体干重，在默认尺寸为 1.7 t，TweakScale 缩放时随容积变化。各资源的附加罐壳质量设为零，因为已包含在本体预算内，避免重复计重；参考 [RealFuels 质量计算实现](https://github.com/KSP-RO/RealFuels/blob/master/Source/Tanks/ModuleFuelTanks.cs)。

四个主球罐外径 1.53 m，实际网格包围体积共 7413.68 L；小增压罐不计入推进剂容量。两罐 UDMH、两罐 NTO 的分配下，7.2 t 由 NTO 侧限制。采用径向 8 mm 包覆/间隙/罐壁预算、2% 内部构件和 3% 气枕，可用 6828.65 L，NTO 两罐余量 24.20 L。可保留外观模型，但这是较紧的内部设计预算，不是实测壁厚或承压认证。原先 20 mm / 5% / 5% 的预留只适用于默认 6.2 t。详细敏感性结果见工作区 `audit/tank-capacity.json`。

UDMH/NTO 的 RF 热模型字段显式设为 3 mm 壁厚、5 mm 隔热层，替代模组通用的 100 mm 壁厚默认值；这两个字段本身不会替游戏自动扣减液体容量，容量已按上述预算另行核算。

按用户要求暂用 YF-50E，不新建或改名为 YF-50D。没有改动全项目 YF-50E：默认 RF 发动机仅允许一次点火，Advanced 配置允许重启但单台质量增加至 0.060 t，改选后整级变为 1.820 / 8.020 t。未来正式 YF-50D 接入时须重新核对接口、质量和推进性能。

RealFuels 下现有 YF-50E 还要求推进剂沉底。自由落体中未稳定推进剂就点火，会报管路气相并消耗点火次数；不能把有电、燃料充足或通信连接正常等同于可点火。此行为在真空 API 测试中实际复现，未通过改写全局发动机或禁用 RF 沉底规则绕过。

## 资产与验证

- 本体 23,248 三角面，加两块转接盘 384 面，共 23,632 面；2 个 Renderer，每个只有 1 个材质、1 个 submesh，共用 1 套材质。34 个简单碰撞体。
- 20 个 `rcsThrust`，四组五喷口，KSP 本地 +Z 为排气方向，`useZaxis=True`；支持六自由度。没有额外反作用轮。
- 三张专用 1024×1024 BC3 DDS，含 11 层 mip。Color RGB 基础色、A=1；Normal A/G 为切线 X/Y；Specular RGB 为彩色 F0。DDS 使用 KSP 行序。材质为 KSP/Bumped Specular (Mapped)，光滑度 0.80；Default 恢复彩色高光，Matte 引用公共零高光图。Mapped shader 的统一光滑度不能完整复现 Blender 的逐像素粗糙度。
- 箔材图集衍生自公共库内的 `Shared_CrumpledFoil_NormalGL.v1.0.0.png` 和 `Shared_CrumpledFoil_Roughness.v1.0.0.png`，原素材为 [ambientCG Foil002](https://ambientcg.com/a/Foil002)，CC0-1.0。图集采用本体专属 UV，不是可直接通用的重复纹理；公共源文件保持不变。
- Blender 源文件、导出脚本、MU 回读装配预览、检查记录和 HTML 报告位于 `local_workspace/yuanzheng-2`。
- 已在 KSP 1.12.3 实际 KIU 路径加载；默认/哑光切换、RF RCS 和双 YF-50E 共用推进剂均取得原生证据。各项兼容性最终结果与适用资产哈希见工作区 `audit/ksp-native-summary.json`，不以离线检查代替未完成的飞行验证。
- 金属外观测试采用反射 Low、256²、MSAA 关闭，以及本目录的 TUFX HDR/FXAA/Neutral 配置。TUFX 是可选项，不在发布配置中强改玩家全局设置；已有存档可能覆盖全局 TUFX 默认项，需要在该场景实际选中对应配置。
- RO 补丁仅避免宣称已完成 RO 平衡；不是 RO/RP-1 生涯适配。


## 目录迁移

目录迁移说明（2026-09-24）：现行位置为 Parts/Yuanzheng/Yuanzheng2，模型引用与维护脚本已同步。按用户要求，本次迁移后未重新测试；下方原生记录与截图来自迁移前版本，测试环境尚未重新部署。
安装新目录时应移除旧的 Parts/Yuanzheng-2 安装副本，避免重复加载同一 Part ID。历史测试日志、哈希清单和截图保留原始路径。
