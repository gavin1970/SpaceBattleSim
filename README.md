# DynamicTimeDraw — Space Battleground Simulation

A pure **.NET 8 / WinForms** battlefield simulation that demonstrates how to build real-time 2-D graphics **without any third-party rendering libraries**. Everything you see — ships, lasers, tow-beams, the grid, shadow effects, and colour blending — is drawn directly with `System.Drawing` (`Graphics`, `Pen`, `Brush`, `Font`, Unicode symbols).

---

## Table of Contents

- [What It Is](#what-it-is)
- [Features](#features)
- [Ship Types and Stats](#ship-types-and-stats)
- [Fleet Configuration](#fleet-configuration)
- [Revive / Recovery System](#revive--recovery-system)
- [Architecture](#architecture)
  - [Thread-Safe State](#thread-safe-state)
  - [TowRig Assignment Dictionary](#towrig-assignment-dictionary)
- [Controls (Keyboard)](#controls-keyboard)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [Dependencies](#dependencies)
- [Contributing](#contributing)

---

## What It Is

**DynamicTimeDraw** has no interaction except to look at stats.  It spawns a configurable fleet of spaceships dynamically on a dark grid and lets them fight autonomously. Ships move randomly across the canvas, detect enemies inside their *hitbox radius*, fire lasers, take damage, and either die or get towed home by a healer. The entire simulation runs with no game engine or graphics framework — it is a showcase of raw WinForms `OnPaint` / `Timer`-driven rendering.

![Battleground full-screen view](imgs/full_screen_view.png)

> Can run by itself or with project in Visual Studio.  Press (F5) at any time to respawn all the dead.  This will happen automatically within 30 seconds of all healers or all raiders being destroyed, but you can also trigger it manually with F5. The F1 and F2 keys show different levels of ship info overlays.

> AI keeps telling me to do things differently in some places, but any time it's done, it destorys the simulation and rendering doesn't work anymore.  So I have to keep it the way it is, even if it's not how I would do it if I were writing it from scratch.  It's a bit of a mess as there are things I need to break up into other classes/methods, but it works, it's fast, and that's the point of the project — to show how to build a real-time simulation with pure `System.Drawing` without any game engine or rendering framework.

---

## Features

- **Flawless and Pure `System.Drawing` rendering** — no Unity, MonoGame, SkiaSharp, or similar.
- **100+ ship fleet** — configurable via constants in `BgPlatform.cs`.
- **4 active ship classes** — TowRig/Healer, Capital Ship, Fighter, Raider — each with unique stats and behaviour.
- **Per-ship independent threads** — every ship runs its AI loop on its own background thread.
- **Thread-safe shared state** — a `ConcurrentDictionary` tracks every ship's current position and health, readable by all threads simultaneously.
- **Conflict-free tow assignments** — a second thread-safe dictionary ensures only one TowRig claims a dead ally at a time.
- **Dynamic colour health indicator** — ship colour shifts as shields drop (green → yellow → orange → red).
- **Laser and tow-beam rendering** — red laser lines for attacks, blue tow-beam lines for recovery.
- **F-key HUD overlays** — press F1/F2 to view live ship stats; press F5 to instantly revive all dead ships.
- **Unicode ship symbols** — each class is rendered as a distinct Unicode glyph using the Arial font.
- **Transparent-background mode** — toggle `_transparentBG` to make the grid background transparent.

---

## Ship Types and Stats

All values are defined in `DynamicTimeDraw/models/ships/ShipStats.cs`.

| Ship Type | Shields | Power | Speed | Hitbox | Recovery Priority | Notes |
|-----------|---------|-------|-------|--------|-------------------|-------|
| **TowRig** (Healer) | 400 | 1 | 2.0 | 20 px | **Critical** (1st) | Smallest hitbox, fastest; sole purpose is recovery |
| **Capital Ship** | 800 | 8 | 0.3 | 75 px | **High** (2nd) | Slowest, highest durability; half the power of a Raider |
| **Fighter** | 200 | 4 | 1.0 | 50 px | **Low** (3rd) | Balanced grunt unit; home-team protector |
| **Raider** (Enemy) | 400 | 16 | 1.0 | 50 px | **None** | Twice Capital Ship power; **never revived** when destroyed |
| *Bomber* | 400 | 6 | 0.5 | 60 px | Medium | *Reserved — not currently deployed* |
| *Transport* | 2000 | 0 | 2.0 | 40 px | Low | *Reserved — not currently deployed* |

> **Raider vs Capital comparison:** Raiders carry twice the firepower (16 vs 8) but only half the shields (400 vs 800), making them glass-cannon enemies.

---

## Fleet Configuration

Default counts are set in `BgPlatform.cs`:

```csharp
const int _flierCount    = 100;               // Total Fighters + Raiders
const int _capShipCount  = _flierCount / 10;  // 10 Capital Ships
const int _towRigCount   = _flierCount / 10;  // 10 TowRigs / Healers
```

The `_flierCount` is split so that **Raiders outnumber Fighters by roughly 3:1**, creating strong enemy pressure that the 10 Healers and 10 Capital Ships must balance. The result is a tight, fluctuating battle where neither side easily dominates.

| Group | Default Count |
|-------|---------------|
| Raiders (enemy) | ~75 |
| Fighters (home team) | ~25 |
| Capital Ships | 10 |
| TowRigs / Healers | 10 |
| **Total ships** | **~120** |

---

## Revive / Recovery System

When a home-team ship's shields reach zero it enters the `Dead` state. TowRigs scan for dead allies and tow them back to HomeBase for revival. The order in which they are prioritised is driven by the `RecoverOrder` enum:

| Priority | Value | Ship |
|----------|-------|------|
| Critical | 4 | TowRig / Healer — revived first |
| High | 3 | Capital Ship — revived second |
| Low | 1 | Fighter — revived last |
| None | 0 | Raider — **permanent death, never revived** |

TowRigs are revived first so the recovery pipeline never collapses. Capital Ships follow because of their high combined shield and firepower value. Fighters are last. Raiders are never revived — when destroyed they are gone for the remainder of the session.

---

## Architecture

### Thread-Safe State

Each `SpaceShip` instance runs its AI on an independent background thread. A shared `ConcurrentDictionary<string, SpaceShip>` (keyed by ship name) allows every thread to read the current position and health of any other ship without locking. This is the foundation of hit-detection and targeting.

```
BgPlatform (UI Thread)
  └── Timer ──► Invalidate() ──► OnPaint()
                    └── Iterates ConcurrentDictionary ──► draws each ship

SpaceShip Thread (× N)
  └── Move ──► scan hitbox ──► attack / tow ──► update shared dictionary entry
```

### TowRig Assignment Dictionary

To prevent multiple TowRigs from all rushing the same dead ship, a second thread-safe dictionary records *in-progress* tow assignments. Before a TowRig claims a target it performs an atomic check-and-add. If another TowRig already holds that entry the current TowRig skips it and looks for the next unclaimed dead ally.

---

## Controls (Keyboard)

| Key | Action |
|-----|--------|
| **F1** | Show detailed ship info and stats overlay (hold to keep visible) |
| **F2** | Show summary ship status overlay (hold to keep visible) |
| **F5** | Revive all currently dead home-team ships |
| Hover over title bar | Reveals the close button |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows OS (WinForms is Windows-only)
- Visual Studio 2022 / 2026 **or** the `dotnet` CLI

### Run via Visual Studio

Open `DynamicTimeDraw.sln` and press **F5**.

### Run via CLI

```powershell
cd DynamicTimeDraw
dotnet run
```

---

## Project Structure

```
DynamicTimeDraw/
├── BgPlatform.cs              # Main form: rendering loop, fleet init, keyboard handling
├── BgPlatform.Designer.cs     # Designer-generated form code
├── Program.cs                 # Entry point
├── models/
│   ├── ships/
│   │   ├── ShipEnums.cs       # ShipType, ShipStatus, ShipMission, RecoverOrder enums
│   │   ├── ShipStats.cs       # Read-only per-type stat definitions
│   │   └── SpaceShip.cs       # Per-ship state, AI loop, hit-detection, rendering data
│   ├── DRectangleF.cs         # Extended RectangleF base for positioned objects
│   ├── DLine.cs               # Line drawing model (laser / tow beams)
│   ├── DText.cs               # Text rendering model
│   └── ItemReq.cs             # Paint request wrapper
├── services/
│   ├── ColorConvert.cs        # Colour blending / damage-colour utilities
│   └── Logger.cs              # Simple file/console logger
└── utils/
    ├── StaticConfig.cs        # Global UI style constants (colours, pens, brushes, fonts)
    ├── DDefaults.cs           # Drawing defaults (shadow, border, laser pens)
    ├── About.cs               # Assembly version helper
    └── atomic/
        ├── ABool.cs           # Atomic boolean (thread-safe flag)
        ├── ADateTime.cs       # Atomic DateTime (thread-safe timestamp)
        └── EventStatus.cs     # Named event flag dictionary
```

---

## Dependencies, no external libraries

| Package | Purpose | Found in... |
|---------|---------|-------------|
| `Chizl.ThreadSupport` | Atomic primitives (`ABool`, `ADateTime`, `EventStatus`) for lock-free thread safety | [`DynamicTimeDraw/utils/atomic/`](DynamicTimeDraw/utils/atomic/) |
| `Chizl.ColorExtension` | Colour manipulation helpers used during dynamic ship colour merging | [`DynamicTimeDraw/services/ColorConvert.cs`](DynamicTimeDraw/services/ColorConvert.cs) |
| `Chizl.Applications` | Application metadata helpers (`About`) | [`DynamicTimeDraw/utils/About.cs`](DynamicTimeDraw/utils/About.cs) |
| `Chizl.IO.Logging` | Async Chizl.TextLogger (DLL) | [`github.com`](https://github.com/gavin1970/Chizl.IO.Logging) |

> All dependencies are first-party [Chizl](https://github.com/gavin1970) libraries — no third-party game engines or rendering frameworks are used.

---

## Contributing

Pull requests are welcome. If you would like to add a new ship class, enable the reserved *Bomber* or *Transport* types, or improve the rotation logic for Raiders, please open an issue first to discuss the change.

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-change`
3. Commit your changes
4. Push and open a Pull Request against `master`

---

*Built with love and pure `System.Drawing` — no game engine required.*
