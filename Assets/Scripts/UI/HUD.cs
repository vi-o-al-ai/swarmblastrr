using UnityEngine;
using UnityEngine.UI;
using Swarmblastrr.Core;

namespace Swarmblastrr.UI
{
    /// <summary>
    /// Always-visible overlay: the player count (top-centre, large, outlined), coins (top-left),
    /// level number (top-right) and a slim boss HP bar that is only shown during the boss fight.
    /// Driven entirely by <see cref="GameEvents"/>; built in code under the root canvas.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        private const int CountFontSize = 110;
        private const int SideFontSize = 48;
        private const int BossFontSize = 32;

        private static readonly Vector2 _center = new Vector2(0.5f, 0.5f);
        private static readonly Color _outlineColor = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color _coinColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color _bossBarBackground = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color _bossBarFill = new Color(0.9f, 0.25f, 0.2f, 1f);

        private GameManager _game;
        private Text _countText;
        private Text _coinsText;
        private Text _levelText;
        private GameObject _bossBar;
        private RectTransform _bossFillRect;
        private Text _bossText;
        private bool _initialized;
        private bool _subscribed;

        /// <summary>
        /// Builds the widgets under <paramref name="root"/>, subscribes to the game events and shows
        /// the current values. Safe to call before <see cref="GameManager.Initialize"/> has run.
        /// </summary>
        public void Initialize(Canvas root, GameManager game)
        {
            _game = game;

            if (!_initialized)
            {
                RectTransform host = PrepareHost(root);
                BuildWidgets(host);
                _initialized = true;
            }

            Subscribe();
            RefreshAll();
            ApplyState(_game != null ? _game.State : GameState.Boot);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameEvents.StateChanged += OnStateChanged;
            GameEvents.CountChanged += OnCountChanged;
            GameEvents.CoinsChanged += OnCoinsChanged;
            GameEvents.BossDamaged += OnBossDamaged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.CountChanged -= OnCountChanged;
            GameEvents.CoinsChanged -= OnCoinsChanged;
            GameEvents.BossDamaged -= OnBossDamaged;
            _subscribed = false;
        }

        private RectTransform PrepareHost(Canvas root)
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null)
            {
                rt = gameObject.AddComponent<RectTransform>();
            }

            if (root != null && transform.parent != root.transform)
            {
                rt.SetParent(root.transform, false);
            }

            UiFactory.Anchor(rt, Vector2.zero, Vector2.one, _center, Vector2.zero, Vector2.zero);
            return rt;
        }

        private void BuildWidgets(RectTransform host)
        {
            _countText = UiFactory.CreateText(host, "0", CountFontSize, TextAnchor.UpperCenter, Color.white);
            _countText.name = "CountText";
            _countText.fontStyle = FontStyle.Bold;
            UiFactory.Anchor(_countText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 140f));
            AddOutline(_countText, 4f);

            _coinsText = UiFactory.CreateText(host, "Coins: 0", SideFontSize, TextAnchor.UpperLeft, _coinColor);
            _coinsText.name = "CoinsText";
            _coinsText.fontStyle = FontStyle.Bold;
            UiFactory.Anchor(_coinsText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(40f, -60f), new Vector2(420f, 70f));
            AddOutline(_coinsText, 2f);

            _levelText = UiFactory.CreateText(host, "Level 1", SideFontSize, TextAnchor.UpperRight, Color.white);
            _levelText.name = "LevelText";
            _levelText.fontStyle = FontStyle.Bold;
            UiFactory.Anchor(_levelText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-40f, -60f), new Vector2(420f, 70f));
            AddOutline(_levelText, 2f);

            BuildBossBar(host);
        }

        private void BuildBossBar(RectTransform host)
        {
            _bossBar = new GameObject("BossBar", typeof(RectTransform));
            RectTransform barRect = _bossBar.GetComponent<RectTransform>();
            barRect.SetParent(host, false);
            UiFactory.Anchor(barRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(760f, 48f));

            Image background = UiFactory.CreatePanel(barRect, _bossBarBackground);
            background.name = "Background";
            background.raycastTarget = false;

            Image fill = UiFactory.CreatePanel(barRect, _bossBarFill);
            fill.name = "Fill";
            fill.raycastTarget = false;
            _bossFillRect = fill.rectTransform;
            UiFactory.Anchor(_bossFillRect, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            _bossText = UiFactory.CreateText(barRect, "BOSS", BossFontSize, TextAnchor.MiddleCenter, Color.white);
            _bossText.name = "BossText";
            _bossText.fontStyle = FontStyle.Bold;
            AddOutline(_bossText, 1.5f);

            _bossBar.SetActive(false);
        }

        private static void AddOutline(Text text, float distance)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = _outlineColor;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        private void OnStateChanged(GameState state)
        {
            RefreshAll();
            ApplyState(state);
        }

        private void OnCountChanged(int previous, int current)
        {
            SetCount(current);
        }

        private void OnCoinsChanged(int total)
        {
            SetCoins(total);
        }

        private void OnBossDamaged(int hp, int maxHp)
        {
            SetBoss(hp, maxHp);
        }

        private void ApplyState(GameState state)
        {
            if (_bossBar == null)
            {
                return;
            }

            bool inBossFight = state == GameState.BossFight;
            if (inBossFight && !_bossBar.activeSelf)
            {
                SetBoss(1, 1);
            }

            _bossBar.SetActive(inBossFight);
        }

        private void RefreshAll()
        {
            if (_game == null)
            {
                return;
            }

            if (_game.Player != null)
            {
                SetCount(_game.Player.Count);
            }

            if (_game.Upgrades != null && _game.Upgrades.Data != null)
            {
                SetCoins(_game.Upgrades.Coins);
            }

            // GameManager.Win advances CurrentLevelIndex before entering Won, so while the result
            // screen is up the index already points at the next level. Show the level just played.
            int shownLevel = _game.CurrentLevelIndex;
            if (_game.State == GameState.Won)
            {
                shownLevel = Mathf.Max(0, shownLevel - 1);
            }

            SetLevel(shownLevel);
        }

        private void SetCount(int count)
        {
            if (_countText != null)
            {
                _countText.text = count.ToString();
            }
        }

        private void SetCoins(int coins)
        {
            if (_coinsText != null)
            {
                _coinsText.text = "Coins: " + coins;
            }
        }

        private void SetLevel(int levelIndex)
        {
            if (_levelText != null)
            {
                _levelText.text = "Level " + (levelIndex + 1);
            }
        }

        private void SetBoss(int hp, int maxHp)
        {
            int clampedMax = Mathf.Max(1, maxHp);
            int clampedHp = Mathf.Clamp(hp, 0, clampedMax);
            float fraction = clampedHp / (float)clampedMax;

            if (_bossFillRect != null)
            {
                _bossFillRect.anchorMax = new Vector2(fraction, 1f);
            }

            if (_bossText != null)
            {
                _bossText.text = "BOSS  " + clampedHp + " / " + clampedMax;
            }
        }
    }
}
