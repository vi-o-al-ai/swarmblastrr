using UnityEngine;
using UnityEngine.UI;
using Swarmblastrr.Core;
using Swarmblastrr.Meta;

namespace Swarmblastrr.UI
{
    /// <summary>
    /// Between-run shop, visible only in <see cref="GameState.Shop"/>. A dark translucent bottom
    /// panel holds one row per <see cref="UpgradeType"/> (name + current value on the left, a cost
    /// button on the right, disabled when the upgrade cannot be bought) and a large PLAY button
    /// floating above it that starts the level. The band between the two is left empty so the
    /// preview crowd behind the shop stays visible.
    /// </summary>
    public class UpgradeShopUI : MonoBehaviour
    {
        private const float PanelHeight = 620f;
        private const float RowHeight = 170f;
        private const float RowSpacing = 20f;
        private const float RowTopPadding = 30f;
        private const float SideMargin = 40f;

        /// <summary>
        /// Gap between the top of the shop panel and the PLAY button. The camera in the shop
        /// projects the preview crowd at roughly 550-900 reference px from the bottom (default
        /// through heavily upgraded starting sizes), so the button sits above that band.
        /// </summary>
        private const float PlayButtonGap = 320f;

        private static readonly Vector2 _center = new Vector2(0.5f, 0.5f);
        private static readonly Color _panelColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color _rowColor = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color _buyColor = new Color(0.25f, 0.6f, 1f, 1f);
        private static readonly Color _playColor = new Color(0.2f, 0.75f, 0.35f, 1f);

        private static readonly UpgradeType[] _types =
        {
            UpgradeType.StartingUnits,
            UpgradeType.UnitDamage,
            UpgradeType.CoinIncome,
        };

        private GameManager _game;
        private GameObject _root;
        private Text[] _rowLabels;
        private Button[] _rowButtons;
        private Text[] _rowButtonLabels;
        private bool _initialized;
        private bool _subscribed;

        /// <summary>
        /// Builds the shop under <paramref name="root"/>, subscribes to the game events and refreshes
        /// every row. Safe to call before <see cref="GameManager.Initialize"/> has run; rows fill in
        /// once <see cref="GameEvents.UpgradesChanged"/> or a state change arrives.
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
            Refresh();
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
            GameEvents.CoinsChanged += OnCoinsChanged;
            GameEvents.UpgradesChanged += OnUpgradesChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.CoinsChanged -= OnCoinsChanged;
            GameEvents.UpgradesChanged -= OnUpgradesChanged;
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

            Image panel = UiFactory.CreatePanel(rootRect, _panelColor);
            panel.name = "ShopPanel";
            RectTransform panelRect = panel.rectTransform;
            UiFactory.Anchor(panelRect, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, PanelHeight));

            int count = _types.Length;
            _rowLabels = new Text[count];
            _rowButtons = new Button[count];
            _rowButtonLabels = new Text[count];

            for (int i = 0; i < count; i++)
            {
                BuildRow(panelRect, i);
            }

            Button play = UiFactory.CreateButton(rootRect, "PLAY", new Vector2(560f, 170f), _playColor, OnPlayClicked);
            play.name = "PlayButton";
            UiFactory.Anchor(play.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, PanelHeight + PlayButtonGap), new Vector2(560f, 170f));
        }

        private void BuildRow(RectTransform panelRect, int index)
        {
            UpgradeType type = _types[index];

            Image row = UiFactory.CreatePanel(panelRect, _rowColor);
            row.name = "Row_" + type;
            row.raycastTarget = false;
            RectTransform rowRect = row.rectTransform;
            float y = -(RowTopPadding + index * (RowHeight + RowSpacing));
            UiFactory.Anchor(rowRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(-2f * SideMargin, RowHeight));

            Text label = UiFactory.CreateText(rowRect, UpgradeCatalog.Get(type).DisplayName, 44, TextAnchor.MiddleLeft, Color.white);
            label.name = "Label";
            UiFactory.Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f),
                new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(-30f, 0f));
            _rowLabels[index] = label;

            int captured = index;
            Button buy = UiFactory.CreateButton(rowRect, "-", new Vector2(300f, 110f), _buyColor, () => OnBuyClicked(captured));
            buy.name = "BuyButton";
            UiFactory.Anchor(buy.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(300f, 110f));
            _rowButtons[index] = buy;
            _rowButtonLabels[index] = buy.GetComponentInChildren<Text>();
        }

        private void OnStateChanged(GameState state)
        {
            Refresh();
            ApplyState(state);
        }

        private void OnCoinsChanged(int total)
        {
            Refresh();
        }

        private void OnUpgradesChanged()
        {
            Refresh();
        }

        private void ApplyState(GameState state)
        {
            if (_root != null)
            {
                _root.SetActive(state == GameState.Shop);
            }
        }

        private void OnBuyClicked(int index)
        {
            if (index < 0 || index >= _types.Length)
            {
                return;
            }

            UpgradeManager upgrades = ResolveUpgrades();
            if (upgrades == null)
            {
                return;
            }

            upgrades.TryBuy(_types[index]);
            Refresh();
        }

        private void OnPlayClicked()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                game = _game;
            }

            if (game != null && game.State == GameState.Shop)
            {
                game.StartLevel();
            }
        }

        private UpgradeManager ResolveUpgrades()
        {
            GameManager game = _game;
            if (game == null)
            {
                game = GameManager.Instance;
            }

            if (game == null || game.Upgrades == null || game.Upgrades.Data == null)
            {
                return null;
            }

            return game.Upgrades;
        }

        private void Refresh()
        {
            if (_rowLabels == null)
            {
                return;
            }

            UpgradeManager upgrades = ResolveUpgrades();

            for (int i = 0; i < _types.Length; i++)
            {
                UpgradeType type = _types[i];
                UpgradeDefinition def = UpgradeCatalog.Get(type);

                if (upgrades == null)
                {
                    SetRow(i, def.DisplayName, "-", false);
                    continue;
                }

                int level = upgrades.GetLevel(type);
                float value = upgrades.GetValue(type);
                bool maxed = UpgradeMath.IsMaxed(def, level);

                string text = def.DisplayName + "  Lv " + (level + 1) + "\n" + FormatValue(type, value);
                string cost = maxed ? "MAX" : upgrades.GetCost(type).ToString();
                SetRow(i, text, cost, upgrades.CanBuy(type));
            }
        }

        private void SetRow(int index, string label, string cost, bool interactable)
        {
            if (_rowLabels[index] != null)
            {
                _rowLabels[index].text = label;
            }

            if (_rowButtonLabels[index] != null)
            {
                _rowButtonLabels[index].text = cost;
            }

            if (_rowButtons[index] != null)
            {
                _rowButtons[index].interactable = interactable;
            }
        }

        private static string FormatValue(UpgradeType type, float value)
        {
            switch (type)
            {
                case UpgradeType.StartingUnits:
                    return "Start with " + Mathf.RoundToInt(value) + " units";
                case UpgradeType.UnitDamage:
                    return Mathf.RoundToInt(value) + " damage per unit";
                case UpgradeType.CoinIncome:
                    return "x" + value.ToString("0.0") + " coins";
                default:
                    return value.ToString("0.##");
            }
        }
    }
}
