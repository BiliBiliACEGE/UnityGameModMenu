[中文](README_zh.md) | [English](README.md)

# UnityGameModMenu

> A collection of mod menus for Unity games based on [BepInEx](https://github.com/BepInEx/BepInEx), covering both Mono and IL2CPP backend Unity games.

## Introduction

This repository contains mod menus / trainers for multiple Unity games. All plugins are built with **BepInEx** + **HarmonyLib**, using OnGUI for visual menu rendering and reflection + Harmony patches for real-time in-game data modification.

Games are organized by scripting backend into two categories:

| Directory | Backend | Description |
|-----------|---------|-------------|
| `MonoGames/` | Mono | For Unity games using the Mono runtime |
| `Li2CPPGames/` | IL2CPP | For Unity games compiled with IL2CPP |

> **BepInEx Version Selection**: The BepInEx version depends on the game's Unity engine version, not the backend type. Unity 2019–2022 uses **BepInEx 5**, while Unity 6 uses **BepInEx 6**. See [Prerequisites](#prerequisites) below.

## Supported Games

### Mono Games

<details>
<summary><b>Bendy and the Dark Revival</b> — Cheat Menu (BATDR Cheat Menu)</summary>

- **Plugin ID**: `com.ace20.batdr.cheatmenu`
- **Menu Toggle**: `F1`
- **Tabs**: Player / Enemies / Items / Weapons & Abilities / Game

| Category | Features |
|----------|----------|
| Player | God mode, infinite health, infinite stamina, no cooldown, full heal, instant respawn, super speed (adjustable multiplier), super jump (adjustable multiplier), stealth (disable Ink Demon detection), full bright, save/teleport position |
| Enemies | Freeze all enemies, enemies ignore player, one-hit kill, set enemy immune state |
| Items | Modify health/food/ammo/cards/toolkits/parts/batteries and other stats |
| Weapons & Abilities | Unlock/switch weapons, set weapon power, unlock/switch ability states |
| Game | Disable Ink Demon, control Ink Demon timer, etc. |

</details>

<details>
<summary><b>SchoolBoy Runaway</b> — Mod Menu</summary>

- **Menu Toggle**: `F1` (configurable)
- **Core Modules**: `ModMenuPlugin.cs` (main menu logic), `GameHelpers.cs` (game object caching & reflection helpers), `PossessionController.cs` (possession/control feature)
- **Features**: Speed multiplier adjustment (walk/run/crouch/climb/swim), jump force adjustment, health management, difficulty modification, possession control, etc.

</details>

<details>
<summary><b>Sniper 3D</b> — Trainer (Sniper3D Trainer)</summary>

- **Plugin ID**: `com.example.sniper3d.trainer`
- **Menu Toggle**: `F1`
- **Languages**: Chinese / English (switchable in-menu)
- **Tabs**: Currency / Shop / Weapons / Unlocks / Settings

| Category | Features |
|----------|----------|
| Currency | Modify soft currency (coins), hard currency (gems), PVP currency, energy; supports infinite mode |
| Shop | Free shopping (store/packs), bypass IAP verification |
| Weapons | One-click unlock/lock all weapons, one-click max/revert all upgrades, per-weapon unlock & upgrade, select upgrade part and set level |
| Unlocks | Remove weapon restrictions, trainer works in all modes, maximize mod level/amount, damage multiplier |
| Settings | Menu scale, font color (RGB), language switch |

</details>

### IL2CPP Games

<details>
<summary><b>Granny</b> — Mod Menu (Granny Mod Menu)</summary>

- **Plugin ID**: `com.granny.modmenu`
- **Target Environment**: BepInEx 6 IL2CPP (Unity 6 + IL2CPP + D3D12)
- **Menu Toggle**: `F1` (configurable)
- **Core Modules**: `Plugin.cs` (plugin entry, IL2CPP component injection), `ModMenuBehavior.cs` (menu UI & feature logic), `ModState.cs` (global mod state)
- **Tabs**: Player / Enemies / Map / Game / Weapon / Items / Vehicle / About

| Category | Features |
|----------|----------|
| Player | God mode, noclip, fly mode (adjustable speed), speed multiplier, jump boost, force crouch, flashlight, in-air control, player scale, never get caught |
| Enemy - Granny | Freeze, blind, deaf, no attack, speed multiplier, no catch, Granny scale |
| Enemy - Spider | Freeze, no hunt, no bite, no catch, spider scale |
| Enemy - MomSpider | Freeze, blind, no catch, force escape, kill, scale |
| Enemy - MomCrawl | Kill, scale |
| Enemy - LittleSanta | Stun, scale |
| Enemy - Crow | Freeze, no attack, no steal, scale |
| Enemy - Rat | Freeze, scale |
| Map | Full bright, disable fog, enable all lights |
| Game | Force Day 2/Day 3, force escape, nightmare mode |
| Weapon | Rapid fire, infinite range, load old shotgun |
| Items | One-click load old shotgun |
| Vehicle | One-click car ready |

</details>

## Prerequisites

### BepInEx

All plugins require the **BepInEx** runtime. **Different Unity versions require different BepInEx versions** — confirm your game's Unity engine version first, then choose the matching BepInEx:

| Unity Engine Version | BepInEx Version | Notes |
|----------------------|-----------------|-------|
| Unity 2019 – 2022 | **BepInEx 5** | Stable release, compatible with most older Unity games |
| Unity 6 | **BepInEx 6** | Bleeding Edge preview, supports Unity 6 new runtime |

> **How to check the game's Unity version**:
> - **Method 1**: Hover your mouse over the game's `.exe` file — the "File version" in the tooltip is the Unity version.
> - **Method 2**: Right-click the `.exe` → Properties → Details → check "Product version".

> **BepInEx Download Links** (choose based on the game's Unity version and scripting backend)
>
> - **BepInEx 5** (Unity 2019 – 2022):
>
>   https://github.com/BepInEx/BepInEx/releases?page=2#release-v5.4.15
>
> - **BepInEx 6 - Mono** (Unity 6, Mono backend):
>
>   https://builds.bepinex.dev/projects/bepinex_be/785/BepInEx-Unity.Mono-win-x64-6.0.0-be.785%2B6abdba4.zip
>
> - **BepInEx 6 - IL2CPP** (Unity 6, IL2CPP backend):
>
>   https://builds.bepinex.dev/projects/bepinex_be/785/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785%2B6abdba4.zip

### Development Environment (Building from Source)

- [.NET SDK](https://dotnet.microsoft.com/) (8.0+ recommended)
- IDE: Visual Studio / Rider / VS Code
- **Mono games**: The game's managed DLLs such as `Assembly-CSharp.dll` and `UnityEngine.dll` (located in `GameName_Data/Managed/`)
- **IL2CPP games**: Game DLLs must be dumped first (see instructions below)

## Installation

1. Download and install BepInEx into the game directory (see download links above).
2. Download the plugin DLL for your game from the [Releases](../../releases) page.
3. Place the DLL file in the game's `BepInEx/plugins/` directory.
4. Launch the game and press `F1` to open the mod menu.

### Directory Structure Example

```
Game Root/
├── BepInEx/
│   ├── core/            # BepInEx core files
│   ├── plugins/         # ← Place plugin DLLs here
│   └── config/          # Plugin config files (auto-generated)
├── GameName_Data/
└── GameName.exe
```

## Usage

| Game | Menu Hotkey | Notes |
|------|------------|-------|
| Bendy and the Dark Revival | `F1` | Hotkey can be changed in BepInEx config |
| SchoolBoy Runaway | `F1` | Hotkey can be changed in BepInEx config |
| Sniper 3D | `F1` | Supports in-menu Chinese/English toggle |
| Granny | `F1` | Hotkey can be changed in BepInEx config |

When the menu is open, the mouse cursor is automatically unlocked for clicking toggles. When closed, the cursor returns to locked state.

## Building from Source

> **Important**: Each project's `.csproj` references game managed DLLs and BepInEx core libraries via relative paths (`HintPath`). The **source directory must be placed inside the game root directory** for the reference paths to resolve correctly.

### Mono Games

1. Clone the repository:

```bash
git clone https://github.com/BiliBiliACEGE/UnityGameModMenu.git
```

2. Copy the game's source folder into the **game root directory** (same level as `GameName.exe` and `GameName_Data/`):

```
Game Root/
├── BepInEx/                  # Installed BepInEx
│   └── core/
├── GameName_Data/
│   └── Managed/              # Game managed DLLs (Assembly-CSharp.dll, etc.)
├── GameName.exe
└── SourceFolder/             # ← Copy from repo here
    ├── *.csproj
    └── *.cs
```

3. Build from the source directory:

```bash
# Example: Sniper3D
cd "Game Root/Sniper3D"
dotnet build -c Release
```

4. The compiled DLL is in `bin/Release/`. Copy it to `BepInEx/plugins/`.

> **Tip**: Different projects' `.csproj` files use slightly different reference path styles (some use `$(MSBuildProjectDirectory)\...`, others use `..\...`), but the universal requirement is that the source folder must be at the same level as the game's `GameName_Data/` and `BepInEx/` (i.e., inside the game root directory). If references can't be found, check the `HintPath` in the `.csproj` and ensure the game DLLs and BepInEx core libraries are in the expected locations.

### IL2CPP Games

Because IL2CPP games compile C# code into native C++ code, **the game directory does not contain directly referenceable managed DLLs**. Therefore, before building IL2CPP game plugins from source, you must first dump the game's DLLs using a tool.

#### Step 0: Dump Game DLLs

Use [Il2CppDumper GUI](https://github.com/AndnixSH/Il2CppDumper-GUI) to dump `Assembly-CSharp.dll` and other DLLs along with `global-metadata.dat` from the IL2CPP game.

> **Tool Download**: [Il2CppDumper GUI Releases](https://github.com/AndnixSH/Il2CppDumper-GUI/releases)
>
> **Usage**:
> 1. Download and run Il2CppDumper GUI.
> 2. Drag the game's binary file (e.g., `GameAssembly.dll`) and `GameName_Data/il2cpp_data/Metadata/global-metadata.dat` into the program.
> 3. Click Start to begin dumping.
> 4. After dumping completes, the output DLLs are located in the `Dump/` directory.

> **Note**: The dumped DLLs are **DummyDlls** (containing only type and method signatures, no actual implementations) — used for compile-time references only, not for runtime. At runtime, BepInEx 6's Il2CppInterop generates the full interop assemblies.

#### Step 1: Install BepInEx 6 IL2CPP & Generate Interop DLLs

1. Install BepInEx 6 (IL2CPP) into the game directory and run the game once. BepInEx will automatically generate full interop assemblies in the `BepInEx/interop/` directory.

```
Game Root/
├── BepInEx/
│   ├── core/                # BepInEx core libraries (BepInEx.Core.dll, BepInEx.Unity.IL2CPP.dll, etc.)
│   ├── interop/             # ← Auto-generated interop assemblies (after running the game once)
│   │   ├── Assembly-CSharp.dll
│   │   ├── UnityEngine.dll
│   │   ├── UnityEngine.CoreModule.dll
│   │   └── ...
│   └── plugins/
├── GameAssembly.dll
├── GameName_Data/
└── GameName.exe
```

#### Step 2: Place Source & Build

1. Copy the IL2CPP game's source folder into the game root directory (same level as `BepInEx/`).
2. Build:

```bash
# Example: Granny
cd "Game Root/Granny"
dotnet build -c Release
```

3. The IL2CPP project's `.csproj` is configured to automatically copy the output to `BepInEx/plugins/` after build — no manual action needed.

> **Tip**: The IL2CPP project's `.csproj` uses reference paths `..\BepInEx\core\` and `..\BepInEx\interop\`, so the source folder must be in the game root directory, at the same level as `BepInEx/`. If the `interop/` directory doesn't exist, run the game once first to let BepInEx generate the interop assemblies.

## Project Structure

```
UnityGameModMenu/
├── MonoGames/                          # Mono backend games
│   ├── Bendy and the Dark Revival/
│   │   ├── BATDRCheatMenu.csproj
│   │   └── BATDRCheatMenuPlugin.cs
│   ├── SchoolBoyRunAway/
│   │   ├── ModMenu.csproj
│   │   ├── ModMenuPlugin.cs
│   │   ├── GameHelpers.cs
│   │   └── PossessionController.cs
│   └── Sniper3D/
│       ├── Sniper3D_Trainer.csproj
│       └── Sniper3D_Trainer.cs
├── Li2CPPGames/                        # IL2CPP backend games
│   └── Granny/
│       ├── GrannyModMenu.csproj
│       ├── Plugin.cs                   # Plugin entry (IL2CPP component injection)
│       ├── ModMenuBehavior.cs          # Menu UI & feature logic
│       └── ModState.cs                 # Global mod state
├── LICENSE
└── README.md
```

## Disclaimer

- This project is for **educational and research purposes only**, aimed at exploring Unity game internals and BepInEx plugin development.
- Do **not** use in multiplayer/online modes to avoid affecting other players' experience.
- Users are solely responsible for any consequences such as account bans or data corruption resulting from the use of these plugins.
- This project is not affiliated with any game developer. All game names and trademarks belong to their respective owners.

## License

This project is licensed under the [MIT License](./LICENSE) — free to use, modify, and distribute.
