# Xuntian (CSST) KCDE Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Progress (2026-09-17):** Tasks 1-9 are done and the automated half of Task 10
> is done. `tests/Xuntian.Tests.ps1` passes and all six pre-existing suites still
> pass. The `tests/` directory is gitignored by design, so none of them are in the
> repository (see Task 10). Remaining: the in-game verification items of Task 10, which the project
> owner has waived for this release, plus the resolved decisions recorded below.
>
> Decisions taken since drafting:
> - RP-1 science rebalancing is out of scope.
> - The nine unreferenced textures were a packaging mistake and were deleted (22 MB).
> - The hull marking switch is **withdrawn**: the B9PartSwitch configuration was
>   removed and the pack ships the marked hull as authored. Tier B was never real.
> - Part display names drop the `XT-01`…`XT-05` numbering the source configs used,
>   following the pack convention `<program> <component>`; `ServiceBus` became
>   `Service Module` in both the display name and the part key, matching KCHS's
>   `天和服务舱 → Tianhe Service Module`.
> - Every science result was rewritten from a sky-survey point of view: the body
>   is foreground or backdrop, the observation is of the sky.
> - No craft file ships.
> - The telescope declares `vesselType = Ship`.
> - The observatory's real dry mass is 15.5 t, split 9.0 t bus / 6.5 t telescope,
>   with all propellant and batteries on the bus and 7500 EC.
> - The aperture cover is manual only: `FxModules = 0` was removed, so the cover
>   no longer follows the experiment (see "Resolved decisions").
> - Chinese names for the stock bodies follow the project owner's glossary; Mun was
>   the only name the survey text had wrong.

**Goal:** Add the Xuntian space telescope (5 parts + 1 custom science experiment) to the KCDE pack, fully localized in `en-us` and `zh-cn`, with science results covering both the stock Kerbol system and the real-solar-system bodies shared by RSS and Sol, plus compatibility layers for RO, RealFuels, RP-1, RemoteTech, TweakScale, VABO, Waterfall and RealAntennas.

**Architecture:** Five standalone parts land under `Parts/Xuntian/<Subsystem>/`, named `KCDE_XT_*` so the pack's existing `KCDE*` wildcard patches (category tag, RP-1 `RP0conf`, real-scale patch) cover them with no changes. All user-visible text moves out of the part configs into the pack's `Localization/{en_us,zh-cn}.cfg` behind `#KCDE_XT_*` keys. The custom experiment ships as a single `EXPERIMENT_DEFINITION` whose `RESULTS` contain both the Kerbol and the real-solar result-key sets; every result value is a `#KCDE_XT_RESULT_*` localization key. Compatibility patches stay one-file-per-module under `Compatibility/`, except RP-1 which must be appended into the five existing `KCDE_RP1*.cfg` files.

**Tech Stack:** KSP 1.12.3 ConfigNode, ModuleManager, RealFuels, RealismOverhaul, RP-1, RemoteTech, RealAntennas, TweakScale, VAB Organizer, Waterfall, PowerShell (Pester-style) tests.

---

## Source material

Supplied under `user_input/xuntian/`:

- `Assets/*.mu` — `Telescope`, `ServiceBus`, `SolarWing`, `Antenna`, `DockingPort`
- `Assets/*.dds` — 29 textures; **20 are referenced, 9 are orphans** (see Task 1)
- `Parts/*.cfg` — 5 part definitions (author: *Xuntian project*), authored at `rescaleFactor = 1` with real-metre node coordinates
- `Parts/ScienceDefs.cfg` — `EXPERIMENT_DEFINITION id = XTDeepSkySurvey`

Verified asset facts (do not re-derive):

| Model | Referenced textures |
|---|---|
| `Telescope.mu` | `ao_telescope`, `ao_telescope_marked`, `optical_black`, `optical_glass`, `side_glass` |
| `ServiceBus.mu` | `ao_servicebus`, `paint` |
| `SolarWing.mu` | `ao_solarwing`, `paint`, `solar_{front,back}_{030,032,036,091,991}` |
| `Antenna.mu` | `ao_antenna` |
| `DockingPort.mu` | `ao_dockingport` |

`Telescope.mu` defines 9 materials (`XT_gold_AO_Telescope`, `XT_paint_AO_Telescope`, `XT_dark_AO_Telescope`, `XT_foil_AO_Telescope`, `XT_metal_AO_Telescope`, `XT_titanium_AO_Telescope`, `XT_hull_marked_AO_Dedicated`, plus two glass materials). Only `XT_hull_marked_AO_Dedicated` uses `ao_telescope_marked`; the rest share `ao_telescope`. This split is what makes a future "marked / unmarked" part variant cheap — repoint one material only.

---

## Global Constraints

- Parts **must** be named `KCDE_XT_*`. Never `XT*`.
- `MODEL` paths **must** be `KIU/KIU_Chinese_Deepspace_Exploration_pack/Parts/Xuntian/<Subsystem>/<Name>` — never `Xuntian/Assets/...`.
- No user-visible string may remain inline in a part config. Every `title`, `description`, `tags`, animation GUI name, and science result is a `#KEY` resolved from the pack's `Localization/` files.
- `RP0conf` is owned by the RP-1 layer, not the RO layer. The existing `KCDE_RP1Patch.cfg` wildcard `@PART[KCDE_*]` already stamps `KCDE_XT_*` before `zzzRP-0` runs, so no change is needed there. `Compatibility/RO/Xuntian.cfg` sets `%RSSROConfig = True` only, matching every other file in `Compatibility/RO/`. (Parts lacking `RP0conf` are deleted outright by RP-1's `HardRemoveNonRP0`, which is why the RP-1 layer must keep covering the new names.)
- RP-1 content must be **appended into the five existing** `Compatibility/RP-1/KCDE_RP1*.cfg` files. Creating an additional `*RP1Avionics.cfg` breaks `tests/RP1Compatibility.Tests.ps1`, which asserts exactly three such files.
- Every exact part selector in an RP-1 patch must resolve to a defined part; the same test enforces this.
- Delete only the nine orphan textures listed in Task 1. **Never delete `ao_telescope_marked.dds`** — `Telescope.mu` references it.
- Localization goes only in `KIU_Chinese_Deepspace_Exploration_pack/Localization/{en_us,zh-cn}.cfg`; shared strings (`#CASC`, `#CNSA`) already live in `Common/Localization/`.
- Do not deploy to `TestRun` automatically.

---

## Science result key naming — verified background

Read this before touching Task 4.

`EXPERIMENT_DEFINITION` localizes both `title` and every `RESULTS` value; stock uses `#autoLOC_*`, RSS uses `#RSS_Science_*`, Sol uses `#Sol_Science_*`. The same mechanism accepts mod keys such as `#KCDE_XT_RESULT_*`.

The `RESULTS` **keys** cannot be namespaced — KSP resolves them at runtime as `<body><situation>[<biome>]` using the celestial body's runtime name. The environment therefore decides the key set:

| Environment | Home world runtime name | Key namespace |
|---|---|---|
| Stock Kerbol | `Kerbin` | `Kerbin*`, `Mun*`, `Minmus*`, `Duna*`, … |
| RSS | `Earth` — `RSSKopernicus/Earth/Earth.cfg` sets `name = Kerbin` with `cbNameLater = Earth` | `Earth*`, `Moon*`, `Mars*`, … |
| Sol | `Earth` — `Sol-Configs/.../Earth-Kopernicus.cfg` sets `name = Earth` with `Template { name = Kerbin }` | **identical to RSS** |

RSS's own `ScienceDefs.cfg` contains **zero** `Kerbin`-prefixed keys and Sol's contains none either, which confirms the runtime rename. Because RSS and Sol share one namespace, the pack needs exactly **two** result-key sets: Kerbol and real-solar.

Authoritative key shape: stock's `magnetometer` is the only stock experiment with `situationMask = 48` / `biomeMask = 0` — the same space-only configuration as the telescope. It uses `<Body>InSpaceLow` and `<Body>InSpaceHigh` plus a single `default`, and no bare `<Body>InSpace` fallback. Follow that shape exactly.

RSS patches stock experiments by explicit `id` (`crewReport`, `evaReport`, …) and Sol does the same, so neither touches a custom experiment id. RP-1 contains no `EXPERIMENT_DEFINITION` patches at all. The two key sets must therefore be supplied entirely by this pack.

---

## Task 1: Stage assets and remove orphan textures

**Files:**
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Parts/Xuntian/{Telescope,ServiceModule,SolarWing,Antenna,DockingPort}/`
- Delete from the staged set: the nine orphan `.dds` files

- [ ] Create the five subsystem directories under `Parts/Xuntian/`.
- [ ] Copy each `.mu` into its subsystem directory. Keep the asset basenames unchanged so the embedded texture references still resolve.
- [ ] Copy the 20 referenced textures into the directories that need them. `paint.dds` is shared by `ServiceBus` and `SolarWing` — copy it to both.
- [ ] Do **not** copy these nine orphans: `dark.dds`, `foil.dds`, `gold.dds`, `hull_marked.dds`, `marking.dds`, `metal.dds`, `solar.dds`, `solar_back.dds`, `titanium.dds` (22.0 MB total).
- [ ] Confirm `ao_telescope_marked.dds` (42.7 MB) **is** copied into `Parts/Xuntian/Telescope/`.
- [x] All nine orphans were deleted from `user_input/xuntian/Assets/` after re-confirming zero references in every `.mu` (22 MB recovered). They were an asset-authoring leftover, not a colour-variant palette, so nothing depends on them.

### Material to texture mapping (verified, exact 1:1 per model)

| Model | Material | Texture |
|---|---|---|
| Telescope | `XT_optical_black` | `optical_black.dds` |
| | `XT_optical_glass` | `optical_glass.dds` |
| | `XT_side_glass` | `side_glass.dds` |
| | `XT_{gold,paint,dark,foil,metal,titanium}_AO_Telescope` | `ao_telescope.dds` |
| | `XT_hull_marked_AO_Dedicated` | `ao_telescope_marked.dds` |
| ServiceBus | `XT_{foil,paint,metal,gold,titanium,gold_foil}_AO_ServiceBus` | `ao_servicebus.dds` |
| | `XT_paint` | `paint.dds` |
| SolarWing | `XT_{paint,metal,gold}_AO_SolarWing` | `ao_solarwing.dds` |
| | `XT_paint` | `paint.dds` |
| | `XT_solar_{030,032,036,091,991}` | `solar_front_{...}.dds` |
| | `XT_solar_back_{030,032,036,091,991}` | `solar_back_{...}.dds` |
| Antenna | `XT_{paint,gold}_AO_Antenna` | `ao_antenna.dds` |
| DockingPort | `XT_{metal,titanium,paint}_AO_DockingPort` | `ao_dockingport.dds` |

## Task 2: Port the five part definitions

**Files:**
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Parts/Xuntian/Telescope/KCDE_XT_Telescope.cfg`
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Parts/Xuntian/ServiceBus/KCDE_XT_ServiceModule.cfg`
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Parts/Xuntian/SolarWing/KCDE_XT_SolarWing.cfg`
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Parts/Xuntian/Antenna/KCDE_XT_Antenna.cfg`
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Parts/Xuntian/DockingPort/KCDE_XT_DockingPort.cfg`

- [ ] Rename each part: `XTTelescope` → `KCDE_XT_Telescope`, `XTServiceBus` → `KCDE_XT_ServiceModule`, `XTSolarWing` → `KCDE_XT_SolarWing`, `XTAntenna` → `KCDE_XT_Antenna`, `XTDockingPort` → `KCDE_XT_DockingPort`.
- [ ] Rewrite every `MODEL { model = ... }` to the `KIU/KIU_Chinese_Deepspace_Exploration_pack/Parts/Xuntian/...` path.
- [ ] Set `author = KIU` and `manufacturer = #CNSA`, matching the other KCDE probe parts.
- [ ] Replace `title`, `description` and `tags` with `#KCDE_XT_<Part>_{title,description,tags}` keys. Tags must keep the `xuntian csst telescope observatory satellite 巡天 望远镜` search terms plus the `KCDE` token the category patch appends.
- [ ] Replace every animation GUI string with a key (Task 3 lists them).
- [ ] Review `bulkheadProfiles` per part instead of copying `size3, srf` uniformly: `KCDE_XT_DockingPort` declares `nodeType = size1` and `KCDE_XT_Antenna` is a 0.08 t mount, so both should be `size1, srf`-class profiles. Leave the bus and telescope on `size3`.
- [ ] Set `vesselType = Ship` on `KCDE_XT_Telescope` (confirmed by the project owner).
- [ ] Keep `rescaleFactor = 1`; the pack's `Common/Extra/KCDE_RealScalePatch.cfg` also drives its covered parts to 1, so the effective scale stays consistent across the pack.
- [ ] Verify each part keeps its functional modules intact: `ModuleAnimateGeneric` (cover), `ModuleScienceExperiment`, `ModuleRCSFX`, `ModuleDockingNode`, `ModuleDeployableSolarPanel`, `ModuleCargoPart`, `ModuleCommand`, `ModuleSAS`, `ModuleReactionWheel`, `ModuleDataTransmitter`.

## Task 3: Localize all part and UI strings

**Files:**
- Modify: `KIU_Chinese_Deepspace_Exploration_pack/Localization/en_us.cfg`
- Modify: `KIU_Chinese_Deepspace_Exploration_pack/Localization/zh-cn.cfg`

- [ ] Add a `// ------------------Xuntian 巡天--------------------------` section to both files, following the existing Tianwen/Chang'e section style.
- [ ] Add 15 part-text keys (5 parts × title/description/tags):

  `#KCDE_XT_Telescope_title|_description|_tags`, `#KCDE_XT_ServiceModule_*`, `#KCDE_XT_SolarWing_*`, `#KCDE_XT_Antenna_*`, `#KCDE_XT_DockingPort_*`

- [ ] Add 13 UI keys for the animation and experiment buttons, replacing the inline bilingual strings:

  | Key | Source string |
  |---|---|
  | `#KCDE_XT_Cover_Open` / `_Close` / `_Toggle` | 打开镜盖 / 关闭镜盖 / 切换镜盖 |
  | `#KCDE_XT_Dock_Extend` / `_Retract` / `_Toggle` | 伸出对接环 / 收回对接环 / 切换对接环 |
  | `#KCDE_XT_Solar_Extend` / `_Retract` / `_Toggle` | 展开太阳翼 / 收拢太阳翼 / 切换太阳翼 |
  | `#KCDE_XT_Survey_Start` / `_Reset` / `_Review` / `_Collect` | 巡天观测 / 重置观测 / 查看观测数据 / 收集数据 |

- [ ] Author real English and Chinese text for every key — no machine-literal transliteration of the stock phrasing.
- [ ] Confirm no `#KEY` used anywhere in `Parts/Xuntian/` is missing from either language file.

## Task 4: Ship the science experiment for Kerbol, RSS and Sol

**Files:**
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Science/KCDE_XT_Science.cfg`
- Modify: `KIU_Chinese_Deepspace_Exploration_pack/Localization/{en_us,zh-cn}.cfg`
- Modify: `Parts/Xuntian/Telescope/KCDE_XT_Telescope.cfg`

- [ ] Define the experiment with `id = KCDE_XT_DeepSkySurvey` (renamed from `XTDeepSkySurvey` for pack namespace consistency) and point `ModuleScienceExperiment.experimentID` at it.
- [ ] Keep the author's `baseValue = 30`, `scienceCap = 40`, `dataScale = 1`, `requireAtmosphere = False`, `situationMask = 48`, `biomeMask = 0`, and add `requireNoAtmosphere = False` for parity with the stock `magnetometer` reference.
- [ ] Set `title = #KCDE_XT_DeepSkySurvey_title`.
- [ ] Set `RESULTS` to `default = #KCDE_XT_RESULT_default` plus the two key sets below. Both sets live in the one definition; keys for bodies that do not exist in the running system are inert.

  **Kerbol set** — `Sun`, `Kerbin`, `Mun`, `Minmus`, `Moho`, `Eve`, `Gilly`, `Duna`, `Ike`, `Dres`, `Jool`, `Laythe`, `Vall`, `Tylo`, `Bop`, `Pol`, `Eeloo`

  **Real-solar set (serves both RSS and Sol)** — `Sun`, `Mercury`, `Venus`, `Earth`, `Moon`, `Mars`, `Phobos`, `Deimos`, `Ceres`, `Vesta`, `Jupiter`, `Io`, `Europa`, `Ganymede`, `Callisto`, `Saturn`, `Titan`, `Uranus`, `Neptune`, `Pluto`

- [ ] For each body emit two keys, `<Body>InSpaceLow` and `<Body>InSpaceHigh`, each valued `#KCDE_XT_RESULT_<Body>InSpaceLow|High`. `Sun` is shared by both sets — write it once.
- [ ] Add all 75 result keys (`default` + 74 body keys) to **both** `en_us.cfg` and `zh-cn.cfg`.
- [x] Every result is written from a **sky-survey** point of view, per the project owner: the body is the foreground or the backdrop, and what the telescope reports is the sky it is looking at — how the body's limb, glow, shadow or silhouette helps or hinders the deep field — never a description of the body's own surface or atmosphere. `InSpaceLow` and `InSpaceHigh` differ in what the vantage point does to the sky, not in how closely the body is examined. Terms of art in use: zodiacal dust, galactic plane, scattered light, occultation edge, albedo.
- [ ] Do not gate the file on `:NEEDS[…]`. A custom experiment id is untouched by RSS, Sol and RP-1, so one ungated definition serves every environment.

## Task 5: Common patches

**Files:**
- Modify: `Common/Extra/KCDE_RealScalePatch.cfg`
- Modify: `Common/RealAntennas_Community_Patch.cfg`

- [ ] Add `@PART[KCDE_XT*]:BEFORE[Waterfall] { %rescaleFactor = 1 }` for symmetry with the existing Tianwen and Chang'e entries.
- [ ] Add a `ModuleRealAntenna` entry for `KCDE_XT_Antenna` under `:HAS[!MODULE[ModuleRTAntenna]]:NEEDS[RealAntennas]`, sized to the real 2 m CSST aperture.

## Task 6: RealismOverhaul and RealFuels

**Files:**
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Compatibility/RO/Xuntian.cfg`
- Create: `KIU_Chinese_Deepspace_Exploration_pack/Compatibility/RealFuels/Xuntian.cfg`

- [ ] RO patch: `@PART[KCDE_XT_*]:NEEDS[RealismOverhaul]` setting **both** `%RSSROConfig = True` and `%RP0conf = True`.
- [ ] RealFuels patch, `:NEEDS[RealFuels]:FINAL`, following the existing Tianwen-1 pattern:
  - `KCDE_XT_ServiceModule` — `@mass` trimmed, `!RESOURCE,*{}`, `ModuleFuelTanks` of `type = ServiceModule` with `MMH` / `NTO` tanks and an `ElectricCharge` tank sized from the supplied 4000 EC.
  - RCS modules on the bus and telescope — `ModuleEngineConfigs` of `type = ModuleRCS,*` with `MMH/NTO` and `Hydrazine` configs.
  - Non-tank parts — `@mass` values reviewed against the real masses noted in the part descriptions.
- [ ] Confirm the patch behaves in the Sol environment, which ships classic RealFuels (MMH/NTO present) but **no** RealismOverhaul and **no** RP-1. Nothing in this task may depend on an RO-only resource.

## Task 7: RP-1 compatibility

**Files:**
- Modify (append): `Compatibility/RP-1/KCDE_RP1Avionics.cfg`
- Modify (append): `Compatibility/RP-1/KCDE_RP1Tags.cfg`
- Modify (append): `Compatibility/RP-1/KCDE_RP1TechMapping.cfg`
- Modify (append): `Compatibility/RP-1/KCDE_RP1Tooling.cfg`
- No change needed: `KCDE_RP1Patch.cfg` — its `@PART[KCDE_*]` wildcard already stamps `RP0conf` on the new parts

- [ ] Avionics: add a `ModuleAvionics` block for `KCDE_XT_ServiceModule` sized to the whole station stack (~15 t with the telescope mounted), guarded with `:NEEDS[RP-0]:BEFORE[RP-0]`.
- [ ] Tags: classify the telescope under `Instruments`, the bus under `Command` and `Power`, the solar wing under `Power`, the antenna under `Instruments`.
- [ ] Tech mapping: map each part's stock `TechRequired` to the RP-1 node matching the CSST programme timeline (2020s) rather than to the stock node name.
- [ ] Tooling: add `ModuleToolingDiamLen` entries for the bus and telescope barrel using their real diameters.
- [ ] Consider extending the RP-1 avionics section only — do **not** create a new file (see Global Constraints).

## Task 8: Remaining compatibility layers

**Files:**
- Create: `Compatibility/RemoteTech/Xuntian.cfg`
- Create: `Compatibility/TweakScale/Xuntian.cfg`
- Create: `Compatibility/Waterfall/Xuntian.cfg`
- Modify: `Compatibility/VABO/Subcategory_tweak.cfg`

- [ ] RemoteTech: `ModuleRTAntenna` on `KCDE_XT_Antenna` plus `ModuleSPU` on the bus, mirroring the Tianwen-1 file; use the dish range implied by the supplied `antennaPower = 2000000000`.
- [ ] TweakScale: two `defaultScale` values per part — one for `:NEEDS[TweakScale,RealFuels]`, one for `:NEEDS[TweakScale,!RealFuels]` — following the KCDE convention.
- [ ] Waterfall: RCS plumes for the bus and telescope using `Common/WaterfallTemplate/RCSTemplate.cfg`; the stock `MODEL_MULTI_PARTICLE` fallback in the part configs already covers installs without Waterfall.
- [ ] VABO: append the new parts to `Subcategory_tweak.cfg` with an appropriate `organizerSubcategory`.

## Task 9: Release metadata

**Files:**
- Modify: `KIU_Chinese_Deepspace_Exploration_pack/Readme.txt`
- Modify (root) `Changelog.txt`

No craft file ships with this release — decided by the project owner. A `.craft`
is an editor-generated artefact: each part carries a full serialised module state
(41 modules for five parts in the Chang'E-2 craft) plus attachment transforms that
KSP does not recompute on load. Producing one by hand without an in-game editor
session would risk shipping a vessel that will not load.

- [x] Bump the KCDE version line in `Readme.txt` (V1.0.6 → V1.1.0) and add the Xuntian entry.
- [x] Add the English and Chinese `V_1.2.4` changelog entries to `Changelog.txt`.
- [ ] Add the Xuntian asset author to the `Author:` line in `Readme.txt` if a credit is owed — the supplied part configs named only "Xuntian project".
- [ ] Optional: add `KIU_Chinese_Deepspace_Exploration_pack/KCDE.version`. The two existing packs do have one, but both are stale (`KCHS.version` reports 1.0.0.0 while its readme says 1.0.10), so adding a third would need theirs fixed too.

## Task 10: Tests and in-game verification

**Files:**
- Modify: `tests/RP1Compatibility.Tests.ps1` (only if new assertions are wanted) — not needed; it already guards the file count.
- Create: `tests/Xuntian.Tests.ps1`

> **The `tests/` directory is gitignored on purpose** (`.gitignore` carries it under a
> `# Tests folder` comment, next to `user_input/` and `archive/`). No suite is
> version-controlled, so this file exists only on the machine that wrote it, and a
> reader of this plan will not find it in the repository. The assertions below are the
> only record of what it checks.

- [x] Run every existing test and confirm no regression: 7/7 suites pass, with
  `powershell -NoProfile -File tests/<name>.Tests.ps1`. The `-ExecutionPolicy Bypass`
  flag this plan originally specified is not needed, and the permission classifier
  refuses it anyway.
- [x] Add a test asserting: 5 parts exist and are named `KCDE_XT_*`; no `MODEL` path contains `Xuntian/Assets`; no part config contains an inline non-ASCII string outside a comment; the experiment defines both `EarthInSpaceLow` and `KerbinInSpaceLow`; every `#KEY` referenced from `Parts/Xuntian/` and `Science/` resolves in both `en_us.cfg` and `zh-cn.cfg`. It also carries the inverse guards that keep two withdrawn or rejected designs from creeping back: no B9PartSwitch config or module, no `FxModules` binding the cover to the experiment, `animationName = OpenCover` still present, and `ao_telescope_marked.dds` still shipping.
- [ ] Verify in `TestRun` (stock-ish): all 5 parts compile without a `PartCompiler` error, appear under the KCDE category, the cover and solar animations play, and the sky-survey experiment runs in low and high space.
- [ ] Verify in an RO/RP-1 environment: parts survive `HardRemoveNonRP0`, appear in the RP-1 tech tree, and the avionics limits allow control of the full stack.
- [ ] Verify in the Sol environment (`D:\KSP\KSP_1.12.5\Full_Up_Build`): the experiment returns the `Earth*` / `Moon*` result text rather than `default`, and the RealFuels patch applies without RO present.
- [ ] Verify in RSS: the same real-solar result keys resolve.

---

## Resolved decisions

- **Aperture cover: manual only, and why it cannot be anything else.** The request was
  that running Sky Survey must open the cover while *resetting* the experiment must
  leave it alone. Both halves come from a single stock mechanism:
  - `FxModules = 0` on `ModuleScienceExperiment` is stock's deployable-science link —
    the index points at the part's first fx module, which is the cover's
    `ModuleAnimateGeneric`. Stock's own Goo canister, Science Jr and Magnetometer use
    the same field the same way, and all three carry a cover or doors animation.
  - `ModuleScienceExperiment` drives that animation on run *and* retracts it in
    `OnExperimentReset`. No config flag keeps one half and drops the other, so the two
    behaviours cannot be separated from a config file.
  - Splitting them needs a small `PartModule` (block the experiment event while
    `ModuleAnimateGeneric.animSwitch` reports the cover shut, and leave the animation
    alone on reset). New C# was ruled out for this release.
  - **Decision (project owner): remove `FxModules = 0`.** The cover now belongs solely
    to its `ModuleAnimateGeneric` and moves only when the player asks. Accepted cost:
    the survey no longer opens the cover for the player. `Xuntian.Tests.ps1` guards
    both halves — no `FxModules` anywhere in the part, and `animationName = OpenCover`
    still present. `hideFxModuleUI = False` is left in place; with no fx modules bound
    it is inert, and it records the intent that the cover button stays visible.
- **Chinese names for the stock bodies: settled against the project owner's glossary.**
  Kerbol 太阳 · Moho 莫霍 · Eve 伊芙 · Gilly 吉莉 · Kerbin 坎星 · Mun 坎月 ·
  Minmus 敏穆斯 · Duna 杜纳 · Ike 艾克 · Dres 德雷斯 · Jool 朱尔 · Laythe 莱斯 ·
  Vall 瓦尔 · Tylo 泰洛 · Bop 波普 · Pol 波尔 · Eeloo 伊鲁.
  The survey text already matched every name except Mun, which was written 月面 (the
  real Moon's word) and is now 坎月 in both of its entries — in Chinese and English
  alike, which also removes the only place where a Kerbol and a real-solar result read
  as the same sentence. 莫霍 and 伊鲁 do not appear in the text: their entries describe
  the body without naming it, in both languages. The real-solar set keeps KIU's
  existing 地球 / 火星 / 金星 naming.


