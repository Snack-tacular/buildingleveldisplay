# Sineus Arena - Building Level Display Mod

This BepInEx mod displays the current and maximum level of player buildings directly in the game world, using a clean and stylized overlay card.

## Features

- **Distance Culling (Hide When Far)**: Automatically hides the level display when the local player character is further than `22f` units away to keep the screen clean.
- **Z-Test Bypass (Render on Top)**: Overrides standard depth-testing so that the level badges render on top of the building's 3D models and never get occluded.
- **Unparented Canvas (No Scale Distortion)**: Instantiated at the scene root without parenting it to the building itself to prevent non-uniform scaling or skewing from warping the badge size/shape.
- **Visual Progression**:
  - **Standard Buildings**: Soft white-blue text (`Lvl X/Y`) and a sleek grey border.
  - **Maxed Buildings**: Golden text (`Lvl Max/Max`) and a golden border.

## Requirements

- [BepInEx 5.x](https://github.com/BepInEx/BepInEx)

## Build & Installation

1. Open the project with any C# compiler supporting `.NET Standard 2.1`.
2. Compile in **Release** configuration.
3. Copy the compiled `BuildingLevelDisplay.dll` into the game's `BepInEx/plugins/` directory:
   ```
   steamapps\common\Sineus Arena\BepInEx\plugins\BuildingLevelDisplay\BuildingLevelDisplay.dll
   ```
