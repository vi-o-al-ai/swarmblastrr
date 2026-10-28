using UnityEngine;
using Swarmblastrr.Crowd;
using Swarmblastrr.Level;
using Swarmblastrr.Meta;

namespace Swarmblastrr.Core
{
    /// <summary>
    /// Singleton that owns the game-state machine. It is wired once by <see cref="GameBootstrap"/>
    /// through <see cref="Initialize"/> and is driven by the other subsystems (track elements, UI,
    /// crowd) calling into it. Its only subscription is <see cref="GameEvents.UpgradesChanged"/>,
    /// used to resize the crowd shown behind the shop when a StartingUnits level is bought. Every
    /// state change goes through <see cref="SetState"/> which raises <see cref="GameEvents.StateChanged"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        /// <summary>The live instance, or null before Awake / after OnDestroy.</summary>
        public static GameManager Instance { get; private set; }

        /// <summary>Tunables for this session.</summary>
        public GameConfig Config { get; private set; }

        /// <summary>Current phase of the game loop.</summary>
        public GameState State { get; private set; } = GameState.Boot;

        /// <summary>0-based index of the level being shown or played. Persisted in SaveData.</summary>
        public int CurrentLevelIndex { get; private set; }

        /// <summary>The player's crowd.</summary>
        public CrowdController Player { get; private set; }

        /// <summary>Builds the track for the current level.</summary>
        public LevelBuilder LevelBuilder { get; private set; }

        /// <summary>Polls track elements against the player while a run is active.</summary>
        public LevelRunner LevelRunner { get; private set; }

        /// <summary>Coins, upgrade levels and persistence.</summary>
        public UpgradeManager Upgrades { get; private set; }

        /// <summary>Follow behaviour on the main camera.</summary>
        public CameraFollow Camera { get; private set; }

        private bool _initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Swarmblastrr] A second GameManager was created; destroying the duplicate.", this);
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Instance = null;
            GameEvents.UpgradesChanged -= OnUpgradesChanged;
            GameEvents.ClearAll();
        }

        /// <summary>
        /// Called once by <see cref="GameBootstrap"/> after all components exist. Loads the save,
        /// hands it to the upgrade manager and restores the persisted level index.
        /// </summary>
        public void Initialize(GameConfig config, CrowdController player, LevelBuilder builder,
                               LevelRunner runner, UpgradeManager upgrades, CameraFollow camera)
        {
            if (_initialized)
            {
                Debug.LogWarning("[Swarmblastrr] GameManager.Initialize called twice; ignoring.", this);
                return;
            }

            Config = config != null ? config : GameConfig.CreateDefault();
            Player = player;
            LevelBuilder = builder;
            LevelRunner = runner;
            Upgrades = upgrades;
            Camera = camera;

            Upgrades.Initialize(SaveSystem.Load());
            CurrentLevelIndex = Mathf.Max(0, Upgrades.Data.LevelIndex);
            _initialized = true;

            GameEvents.UpgradesChanged -= OnUpgradesChanged;
            GameEvents.UpgradesChanged += OnUpgradesChanged;

            // Let UI that was built before Initialize pick up the persisted values.
            GameEvents.RaiseCoinsChanged(Upgrades.Coins);
            GameEvents.RaiseUpgradesChanged();
        }

        /// <summary>
        /// State = Shop. Builds (or rebuilds) the current level so it is visible behind the shop,
        /// resets the player crowd to the upgraded starting size and snaps the camera to the start.
        /// </summary>
        public void EnterShop()
        {
            if (!EnsureInitialized("EnterShop"))
            {
                return;
            }

            LevelDefinition previous = LevelBuilder.Current;
            LevelDefinition level = LevelGenerator.Generate(CurrentLevelIndex);
            LevelBuilder.Build(level, Config);
            if (previous != null && previous != level)
            {
                Destroy(previous);
            }

            Player.ResetTo(new Vector3(0f, 0f, 0f), Upgrades.GetValueInt(UpgradeType.StartingUnits));
            Camera.Target = Player.transform;
            Camera.SnapToTarget();
            LevelRunner.Stop();

            SetState(GameState.Shop);
        }

        /// <summary>
        /// State = Running. The player starts moving and the runner begins polling the track.
        /// Only valid from <see cref="GameState.Shop"/>.
        /// </summary>
        public void StartLevel()
        {
            if (!EnsureInitialized("StartLevel"))
            {
                return;
            }

            if (State != GameState.Shop)
            {
                Debug.LogWarning("[Swarmblastrr] StartLevel ignored: state is " + State + ", expected Shop.", this);
                return;
            }

            // Re-apply the starting size: the shop may have sold a StartingUnits level since EnterShop.
            Player.SetCount(Upgrades.GetValueInt(UpgradeType.StartingUnits));
            Player.IsRunning = true;
            LevelRunner.Begin(LevelBuilder.Elements, Player);
            SetState(GameState.Running);
        }

        /// <summary>State = BossFight. Called by the boss when the crowd first reaches it.</summary>
        public void EnterBossFight()
        {
            if (State != GameState.Running)
            {
                return;
            }

            SetState(GameState.BossFight);
        }

        /// <summary>State = RewardRamp. Called by the boss when its HP reaches zero.</summary>
        public void EnterRewardRamp()
        {
            if (State != GameState.BossFight && State != GameState.Running)
            {
                return;
            }

            SetState(GameState.RewardRamp);
        }

        /// <summary>
        /// State = Won. Computes the <see cref="LevelResult"/> via <see cref="Economy"/>, adds the
        /// coins, advances <see cref="CurrentLevelIndex"/>, saves and raises
        /// <see cref="GameEvents.LevelFinished"/>. Idempotent once the run has ended.
        /// </summary>
        public void Win(int rewardMultiplier)
        {
            if (!EnsureInitialized("Win") || !IsInRun("Win"))
            {
                return;
            }

            int multiplier = Mathf.Max(1, rewardMultiplier);
            int unitsRemaining = Player.Count;
            int coins = Economy.WinReward(unitsRemaining, multiplier,
                Upgrades.GetValue(UpgradeType.CoinIncome), CurrentLevelIndex, Config);

            Player.IsRunning = false;
            LevelRunner.Stop();

            LevelResult result = new LevelResult
            {
                Won = true,
                LevelIndex = CurrentLevelIndex,
                UnitsRemaining = unitsRemaining,
                RewardMultiplier = multiplier,
                CoinsEarned = coins
            };

            Upgrades.AddCoins(coins);
            CurrentLevelIndex++;
            Upgrades.Data.LevelIndex = CurrentLevelIndex;
            Upgrades.Save();

            SetState(GameState.Won);
            GameEvents.RaiseLevelFinished(result);
        }

        /// <summary>
        /// State = Lost. Computes a consolation <see cref="LevelResult"/> via <see cref="Economy"/>,
        /// stops the run, saves and raises <see cref="GameEvents.LevelFinished"/>. Idempotent once
        /// the run has ended.
        /// </summary>
        public void Lose()
        {
            if (!EnsureInitialized("Lose") || !IsInRun("Lose"))
            {
                return;
            }

            int coins = Economy.LoseReward(CurrentLevelIndex, Upgrades.GetValue(UpgradeType.CoinIncome));

            Player.IsRunning = false;
            LevelRunner.Stop();

            LevelResult result = new LevelResult
            {
                Won = false,
                LevelIndex = CurrentLevelIndex,
                UnitsRemaining = Player.Count,
                RewardMultiplier = 1,
                CoinsEarned = coins
            };

            Upgrades.AddCoins(coins);
            Upgrades.Save();

            SetState(GameState.Lost);
            GameEvents.RaiseLevelFinished(result);
        }

        /// <summary>
        /// From <see cref="GameState.Won"/> / <see cref="GameState.Lost"/> back to the shop. The
        /// level index has already been advanced if the run was won.
        /// </summary>
        public void RestartLevel()
        {
            if (State != GameState.Won && State != GameState.Lost)
            {
                Debug.LogWarning("[Swarmblastrr] RestartLevel ignored: state is " + State + ".", this);
                return;
            }

            EnterShop();
        }

        private void SetState(GameState next)
        {
            State = next;
            GameEvents.RaiseStateChanged(next);
        }

        /// <summary>
        /// While the shop is open the crowd behind it previews the starting size, so a StartingUnits
        /// purchase is reflected immediately (StartLevel re-applies it anyway as a safety net).
        /// </summary>
        private void OnUpgradesChanged()
        {
            if (State != GameState.Shop || Player == null || Upgrades == null)
            {
                return;
            }

            int startingUnits = Upgrades.GetValueInt(UpgradeType.StartingUnits);
            if (startingUnits != Player.Count)
            {
                Player.SetCount(startingUnits);
            }
        }

        private bool EnsureInitialized(string caller)
        {
            if (_initialized)
            {
                return true;
            }

            Debug.LogError("[Swarmblastrr] GameManager." + caller + " called before Initialize.", this);
            return false;
        }

        private bool IsInRun(string caller)
        {
            if (State == GameState.Running || State == GameState.BossFight || State == GameState.RewardRamp)
            {
                return true;
            }

            if (State != GameState.Won && State != GameState.Lost)
            {
                Debug.LogWarning("[Swarmblastrr] GameManager." + caller + " ignored: state is " + State + ".", this);
            }

            return false;
        }
    }
}
