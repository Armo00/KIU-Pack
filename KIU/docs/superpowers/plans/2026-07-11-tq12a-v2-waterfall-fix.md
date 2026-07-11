# TQ-12A V2 Waterfall Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bind every TQ-12A V2 Waterfall plume to its matching engine mode without runtime reflection.

**Architecture:** Replace the dynamic single-module configuration with four static `ModuleWaterfallFX` modules. Each throttle controller explicitly selects one engine ID and each template has a fixed parent transform. The engine switcher no longer controls Waterfall internals.

**Tech Stack:** KSP ConfigNode, ModuleManager, Waterfall 0.10.5, C# net472, PowerShell.

## Global Constraints

- Preserve the four existing ModuleEnginesFX/ModuleEnginesRF IDs: Mode1, Mode3, Mode5, Mode9.
- Work with Waterfall 0.10.5 and RealFuels 15.12.0.
- Do not change the engine-mode activation semantics.

---

### Task 1: Add a static Waterfall configuration regression check

**Files:**
- Create: `tests/WaterfallConfig.Tests.ps1`
- Test: `tests/WaterfallConfig.Tests.ps1`

- [ ] **Step 1: Write the failing test**

Assert that the V2 config has four `ModuleWaterfallFX` modules and that each contains a throttle controller whose `engineID` and `overrideParentTransform` match Mode1/thrustTransform1, Mode3/thrustTransform3, Mode5/thrustTransform5, and Mode9/thrustTransform.

- [ ] **Step 2: Run test to verify it fails**

Run: `powershell -ExecutionPolicy Bypass -File tests/WaterfallConfig.Tests.ps1`

Expected: FAIL because the current config has one module and no throttle-controller engine ID.

- [ ] **Step 3: Implement the configuration change**

Replace the single dynamic module with four static modules and add explicit controller `engineID` values.

- [ ] **Step 4: Run test to verify it passes**

Run: `powershell -ExecutionPolicy Bypass -File tests/WaterfallConfig.Tests.ps1`

Expected: PASS.

### Task 2: Remove obsolete Waterfall reflection from the engine switcher

**Files:**
- Modify: `CSScripts/ModuleTQEngineModeSwitch.cs`
- Modify: `tests/WaterfallConfig.Tests.ps1`
- Test: `tests/WaterfallConfig.Tests.ps1`

- [ ] **Step 1: Extend the failing test**

Assert that `ModuleTQEngineModeSwitch.cs` does not reference `ModuleWaterfallFX`, `System.Reflection`, or `UpdateWaterfall`.

- [ ] **Step 2: Run test to verify it fails**

Run: `powershell -ExecutionPolicy Bypass -File tests/WaterfallConfig.Tests.ps1`

Expected: FAIL because the current switcher reflects into Waterfall.

- [ ] **Step 3: Implement the minimal C# change**

Remove the reflection import, both calls to `UpdateWaterfall`, and the `UpdateWaterfall` method. Leave engine-mode activation unchanged.

- [ ] **Step 4: Verify the final artifact**

Run: `powershell -ExecutionPolicy Bypass -File tests/WaterfallConfig.Tests.ps1; dotnet build KIU.csproj`

Expected: all regression assertions pass and the project builds successfully after temporarily pointing KSP references at the TestRun installation, then restoring the project file.
