# Linghangzhe / 领航者

Runtime: 2.7.2-dev. Editable model source remains 2.7.1-dev (source baseline 2.5.0-dev).
Install the complete KIU directory as GameData/KIU, including
Plugins/KIUNetRecovery.dll, the current CZ-10B RecoveryHook and Common textures.
No test bridge or additional demo Parts ship in KIU.

Open recovery control with the blue net icon in the stock KSP toolbar.
While controlling the core, this window can arm/disarm/reset the receiver,
toggle station keeping or ship SAS, and release a captured core. It reports
the target, installed hooks, contacts, relative motion, aperture and exact
last rejection. There is no automatic overlay on the core.

The receiver may operate on land, in flight or at sea: no water gate.
Station keeping applies at sea. The 1200 t ship has native SAS and an aggregate
reaction wheel configured for 300,000 kN m on each axis (equivalent to many
wheels), up to 100 ElectricCharge/s by configuration, and 100,000 stored EC.
Existing mods may alter stock resource behavior. This is game tuning.

Install at least four original-scale CZ-10B hooks around the core and fully
deploy at least four. The controller chooses four distinct reachable hooks
for the four cable roles; extra hooks no longer prevent tracking. Cardinal
and diagonal arrangements are supported. Extra deployed hooks follow the
24-degree retraction; folded extras stay folded.

Approach from above, allowing time for the carriages to reach the hooks.
Core origin range: 180 m; hook height: 0–140 m above the cables; relative
downward speed: >0.5 m/s; no time or physics warp. Reset clears target/contact
history and re-arms immediately. It does not move the core: after a missed
pass, raise the hooks above the cable plane before descending again.

| Capture setting | Default |
| --- | --- |
| Target entry qualification | 15 degrees, 20 m/s |
| Total hook-relative speed ceiling | 25 m/s |
| Hook/receiver tilt ceiling | 20 degrees |
| Transverse mouth tolerance | 1.25 m |
| First-to-fourth contact retention | 1.5 s |
| Contact yield stroke | 4 m |
| Aperture minimum per axis | 5.6 m |
| Rail bounds | +/-20 m; 48 m/s; nominal 144 m/s² |
| Acquisition bounds | Current cross-coordinate within +/-21.25 m; along-cable coordinate within +/-22 m |

At rescaleFactor=1, waiting rails close to 5.6 x 5.6 m. This is a minimum,
not a maximum: while tracking and after capture, cables meet the actual hook
mouths (about 10.9 m across for the reference deployed diagonal layout).
The window reports measured world-space spacing. A 5.6 m opening around this
unchanged layout would miss the mouths. Clearance and rail boundaries take
priority over individual carriage goals. Four actual gated contacts remain
required. Engine thrust, count, type and ignition state never reject capture;
source engines shut down 0.18 s after capture. Obsolete engine/prediction
rejections in loaded saves are cleared.

Acquisition no longer rejects targets because an extrapolated landing point
crossed the old +/-12.5 m center bound. It uses each current hook's assigned
finite cable span and carriage travel, including the 1.25 m mouth tolerance.
Prediction only steers carriages: lookahead <=1.5 s, lead <=6 m, rail-clamped.
Actual crossing, speed, tilt and retained contacts still decide capture.
Carriage speed is 48 m/s (previously 12), acceleration 144 m/s² (previously
24), with doubled position response. These are visual servo game settings.

Capture merges rigidbody momentum once and makes one native KSP coupling.
When the pilot controls the incoming core, capture preserves the camera target,
mode, heading, pitch and distance while KSP selects the merged vessel. A render
anchor follows the recovered core; it adds no Part, collider or force.
Earlier cable contacts yield visually while awaiting the other hooks.
After capture, native joints remain fixed while original hook/hydraulic
animations retract 24 degrees and model roots sink/rebound over 2.6 s.
Active/extra hook identities and buffer progress survive saves. Release uses
native Undock and restores models. This is visual compliance, not physical
rope or cable-tension simulation; it does not certify return guidance.

2.7.2 samples each hook's owned DeployHook clip directly and verifies the actual
hinge movement before advancing the buffer. The control window reports buffer
progress, actual retraction, sink and calibration errors; "Replay recovery
animation" restarts visual compliance without releasing the core. Captured
2.7.1 saves replay once on load, then retain the new revision when saved.
Toolbar GameEvents use instance callbacks, and recovered RealFuels engines
shut down after all PartModules have initialized during save restoration.
Local native tests reproduce the old suppressed-player failure and verify
normal/core-controlled capture, powered Waterfall capture, replay, legacy
save recovery, held-save reload and missing-clip diagnostics. A Kerbin derivative
of the user's interstage-mounted hook assembly also restores all four hooks.
The user's complete KSP 1.12.5 RSS/Sol environment still needs confirmation.

2.7 corrects the MU handedness/winding export that mirrored ship lettering
and CASC markings. The archived Blender and shared textures retain their
original bytes. Validation details/limits are in the local HTML report.

2.7.1 additionally corrects the CASC vertical UV direction and the inward
face normal on one side. Institute hull letters retain their 20 m width and
increase from 0.72 m to 1.00 m height. Existing shared texture bytes are reused.
