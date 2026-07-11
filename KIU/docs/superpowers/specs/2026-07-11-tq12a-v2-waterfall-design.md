# TQ-12A V2 Waterfall Design

## Goal

Render the plume corresponding to the active Mode1, Mode3, Mode5, or Mode9 engine, including when RealFuels replaces the stock engines with ModuleEnginesRF.

## Design

Use four static `ModuleWaterfallFX` modules. Each has a fixed plume transform and its throttle controller declares the matching `engineID`. Waterfall therefore caches the intended `ModuleEngines`-derived module during startup and naturally renders only the operational engine mode.

`ModuleTQEngineModeSwitch` remains responsible only for activating one engine mode. It must not reflect into Waterfall or toggle component enablement because those operations do not rebuild Waterfall controller caches.

## Validation

A PowerShell regression check verifies four static Waterfall modules, the one-to-one mode/transform/controller bindings, and the absence of the obsolete reflection path. A normal project build verifies the C# assembly.
