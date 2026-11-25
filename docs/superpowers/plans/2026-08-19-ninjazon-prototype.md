# Ninjazon Prototype Implementation Checklist

- [x] Create Bootstrap, MainMenu, and Gameplay scenes and configure build order.
- [x] Add persistent application services and versioned atomic JSON persistence.
- [x] Implement layered screen/modal navigation with Back fallbacks.
- [x] Build saved, hierarchy-editable Main Menu, Options, Credits, Quit, Pause, HUD, Tutorial, and Results UI.
- [x] Add serialized scene views and controller entry points; validate missing references.
- [x] Implement three-route movement, buffered switching, jump, slide, and keyboard/touch controls.
- [x] Implement first-run tutorial progression, completion persistence, and reset.
- [x] Add pooled hazards, chips, credits, jammers, scanners, grace periods, Alert, capture, scoring, and difficulty growth.
- [x] Add persistent parallax city, full-width route layers, rain, streaks, player/drone presentation, particles, shake, pulses, and temporary audio/haptics.
- [x] Prevent content from spawning visibly at the left edge and validate scrolling-layer coverage.
- [x] Add EditMode coverage for navigation, persistence, run rules, segment rules, serialized scene contracts, and scrolling seams.
- [x] Add PlayMode scene-factory/entry validation.
- [x] Verify 38/38 EditMode tests and 2/2 PlayMode tests after rebuilding saved scenes.
- [ ] Initialize Git in the D: project and create milestone commits (requires an explicit repository decision because the project is not currently a Git repository).
- [ ] Replace temporary visuals/audio with final licensed production assets.
- [ ] Perform device-specific Android/iOS profiling, signing, store compliance, and release packaging.

