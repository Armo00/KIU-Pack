# ConfigurableTank: continuous tank collision geometry

The ordinary tank extension path can keep a constant set of physical colliders while retaining the authored, repeated visual bays. This is an explicit model profile, not a bounding-box approximation of arbitrary parts. Existing configurations without the fields below keep their legacy behavior.

## Configuration

Add these fields inside the existing `MODULE { name = ConfigurableTank }`. Names must match the authored model; this example is for CZ-10B tanks:

```ini
continuousColliderTransforms = tankBody_collision
continuousTemplateColliderTransforms = extensionTemplate_collision
continuousPrimarySolidTransform = tankBottom_collision
```

- `continuousColliderTransforms`: comma-separated, collision-only direct children of `bodyTransform`. Their original geometry must cover the full constant-radius main body interval. A single convex cylinder proxy is sufficient for the current tanks. A hollow or changing-cross-section part needs its own suitable profile; do not use this to fill a required cavity.
- `continuousTemplateColliderTransforms`: comma-separated collider names present in every extension template. These collider components are disabled and removed from the templates before visual cloning. All authored template colliders must be classified. Do not list interaction triggers.
- `continuousPrimarySolidTransform`: the authored solid collider that KSP converts into the Part's primary trigger. The trigger remains intact. If it is a trigger at initialization, a separate physical collider is created at the same pose and follows the same body/cap motion. If it is still solid, no duplicate is created.

Supported body collider types are BoxCollider and convex MeshCollider. Each selected transform must be a leaf with exactly one collider and no Renderer, MeshFilter or Animation. The body root must have identity local position, rotation and scale. MODEL/ancestor scaling remains outside the collision frame. Invalid profiles report an initialization error rather than guessing a safe shape; leaving the profile empty is the legacy compatibility path.

## Geometry and lifecycle

Only collider-only children are parented to `ConfigurableTank_CollisionStretch`. Rendering transforms, visible mesh vertices, UVs, textures and animation hierarchies are not stretched or rewritten. End caps retain the existing movement logic.

For original main-body height H, lower extension d, upper extension u, and original lower coordinate a:

```text
axial scale = (H + d + u) / H
axial offset = a * (1 - axial scale) - d
```

Thus the lower edge becomes a-d, the upper edge becomes a+H+u, and radius remains constant. The existing frame and restored physical collider are reused when a Part is copied. Existing collider instances remain stable across length changes. Extension render objects still follow the preexisting rebuild path; this change does not claim to eliminate all VAB allocation or frame-time costs.

## Current profiles and boundaries

Profiles are provided for CZ-10B S1/S2, CZ-8A S2, CZ-12B S1/S2, and CZ-16 S1/S2. Current native results should be read in the accompanying development report. These seven models use three physical solids plus one KSP trigger at every tested length.

Tower `upperOnly` and CBC `useFixedAnchorSpan` modes remain separate and unchanged. Unknown third-party models retain legacy colliders until explicitly configured and checked. No additional DLL is required: `ConfigurableTankColliders.cs` is compiled into the existing public `ConfigurableTank.dll` through `plugin-build-manifest.json`.

## Regression requirements

Check minimum, odd/even, maximum, shrink and regrow; ray-pick coverage and end locations; symmetry propagation and cloning an extended model; saved-craft reload; B9 variants; stock and RealFuels resources, mass and nodes. Compare visual mesh/UV/material snapshots to the same pre-change DLL. Record TweakScale and flight collision validation separately; editor checks are not substitutes for them. Temporary test plugins must not ship.
