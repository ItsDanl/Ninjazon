# Ninjazon Pixel-Art Production Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Ninjazon's geometric placeholders with a cohesive, editable, high-detail pixel-art street-runner presentation suitable for a polished mobile prototype.

**Architecture:** Authored environment layers, player renderers, and prefab templates remain serialized in saved Unity scenes. Small focused presentation components own sprite animation and art configuration; procedural gameplay only activates and recycles prefab-backed entities. Generated PNG sheets are imported deterministically by an editor pipeline so the project can be rebuilt without recreating cubes.

**Tech Stack:** Unity 6.3, C#, uGUI/TextMesh Pro, 2D SpriteRenderer, Animator-independent frame animation, Unity Test Framework, PNG sprite sheets.

**Spec:** `docs/superpowers/specs/2026-09-02-ninjazon-pixel-art-pass-design.md`

## Global Constraints

- Work only in `D:\Unity\Projects\2DPLATFORMER\2D PLATFORMER`.
- Keep authored visuals persistent, serialized, visible, and editable in the Unity hierarchy.
- Runtime code may generate or pool repeated hazards, pickups, particles, and segment instances only.
- Preserve existing assets and isolate replaced geometry under `LegacyPlaceholders`; do not delete it.
- Use point filtering, no mipmaps, no lossy compression, transparent sprite backgrounds, and a consistent pixels-per-unit value.
- Preserve gameplay timing and collision bounds while replacing presentation.
- Keep the existing menu structure; limit UI changes to visual compatibility and gameplay readability.
- Completion requires clean EditMode, PlayMode, hierarchy, fresh-editor smoke, and extended-run validation.
- The D: project is not currently a Git repository; commit steps remain blocked until repository initialization is explicitly authorized.

---

### Task 1: Pixel-art asset contract and importer

**Files:**
- Create: `Assets/Ninjazon/Runtime/Presentation/PixelArtCatalog.cs`
- Create: `Assets/Ninjazon/Editor/NinjazonPixelImporter.cs`
- Create: `Assets/Ninjazon/Tests/EditMode/PixelArtImportTests.cs`
- Modify: `Assets/Ninjazon/Runtime/Ninjazon.Runtime.asmdef`

**Interfaces:**
- Produces: `PixelArtCatalog`, `PixelAnimationClip`, and deterministic texture import settings used by all later tasks.

- [ ] Write an EditMode test that loads every catalog texture and asserts `FilterMode.Point`, `TextureImporterCompression.Uncompressed`, disabled mipmaps, sprite texture type, and identical pixels-per-unit.
- [ ] Run the targeted test and confirm it fails because the catalog/importer do not exist.
- [ ] Implement serializable `PixelAnimationClip { Sprite[] frames; float framesPerSecond; bool loop; }` and `PixelArtCatalog` fields for character clips, environment sprites, hazard sprites, pickup sprites, and VFX sprites.
- [ ] Implement `AssetPostprocessor.OnPreprocessTexture()` for `Assets/Ninjazon/Art/Pixel/` with sprite mode, alpha transparency, point filtering, no mipmaps, uncompressed texture data, and `32f` pixels per unit.
- [ ] Create `Assets/Ninjazon/Art/Pixel/{Character,Environment,Hazards,Pickups,VFX,UI}` and a catalog asset at `Assets/Ninjazon/Content/PixelArtCatalog.asset`.
- [ ] Run the targeted test and confirm it passes.

### Task 2: Produce and import the pixel-art source kit

**Files:**
- Create: `Assets/Ninjazon/Art/Pixel/Character/ninjazon-runner-sheet.png`
- Create: `Assets/Ninjazon/Art/Pixel/Environment/ninjazon-rooftop-kit.png`
- Create: `Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-far.png`
- Create: `Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-mid.png`
- Create: `Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-near.png`
- Create: `Assets/Ninjazon/Art/Pixel/Hazards/ninjazon-hazards-sheet.png`
- Create: `Assets/Ninjazon/Art/Pixel/Pickups/ninjazon-pickups-sheet.png`
- Create: `Assets/Ninjazon/Art/Pixel/VFX/ninjazon-vfx-sheet.png`
- Modify: `Assets/Ninjazon/Content/PixelArtCatalog.asset`

**Interfaces:**
- Consumes: importer and catalog from Task 1.
- Produces: sliced sprites with stable names such as `runner_run_00`, `hazard_barricade`, and `pickup_chip_00`.

- [ ] Generate a locked-design street runner reference and inspect silhouette, costume, palette, transparency, and pixel consistency.
- [ ] Generate the complete character sheet with idle, run, jump, slide, hit, and capture rows on fixed-size cells.
- [ ] Generate modular rooftop and three seamless skyline layers using the approved navy/violet, warm-window, cyan, magenta, green, and amber palette.
- [ ] Generate hazard, pickup, and VFX sheets with distinct silhouettes and no embedded text or watermark.
- [ ] Slice every sheet with explicit rectangles, names, pivots, and borders in `NinjazonPixelImporter` rather than relying on automatic slicing.
- [ ] Assign all sliced sprites to `PixelArtCatalog.asset` and run the import contract test.

### Task 3: Character presentation and complete animation set

**Files:**
- Create: `Assets/Ninjazon/Runtime/Presentation/PixelSpriteAnimator.cs`
- Create: `Assets/Ninjazon/Runtime/Presentation/RunnerArtController.cs`
- Create: `Assets/Ninjazon/Tests/EditMode/PixelSpriteAnimatorTests.cs`
- Modify: `Assets/Ninjazon/Runtime/Gameplay/RunnerPresentation.cs`
- Modify: `Assets/Ninjazon/Editor/NinjazonProjectBuilder.cs`

**Interfaces:**
- Produces: `PixelSpriteAnimator.Play(PixelAnimationClip clip, bool restart = false)` and `RunnerArtController.SetState(RunnerVisualState state)`.

- [ ] Write tests proving non-looping clips stop on their last frame, looping clips wrap, zero-frame clips safely no-op, and state changes do not restart the same clip each frame.
- [ ] Run the tests and confirm failure before implementation.
- [ ] Implement time-step-driven frame selection independent of gameplay movement.
- [ ] Map idle, run, jump, slide, hit, and capture states to catalog clips.
- [ ] Rebuild `PlayerPresentation` as persistent serialized sprite children for body, ground shadow, bag accent, and motion trail; move the geometric body beneath inactive `LegacyPlaceholders`.
- [ ] Connect existing jump, slide, mistake, and capture events without changing their collision timing.
- [ ] Run targeted and existing gameplay tests.

### Task 4: Persistent rooftop environment and seamless parallax

**Files:**
- Create: `Assets/Ninjazon/Runtime/Presentation/RooftopArtLayer.cs`
- Create: `Assets/Ninjazon/Tests/EditMode/RooftopArtLayerTests.cs`
- Modify: `Assets/Ninjazon/Runtime/Gameplay/GameplayEnvironmentController.cs`
- Modify: `Assets/Ninjazon/Runtime/Gameplay/ScrollingLayer.cs`
- Modify: `Assets/Ninjazon/Editor/NinjazonProjectBuilder.cs`
- Modify: `Assets/Ninjazon/Scenes/Gameplay.unity`

**Interfaces:**
- Consumes: environment sprites from `PixelArtCatalog`.
- Produces: persistent full-coverage sky, skyline, rooftop, foreground, rain, and atmosphere render layers.

- [ ] Write tests for full camera coverage, wrap continuity within seam tolerance, stable child counts, and serialized layer bindings.
- [ ] Run tests and confirm the new bindings fail.
- [ ] Replace flat building rectangles with persistent tiled skyline sprites at three parallax speeds.
- [ ] Replace route bars with rooftop panels, seams, lamps, cables, vents, puddles, and maintenance marks while preserving route coordinates.
- [ ] Add foreground parapet/details plus restrained fog and rain overlays below gameplay-critical sorting layers.
- [ ] Move old geometric background children beneath inactive `LegacyPlaceholders`.
- [ ] Run scrolling and scene-contract tests, then visually inspect a long scroll for seams.

### Task 5: Prefab-backed hazards and pickups

**Files:**
- Create: `Assets/Ninjazon/Runtime/Presentation/GameplayEntityArt.cs`
- Create: `Assets/Ninjazon/Prefabs/Gameplay/Barricade.prefab`
- Create: `Assets/Ninjazon/Prefabs/Gameplay/LaserGantry.prefab`
- Create: `Assets/Ninjazon/Prefabs/Gameplay/BlockedRoute.prefab`
- Create: `Assets/Ninjazon/Prefabs/Gameplay/ScannerDrone.prefab`
- Create: `Assets/Ninjazon/Prefabs/Gameplay/DataChip.prefab`
- Create: `Assets/Ninjazon/Prefabs/Gameplay/Credit.prefab`
- Create: `Assets/Ninjazon/Prefabs/Gameplay/Jammer.prefab`
- Create: `Assets/Ninjazon/Tests/EditMode/GameplayEntityArtTests.cs`
- Modify: `Assets/Ninjazon/Runtime/Gameplay/GameplayController.cs`
- Modify: `Assets/Ninjazon/Editor/NinjazonProjectBuilder.cs`

**Interfaces:**
- Produces: `GameplayEntityArt.Configure(EntityKind kind, PixelArtCatalog catalog)` and seven serialized prefab templates.

- [ ] Write tests asserting every entity kind has a configured prefab, sprite renderer, collider, catalog art, valid sorting layer, and original gameplay bounds.
- [ ] Run tests and confirm failure with the current cube factory.
- [ ] Build each prefab with the approved silhouette, optional animation/glow children, and visible editable collision bounds.
- [ ] Replace `CreateSprite("PooledEntity", ...)` with pool acquisition from serialized prefab templates keyed by `EntityKind`.
- [ ] Keep spawn selection, off-screen placement, movement, collision rules, and recycling behavior unchanged.
- [ ] Retain the old cube renderer only as an inactive named fallback under `LegacyPlaceholders`.
- [ ] Run entity, collision, pooling, and scene-contract tests.

### Task 6: Pixel VFX and gameplay juice integration

**Files:**
- Create: `Assets/Ninjazon/Runtime/Presentation/PixelVfxPool.cs`
- Create: `Assets/Ninjazon/Tests/EditMode/PixelVfxPoolTests.cs`
- Modify: `Assets/Ninjazon/Runtime/Gameplay/RunnerPresentation.cs`
- Modify: `Assets/Ninjazon/Runtime/Gameplay/GameplayController.cs`
- Modify: `Assets/Ninjazon/Editor/NinjazonProjectBuilder.cs`

**Interfaces:**
- Produces: `PixelVfxPool.Play(VfxKind kind, Vector3 position, Color tint)` with pooled landing, splash, pickup, spark, glitch, searchlight, hit, and capture effects.

- [ ] Write tests proving repeated effects reuse objects, reset sprite/transform/color on reuse, and create no steady-state instances after warm-up.
- [ ] Run tests and confirm failure before implementation.
- [ ] Implement pooled frame-animated VFX using catalog clips.
- [ ] Trigger landing dust and rain splash from vertical motion, pickup bursts from collection, sparks/hit flash from mistakes, and glitch/searchlight/capture effects from Alert transitions.
- [ ] Preserve vibration and screen-shake settings and cap simultaneous effects to avoid obscuring hazards.
- [ ] Run VFX, Alert, and pooling tests.

### Task 7: Gameplay readability and minimal UI compatibility

**Files:**
- Modify: `Assets/Ninjazon/Runtime/Gameplay/GameplayView.cs`
- Modify: `Assets/Ninjazon/Runtime/Gameplay/GameplayController.cs`
- Modify: `Assets/Ninjazon/Editor/NinjazonProjectBuilder.cs`
- Modify: `Assets/Ninjazon/Scenes/Gameplay.unity`
- Test: `Assets/Ninjazon/Tests/EditMode/PersistentSceneContractTests.cs`

**Interfaces:**
- Consumes: existing HUD state and new art sorting layers.
- Produces: readable HUD contrast and Alert treatment without restructuring menu navigation.

- [ ] Extend scene-contract assertions for player visibility, HUD safe-area binding, Alert readability, and hazard-over-background sorting.
- [ ] Run the contract tests and confirm failure for the new requirements.
- [ ] Add restrained pixel-frame backing to score, distance, credits, speed, and Alert while retaining existing buttons and navigation.
- [ ] Tune foreground contrast and Alert overlays so they never cover the route reaction zone.
- [ ] Run scene-contract and navigation tests at 9:16 and a taller safe-area profile.

### Task 8: Final rebuild, validation, and handoff

**Files:**
- Modify: `Assets/Ninjazon/Editor/NinjazonSceneValidator.cs`
- Modify: `docs/superpowers/plans/2026-09-02-ninjazon-pixel-art-pass.md`

**Interfaces:**
- Consumes: every asset, prefab, scene, and test from Tasks 1–7.
- Produces: a reproducibly rebuilt and verified Ninjazon presentation.

- [ ] Extend the validator to report missing scripts, missing serialized references, unsliced required sprites, invalid import settings, inactive active-art roots, and prefab/catalog gaps.
- [ ] Run `Ninjazon/Build Prototype Scenes` and confirm all authored art remains visible and editable in the hierarchy.
- [ ] Run all EditMode tests and require zero failures.
- [ ] Run all PlayMode tests and require zero failures.
- [ ] Restart Unity and run Bootstrap → Main Menu → Gameplay → Pause → Options → Back → Resume → Results → Retry.
- [ ] Continue gameplay long enough to observe repeated pool reuse and every entity type; require continuous backgrounds, off-screen spawning, stable object counts, and no new Ninjazon errors or warnings.
- [ ] Check every completed box in this plan and report exact test totals plus any genuinely deferred release work.

