# TQ-12A V3 Official Replacement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the original TQ-12A nine-engine cluster with the V3 continuous-burning implementation under the original PART name, with no legacy craft compatibility bridge.

**Architecture:** A single V3 PART definition becomes `KCLV_TQ-12A_Series_Engine_Cluster`; its RF and Waterfall patches target that name. All old original and V2 runtime definitions are removed after creating a non-loadable external archive. The V2 model asset remains because the V3 PART references it.

**Tech Stack:** KSP 1.12.3 ConfigNode, ModuleManager, RealFuels, Waterfall, C# PartModule, PowerShell tests.

## Global Constraints

- Do not modify or remove `TQ-12A_Engine_Cluster_V2.mu`.
- Do not retain old craft or in-flight save compatibility aliases.
- Preserve native `thrustPercentage` / thrust limiter behavior.
- Do not deploy to TestRun automatically.
- Archive `.cfg` files outside the GameData tree so KSP cannot load them.

---

### Task 1: Write and run promotion assertions

**Files:**
- Modify: `tests/WaterfallV3Config.Tests.ps1`
- Delete: `tests/WaterfallConfig.Tests.ps1`

- [ ] Add assertions for exactly one original PART name, V3 RF/Waterfall targeting it, absence of V2/original runtime files, and retained V2 model asset.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File tests/WaterfallV3Config.Tests.ps1` and confirm it fails before migration.

### Task 2: Create a non-loadable legacy archive

**Files:**
- Create: `../archive/tq12a-pre-v3-<timestamp>.zip`
- Create: `../archive/tq12a-pre-v3-<timestamp>-manifest.txt`

- [ ] Copy original/V2 part cfg and model files, Waterfall cfg files, both legacy switcher source, and the pre-edit `TQ_series.cfg` into the archive.
- [ ] Record source path, byte length, and SHA-256 for every archived file.
- [ ] Verify no archive source path is under `KIU_Chinese_Launch_Vehicle_pack` and no archived `.cfg` remains loadable.

### Task 3: Promote V3 and remove legacy runtime definitions

**Files:**
- Rename: `KIU_Chinese_Launch_Vehicle_pack/Parts/Engines/TQ-12A_Series/TQ-12A_Engine_Cluster_V3.cfg` to `.../TQ-12A_Series_Engine_Cluster.cfg`
- Rename: `KIU_Chinese_Launch_Vehicle_pack/Compatibility/RealFuels/TQ-12A_Cluster_V3.cfg` to `.../TQ-12A_Cluster.cfg`
- Replace: `KIU_Chinese_Launch_Vehicle_pack/Compatibility/Waterfall/TQ-12A_Cluster.cfg`
- Delete: `KIU_Chinese_Launch_Vehicle_pack/Parts/Engines/TQ-12A_Series/TQ-12A_Engine_Cluster_V2.cfg`
- Delete: `KIU_Chinese_Launch_Vehicle_pack/Parts/Engines/TQ-12A_Series/TQ-12A_Engine_Cluster_V2.mu` is explicitly prohibited; retain it.
- Delete: `KIU_Chinese_Launch_Vehicle_pack/Parts/Engines/TQ-12A_Series/TQ-12A_Series_Engine_Cluster.mu`
- Delete: `KIU_Chinese_Launch_Vehicle_pack/Compatibility/Waterfall/TQ-12A_Cluster_V2.cfg`
- Delete: `CSScripts/ModuleTQEngineModeSwitch.cs`
- Modify: `KIU_Chinese_Launch_Vehicle_pack/Compatibility/RealFuels/TQ_series.cfg`

- [ ] Change V3 `name` and both V3 `@PART[...]` selectors to `KCLV_TQ-12A_Series_Engine_Cluster`.
- [ ] Keep `TQ12A_V3`, `ModuleTQEngineModeSwitch_v3`, the four V3 Waterfall module IDs, and all 12 RF configurations unchanged.
- [ ] Remove the original and V2 nine-engine RF patch nodes from `TQ_series.cfg`, without changing its unrelated engine nodes.
- [ ] Ensure the only retained V2 file is the model asset required by the V3 `MODEL` path.

### Task 4: Verify configuration and compile the plugin

**Files:**
- Modify: `tests/WaterfallV3Config.Tests.ps1`
- Build: `Plugins/KIU.dll`

- [ ] Run the promotion test and confirm it passes.
- [ ] Run brace-balance and `rg` checks for duplicate original PART definitions or obsolete V2 selectors.
- [ ] Build with TestRun managed-assembly references, restore `KIU.csproj` to its original reference paths, and confirm a zero-warning, zero-error build.
- [ ] Do not copy build output into TestRun.
