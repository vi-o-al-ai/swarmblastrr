# Swarmblastrr

A mobile **crowd runner** (Count Masters / Mob Control style) built as a Unity 6 scaffold.
A blob of units runs down a lane; gates add or multiply the count, enemy crowds subtract it,
a boss at the end has an HP number the crowd must beat, and a reward ramp after the boss
multiplies the coins you earn. Between runs you spend coins on permanent upgrades.

> **Status: scaffold, not yet opened in a Unity editor.** Every file in this repository was
> written against the API contract in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and
> checked for C# syntax, but it has **not** been compiled by Unity, played, or profiled yet.
> Expect to fix a handful of compile errors or runtime warnings on the first import. If
> something is wrong, the contract says what the code is *supposed* to do.

## What is in the box

- One scene, zero prefabs. `Assets/Scenes/Main.unity` contains a single GameObject with a
  `GameBootstrap` component; it builds the camera, light, managers, player crowd, level and UI
  from code at startup. All visuals are primitive meshes plus runtime materials.
- A data-oriented crowd: unit positions live in native arrays, a Burst-compiled flocking job
  moves them, and `Graphics.RenderMeshInstanced` draws them. Thousands of units on a phone
  without thousands of GameObjects.
- Deterministic procedural levels (`LevelGenerator`), a tiny meta loop (coins, three
  permanent upgrades, JSON save file) and legacy uGUI screens (HUD, shop, result).
- An edit-mode test assembly (`Swarmblastrr.Tests.EditMode`, NUnit) with seven test classes
  covering the pure math (gates, upgrades, economy, saves, level generation, crowd simulation
  and layout). See [Running the tests](#running-the-tests).

## Requirements

| Item | Value |
|---|---|
| Unity | **6000.0.50f1** is pinned in `ProjectSettings/ProjectVersion.txt`. Any **6000.x** works; 2022.3 LTS should compile with minor changes. |
| Render pipeline | **Built-in**. No URP/HDRP setup, no pink materials. |
| Packages | Burst 1.8, Collections 2.5, Mathematics 1.3, Input System 1.11, Test Framework 1.4, uGUI 2.0 (resolved automatically from `Packages/manifest.json`). |
| Platforms | Designed for Android/iOS portrait; runs in the editor Game view with a mouse. |

## Opening the project

1. Install a Unity 6000.x editor through **Unity Hub** (the Android and/or iOS build modules
   are only needed for device builds).
2. In Unity Hub choose **Add → Add project from disk** and pick this folder.
3. If the Hub says the pinned editor version is missing, pick **any 6000.x** editor from the
   version dropdown and let Unity upgrade the project. Accept the "Opening Project in
   Non-Matching Editor Installation" dialog.
4. The first import takes a few minutes: packages resolve, Burst compiles, and Unity generates
   `.meta` files for everything that lacks one (very few are committed; the important one is
   `GameBootstrap.cs.meta`, whose GUID the scene references).
5. If Unity asks to **enable the new Input System backends and restart**, either answer is
   fine. The input code compiles with both `ENABLE_INPUT_SYSTEM` and the legacy input manager.

## Playing

- Open `Assets/Scenes/Main.unity` and press **Play**.
- If the scene is missing or will not open, use the menu **Swarmblastrr → Create Demo Scene**.
  It creates an empty scene with a `Bootstrap` GameObject, saves it to
  `Assets/Scenes/Main.unity` and adds it to Build Settings.
- **Swarmblastrr → Reset Save Data** deletes `save.json` from `Application.persistentDataPath`
  (coins, level progress and upgrades).

### Controls

| Action | Editor / desktop | Device |
|---|---|---|
| Steer the crowd left/right | Hold the left mouse button and drag horizontally | Touch and drag horizontally |
| Start a run | Tap **PLAY** in the shop | same |
| Buy an upgrade | Tap the cost button on a shop row | same |
| Continue after a run | Tap **NEXT** (won) or **RETRY** (lost) | same |

Only horizontal drag matters: the crowd runs forward by itself. Its centre follows the drag
(a full screen-width drag moves it about `2 × LaneHalfWidth × SwipeInput.Sensitivity` world
units) and is clamped to the lane.

### The loop

1. **Shop** – the current level is already built behind the UI. Spend coins, then press PLAY.
2. **Running** – gates come in left/right pairs (`+5`, `×3`, `−10`, `÷2`); steer into the
   better side. Enemy crowds trade units with you one-for-one over time.
3. **Boss fight** – the boss has an HP number. Every unit that touches it is consumed and
   deals `UnitDamage` damage. Beat the HP before you run out of units.
4. **Reward ramp** – surviving units carry you up a ramp of `×N` tiles (every `UnitsPerTile`
   units reach one tile further). The tile you stop on multiplies the coin reward.
5. **Won / Lost** – coins are banked, the level index advances on a win, and you are back in
   the shop.

## Architecture in one screen

Full, binding detail (every public type and signature) lives in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). The short version:

```
Assets/
  Scripts/                      Swarmblastrr.Runtime assembly
    Core/      GameBootstrap, GameManager, GameConfig, GameEvents, GameState, CameraFollow
    Crowd/     CrowdSimulation (+ Burst CrowdJobs), CrowdRenderer, CrowdController, CrowdLayout
    Track/     TrackElement → Gate, EnemyCrowd, Boss, RewardRamp; GateMath; TrackVisuals
    Level/     LevelDefinition, LevelGenerator, LevelBuilder, LevelRunner
    Meta/      UpgradeCatalog/Math/Manager, Economy, SaveData, SaveSystem, ISaveStore
    UI/        UiFactory, HUD, UpgradeShopUI, ResultScreen, FloatingText
    Controls/  SwipeInput
  Resources/Shaders/            CrowdInstanced, Solid, SolidTransparent (built-in pipeline)
  Editor/                       Swarmblastrr.Editor assembly: DemoSceneBuilder (menu items)
  Tests/EditMode/               Swarmblastrr.Tests.EditMode assembly (NUnit; seven test classes)
  Scenes/Main.unity             one GameObject: Bootstrap (GameBootstrap)
docs/ARCHITECTURE.md            the contract everything above is written against
```

Dependency direction: every folder may reference `Core`; `Track → Crowd`; `Level → Track,
Crowd`; `UI → Core, Meta`; only `GameManager`/`GameBootstrap` reach into everything. Systems
talk through static C# events on `GameEvents` (`StateChanged`, `CountChanged`, `GateHit`,
`CoinsChanged`, `BossDamaged`, `LevelFinished`, ...). There is **no physics**: `LevelRunner`
polls `TrackElement.Overlaps(player)` every frame for the elements near the crowd.

Frame flow while running:

```
SwipeInput.Update (order -100)  →  CrowdController.Update        →  LevelRunner.Update
  reads pointer, sets DeltaX        moves centre, schedules jobs       Overlaps()/OnCrowdOverlap()
                                 →  CrowdController.LateUpdate   →  CrowdRenderer.Render
                                    Complete() the jobs               RenderMeshInstanced, 1023 per call
```

## How the crowd stays cheap

- **Data, not GameObjects.** A crowd is a `NativeArray<float2>` of local XZ offsets plus a
  velocity array inside `CrowdSimulation`. The player crowd is one GameObject; each enemy crowd
  is one GameObject.
- **Burst jobs.** `BuildHashJob` (single-threaded `IJob`) buckets units into a
  `NativeParallelMultiHashMap` spatial hash; `FlockJob` (`IJobParallelFor`) does separation
  against the 3×3 neighbouring cells, cohesion toward the crowd centre, damping, integration
  and lane clamping. Input and output position buffers are separate and swapped after
  `Complete()`, so the parallel job never aliases.
- **GPU instancing.** `CrowdRenderer` fills cached `Matrix4x4[1023]` chunks and calls
  `Graphics.RenderMeshInstanced` with shadows off and a large explicit `worldBounds`. The
  `Swarmblastrr/CrowdInstanced` shader is a one-pass half-Lambert with a per-instance vertex
  bob, so the whole crowd is a handful of draw calls and no animators.
- **Logical vs simulated count.** The score can reach `GateMath.MaxCount` (1,000,000) but only
  `GameConfig.MaxSimulatedUnits` (default 3000) are simulated and drawn. Gates, fights and
  rewards use the logical count.
- **No colliders, no rigidbodies, no NavMesh.** Interaction is a distance test per nearby
  track element per frame.

## Tuning knobs

Everything lives in `GameConfig` (a `ScriptableObject`). By default `GameBootstrap` uses
`GameConfig.CreateDefault()`; to tune without code, **Assets → Create → Swarmblastrr → Game
Config**, edit the asset, and drop it on the `Bootstrap` object's **Config Override** field.

| Field | Default | Effect |
|---|---|---|
| `ForwardSpeed` | 8 | Run speed along +Z (units/s) |
| `LateralSpeed` | 10 | How fast the crowd centre chases the finger |
| `LaneHalfWidth` | 3 | Half the lane width; gates sit at ±half of this |
| `UnitRadius` | 0.22 | Separation radius and spatial-hash cell size (×2) |
| `UnitScale` | 0.45 | Uniform mesh scale of one unit |
| `MaxSimulatedUnits` | 3000 | Hard cap on simulated/rendered units (biggest perf lever) |
| `SeparationStrength` / `CohesionStrength` / `Damping` | 25 / 6 / 8 | Flocking feel |
| `EnemyKillRate` | 40 | Units per second exchanged in a crowd-vs-crowd fight |
| `BossDrainRate` | 60 | Player units per second consumed by the boss |
| `CoinsPerSurvivingUnit` / `LevelClearBonus` | 1 / 50 | Economy |
| `PlayerColor` / `EnemyColor` / `BossColor` | blue / red / gold | Tints |
| `CrowdMaterialOverride` / `UnitMeshOverride` | none | Swap the look without touching code |

Other places to tune:

- `Meta/UpgradeCatalog.cs` – base values, per-level gains and cost curves of the three
  upgrades (`StartingUnits`, `UnitDamage`, `CoinIncome`).
- `Level/LevelGenerator.cs` – gate count, spacing, enemy sizes and boss HP as functions of the
  level index; deterministic from `(levelIndex, seed)`.
- `Meta/Economy.cs` – win/lose coin formulas.
- `Track/RewardRamp.cs` – tile multipliers (`1,2,3,4,5,8,10,20,50,100`) and `UnitsPerTile`.

## Running the tests

`Assets/Tests/EditMode/` holds seven NUnit edit-mode test classes in the
`Swarmblastrr.Tests.EditMode` assembly (editor-only; references `Swarmblastrr.Runtime`,
Collections, Mathematics and Burst). They cover only pure code, so no scene or `GameManager`
is needed:

| Test class | What it pins down |
|---|---|
| `GateMathTests` | every gate operation, clamping at 0 and `MaxCount`, divide-by-zero, labels |
| `UpgradeMathTests` | geometric cost curve, linear value curve, max level, no overflow |
| `EconomyTests` | win and lose rewards, income multiplier, clamping |
| `SaveDataTests` | round trip through an in-memory `ISaveStore`, corrupt JSON, `EnsureArrays` |
| `LevelGeneratorTests` | determinism, ascending Z, one boss then one ramp, length, difficulty growth |
| `CrowdSimulationTests` | 500 units x 120 steps: no NaN, stays in lane, no overlap, grow/shrink, dispose |
| `CrowdLayoutTests` | distinct spawn offsets, radius grows with sqrt(n) |

Open **Window → General → Test Runner**, select the **EditMode** tab and press **Run All**.
The crowd tests schedule real Burst jobs, so the first run compiles them and takes a few
seconds longer.

## Building for a device

Use the menu **Swarmblastrr → Create Demo Scene** once so `Main.unity` is registered in
**File → Build Profiles** (Build Settings), switch platform to Android or iOS, set the
orientation to portrait, and build. No platform-specific code exists yet.

## Roadmap

- **URP migration** – move the three shaders to Shader Graph / URP and enable SRP Batcher.
- **Skins** – alternate unit meshes and materials unlocked with coins (the override fields in
  `GameConfig` are the hook).
- **Rewarded ads** – double the end-of-run multiplier or revive after a loss.
- **Procedural levels, phase 2** – biomes, curves and vertical variety on top of the current
  deterministic generator.
- **VAT animation** – vertex-animation textures so instanced units can run, cheer and die
  without animators.
- **Polish** – audio, haptics, hit stops, better camera shake, a proper main menu.

## Honest gaps

- Not yet compiled or run in Unity; the first import will probably surface small issues.
- Only two `.meta` files are checked in (the bootstrap script and the scene, whose GUIDs the
  scene references). Unity generates the rest on first import; commit them so GUIDs stay stable
  across machines.
- The edit-mode tests have been written against the source but, like the rest of the
  project, have not yet been executed inside a Unity editor.
- `ProjectSettings/EditorBuildSettings.asset` is not committed, so the scene must be added to
  Build Settings once (the Create Demo Scene menu item does it).
- Lighting is flat and runtime-generated; there is no baked data and no post-processing.
