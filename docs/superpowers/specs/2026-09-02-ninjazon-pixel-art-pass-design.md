# Ninjazon Pixel-Art Production Pass

## Goal

Replace the prototype's geometric placeholder presentation with a cohesive, high-detail pixel-art mobile-game look. The pass must improve the player, gameplay objects, rooftop environment, animation, and moment-to-moment visual feedback while preserving the existing gameplay and layered navigation.

## Art direction

Ninjazon takes place across rain-soaked neon rooftops. The playable courier is a youthful street runner rather than an armored ninja: visible face, windbreaker, sneakers, messenger bag, and a bright trailing scarf or bag strap that reinforces speed and direction.

The palette uses deep navy and violet shadows, warm window light, cyan navigation technology, magenta corporate security, green/cyan jammer effects, and amber courier tokens. Color supports recognition but does not carry it alone. Every hazard and pickup needs a distinct silhouette, material treatment, and motion language.

The intended finish is modern high-detail pixel art with consistent pixel density. Pixel sprites may be supported by restrained bloom, particles, screen-space flashes, and smooth camera movement, but the source artwork must remain crisp and deliberate.

## Persistent scene architecture

Authored visual content remains saved, visible, and editable in the Unity hierarchy.

- `GameplayEnvironment` contains persistent sky, distant skyline, middle skyline, near buildings, rooftop surface, foreground detail, rain, and atmosphere layers.
- `PlayerPresentation` contains the animated player sprite plus editable shadow, messenger-bag accent, and motion-trail children.
- Persistent route presentation uses rooftop seams, lamps, cables, vents, and painted maintenance marks instead of abstract glowing lane bars.
- Authored hazard and pickup prefabs contain editable sprite renderers, collision bounds, glow accents, and animation components.
- Runtime systems only select, position, activate, and recycle repeated hazards, pickups, particles, and segment instances.
- Existing geometric art is retained under an inactive `LegacyPlaceholders` hierarchy or legacy asset area and is not used as the active presentation.

## Asset organization and import

New production assets live below `Assets/Ninjazon/Art/Pixel`:

- `Character`
- `Environment`
- `Hazards`
- `Pickups`
- `VFX`
- `UI`

Sprites use point filtering, no lossy compression, no mipmaps for gameplay sprites, consistent pixels per unit, transparent backgrounds, and explicit sprite-sheet slicing. Animation frames must share fixed canvas dimensions, pivots, scale, silhouette, costume, and palette.

## Character animation

The street runner receives complete prototype animation coverage:

- idle
- run
- jump
- slide
- hit
- capture

The controller exposes serialized animation timing and sprite references. Movement logic remains independent of the presentation so art can be replaced without changing gameplay rules.

## Environment

Rooftop tiles use vents, access plates, cables, puddles, warning paint, antennas, parapets, and occasional holographic signage. Tiles must join without visible seams and provide enough variation to avoid obvious repetition.

Three skyline depths, atmospheric fog, and rain create parallax. Persistent scrolling layers retain full viewport coverage at all supported speeds and portrait aspect ratios. Near-layer contrast stays below the player and hazard silhouettes so gameplay remains readable.

## Gameplay objects

- Jump hazard: folding construction barricade with warning lamps and a strong waist-high silhouette.
- Slide hazard: overhead security gantry with an animated laser field and clear safe gap.
- Blocked route: large rooftop machinery, ducting, or a locked access structure occupying the route.
- Scanner: hovering security drone with articulated searchlight and scanning cone.
- Data chip: rotating cyan data shard with a compact digital trail.
- Credit: amber courier token with a distinct circular silhouette.
- Jammer: green/cyan signal device with antenna arcs and a disruption pulse.

Collision bounds remain authored and visible on prefabs. Visual extents must not silently alter existing reaction timing.

## Visual feedback

The pass adds landing dust, rain splashes, pickup bursts, Alert glitches, speed streaks, collision sparks, drone searchlights, hit flashes, and capture effects. Effects use pooling where repeated. Alert escalation progressively changes drone presence, vignette/glitch intensity, warning animation, and environmental color without obscuring hazards.

## UI boundary

The existing menu structure remains intact in this pass. Only minimal background or readability changes are allowed where the old menu would visibly conflict with the new gameplay presentation. A separate full UI redesign can follow later.

## Error handling and fallbacks

Scene views validate required serialized references before use and report a single actionable error rather than cascading null-reference failures. Missing optional art falls back to a clearly identified development sprite without constructing anonymous cubes. Asset loading never mutates or deletes legacy content.

## Verification gate

The pass is not complete until all of the following succeed:

1. Unity compiles with no C# errors.
2. Saved-scene and prefab validation reports no missing scripts or serialized references.
3. Sprite import settings and animation frame assignments are validated.
4. All existing EditMode tests pass, with new tests covering asset configuration and presentation bindings.
5. All PlayMode tests pass.
6. A fresh-editor Bootstrap-to-gameplay smoke test produces no new errors or warnings attributable to Ninjazon.
7. An extended gameplay run verifies stable pooling, continuous backgrounds, off-screen spawning, collision readability, pause/options navigation, and clean retry/reset behavior.
8. Portrait and taller safe-area layouts remain playable and readable.

## Deferred scope

This pass does not redesign every menu, add a cosmetics shop, create platform store artwork, or replace the final music and sound library. Those remain separate production milestones.

