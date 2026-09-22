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
