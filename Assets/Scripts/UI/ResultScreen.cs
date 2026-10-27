using UnityEngine;
using UnityEngine.UI;
using Swarmblastrr.Core;

namespace Swarmblastrr.UI
{
    /// <summary>
    /// End-of-run overlay, visible only in <see cref="GameState.Won"/> / <see cref="GameState.Lost"/>.
    /// Shows a title ("LEVEL CLEARED!" / "CRUSHED..."), the units remaining, the reward multiplier
    /// and the coins earned, plus one NEXT / RETRY button that returns to the shop.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        private const string WonTitle = "LEVEL CLEARED!";
        private const string LostTitle = "CRUSHED...";
        private const string NextLabel = "NEXT";
        private const string RetryLabel = "RETRY";

        private static readonly Vector2 _center = new Vector2(0.5f, 0.5f);
        private static readonly Color _backdropColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color _wonColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color _lostColor = new Color(1f, 0.4f, 0.35f, 1f);
        private static readonly Color _buttonColor = new Color(0.2f, 0.75f, 0.35f, 1f);

        private GameManager _game;
        private GameObject _root;
        private Text _title;
        private Text _stats;
        private Text _buttonLabel;
        private LevelResult _lastResult;
        private bool _hasResult;
        private bool _initialized;
        private bool _subscribed;

        /// <summary>
        /// Builds the screen under <paramref name="root"/>, subscribes to the game events and hides
        /// itself until a run ends.
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
            GameEvents.LevelFinished += OnLevelFinished;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.LevelFinished -= OnLevelFinished;
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
            _root = new GameObject("Root", typeof(RectTransform));
            RectTransform rootRect = _root.GetComponent<RectTransform>();
            rootRect.SetParent(host, false);
            UiFactory.Anchor(rootRect, Vector2.zero, Vector2.one, _center, Vector2.zero, Vector2.zero);

            Image backdrop = UiFactory.CreatePanel(rootRect, _backdropColor);
            backdrop.name = "Backdrop";

            _title = UiFactory.CreateText(rootRect, WonTitle, 100, TextAnchor.MiddleCenter, _wonColor);
            _title.name = "Title";
            _title.fontStyle = FontStyle.Bold;
            UiFactory.Anchor(_title.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f),
                _center, Vector2.zero, new Vector2(1000f, 160f));
            Outline titleOutline = _title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            titleOutline.effectDistance = new Vector2(4f, -4f);
            titleOutline.useGraphicAlpha = true;

            _stats = UiFactory.CreateText(rootRect, string.Empty, 56, TextAnchor.MiddleCenter, Color.white);
            _stats.name = "Stats";
            _stats.lineSpacing = 1.25f;
            UiFactory.Anchor(_stats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                _center, new Vector2(0f, 20f), new Vector2(900f, 360f));

            Button button = UiFactory.CreateButton(rootRect, NextLabel, new Vector2(560f, 170f), _buttonColor, OnContinueClicked);
            button.name = "ContinueButton";
            UiFactory.Anchor(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f),
                _center, Vector2.zero, new Vector2(560f, 170f));
            _buttonLabel = button.GetComponentInChildren<Text>();

            _root.SetActive(false);
        }

        private void OnStateChanged(GameState state)
        {
            ApplyState(state);
        }

        private void OnLevelFinished(LevelResult result)
        {
            _lastResult = result;
            _hasResult = true;
            Fill();
        }

        private void ApplyState(GameState state)
        {
            bool visible = state == GameState.Won || state == GameState.Lost;

            if (visible)
            {
                Fill();
            }
            else
            {
                _hasResult = false;
            }

            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

        private void Fill()
        {
            bool won;
            if (_hasResult)
            {
                won = _lastResult.Won;
            }
            else
            {
                won = _game != null && _game.State == GameState.Won;
            }

            if (_title != null)
            {
                _title.text = won ? WonTitle : LostTitle;
                _title.color = won ? _wonColor : _lostColor;
            }

            if (_buttonLabel != null)
            {
                _buttonLabel.text = won ? NextLabel : RetryLabel;
            }

            if (_stats == null)
            {
                return;
            }

            if (!_hasResult)
            {
                int units = 0;
                if (_game != null && _game.Player != null)
                {
                    units = _game.Player.Count;
                }

                _stats.text = "Units remaining: " + units + "\nMultiplier: x1\nCoins earned: ...";
                return;
            }

            _stats.text = "Units remaining: " + _lastResult.UnitsRemaining
                + "\nMultiplier: x" + Mathf.Max(1, _lastResult.RewardMultiplier)
                + "\nCoins earned: +" + _lastResult.CoinsEarned;
        }

        private void OnContinueClicked()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                game = _game;
            }

            if (game != null && (game.State == GameState.Won || game.State == GameState.Lost))
            {
                game.RestartLevel();
            }
        }
    }
}
