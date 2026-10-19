# KIU shared plugins

Authoritative sources: PusherAnimatedInterstage.cs and ConfigurableTank.cs.
Build with ../build-plugins.ps1, using -ManagedDirectory / -Roslyn as needed.
The manifest builds KIU.dll (existing engine/gimbal/Waterfall modules plus
PusherAnimatedInterstage) and the independent ConfigurableTank.dll.

Replace cfg MODULE name MyInterstage with PusherAnimatedInterstage. Keep the
stock ModuleAnimateGeneric and animation name. Staging requests extension only,
even if manually extended already. The helper uses the first animation module;
it does not apply forces or listen to arbitrary decouple events.

ConfigurableTank retains its cfg module name. Do not also load an older
CZ10BModules.dll containing ConfigurableTank. CZ10B must use the split version.
These two shared capabilities must not be bundled again with CZ10B.

## ConfigurableTank + TweakScale (2026-09-28)

Ship `Plugins/ConfigurableTank_TweakScale.cfg` together with the DLL. The patch
applies only to Parts already containing both modules; it does not add scaling to
every tank or alter their original balances. CZ-8A enables its module in its own
`Compatibility/Tweakscale` directory.

ConfigurableTank owns segment count and the combined capacity, dry mass and full
capacity cost. TweakScale owns uniform model scale. A ratio `s` multiplies length
and diameter by `s`, and tank volume/mass/cost by `s^3`. It scales the whole tank,
including each extension's length; this is not diameter-only resizing. Authored
MODEL/rescaleFactor is kept separate from `s`. Existing stock/RF cfg quantities
are already balanced for that authored scale and are not multiplied twice.

Both branches send `OnPartScaleChanged`, but at different points relative to RF.
Reconciliation runs after their handlers, preserves filling fractions, restores
extension nodes, refreshes B9 and renderer caches, and updates RP-1 dimensions.
After deferred startup it also notifies the editor so the displayed vessel cost
agrees with the restored dimensions/resources after loading a saved craft.
The local TweakScale mass/cost exponents must remain zero to avoid a second
multiplier. Original /L's separate resource-cost contribution is compensated.

Test targets are TweakScale /L 2.4.8.9 and TweakScale Rescaled release 3.3.2.1
(its published DLL still identifies itself as 3.3.2.0). Install one distribution
at a time, with its own dependencies.

Original /L rejects **all** B9 + ModuleFuelTanks combinations before the editor,
even when B9 only changes paint. For ConfigurableTank + RF + visual B9, install
[Harmony 2 for KSP](https://github.com/KSPModdingLibs/HarmonyKSP) in
`GameData/000_Harmony`. The optional adapter in ConfigurableTank.dll uses it to
accept only material/visibility-only B9 configs. Fuel, volume, mass, cost, node
or nested module changes keep the original rejection. Other Parts and other
sanity failures are untouched. No TweakScale/Recall/Harmony binaries are bundled
in KIU, and the tank DLL has no hard assembly reference to these mods. Without
Harmony, ordinary tank operation still loads, but /L's RF+B9 restriction remains.
Rescaled does not require this extra adapter path.

Native test scripts, exact dependency hashes, operation order tests, attachment,
symmetry and persistence evidence: `local_workspace/ConfigurableTank/20260928-tweakscale/`.

CZ10BLaunchErector.cs is maintained in TowerControl.dll; the former standalone DLL is retired.
Grid-fin/fairing custom bridges and the editor node helper are retired.
Future edits and new shared DLLs are maintained and built here, not in CZ10B.

## CZ-12B integration (2026-09-23)
CZ12BModules.dll, its two sources and standalone builder are retired from active KIU.
CZ-12B uses ConfigurableTank (originFraction=0.5, scaleWithModel=true,
realFuelsVolumeIsUsable=true, configurable localized labels), plus
PusherAnimatedInterstage (triggerOnDecouple=true, decouplerNodeID=top).
Defaults retain existing bottom-origin / unscaled / geometric-volume tank semantics
and staging-only pusher behavior for existing consumers.
Grid fin uses stock ModuleAnimateGeneric + ModuleControlSurface; fairing shielding
uses stock connected ModuleCargoBay and ModuleJettison state transforms. No DAS
module or custom CZ12B PartModule is required. No KSP run performed for this change.

## Procedural Fairings decals (2026-09-26)
ModuleProceduralFairingDecal.cs is included in KIU.dll. There is no CZ10-specific DLL.
The optional module reads PF 6.8 public shape fields through reflection and clips
markings to the actual outer mesh. It parents render-only meshes to PF's model
transform, so PF owns editor opening, hinges, staging, colliders and shielding.
No hard reference to PF is added to KIU.dll. Each configured marking needs a unique
decalID, textureURL and textureRect; width/height are physical dimensions at
referenceDiameter. Shared groupWidth/groupHeight/verticalOffset maintain a pair's
layout; barrelFraction sets the group's height. Extremely short barrels uniformly
shrink markings; zero-height barrels hide them. Surface offset is 1.5 mm by default.
Mesh/material instances are disposed on destruction. Original mesh/colliders and
GameDatabase textures are not modified. Tests are in local_workspace/CZ-10B/tests/
build/2026-09-26-pf-v070. Offline validation only; no KSP session was run.

## Linghangzhe recovery (2026-10-02)
KIUNetRecovery.dll is an independent production assembly built from NetRecovery/*.cs
through the shared build manifest. It contains LHZNetController, LHZReceiverProbe
and LHZHookProbe, with no test KSPAddon, filesystem command bridge or craft IDs.
Build only this assembly with -AssemblyNames KIUNetRecovery. Actual CZ-10B hooks
carry the persistent lhzCapture node and LHZHookProbe directly in their part.cfg.
Four tracked cable contacts create one native KSP Part connection. MODEL-only
24-degree hook/yield/rebound presentation preserves fixed physics Part poses.
Engines do not gate capture. Actual source-engine telemetry and automatic
shutdown after native capture remain; return guidance is not certified.


## Linghangzhe recovery 2.7 (2026-10-03)
The same assembly adds a stock ApplicationLauncher control window, removes
the water-only gate, supports >=4 installed hooks by selecting four distinct
reachable contacts, and retains sequential contacts for 1.5 s / 4 m of yield.
Defaults allow 20-degree tilt / 25 m/s with 1.25 m mouth tolerance, qualifying
15-degree / 20 m/s entries. A rail governor preserves at least 5.6 m aperture
per axis. Reset clears target/contact history and re-arms. Additional deployed
hook animations retain their identities across save/load. No KSPAddon or test
command/file bridge is included. Configuration includes native ship reaction
wheels and SAS. The local HTML report records actual native/offline results.

## Linghangzhe recovery 2.7.1 (2026-10-03)
Removes all engine capture gates and migrates obsolete rejection records.
Carriages use 48 m/s, 144 m/s² and doubled position response. Acquisition checks
current assigned cable reach instead of extrapolated center bounds; prediction
is bounded steering only. Waiting rails close to 5.6 m at authored scale 1,
while tracking follows actual mouths. The toolbar reports measured world-space
spacing rather than implying every opening is 5.6 m. Editable ship source 2.7.1
also corrects CASC UV/face direction and increases hull letter height to 1 m.
CaptureCamera.cs preserves the incoming pilot's camera across native coupling,
preventing the automatic merged-vessel switch from hiding the visual buffer.

## Shared dimension, tower and editor-node split (2026-10-14)

ConfigurableTank.dll owns variable model size, attachment movement, rendering
caches/highlighting, and size-dependent capacity/mass/cost reconciliation. It
contains reusable KIUModelRenderers and KIUTemplateExtensionPool helpers, plus
the existing optional TweakScale compatibility. It has no tower service,
staging, elevator or engine-node hooks.

TowerControl.dll references ConfigurableTank.dll. TowerControl owns tower UI,
independent service heights, bounds, followers and launch retraction. Existing
CZ10BLaunchErector keeps its module name, LaunchClamp inheritance, state fields
and release/animation sequence in this assembly; model construction uses the
shared extension pool. Do not also install CZ10BLaunchErector.dll.

CZ16 tower Parts compose ConfigurableTank with TowerControl. The generic module
provides a read-only load snapshot; TowerControl migrates old saved service
heights, step and retraction state once, then owns them. Dimension changes
update both Field and actual PAW slider bounds. The 3m service top margin stays.

GroupEngineNodes.dll contains KIUGroupedEngineNodes and its editor hooks,
independent of the other two assemblies. Parts opt in by module, and the host's
existing Harmony 2 is required to enable the hook. No Harmony/EEX/test binaries
are bundled. Never retain an old ConfigurableTank containing duplicate node
types alongside this assembly.

Build via plugin-build-manifest.json/build-plugins.ps1. Entries support either
the original source array or sources/references. Dependency assemblies build
first, including when selecting only TowerControl.

Scoped native evidence and validation limits remain under
local_workspace/CZ-16/working/feedback023/three_dll and the consolidated report.
No model archive, runtime commit or push is implied by this integration.

## Optional per-end tank profiles and extra nodes (2026-10-19)

ConfigurableTank still uses one shared `templateTransform` by default. Optional
`lowerTemplateTransform` and `upperTemplateTransform` each fall back independently
to that shared name when empty. This selects template types; initial extension
count remains controlled by `segments`. Existing CZ-10B configs need no changes.

`extraLowerNodes` / `extraUpperNodes` accept comma-separated existing AttachNode
IDs. Their position and originalPosition use the prefab zero baseline plus the
current end offset, including attached-child movement and parent anchoring.
Retain saved-craft node IDs; KSP splits underscores in `node_stack_*` names.

`useTerminalSurfaceVariants` optionally selects exactly one authored group per
clone: `surfaceSingle`, `surfaceFirst`, `surfaceMiddle`, or `surfaceLast` (names
are configurable). Lower-end physical ordering is reversed. Leave this disabled
for templates without those four groups.

Optional `bodyNormalTransform`, `baseNormalTextureURL` and
`extendedNormalTextureURL` restore the original body normal when the upper end
has no extensions and select the extended map otherwise. Both maps use raw
GameDatabase texture loading, preserving already AG-packed legacy normals.

These options use the existing ConfigurableTank.dll and shared build manifest.
The current RequestRendererRefresh API and shared highlight fixes are retained.
