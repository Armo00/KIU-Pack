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

CZ10BLaunchErector.dll and CZ10BLaunchErector.cs are retained in KIU.
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
