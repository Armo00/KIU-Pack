# Linghangzhe / 领航者

Latest archived editable model: 2.9.0-dev.
Current working update (2026-10-24): collider optimization and native core
uprighting based on that archive; not a new archived release.
Install the complete KIU directory as GameData/KIU, including
Plugins/KIUNetRecovery.dll, Plugins/ConfigurableTank.dll (shared renderer-cache
helper), the current CZ-10B RecoveryHook and Common textures.
No test bridge or additional demo Parts ship in KIU.

Open recovery control with the blue net icon in the stock KSP toolbar.
While controlling the core, this window can arm/disarm/reset the receiver,
toggle station keeping or ship SAS, and release a captured core. It reports
the target, installed hooks, contacts, relative motion, aperture and exact
last rejection. There is no automatic overlay on the core.

The receiver may operate on land, in flight or at sea: no water gate.
Station keeping applies at sea. The 25,000 t full-load-equivalent ship has native SAS and an aggregate
reaction wheel configured for 6,250,000 kN m on each axis (equivalent to many
wheels), up to 100 ElectricCharge/s by configuration, and 100,000 stored EC.
Existing mods may alter stock resource behavior. This is game tuning.

Install at least four original-scale CZ-10B hooks around the core and fully
deploy at least four. The controller chooses four distinct reachable hooks
for the four cable roles; extra hooks no longer prevent tracking. Cardinal
and diagonal arrangements are supported. Extra deployed hooks follow the
50-degree retraction; folded extras stay folded.

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
| Contact yield stroke | 6 m |
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
Carriage speed is 48 m/s and acceleration 144 m/s². The controller reserves
braking distance before reaching the hook and initializes velocity feedforward
from the acquired target. These are visual servo game settings; a very late
acquisition can still leave insufficient travel time.

Capture merges rigidbody momentum once and makes one native KSP coupling.
When the pilot controls the incoming core, capture preserves the camera target,
mode, heading, pitch and distance while KSP selects the merged vessel. A render
anchor follows the recovered core; it adds no Part, collider or force.
Earlier cable contacts yield visually while awaiting the other hooks.
After capture, the original hook/hydraulic animations retract 50 degrees while
a bounded 3.2 s recovery phase moves the complete native core assembly down and
aligns its axis with local gravity-up, including a small damped return swing.
Part transforms, rigidbodies, colliders and visible models share that pose.
At rest, ordinary rigidbody dynamics resume and the native KSP attachment plus
three fixed supports distribute the load across the four selected hooks.
Active/extra hook identities and buffer progress survive saves. Release first
withdraws the steadying lines/cradle and unfolds the hooks, then native Undock
releases the recovered vessel at its current physical pose without adding the
sink displacement a second time. The original surface attachment mode is preserved through rerooting;
cross-vessel stock/KJR constraints are freed before the displacement, including
constraints awaiting Unity destruction. This is prescribed recovery motion; it does not
solve cable tension or certify return guidance.

2.7.2 samples each hook's owned DeployHook clip directly and verifies the actual
hinge movement before advancing the buffer. The control window reports buffer
progress, actual retraction, sink and calibration errors; "Replay recovery
animation" restarts visual compliance without releasing the core. Captured
Older buffer revisions replay once on load, then retain the new revision when saved.
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

2.8.0 uses the published 25,000 metric tonne full-load displacement as the
complete Part's effective mass. This is not a claim of published lightship mass.
Source: https://kjj.gz.gov.cn/xwlb/yw/content/post_10693741.html
Native buoyancy is configured at 0.58; the 2.9.0 scoped native check settled at
about 5.36 m draft with a recovered test core and without vertical assist.
The final cable contact center drops 8 m below the armed cable plane; the core's
native displacement accounts for the moving mouths during 50-degree hook
retraction and uprighting. A gentle rebound remains. The motion is prescribed
recovery choreography; cable tension and elastic energy are not solved.

2.9.0 uses round shaded cable meshes with fixed ends, hook contacts and four
unilateral crossover constraints. Small self-weight bow is limited to 0.025 m
per span. Extra length drives visible winch payout. Captured cables have upward
supporting tangents in the inspected cases; crossing clearance is checked in
actual 3D geometry. Four auxiliary lines deploy, then a movable cradle closes
its pads in alignment with the core. These mechanisms are presentation
animations; tension, brake forces and elastic energy are not solved.

The toolbar adds target name, X/Z offsets, arrival estimate, per-hook contact
state and a plan view. Settled animation and cable geometry are cached; replay
and update counters are in the diagnostics foldout. Station keeping uses
mass/inertia-normalized horizontal and attitude control, with no vertical force.

In VAB/SPH, right-click the ship and choose Tower extension segments: an
integer 0–15, default 0. Each segment adds 1 m at the supported 1:1 scale.
Braced one-meter sections are combined by material into bounded-size meshes;
one continuous BoxCollider per full leg stretches from its unchanged lower
endpoint to the raised top. The ship has 15 active colliders at every supported
height; changing height creates no extra leg collider objects. The upper
frame, rope carriages, cable plane, tracking reference and top attachment
node rise together; diagonal stays retain their lower anchorage. Height is
saved in the craft and game, and is fixed during normal flight operation.
Total mass remains 25,000 t for all optional game-height variants.

## 2.9.0 validation scope

KSP 1.12.3 with the existing KJR and Deferred installation: core-controlled
offset captures near 2 and 15 degrees, contact speeds around 20 m/s, four
individually measured 50-degree hook angles, sink/rebound, tilted cradle,
0/1/15 m editor height and saved-craft reload. Legacy, fresh-capture and
save/reload releases retained all 19 core parts during approximately 1–1.6 s
of free-motion observation, after which the fixture froze the separated core.
This is not a long free-flight or complete return-mission qualification.

The source export has 136 single-material, single-submesh renderers versus
193 in 2.8.0, with 182,057 triangles. The compact editable source archive is
`KIU-Model/Linghangzhe/versions/v2.9.0-dev`; native runs, media and the consolidated
HTML report remain in the ignored Linghangzhe workspace.

## Collider working update: 2026-10-24

The MU defines 15 boxes instead of 23 (four old definitions were inactive
extension templates). Active loaded counts change from 19 to 15 at zero
extension and from 23 to 15 at 1/15 m. In the editor, one active box is a
trigger and a stock Surface Attach Collider remains inactive in both fixtures;
flight has 15 solid boxes and no trigger. Visual meshes, UVs, materials,
textures, hull and the other 10 non-leg boxes are unchanged. All 0–15 m leg
volumes match the previous envelope; this preserves the central opening.

Repeated height changes, same-height renderer identity, craft reload, two
core-controlled captures (about 15 degrees / 21.2 m/s and 2 degrees / 20.4 m/s),
50-degree hook motion, sink/rebound and saved/fresh releases were inspected
in KSP 1.12.3. No frame-rate improvement is claimed from the collider count.
The old-MU count fixture uses the current DLL's legacy-template branch.

The initial collider-only candidate retained about 14.8 degrees of core tilt
after a 15-degree entry. The subsequent working correction replaces that held
entry pose with native uprighting, calibrates support anchors after restoring
the held hook clip, and keeps ropes/cradle following actual contact positions.

Final KSP 1.12.3 checks used core-controlled 0, +/-5, +/-10 and +/-15 degree
entries at offset positions and 0/15 m tower heights. Settled core/model/body
tilt stayed within about 0.03 degrees in these cases and a 20 s calm-water hold,
with no residual kinematic bodies. Legacy captures, a save made 0.74 s into
buffering, held saves, replay, and mid-buffer release were checked. An early
release preserves the current pose instead of forcing uprighting to finish.
All observed releases retained 19 core parts with less than 1 mm position jump;
the fixture freezes the separated core after its short observation interval.
Sources and exact native evidence remain in the current ignored work iteration.
