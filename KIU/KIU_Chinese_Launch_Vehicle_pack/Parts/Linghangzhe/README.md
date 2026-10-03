# Linghangzhe / 领航者

Source/runtime version: 2.6.0-dev. Part: KCLV_Linghangzhe_Recovery_Ship, Utility,
veryHeavyRocketry. Assets, RecoveryShip, Localization and Compatibility follow
CZ-10B's per-project layout. Editable sources and native evidence are archived
in KIU-Model/Linghangzhe; no authoring files or test bridge ship with this Part.

Install the complete KIU directory as GameData/KIU, including Plugins/KIUNetRecovery.dll,
the current CZ-10B RecoveryHook and Common/KIU_Common_texture. Waterfall remains
an optional existing engine dependency. No extra demo Parts or copied CZ-10B models.

Deploy at sea, enable station keeping, then arm the net from the ship's Part menu
or action groups. Install and deploy four actual CZ-10B hooks around the core in
the validated square arrangement. Approach the central +/-12 m in both horizontal
axes with relative speed <=20 m/s and sufficient tracking time (tested 80 m above
the cable plane). Motion is limited to +/-20 m rail travel, 12 m/s and 24 m/s^2.
One YF-100 may burn at <=70% rated thrust at capture, then shuts down after 0.18 s.
Capture completes only after four contacts within 0.12 s. Station keeping applies
bounded game forces at the selected sea position; it provides no ship propulsion.

Captured native joints remain fixed. Original DeployHook/hydraulic animation
retracts 24 degrees; render roots sink and rebound over 2.6 s. Hook/rope pairing
and visual progress survive save/reload. Release restores model offsets and uses
native Undock. Reset/arm again for another attempt. Do not physics-warp an approach.

1200 t mass, buoyancy and capture limits are game tuning. This is not sea-engineering
or real rope simulation. Native production validation includes a 9-point central
grid with the actual RF YF-100 and actual KIU hooks/core, plus cold full-fuel coupling
and its save/reload/release lifecycle. RF approaches use controlled initial pose,
velocity, 7% remaining propellant and a one-time settled-fuel initial condition;
RF's subsequent ullage calculation remains enabled. This does not validate complete
return guidance, an RCS settling sequence or an RO mission. Arbitrary hook layouts,
scaling and high rotation require separate checks. Use the original hook scale.
Existing accepted 2.4/2.5 videos remain reference evidence; 2.6 introduces production
bindings and controls. Detailed native cases and limits are in the HTML report.
