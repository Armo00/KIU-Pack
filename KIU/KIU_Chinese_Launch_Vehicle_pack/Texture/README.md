# Integrated KCLV textures — v3.5.7

Integrated on 2026-10-11 from the complete v3.5.7 texture package, which the user reported as fully tested. The 73 MU files and 150 local texture files retain their tested bytes. Public textures are referenced from `KIU/Common/KIU_Common_texture`, with 28 shared images reused rather than copied here.

The 75 Part configurations now point directly to these models. Livery, serial, separation-cover, RealFuels and finish rules remain in `Compatibility/TextureRepair`; localized labels are in `Localization/TextureRepair.cfg`. B9PartSwitch and ModuleManager remain required for the tested switching behavior. The existing package's `KIUSeparationFuel.dll` is carried over unchanged into `KIU/Plugins`.

When upgrading an installation, remove the old standalone `GameData/KIU_CZFamily` directory. Do not install the old texture addon alongside this integrated tree. Replace the old KIU installation so the retired models/textures do not remain as unused files; preserve unrelated user mods and files separately. Do not delete the shared library when removing one Part.

Public assets and their catalog remain user-maintained. The original Part IDs, physical parameters, attachment nodes and resources are retained; only the package's established variant/visual bindings are integrated. `000_Registration.cfg` preserves the package's `KIU_CZFamily` ModuleManager identity so legacy fallback patches remain disabled.

Archive SHA256: `83bd674b9af4b617aa662bcde46a8ca616af62943ba008f184eb2a3030138d58` (`KCLV_TextureRepair.v3.5.7_install.zip`). Integration provenance and file hashes are in `integration.json`. The frozen release, rollback copies and consolidated HTML comparison report remain under `local_workspace/KIU_ReTexture`.

Validation states: v3.5.7 package native testing passed according to the user; this path/configuration integration is checked offline and does not claim a new native KSP run. The pre-existing “Matte resets after configuration switching” limitation remains deferred.
