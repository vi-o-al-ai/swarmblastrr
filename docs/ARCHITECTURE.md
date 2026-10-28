# Swarmblastrr — architecture contract

Swarmblastrr is a mobile "crowd runner" (Count Masters / Mob Control style). A blob of
units runs forward down a lane. Gates add or multiply the count, enemy crowds subtract it,
a boss at the end has an HP number the crowd must beat, and a reward ramp after the boss
multiplies the coins earned. Between runs the player spends coins on permanent upgrades.

This file is the **binding contract** between subsystems. Every public type, member and
signature listed here must exist exactly as written. Private helpers may be added freely.
If something here is impossible or wrong, change the implementation to match the contract's
*intent* and record the deviation in the "Deviations" section at the bottom of this file.

## Targets and conventions

| Topic | Decision |
|---|---|
| Unity version | Unity 6 (6000.0 LTS). Code must also compile on 2022.3 LTS where cheap. |
| Render pipeline | **Built-in** (zero setup, no pink materials). URP migration is a later step. |
| Packages | Burst 1.8, Collections 2.5, Mathematics 1.3, Input System 1.11, uGUI (legacy `Text`, not TMP). |
| Assemblies | `Swarmblastrr.Runtime` (Assets/Scripts), `Swarmblastrr.Editor` (Assets/Editor), `Swarmblastrr.Tests.EditMode` (Assets/Tests/EditMode). |
| Root namespace | `Swarmblastrr`. Sub-namespaces per folder: `Core`, `Crowd`, `Track`, `Level`, `Meta`, `UI`, `Controls`. **Never** name a namespace `Input` (it shadows `UnityEngine.Input`). |
| Coordinate frame | Lane runs along **+Z**. Lateral axis is **X**. Ground is **Y = 0**. Units stand on the ground. |
| Lane | Half-width is `GameConfig.LaneHalfWidth` (default 3). Track length comes from the `LevelDefinition`. |
| Object finding | Use `Object.FindFirstObjectByType<T>()`, never the obsolete `FindObjectOfType`. |
| Randomness | Gameplay generation uses `System.Random` seeded deterministically. Never `UnityEngine.Random` in pure code. |
| Scene wiring | **None required.** One `GameBootstrap` component in the scene creates everything else from code. All visuals are built from primitive meshes and runtime materials so the project runs with zero prefabs. |
| Fonts | `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`. |
| Primitive meshes | `Resources.GetBuiltinResource<Mesh>("Capsule.fbx")`, `"Cube.fbx"`, `"Sphere.fbx"`, `"Quad.fbx"`, `"Cylinder.fbx"`. |
| Shaders | Custom shaders live in `Assets/Resources/Shaders/` so they are always included in builds. Load with `Shader.Find("Swarmblastrr/CrowdInstanced")`. Fallback is `Shader.Find("Standard")` with `material.enableInstancing = true`. |
| Input | `#if ENABLE_INPUT_SYSTEM` → `UnityEngine.InputSystem` (`Pointer.current`); `#else` → legacy `UnityEngine.Input`. Both branches must compile. |
| Null checks | Unity objects: `if (obj == null)` (Unity overload). Never `?.` / `??` on `UnityEngine.Object` references. |
| Events | Static C# events on `GameEvents`. Every subscriber unsubscribes in `OnDestroy`/`OnDisable`. |
| Count ceiling | `GateMath.MaxCount = 1_000_000`. Logical count is an `int` clamped to `[0, MaxCount]`. |
| Allocators | `Allocator.Persistent` for crowd arrays; dispose in `Dispose()`; owners call it from `OnDestroy`. |
| Code style | 4-space indent, `PascalCase` public, `_camelCase` private fields, one type per file, file name == type name, XML doc comment on every public type. No `#region`. |

## Dependency direction

```
Controls  ─┐
Crowd     ─┤
Track     ─┼─► Core (GameConfig, GameEvents, GameState) ◄── everything may reference Core
Level     ─┤
Meta      ─┤
UI        ─┘

Track  → Crowd (EnemyCrowd reuses CrowdSimulation + CrowdRenderer; TrackElement reads CrowdController)
Level  → Track, Crowd
Core.GameManager / Core.GameBootstrap → everything (they are the composition root)
UI     → Core, Meta (reads state through events and the managers)
Tests  → pure helpers only (GateMath, UpgradeMath, LevelGenerator, Economy, SaveData, CrowdSimulation)
```

Nothing except `GameManager` and `GameBootstrap` may hold references into more than two other
namespaces.

---

## Core (`Assets/Scripts/Core`, namespace `Swarmblastrr.Core`)

### `GameState.cs`
```csharp
public enum GameState { Boot, Shop, Running, BossFight, RewardRamp, Won, Lost }
```

### `LevelResult.cs`
```csharp
[System.Serializable]
public struct LevelResult
{
    public bool Won;
    public int LevelIndex;
    public int UnitsRemaining;
    public int RewardMultiplier;   // from the RewardRamp, 1 if none reached
    public int CoinsEarned;
}
```

### `GameConfig.cs` — `ScriptableObject`, `[CreateAssetMenu(menuName = "Swarmblastrr/Game Config")]`
All tunables. Fields are public and serialized with sensible defaults:
```csharp
public float ForwardSpeed = 8f;          // units per second along +Z
public float LateralSpeed = 10f;         // how fast the crowd centre follows the finger
public float LaneHalfWidth = 3f;
public float UnitRadius = 0.22f;         // visual + separation radius
public float UnitScale = 0.45f;          // uniform mesh scale for one unit
public int   MaxSimulatedUnits = 3000;   // hard cap on individually simulated/rendered units
public float SeparationStrength = 25f;
public float CohesionStrength = 6f;
public float Damping = 8f;
public float EnemyKillRate = 40f;        // units per second exchanged in a crowd-vs-crowd fight
public float BossDrainRate = 60f;        // player units per second consumed by the boss
public int   CoinsPerSurvivingUnit = 1;
public int   LevelClearBonus = 50;
public Color PlayerColor = new Color(0.35f, 0.55f, 1f);
public Color EnemyColor  = new Color(1f, 0.3f, 0.25f);
public Color BossColor   = new Color(0.85f, 0.7f, 0.45f);
public Material CrowdMaterialOverride;   // optional
public Mesh UnitMeshOverride;            // optional

public static GameConfig CreateDefault();   // ScriptableObject.CreateInstance with the defaults above
```

### `GameEvents.cs` — `public static class GameEvents`
```csharp
public static event Action<GameState> StateChanged;
public static event Action<int, int> CountChanged;              // (previous, current) player count
public static event Action<GateOperation, int, int, Vector3> GateHit;   // op, before, after, worldPos
public static event Action<int> CoinsChanged;                   // new total
public static event Action UpgradesChanged;
public static event Action<int, int> BossDamaged;               // (hp, maxHp)
public static event Action<LevelResult> LevelFinished;          // won or lost

// Raise helpers (null-safe): RaiseStateChanged(..), RaiseCountChanged(..), RaiseGateHit(..),
// RaiseCoinsChanged(..), RaiseUpgradesChanged(), RaiseBossDamaged(..), RaiseLevelFinished(..)
public static void ClearAll();   // sets every event to null; called by GameManager.OnDestroy and tests
```
`GateOperation` lives in `Swarmblastrr.Track`; Core may reference it (it is a plain enum).

### `GameManager.cs` — `MonoBehaviour`, singleton
```csharp
public static GameManager Instance { get; }
public GameConfig Config { get; }
public GameState State { get; }
public int CurrentLevelIndex { get; }      // 0-based, persisted in SaveData
public CrowdController Player { get; }
public LevelBuilder LevelBuilder { get; }
public LevelRunner LevelRunner { get; }
public UpgradeManager Upgrades { get; }
public CameraFollow Camera { get; }

/// Called once by GameBootstrap after all components exist.
public void Initialize(GameConfig config, CrowdController player, LevelBuilder builder,
                       LevelRunner runner, UpgradeManager upgrades, CameraFollow camera);

public void EnterShop();           // State = Shop. Builds (or rebuilds) the current level so it is visible behind the shop, resets the player crowd to the upgraded starting size, camera at start.
public void StartLevel();          // State = Running. Player starts moving. Only valid from Shop.
public void EnterBossFight();      // State = BossFight. Called by Boss when reached.
public void EnterRewardRamp();     // State = RewardRamp. Called by Boss when it dies.
public void Win(int rewardMultiplier);   // State = Won. Computes LevelResult via Economy, adds coins, advances CurrentLevelIndex, saves, raises LevelFinished.
public void Lose();                      // State = Lost. Computes LevelResult (Won=false, CoinsEarned = Economy consolation), saves, raises LevelFinished.
public void RestartLevel();        // From Won/Lost → EnterShop() (level index already advanced if won).
```
Rules: `StartLevel` must set `Player.IsRunning = true` and call `LevelRunner.Begin(...)`. Every state
change goes through a private `SetState(GameState)` that raises `GameEvents.StateChanged`.
`Win` and `Lose` must both be idempotent (ignore if State is already Won/Lost). Its only
subscription is `GameEvents.UpgradesChanged`: while `State == Shop` it re-applies
`Upgrades.GetValueInt(StartingUnits)` to the player crowd so the preview behind the shop and the HUD
update the moment a level is bought. `OnDestroy` → unsubscribe, `GameEvents.ClearAll()`, clear `Instance`.

### `GameBootstrap.cs` — `MonoBehaviour`, the only thing a scene needs
```csharp
public GameConfig ConfigOverride;   // optional inspector field
private void Awake()  // creates, in order:
//  1. GameConfig (ConfigOverride or GameConfig.CreateDefault())
//  2. Main camera (if Camera.main == null: new GameObject("Main Camera") with Camera, tag MainCamera, AudioListener) + CameraFollow
//  3. Directional light (if none exists)
//  4. "Managers" GameObject: GameManager, UpgradeManager, LevelBuilder, LevelRunner, SwipeInput
//  5. "Player" GameObject: CrowdController (+ CrowdRenderer on the same object)
//  6. UI: UiFactory.CreateRootCanvas() then HUD, UpgradeShopUI, ResultScreen
//  7. GameManager.Initialize(...) then GameManager.EnterShop()
```
Must be safe to run with absolutely nothing else in the scene.

### `CameraFollow.cs` — `MonoBehaviour`
```csharp
public Transform Target;
public Vector3 Offset = new Vector3(0f, 9f, -11f);
public float LookAheadZ = 6f;
public float Smoothing = 6f;
public void SnapToTarget();   // no smoothing, used when the level is (re)built
```
Follows `Target.position.z` only (x stays at 0 so the whole lane is visible), looks at
`Target + (0, 0, LookAheadZ)`.

---

## Crowd (`Assets/Scripts/Crowd`, namespace `Swarmblastrr.Crowd`)

The crowd is **data, not GameObjects**. Positions are 2D offsets (`float2 x,z`) in the crowd's
local frame. One `CrowdSimulation` + one `CrowdRenderer` per crowd (player or enemy).

### `CrowdLayout.cs` — `public static class CrowdLayout`
```csharp
/// Deterministic spawn offset for unit i (sunflower / Vogel spiral), spacing in world units.
public static float2 SpawnOffset(int index, float spacing);
/// Approximate radius of a crowd of n units packed with the given spacing.
public static float Radius(int count, float spacing);
```

### `CrowdSimParams.cs`
```csharp
public struct CrowdSimParams
{
    public float DeltaTime;
    public float UnitRadius;
    public float LaneHalfWidth;        // local clamp: |x - LaneCenterX| <= LaneHalfWidth - UnitRadius. Enemies pass float.MaxValue.
    public float LaneCenterX;          // lane centre in local x (0 for enemies/tests; the player passes -centreX so the walls stay fixed in world space)
    public float SeparationStrength;
    public float CohesionStrength;
    public float Damping;
    public float2 LocalCenterOffset;   // where the crowd should gather, in local space (normally 0)
    public float CellSize;             // spatial hash cell size, = UnitRadius * 2
}
```

### `CrowdJobs.cs` — Burst jobs (internal to the namespace is fine, but `public` is simpler)
```csharp
[BurstCompile] public struct BuildHashJob : IJob
// fills NativeParallelMultiHashMap<int,int> from positions using CrowdJobs.Hash(cell)
[BurstCompile] public struct FlockJob : IJobParallelFor
// per unit: separation from neighbours in the 3x3 cells around it, cohesion toward LocalCenterOffset,
// velocity damping, integrate, clamp x to lane. Writes to a separate output positions array (no aliasing).
public static class CrowdJobs
{
    public static int2 Cell(float2 p, float cellSize);
    public static int Hash(int2 cell);   // e.g. unchecked(cell.x * 73856093) ^ (cell.y * 19349663)
}
```
Use `Unity.Mathematics` everywhere in jobs. `FlockJob` reads `PositionsIn` and writes
`PositionsOut` and `Velocities` (velocities indexed by the same unit only, so `IJobParallelFor` is safe).

### `CrowdSimulation.cs` — plain `class`, `IDisposable`
```csharp
public int Capacity { get; }          // allocated size
public int Count { get; }             // currently simulated units (<= Capacity)
public NativeArray<float2> Positions { get; }   // valid only after Complete()
public bool IsCreated { get; }

public CrowdSimulation(int capacity, float unitRadius);
public void SetCount(int count);      // grows: new units spawn at CrowdLayout.SpawnOffset(i, spacing) ; shrinks: drops the last units. Clamped to Capacity.
public JobHandle Schedule(CrowdSimParams p, JobHandle dependsOn = default);   // BuildHash then Flock; swaps buffers internally after Complete
public void Complete();               // completes the last scheduled handle; must be called before reading Positions or calling SetCount/Schedule again
public float Radius(float spacing);   // CrowdLayout.Radius(Count, spacing)
public void Dispose();
```
Spacing used for spawning is `unitRadius * 2.2f`. Must never throw if `Count == 0`.

### `CrowdRenderer.cs` — `MonoBehaviour`
```csharp
public Mesh Mesh;
public Material Material;
public float UnitScale = 0.45f;
public void Configure(Mesh mesh, Material material, float unitScale);
/// Render `count` instances at origin + (p.x, 0, p.y) for p in positions. Chunks of 1023 via Graphics.RenderMeshInstanced.
public void Render(NativeArray<float2> positions, int count, Vector3 origin);
```
Implementation notes: cache `Matrix4x4[]` chunks of 1023; `RenderParams` must set
`worldBounds` (a large `Bounds` centred on `origin`, e.g. size 200) or everything is culled;
`shadowCastingMode = Off`, `receiveShadows = false`. `Material.enableInstancing` must be
true (set it in `Configure`). `Render` is called by the owner every `LateUpdate`.

### `CrowdMaterials.cs` — `public static class CrowdMaterials`
```csharp
/// Returns a new instanced material tinted `color`. Uses "Swarmblastrr/CrowdInstanced" if present, else "Standard".
public static Material Create(Color color, Material overrideTemplate = null);
public static Mesh DefaultUnitMesh();   // Capsule.fbx builtin
```

### `CrowdController.cs` — `MonoBehaviour` (the **player** crowd)
```csharp
public int Count { get; }               // logical count, may exceed MaxSimulatedUnits
public bool IsRunning { get; set; }     // moves forward along +Z when true
public float TargetX { get; set; }      // desired centre x, clamped to the lane by the controller
public float ForwardLimitZ { get; set; } // crowd centre never advances past this Z while running (never moves backwards); +Infinity = no limit; reset by ResetTo
public Vector3 Position => transform.position;
public float FrontZ { get; }            // Position.z + Radius
public float Radius { get; }            // CrowdLayout.Radius(min(Count, MaxSimulated), spacing)
public CrowdSimulation Simulation { get; }

public void Configure(GameConfig config, SwipeInput input);   // creates simulation (capacity = config.MaxSimulatedUnits), renderer material/mesh in PlayerColor
public void ResetTo(Vector3 position, int count);             // teleport, set count, IsRunning = false, TargetX = 0
public void SetCount(int count);                               // clamps to [0, GateMath.MaxCount], raises GameEvents.CountChanged(prev, cur), syncs simulation to min(count, MaxSimulated)
public int  AddUnits(int amount);                              // returns new count
public int  RemoveUnits(int amount);                           // returns number actually removed
public void ApplyGate(GateOperation op, int value, Vector3 worldPos);   // SetCount(GateMath.Apply(...)) + GameEvents.RaiseGateHit
```
Per frame: read `input.DeltaX` → `TargetX`; lerp `transform.position.x` toward `TargetX`
at `LateralSpeed`; if `IsRunning` add `ForwardSpeed * dt` to z; schedule simulation step;
in `LateUpdate` complete and render. If `Count` hits 0 while running → `GameManager.Instance.Lose()`.

---

## Track (`Assets/Scripts/Track`, namespace `Swarmblastrr.Track`)

Things placed along the lane. **No physics**: `LevelRunner` polls `Overlaps(player)` each frame.

### `GateOperation.cs`
```csharp
public enum GateOperation { Add, Subtract, Multiply, Divide }
```

### `GateMath.cs` — `public static class GateMath` (pure, tested)
```csharp
public const int MaxCount = 1_000_000;
public static int Apply(int count, GateOperation op, int value);   // Add: count+value; Subtract: count-value; Multiply: count*value (long math); Divide: value<=0 ? count : count/value. Result clamped to [0, MaxCount].
public static string Label(GateOperation op, int value);           // "+5", "−5" (U+2212), "×3", "÷2"
public static bool IsPositive(GateOperation op, int value);        // Add with value>0, Multiply with value>1
public static Color ColorFor(GateOperation op, int value);         // positive → blue (0.25,0.6,1), negative → red (1,0.3,0.3)
```

### `TrackElement.cs` — `public abstract class TrackElement : MonoBehaviour`
```csharp
public bool Consumed { get; protected set; }
public float Z => transform.position.z;
public float HalfWidth = 3f;                    // lateral extent, centre is transform.position.x
/// True when the player's crowd should interact with this element this frame.
public virtual bool Overlaps(CrowdController player);   // default: player.FrontZ >= Z && |player.Position.x - transform.position.x| <= HalfWidth + player.Radius * 0.5f
/// Called by LevelRunner every frame while Overlaps() is true and !Consumed.
public abstract void OnCrowdOverlap(CrowdController player, float deltaTime);
/// Build the visual children from primitives. Called by LevelBuilder after fields are set.
public abstract void BuildVisuals(GameConfig config);
```

### `Gate.cs` — `: TrackElement`, one-shot
```csharp
public GateOperation Operation;
public int Value;
public void Configure(GateOperation op, int value, float halfWidth);
```
`OnCrowdOverlap` → `player.ApplyGate(Operation, Value, transform.position)`, `Consumed = true`,
hide visuals (disable child renderers). Visual: a translucent quad/cube "wall" tinted
`GateMath.ColorFor`, with a world-space label (use a `TextMesh` component; it needs no font setup)
showing `GateMath.Label`. Gates always come in **pairs** (left/right halves of the lane); the
builder places two `Gate`s at x = ±LaneHalfWidth/2 with halfWidth = LaneHalfWidth/2. A gate's
`Overlaps` override must use the **player's centre x** against its own half-width (no radius
slack) so only one of the pair triggers.

### `EnemyCrowd.cs` — `: TrackElement`
```csharp
public int Count { get; }
public void Configure(int count, GameConfig config);   // creates its own CrowdSimulation (capacity min(count, MaxSimulated)) and a CrowdRenderer child in EnemyColor
```
Idle: schedules its simulation each frame with `LaneHalfWidth = float.MaxValue` so it mills in
place. `Overlaps`: circle-vs-circle in XZ using both radii. `OnCrowdOverlap`: `k = max(1,
round(EnemyKillRate * dt))`, `k = min(k, Count, player.Count)`; `player.RemoveUnits(k)`;
own count -= k; when own count reaches 0 → `Consumed = true`, destroy visuals/dispose. Disposes
simulation in `OnDestroy`.

### `Boss.cs` — `: TrackElement`
```csharp
public int MaxHp { get; }
public int Hp { get; }
public void Configure(int maxHp, GameConfig config);
```
Visual: a big scaled capsule in `BossColor` plus a `TextMesh` HP label. First overlap →
`GameManager.Instance.EnterBossFight()`. Each overlap frame: `n = max(1, round(BossDrainRate *
dt))`, `n = min(n, player.Count)`; `removed = player.RemoveUnits(n)`; `Hp -= removed *
unitDamage` where `unitDamage = GameManager.Instance.Upgrades.GetValueInt(UpgradeType.UnitDamage)`;
raise `GameEvents.BossDamaged(Hp, MaxHp)`. `Hp <= 0` → `Consumed = true`, hide visuals,
`GameManager.Instance.EnterRewardRamp()`. Player reaching 0 is handled by the controller (Lose).
`Overlaps` uses the default plus `HalfWidth = LaneHalfWidth` (bosses span the lane) until the fight
starts; from then on it returns true until the boss is consumed. On the first overlap the boss sets
`player.ForwardLimitZ = Z - BodyScale/2 - player.Radius/2` so the crowd is held against it for the
whole fight, and clears it (`float.PositiveInfinity`) when it dies.

### `RewardRamp.cs` — `: TrackElement`
```csharp
public int TileCount = 10;
public float TileLength = 2f;
public int UnitsPerTile = 20;          // every N surviving units lets the crowd reach one tile further
public int MultiplierForTile(int tile);   // 1,2,3,4,5,8,10,20,50,100 pattern: tile<5 ? tile+1 : {8,10,20,50,100}[tile-5] clamped
public void Configure(int tileCount, float tileLength, int unitsPerTile, GameConfig config);
```
Visual: `TileCount` flat tiles along +Z, each with a `TextMesh` "×N" label, hue rising with
multiplier. `OnCrowdOverlap`: the crowd keeps running; the reachable tile is
`min(TileCount-1, player.Count / UnitsPerTile)`; when `player.Position.z` passes the end of
that tile → `Consumed = true`, `player.IsRunning = false`,
`GameManager.Instance.Win(MultiplierForTile(tile))`. Also wins if the crowd passes the last tile.

### `TrackVisuals.cs` — `public static class TrackVisuals`
```csharp
public static GameObject CreateFloor(Transform parent, float length, float laneHalfWidth, Color color);   // scaled cube, y = -0.5 top at 0
public static GameObject CreateRails(Transform parent, float length, float laneHalfWidth, Color color);   // two thin cubes
public static TextMesh CreateLabel(Transform parent, string text, Color color, float characterSize, Vector3 localPos);   // faces -Z toward the camera, anchored middle-center
public static Material SolidMaterial(Color color);   // Standard (or any found) material, cached per colour
```

---

## Level (`Assets/Scripts/Level`, namespace `Swarmblastrr.Level`)

### `LevelElementType.cs`
```csharp
public enum LevelElementType { GatePair, EnemyCrowd, Boss, RewardRamp }
```

### `LevelElementData.cs`
```csharp
[System.Serializable]
public struct LevelElementData
{
    public LevelElementType Type;
    public float Z;
    public GateOperation LeftOp;   public int LeftValue;    // GatePair
    public GateOperation RightOp;  public int RightValue;   // GatePair
    public int Count;                                       // EnemyCrowd
    public int BossHp;                                      // Boss
    public int RampTiles;                                   // RewardRamp
}
```

### `LevelDefinition.cs` — `ScriptableObject`, `[CreateAssetMenu(menuName = "Swarmblastrr/Level")]`
```csharp
public int StartingUnits = 10;          // only used when GameConfig/Upgrades don't apply (tests)
public float Length = 200f;             // floor length; must be >= last element Z + 30
public List<LevelElementData> Elements = new List<LevelElementData>();
public static LevelDefinition CreateEmpty();   // CreateInstance
```

### `LevelGenerator.cs` — `public static class LevelGenerator` (pure, tested)
```csharp
public static LevelDefinition Generate(int levelIndex, int seed = 1337);
```
Deterministic from `(levelIndex, seed)` via `System.Random(seed * 7919 + levelIndex)`.
Layout rules: elements sorted by ascending Z, first element at Z >= 20, spacing 14–22;
`4 + levelIndex/2` gate pairs (cap 10) where each pair has one clearly better side; enemy crowds
every ~3 gates with `Count` scaling with `levelIndex`; exactly one `Boss` as the second to last
element with `BossHp ≈ expectedUnits * 0.8` where `expectedUnits` is a simple simulation of taking
the better gate each time starting from 10 units; exactly one `RewardRamp` last. `Length = ramp Z +
RampTiles * 2 + 30`.

### `LevelBuilder.cs` — `MonoBehaviour`
```csharp
public Transform Root { get; }                      // destroyed and recreated on every Build
public IReadOnlyList<TrackElement> Elements { get; }
public LevelDefinition Current { get; }
public void Build(LevelDefinition level, GameConfig config);   // floor + rails + every element (GatePair → two Gate objects)
public void Clear();
```

### `LevelRunner.cs` — `MonoBehaviour`
```csharp
public bool Active { get; }
public void Begin(IReadOnlyList<TrackElement> elements, CrowdController player);
public void Stop();
```
`Update`: if `Active`, for each non-consumed element within `Z - 15 .. Z + 60` of the player,
if `Overlaps(player)` call `OnCrowdOverlap(player, Time.deltaTime)`. Skips null/destroyed entries.

---

## Meta (`Assets/Scripts/Meta`, namespace `Swarmblastrr.Meta`)

### `UpgradeType.cs`
```csharp
public enum UpgradeType { StartingUnits = 0, UnitDamage = 1, CoinIncome = 2 }
```

### `UpgradeDefinition.cs`
```csharp
[System.Serializable]
public struct UpgradeDefinition
{
    public UpgradeType Type;
    public string DisplayName;
    public int BaseCost;
    public float CostGrowth;      // geometric: cost(level) = round(BaseCost * CostGrowth^level)
    public float BaseValue;       // value at level 0
    public float ValuePerLevel;   // value(level) = BaseValue + ValuePerLevel * level
    public int MaxLevel;          // 0 = unlimited
}
```

### `UpgradeCatalog.cs` — `public static class UpgradeCatalog`
```csharp
public static readonly UpgradeDefinition[] All;   // index == (int)UpgradeType
public static UpgradeDefinition Get(UpgradeType type);
// StartingUnits: base 10, +1/level, cost 30 * 1.25^lvl
// UnitDamage:    base 1,  +1/level, cost 80 * 1.5^lvl
// CoinIncome:    base 1.0, +0.1/level, cost 50 * 1.35^lvl
```

### `UpgradeMath.cs` — `public static class UpgradeMath` (pure, tested)
```csharp
public static int CostFor(in UpgradeDefinition def, int level);       // Mathf.RoundToInt(BaseCost * Mathf.Pow(CostGrowth, level)); use System.Math so it works without Unity
public static float ValueFor(in UpgradeDefinition def, int level);
public static bool IsMaxed(in UpgradeDefinition def, int level);
```

### `SaveData.cs`
```csharp
[System.Serializable]
public class SaveData
{
    public int Version = 1;
    public int Coins;
    public int LevelIndex;
    public int[] UpgradeLevels = new int[3];   // indexed by (int)UpgradeType
    public static SaveData CreateNew();
    public void EnsureArrays();                // resizes UpgradeLevels if a newer build adds upgrades
}
```

### `ISaveStore.cs`, `FileSaveStore.cs`, `MemorySaveStore.cs`
```csharp
public interface ISaveStore { string Read(); void Write(string json); bool Exists { get; } void Delete(); }
public sealed class FileSaveStore : ISaveStore { public FileSaveStore(string path); public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save.json"); }
public sealed class MemorySaveStore : ISaveStore { }
```

### `SaveSystem.cs` — `public static class SaveSystem`
```csharp
public static ISaveStore Store { get; set; }     // defaults to FileSaveStore(DefaultPath) lazily
public static SaveData Load();                   // never throws: corrupt/missing → CreateNew()
public static void Save(SaveData data);          // JsonUtility.ToJson(data, true)
public static void Reset();                      // Store.Delete()
```

### `Economy.cs` — `public static class Economy` (pure, tested)
```csharp
public static int WinReward(int unitsRemaining, int rampMultiplier, float incomeMultiplier, int levelIndex, GameConfig config);
//   = round((unitsRemaining * CoinsPerSurvivingUnit * rampMultiplier + LevelClearBonus + levelIndex * 10) * incomeMultiplier)
public static int LoseReward(int levelIndex, float incomeMultiplier);   // = round((5 + levelIndex) * incomeMultiplier)
```

### `UpgradeManager.cs` — `MonoBehaviour`
```csharp
public SaveData Data { get; }
public int Coins => Data.Coins;
public void Initialize(SaveData data);                 // called by GameManager.Initialize (loads via SaveSystem if null passed)
public int GetLevel(UpgradeType type);
public float GetValue(UpgradeType type);               // UpgradeMath.ValueFor
public int GetValueInt(UpgradeType type);              // RoundToInt(GetValue)
public int GetCost(UpgradeType type);
public bool CanBuy(UpgradeType type);
public bool TryBuy(UpgradeType type);                  // spends coins, increments level, saves, raises CoinsChanged + UpgradesChanged
public void AddCoins(int amount);                      // raises CoinsChanged, does not save (GameManager saves)
public void Save();                                    // SaveSystem.Save(Data)
```

---

## UI (`Assets/Scripts/UI`, namespace `Swarmblastrr.UI`)

Everything is built in code with legacy uGUI (`Canvas`, `Text`, `Button`, `Image`).

### `UiFactory.cs` — `public static class UiFactory`
```csharp
public static Canvas CreateRootCanvas();   // ScreenSpaceOverlay, CanvasScaler ScaleWithScreenSize 1080x1920 match 0.5, GraphicRaycaster, plus an EventSystem if none exists (with StandaloneInputModule under #if !ENABLE_INPUT_SYSTEM, else InputSystemUIInputModule)
public static Font DefaultFont();
public static Text CreateText(Transform parent, string text, int fontSize, TextAnchor anchor, Color color);
public static Button CreateButton(Transform parent, string label, Vector2 size, Color bg, UnityAction onClick);
public static Image CreatePanel(Transform parent, Color color);   // stretched full-rect
public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size);
```

### `HUD.cs` — `MonoBehaviour`
Shows player count (top-centre, large), coins (top-left), level number (top-right). Subscribes to
`CountChanged`, `CoinsChanged`, `StateChanged`. Visible in every state. Also owns a `BossBar`:
a slim fill image + text shown only in `BossFight`, driven by `BossDamaged`.
```csharp
public void Initialize(Canvas root, GameManager game);
```

### `UpgradeShopUI.cs` — `MonoBehaviour`
Visible only in `GameState.Shop`. Three rows (one per `UpgradeType`): name, current value,
cost button (disabled when `!CanBuy`). A big "PLAY" button → `GameManager.Instance.StartLevel()`.
```csharp
public void Initialize(Canvas root, GameManager game);
```

### `ResultScreen.cs` — `MonoBehaviour`
Visible only in `Won`/`Lost`. Title, units remaining, multiplier, coins earned, one button
("NEXT" / "RETRY") → `GameManager.Instance.RestartLevel()`.
```csharp
public void Initialize(Canvas root, GameManager game);
```

### `FloatingText.cs` — `MonoBehaviour`
On `GateHit`, spawns a `TextMesh` at the gate world position reading `GateMath.Label` that
rises and fades for 0.8 s, then is destroyed. Pure polish; must never throw.
```csharp
public static FloatingText Ensure();   // finds or creates a singleton GameObject "FloatingText"
```

---

## Controls (`Assets/Scripts/Controls`, namespace `Swarmblastrr.Controls`)

### `SwipeInput.cs` — `MonoBehaviour`
```csharp
public float Sensitivity = 1f;        // world units per screen-width of drag ≈ 2 * LaneHalfWidth * Sensitivity
public bool IsPressed { get; }
public float DeltaX { get; }          // world-space lateral delta this frame (0 when not dragging)
public float NormalizedScreenX { get; }   // -1..1 pointer x, for debugging
```
Read in `Update` (not `LateUpdate`) so `CrowdController.Update` sees this frame's value; use
script execution order attribute `[DefaultExecutionOrder(-100)]`. Both `#if ENABLE_INPUT_SYSTEM`
and legacy branches must compile.

---

## Shaders (`Assets/Resources/Shaders`)

All three live in `Assets/Resources/Shaders/` (Resources folder ⇒ always included in builds,
which the built-in `Standard` shader is **not** guaranteed to be). All are Built-in pipeline,
`Tags { "RenderType"="Opaque" }` unless stated, single `ForwardBase` pass, half-Lambert lighting
against `_WorldSpaceLightPos0` + `UNITY_LIGHTMODEL_AMBIENT`, `#pragma multi_compile_instancing`,
`UNITY_INSTANCING_BUFFER` with `_Color` so per-material colour works with instancing.
`FallBack "Legacy Shaders/Diffuse"`.

### `CrowdInstanced.shader` — `"Swarmblastrr/CrowdInstanced"` (owned by the Crowd writer)
Properties `_Color`, `_BobAmplitude` (0.08), `_BobSpeed` (10). Vertex bob:
`y += sin(_Time.y * _BobSpeed + instanceId * 1.7) * _BobAmplitude * saturate(v.vertex.y)` where
`instanceId` is `unity_InstanceID` inside `#ifdef UNITY_INSTANCING_ENABLED` and `0` otherwise, after
`UNITY_SETUP_INSTANCE_ID(v)`.

### `Solid.shader` — `"Swarmblastrr/Solid"` (owned by the Track writer)
Opaque, property `_Color`. Used by `TrackVisuals.SolidMaterial` for floors, rails, gate frames,
boss body, ramp tiles.

### `SolidTransparent.shader` — `"Swarmblastrr/SolidTransparent"` (owned by the Track writer)
`Tags { "Queue"="Transparent" "RenderType"="Transparent" }`, `Blend SrcAlpha OneMinusSrcAlpha`,
`ZWrite Off`, `Cull Off`, property `_Color` whose alpha is honoured. Used for gate walls.

`TrackVisuals.SolidMaterial(color)` picks `Swarmblastrr/Solid` when `color.a >= 0.999f`, else
`Swarmblastrr/SolidTransparent`; falls back to `Standard` when `Shader.Find` returns null. Cached
per colour (dictionary keyed by `Color`).

---

## Editor (`Assets/Editor`, namespace `Swarmblastrr.Editor`)

### `DemoSceneBuilder.cs`
`[MenuItem("Swarmblastrr/Create Demo Scene")]` → new empty scene, add a `GameBootstrap`
GameObject, save as `Assets/Scenes/Main.unity` (create folder if missing), add to Build
Settings if not present, open it. `[MenuItem("Swarmblastrr/Reset Save Data")]` →
`SaveSystem.Reset()`.

## Scene (`Assets/Scenes/Main.unity` + `.meta`)
A hand-authored minimal scene YAML containing one GameObject "Bootstrap" with a `GameBootstrap`
component. The script reference uses the GUID pinned in `Assets/Scripts/Core/GameBootstrap.cs.meta`
(`guid: 5b1c0f2e7a3d4c6e9f8a1b2c3d4e5f60`). If Unity fails to open it for any reason, the
Editor menu above regenerates it.

---

## Tests (`Assets/Tests/EditMode`, namespace `Swarmblastrr.Tests`)
NUnit edit-mode tests. Must not depend on a scene or on `GameManager`.
- `GateMathTests` — every op, clamping at 0 and `MaxCount`, divide-by-zero, labels.
- `UpgradeMathTests` — cost curve monotonic, value curve, max level.
- `EconomyTests` — win/lose rewards, income multiplier.
- `SaveDataTests` — `MemorySaveStore` round-trip through `SaveSystem`, corrupt JSON → new data, `EnsureArrays`.
- `LevelGeneratorTests` — deterministic for same inputs, Z strictly ascending, exactly one Boss then one RewardRamp last, Length covers everything, difficulty grows with level.
- `CrowdSimulationTests` — 500 units, 120 steps of 1/60 s: no NaN, all |x| within lane, no two units closer than `UnitRadius * 0.5`, `SetCount` grow/shrink, disposal.
- `CrowdLayoutTests` — spawn offsets distinct, radius grows with sqrt(n).

---

## Deviations
Intentional divergences from the listings above, with the reason. Public-surface additions are
also folded into the listings; this section is the audit trail.

**Public-surface additions**
- `CrowdSimParams.LaneCenterX` (float, default 0). The player crowd simulates in a frame centred on
  its own transform, so a symmetric `|x| <= LaneHalfWidth` clamp would squeeze the crowd to the
  near-wall distance whenever it steers off centre. The player passes `-centreX`; enemies and tests
  leave it 0, so the documented semantics are unchanged for them.
- `CrowdController.ForwardLimitZ` (float, default +Infinity). Needed so the boss can hold the crowd
  during the fight; without it the crowd ran straight past the boss and the reward ramp while the
  boss was still draining units. The clamp never moves the crowd backwards and `ResetTo` clears it.
- `Boss.Overlaps` override: once the fight has started it stays true until the boss is consumed.
  Otherwise a crowd held short of the boss that shrinks below the contact distance would stall the
  fight forever.
- `GameManager` subscribes to `GameEvents.UpgradesChanged` (contract said "subscribes to nothing")
  to resize the preview crowd behind the shop when StartingUnits is bought. `StartLevel` still
  re-applies the value as a safety net.
- `FileSaveStore.FilePath` (get-only) and a `MemorySaveStore(string initialJson)` convenience
  constructor, both additive; used by the editor "Reset Save Data" menu and the tests.
- `TrackVisuals` has four `internal` helpers (`CreatePrimitive`, `CubeMesh`, `CapsuleMesh`,
  `CylinderMesh`) shared by the track elements. Not public.

**Behavioural extensions (signatures unchanged)**
- `GameManager.Win()/Lose()` are also ignored (with a warning) in `Boot`/`Shop`, so a stray call
  cannot credit coins or show the result screen when no run is active. `Win` additionally stops the
  player and the runner, as `Lose` does.
- `GameBootstrap` reuses an existing untagged `Camera` (tagging it MainCamera) before creating one,
  and when it creates the directional light it also sets a flat mid-grey ambient so the half-Lambert
  shaders are not black in an empty scene.
- `CameraFollow` looks at `(0, target.y, target.z + LookAheadZ)` rather than the target's x, so the
  view does not swing as the crowd strafes; x stays pinned at 0 as the contract intends.
- `Boss` and `RewardRamp` keep evaluating in `LateUpdate` after their first overlap (gated on
  `player.IsRunning`, with a frame guard). `LevelRunner` only polls elements within
  `[playerZ-15, playerZ+60]`, so a 20-unit ramp would otherwise drop out of polling before its far
  tiles could be reached, and a crowd could outrun a boss by 15 units.
- `RewardRamp` does nothing while `State == BossFight`, so the multiplier can never be banked with
  the boss alive.
- `Gate.Overlaps` uses a half-open rule at the lane centre line (the left gate does not own x = 0),
  so a crowd that never steered triggers exactly one gate of the pair.
- `FlockJob` separation weight is `min(2r/dist - 1, 4)` (inverse, capped) instead of a linear
  falloff; a replay with the default tunables showed the linear version packs 500+ units tighter
  than the `UnitRadius * 0.5` minimum the tests require. Cohesion is a spring inside one unit of
  distance and a constant-magnitude pull beyond, so large crowds are not crushed into the centre.
- `CrowdSimulation.Schedule` clamps `DeltaTime` to `[0, 0.05]` and `FlockJob` caps per-step
  displacement to one interaction diameter so a frame hitch cannot tunnel units through each other
  or the lane walls.
- `FloatingText` reconstructs the gate value from `(before, after)` carried by `GateHit`, since the
  event does not carry the gate's value; ambiguous cases (clamped to 0 or `MaxCount`) show the raw
  delta.
- `SwipeInput.DeltaX` is also 0 while dragging outside a run (Boot/Shop/Won/Lost), so the preview
  crowd cannot be steered from behind the shop.
- `LevelGenerator` uses a narrower "good side" band `(5 + 2L) .. (7 + 2L)` and puts multiply gates
  at fixed pair indices (4, 7, 10) rather than randomly, so expected units, boss HP and enemy counts
  grow monotonically with level and the difficulty test cannot be flaky by seed.
- `Economy` and `UpgradeMath` clamp negative inputs to 0 and saturate at `int.MaxValue` instead of
  overflowing. The tests assert "positive and monotonic", not the exact saturation value.
- `TrackVisuals.CreateFloor/CreateRails` extend 12 units behind Z = 0 so the start has ground, and the
  gate wall gets an opaque frame and sill so its ZWrite-off bottom edge does not z-fight the floor.

**Contract tensions resolved as written**
- `CrowdJobs.cs` holds three types because the contract places them there (overrides
  one-type-per-file).
- `CrowdController` references `Core`, `Controls` and `Track`, and `Boss` references `Core`,
  `Crowd` and `Meta`, because their contract signatures require it (overrides the
  "at most two other namespaces" rule).
