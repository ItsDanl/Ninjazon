# Ninjazon Prototype Design

## Product goal

Ninjazon is a portrait mobile endless runner set in a neon cyberpunk city. The player remains near the left third of the view while the world scrolls left. Runs combine three-route movement, jump and slide reactions, data-chip and credit collection, scanner avoidance, Alert management, and an escalating drone pursuit.

## Prototype scope

The vertical slice contains saved Bootstrap, MainMenu, and Gameplay scenes. Menu screens, gameplay HUD, pause/options/results/tutorial layers, background layers, route art, the player presentation, and their serialized references are persistent scene objects visible and editable in the Unity hierarchy. Repeated run content—hazards, pickups, particles, and scrolling repetitions—is created and recycled at runtime.

## Navigation

Scene-local UI uses separate LIFO screen and modal histories. Back closes the top modal, then pops the top screen, then applies the scene fallback. Options therefore returns to its actual caller, including a paused run. Main Menu Back opens Quit confirmation; Gameplay Back opens Pause; Pause Back resumes.

## Gameplay loop

The player changes one adjacent route per input and may buffer one route change. Jump and slide can overlap route movement. Distance increases score and difficulty. Data chips add score, credits persist after a completed run, and jammers reduce Alert. Hazard mistakes add Alert with feedback and a grace period. At maximum Alert, capture ends the run and opens results.

## Presentation

The replaceable placeholder presentation uses dark city silhouettes, cyan/magenta/amber accents, parallax, rain, route streaks, particles, screen shake, warning pulses, a pursuing drone, and synthesized temporary audio. All authored scene visuals remain inspectable and adjustable in Edit Mode.

## Persistence

A versioned JSON save stores music/SFX volume, vibration, screen shake, control-hint preference, tutorial completion, best score/distance, and lifetime credits. Missing or malformed data falls back safely to defaults; writes use an atomic replacement flow.

## Acceptance boundary

The prototype target is Unity 6.3 Editor Play Mode in portrait presentation. Store packaging, platform signing, online services, monetization, analytics, final licensed art/audio, and a credit-spending shop are later milestones.

