# Linghangzhe / 领航者

Runtime: 2.7.0-dev. Editable model source: 2.5.0-dev, unchanged.
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
| Rail bounds | +/-20 m; 12 m/s; nominal 24 m/s² |
| Predicted central region | +/-12.5 m |

The rail governor preserves a 5.6 x 5.6 m minimum tracking rectangle,
containing a 5.4 m diameter circle. Clearance and rail boundaries take priority
over individual carriage goals. Four actual gated contacts remain required.
One YF-100 may burn at <=70% rated thrust; others must be shut down. Capture
cuts the engine after 0.18 s. Engine rejection is visible in the window.

Capture merges rigidbody momentum once and makes one native KSP coupling.
Earlier cable contacts yield visually while awaiting the other hooks.
After capture, native joints remain fixed while original hook/hydraulic
animations retract 24 degrees and model roots sink/rebound over 2.6 s.
Active/extra hook identities and buffer progress survive saves. Release uses
native Undock and restores models. This is visual compliance, not physical
rope or cable-tension simulation; it does not certify return guidance.

2.7 corrects the MU handedness/winding export that mirrored ship lettering
and CASC markings. The archived Blender and shared textures retain their
original bytes. Validation details/limits are in the local HTML report.
