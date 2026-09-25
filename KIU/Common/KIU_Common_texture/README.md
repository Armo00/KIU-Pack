# KIU 公共贴图

用户手动维护的公共资源目录。按用户于2026-09-22确认并移动后的路径，仓库源目录为 **`KIU/Common/KIU_Common_texture`**，安装到 **`GameData/KIU/Common/KIU_Common_texture`**，各 Part 通过 `KIU/Common/KIU_Common_texture/文件名` 引用。后续公共贴图与索引统一在本目录维护，不再创建仓库根目录下的旧公共库。历史冻结包内旧路径与索引保持原样。

## 所有 agent 必须遵守的复用流程

统一检索入口为 [catalog.json](catalog.json)。每张图记录路径、名称、实际内容、检索标签、尺寸/格式、来源、哈希、用途与已知消费者。下面的 PROVENANCE 文件是来源历史，不能代替这个统一内容索引。

1. 每次绘制或制作贴图前先读 `catalog.json`，按内容搜索并检查现有图像及 UV/alpha/法线编码，优先复用合适素材。
2. 确实没有合适素材，且新内容有公共复用价值时，将版本化贴图加入本目录，并在同一任务更新 `catalog.json`。
3. 更新前重读当前 JSON，只合并自己的改动，保留其他 agent 的条目；不能只放图片却不写索引。旧发布包内的 JSON 是快照，不可覆盖较新的公共索引。

从仓库根目录可运行 `python tools/validate_common_textures.py`，核对所有图片均有条目、路径存在、URL/大小/哈希一致。规则也写入根目录 `AGENTS.md`，适用于同项目后续任务。

本次初始化加入：

| 文件 | 来源 | 用途 |
|---|---|---|
| `Shared_ChinaFlag.v3.3.1.dds` | CZ 家族 v3.4.0 包，逐字节复用 | 公共国旗，2048² |
| `Shared_CASC.v3.3.1.dds` | CZ 家族 v3.4.0 包，逐字节复用 | 公共 CASC 标志，2048² |
| `Shared_White.v3.3.0.dds` | CZ 家族 v3.4.0 包，逐字节复用 | 公共白色，4×4 |
| `Shared_ChinaSpaceText.v1.0.0.png` | 本次从 Microsoft YaHei Bold 字体栅格化 | “中国航天”字形图集，1024×256 |

文件的固定版本名是消费者使用的接口；改名须同时更新消费者的 `MODEL/texture` 配置和 UV。如果后续更新同名文件，请由用户自行决定兼容性。生成工具不会悄悄覆盖同名不同哈希的公共资源。

目前消费者包括 `KIU_CZ335` 的燃料箱，以及 CZ 家族 v3.4.1 中使用公共贴图的 Part。初始四张资产的哈希与来源见 `PROVENANCE.v1.0.0.json`；新中文字形的布局与 SVG 源在 `MODELS/CZ-335_Cryogenic/source/v1.0.0`。不分发字体文件。

卸载某个 Part 包时，请保留仍被其他 Part 引用的公共文件。本目录不是随某一个 Part 自动删除的私有文件夹。

## CZ 家族 v3.4.1 接入

将 CZ 家族的全部 15 张 `Shared_*` 接入公共库：国旗、CASC、白底复用已有文件，其余 12 张加入同目录。当前共有 16 张贴图，包含另一任务独有的“中国航天”字形图；文件名、像素、压缩格式和分辨率均保持各自来源。

新增项包括 LM、LONGMARCH、CMS、CNSA、六甲标识、编号、补充字形、平台标语、棋盘调色板，以及三张共用法线。具体消费者、来源与 SHA-256 见 `PROVENANCE.CZFamily.v3.4.1.json`。CZ 家族 MU 旁只有 4×4 `Ref_*.v3.4.1.png` 加载占位图，`MODEL/texture` 绑定到本目录的真实资源。

CZ 家族安装器只检查公共资源，完全不复制、移动或删除本目录内容。请先手动合并新包 `GameData/KIU_Common_texture`；同名同哈希可直接复用，同名不同哈希应核对版本后由你决定，脚本会拒绝继续。回退 CZ 家族保留公共目录及其中的用户修改。已有 3.35 米上面级发布包保持不变。

CZ 家族 v3.4.2 已合入单材质模型修复，继续复用同样的 15 张公共图及现有索引；没有新增图片或改变内容。公共映射仍需在合并包上完成原生 KSP 验证。

## CZ 家族 v3.5.0 扩展

长二F、长三、长六及引力一号复用已有国旗、CMS、CASC、LM、白底和平坦法线，批准的2048²图标保持原字节。新增 `Shared_CZ6Blue.v3.5.0.dds`：4×4均匀蓝色色块，BC1解码为RGBA=(49,81,132,255)，来源及量化差异见catalog。当前索引共17张图片，已有其他任务资源保持。

v3.5.0统一使用本页开头的新运行路径 `GameData/KIU/Common/KIU_Common_texture`。前述v3.4.1历史段落的旧路径仅对应当时的冻结包。安装与回退仍由用户维护公共目录，脚本只做校验。本次扩展尚未进行KSP原生验证。

## Finish 高光控制图

`Shared_MatteSpec.v1.0.0.png` 是 4×4、RGBA 四通道全零的控制数据，仅供 B9 Matte Finish 替换 Mapped Specular 材质的 `_SpecMap`。它没有图案，不用于 `_MainTex`，也不改变主色图透明度。CZ-8A 燃料箱／支架及 YF-20／YF-21 共用该文件；切回 Default 恢复原高光图。新增索引项 `MatteSpec` 记录其路径、哈希和消费者，原有公共图片不变。

## 2026-09-25 旧 CASC 退役

按用户明确要求，从当前公共库移除 `Shared_CASC.v3.3.1.dds`，正式 CFG 引用已迁移到 `Shared_CASC_Blue.v3.5.3.dds`。旧文件的哈希核对备份位于 `local_workspace/CZ-335_Cryogenic/surface-fix-20260925/audit/retired-public`；上文初始化记录和 PROVENANCE 是历史来源，不代表旧文件仍在运行库。Classic / Teal 等独立图样保留，历史发布包不改写。新文字继续复用 `Shared_ChinaSpaceBold.v3.5.4.dds`。
