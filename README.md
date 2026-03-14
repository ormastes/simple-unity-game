# Elemental Siege

A physics-based puzzle launcher game for iOS and Mac, built with Unity 6.

## Overview

Elemental Siege challenges players to launch elemental orbs at fortified structures defended by elemental guardians. Combine elements, exploit weaknesses, and solve physics puzzles to breach enemy defenses across a sprawling world map.

## Requirements

- **Unity 6** (6000.x) — required
- **Universal Render Pipeline (URP)** — included via packages
- **Platforms:** iOS, macOS (Standalone)

## Setup

1. Install Unity 6 via [Unity Hub](https://unity.com/download)
2. Clone this repository
3. Open the project folder in Unity Hub
4. Unity will import packages and compile — this may take a few minutes on first open

## Project Structure

```
Assets/_Project/
├── Art/
│   ├── Sprites/        # 2D sprite sheets and images
│   ├── Animations/     # Animation clips and controllers
│   ├── Shaders/        # Custom shader graphs and code
│   ├── Materials/      # Material assets
│   └── VFX/            # Visual effects (particle systems, VFX Graph)
├── Audio/
│   ├── Music/          # Background music tracks
│   ├── SFX/            # Sound effects
│   └── Mixers/         # Audio mixer groups
├── Data/
│   ├── Elements/       # Element type ScriptableObjects
│   ├── Levels/         # Level configuration data
│   ├── Structures/     # Structure/building definitions
│   └── Progression/    # Player progression and unlock data
├── Prefabs/
│   ├── Orbs/           # Elemental orb prefabs
│   ├── Structures/     # Destructible structure prefabs
│   ├── Environment/    # Environment and terrain prefabs
│   ├── Guardians/      # Enemy guardian prefabs
│   ├── UI/             # UI element prefabs
│   └── Core/           # Core system prefabs (managers, etc.)
├── Scenes/
│   ├── Boot.unity      # Bootstrap / loading scene
│   ├── MainMenu.unity  # Main menu
│   ├── WorldMap.unity  # World / level select map
│   └── Gameplay.unity  # Core gameplay scene
└── UI Toolkit/
    ├── Styles/         # USS stylesheets
    └── Templates/      # UXML templates
```

## Build Scenes

| Index | Scene     | Purpose                  |
|-------|-----------|--------------------------|
| 0     | Boot      | Initialization & loading |
| 1     | MainMenu  | Title screen & settings  |
| 2     | WorldMap  | Level selection          |
| 3     | Gameplay  | Core puzzle gameplay     |

## License

All rights reserved. Copyright Ormastes.
