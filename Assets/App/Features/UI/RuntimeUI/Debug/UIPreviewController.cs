using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.RuntimeUI.Documents
{
    /// <summary>
    /// UI プレビュー用コントローラー。
    /// キー入力でダミーデータを変化させ、HUD / 強化オーバーレイ / リザルトの表示を確認する。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIPreviewController : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset _upgradeCardTemplate;

        private VisualElement _root;

        // 各プレイヤー HUD 要素
        private Label _p1TimerLabel, _p2TimerLabel;
        private Label _p1HpLabel, _p2HpLabel;
        private VisualElement _p1HpFill, _p2HpFill;
        private Label _p1CoinLabel, _p2CoinLabel;
        private VisualElement _p1FireCdFill, _p2FireCdFill;
        private VisualElement _p1FallCdFill, _p2FallCdFill;
        private VisualElement _p1AcquiredRow, _p2AcquiredRow;

        // オーバーレイ
        private VisualElement _upgradeOverlay;
        private Label _leftCountdown, _rightCountdown;
        private VisualElement _leftCards, _rightCards;
        private Label _leftStatus, _rightStatus;

        // リザルト
        private VisualElement _resultRoot;
        private Label _leftResultLabel, _rightResultLabel;

        // シミュレーション状態
        private int _p1Hp = 10, _p2Hp = 10;
        private int _p1Coins = 0, _p2Coins = 0;
        private float _timer = 20f;
        private float _fireCd = 0f, _fallCd = 0f;
        private int _upgradeCount = 0;
        private int _selectedCard = 0;
        private string _currentPhase = "match";

        private readonly string[] _upgradeNames = {
            "炎ボム飛距離増強", "炎ボムフライ", "炎ボム威力増強",
            "滑落ボム飛距離増強", "移動速度上昇", "体力回復",
        };
        private readonly string[] _upgradeCosts = { "2", "3", "3", "3", "2", "2" };
        private readonly string[] _bandClasses = {
            "card__band--fire", "card__band--fire", "card__band--fire",
            "card__band--fall", "card__band--general", "card__band--general",
        };

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;

            // P1 HUD
            var leftHud = _root.Q("LeftHud");
            _p1TimerLabel = leftHud.Q<Label>("TimerLabel");
            _p1HpLabel = leftHud.Q<Label>("HpLabel");
            _p1HpFill = leftHud.Q("HpFill");
            _p1CoinLabel = leftHud.Q<Label>("CoinLabel");
            _p1FireCdFill = leftHud.Q("FireCdFill");
            _p1FallCdFill = leftHud.Q("FallCdFill");
            _p1AcquiredRow = leftHud.Q("AcquiredUpgrades");

            // P2 HUD
            var rightHud = _root.Q("RightHud");
            _p2TimerLabel = rightHud.Q<Label>("TimerLabel");
            _p2HpLabel = rightHud.Q<Label>("HpLabel");
            _p2HpFill = rightHud.Q("HpFill");
            _p2CoinLabel = rightHud.Q<Label>("CoinLabel");
            _p2FireCdFill = rightHud.Q("FireCdFill");
            _p2FallCdFill = rightHud.Q("FallCdFill");
            _p2AcquiredRow = rightHud.Q("AcquiredUpgrades");

            // オーバーレイ
            _upgradeOverlay = _root.Q("UpgradeOverlayRoot");
            _leftCountdown = _root.Q<Label>("LeftCountdown");
            _rightCountdown = _root.Q<Label>("RightCountdown");
            _leftCards = _root.Q("LeftCards");
            _rightCards = _root.Q("RightCards");
            _leftStatus = _root.Q<Label>("LeftStatus");
            _rightStatus = _root.Q<Label>("RightStatus");

            // リロールボタン
            _root.Q<Button>("LeftRerollBtn")?.RegisterCallback<ClickEvent>(_ =>
            {
                if (_p1Coins > 0) { _p1Coins--; UpdateHud(); PopulateDummyCards(_leftCards); UpdateCardSelection(); }
            });
            _root.Q<Button>("RightRerollBtn")?.RegisterCallback<ClickEvent>(_ =>
            {
                if (_p2Coins > 0) { _p2Coins--; UpdateHud(); PopulateDummyCards(_rightCards); }
            });

            // リザルト
            _resultRoot = _root.Q("ResultRoot");
            _leftResultLabel = _root.Q<Label>("LeftResultLabel");
            _rightResultLabel = _root.Q<Label>("RightResultLabel");

            // ボタン
            _root.Q<Button>("RematchButton")?.RegisterCallback<ClickEvent>(_ =>
            {
                _currentPhase = "match";
                _p1Hp = 10; _p2Hp = 10; _p1Coins = 0; _p2Coins = 0;
                _timer = 20f; _upgradeCount = 0;
                _p1AcquiredRow.Clear(); _p2AcquiredRow.Clear();
            });

            _root.Q<Button>("TitleButton")?.RegisterCallback<ClickEvent>(_ =>
            {
                Debug.Log("[UIPreview] タイトルへ戻る");
            });

            UpdateHud();
            ShowMatchPhase();

            Debug.Log("[UIPreview] 操作ガイド:");
            Debug.Log("  1/2: P1/P2 ダメージ  |  3/4: P1/P2 コイン+1");
            Debug.Log("  Q: 炎ボムCD開始  |  W: 滑落ボムCD開始");
            Debug.Log("  E: 強化取得(P1)");
            Debug.Log("  Tab: フェーズ切替 (Match → Upgrade → Result → Match)");
            Debug.Log("  Left/Right: カード選択移動  |  Space: カード選択確定");
        }

        private void Update()
        {
            // タイマー
            if (_currentPhase == "match")
            {
                _timer -= Time.deltaTime;
                if (_timer < 0f) _timer = 20f;
                string timerText = Mathf.CeilToInt(_timer).ToString();
                _p1TimerLabel.text = timerText;
                _p2TimerLabel.text = timerText;
            }

            // CD ゲージ
            if (_fireCd > 0f) { _fireCd -= Time.deltaTime; if (_fireCd < 0f) _fireCd = 0f; }
            if (_fallCd > 0f) { _fallCd -= Time.deltaTime; if (_fallCd < 0f) _fallCd = 0f; }
            UpdateCdBars();

            // キー入力
            if (Input.GetKeyDown(KeyCode.Alpha1)) { _p1Hp = Mathf.Max(0, _p1Hp - 2); UpdateHud(); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { _p2Hp = Mathf.Max(0, _p2Hp - 2); UpdateHud(); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { _p1Coins++; UpdateHud(); }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { _p2Coins++; UpdateHud(); }
            if (Input.GetKeyDown(KeyCode.Q) && _fireCd <= 0f) { _fireCd = 2f; }
            if (Input.GetKeyDown(KeyCode.W) && _fallCd <= 0f) { _fallCd = 4f; }
            if (Input.GetKeyDown(KeyCode.E)) { AddUpgrade(_p1AcquiredRow); }
            if (Input.GetKeyDown(KeyCode.Tab)) CyclePhase();

            if (_currentPhase == "upgrade")
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) { _selectedCard = Mathf.Max(0, _selectedCard - 1); UpdateCardSelection(); }
                if (Input.GetKeyDown(KeyCode.RightArrow)) { _selectedCard = Mathf.Min(2, _selectedCard + 1); UpdateCardSelection(); }
                if (Input.GetKeyDown(KeyCode.Space)) { _leftStatus.text = "選択完了"; }
            }
        }

        private void UpdateHud()
        {
            _p1HpLabel.text = _p1Hp.ToString();
            _p1HpFill.style.width = Length.Percent(_p1Hp / 10f * 100f);
            _p1CoinLabel.text = _p1Coins.ToString();

            _p2HpLabel.text = _p2Hp.ToString();
            _p2HpFill.style.width = Length.Percent(_p2Hp / 10f * 100f);
            _p2CoinLabel.text = _p2Coins.ToString();
        }

        private void UpdateCdBars()
        {
            float fireFill = (1f - _fireCd / 2f) * 100f;
            float fallFill = (1f - _fallCd / 4f) * 100f;

            _p1FireCdFill.style.width = Length.Percent(fireFill);
            _p1FallCdFill.style.width = Length.Percent(fallFill);
            _p2FireCdFill.style.width = Length.Percent(fireFill);
            _p2FallCdFill.style.width = Length.Percent(fallFill);
        }

        private void AddUpgrade(VisualElement row)
        {
            string[] dotClasses = { "hud__upgrade-dot--fire", "hud__upgrade-dot--fall", "hud__upgrade-dot--general" };
            var dot = new VisualElement();
            dot.AddToClassList("hud__upgrade-dot");
            dot.AddToClassList(dotClasses[_upgradeCount % dotClasses.Length]);
            row.Add(dot);
            _upgradeCount++;
        }

        private void CyclePhase()
        {
            if (_currentPhase == "match") { _currentPhase = "upgrade"; ShowUpgradePhase(); }
            else if (_currentPhase == "upgrade") { _currentPhase = "result"; ShowResultPhase(); }
            else { _currentPhase = "match"; ShowMatchPhase(); }
        }

        private void ShowMatchPhase()
        {
            _upgradeOverlay.AddToClassList("upgrade-overlay--hidden");
            _resultRoot.AddToClassList("result-root--hidden");
        }

        private void ShowUpgradePhase()
        {
            _upgradeOverlay.RemoveFromClassList("upgrade-overlay--hidden");
            _resultRoot.AddToClassList("result-root--hidden");
            _leftCountdown.text = "10";
            _rightCountdown.text = "10";
            _leftStatus.text = "";
            _rightStatus.text = "";
            _selectedCard = 0;
            PopulateDummyCards(_leftCards);
            PopulateDummyCards(_rightCards);
            UpdateCardSelection();
        }

        private void ShowResultPhase()
        {
            _upgradeOverlay.AddToClassList("upgrade-overlay--hidden");
            _resultRoot.RemoveFromClassList("result-root--hidden");

            bool p1Win = _p1Hp > _p2Hp;
            _leftResultLabel.text = p1Win ? "WIN!" : "LOSE";
            _rightResultLabel.text = p1Win ? "LOSE" : "WIN!";
        }

        private void PopulateDummyCards(VisualElement container)
        {
            container.Clear();
            for (int i = 0; i < 3; i++)
            {
                int idx = (i + _upgradeCount) % _upgradeNames.Length;
                VisualElement card;

                if (_upgradeCardTemplate != null)
                {
                    var instance = _upgradeCardTemplate.Instantiate();
                    card = instance.Q(className: "card") ?? instance;
                    var band = card.Q("CategoryBand");
                    if (band != null) band.AddToClassList(_bandClasses[idx]);
                    var nameLabel = card.Q<Label>("CardName");
                    if (nameLabel != null) nameLabel.text = _upgradeNames[idx];
                    var descLabel = card.Q<Label>("CardDesc");
                    if (descLabel != null) descLabel.text = "効果説明テキスト";
                    var costLabel = card.Q<Label>("CardCost");
                    if (costLabel != null) costLabel.text = _upgradeCosts[idx];
                }
                else
                {
                    card = new VisualElement();
                    card.AddToClassList("card");
                    var band = new VisualElement();
                    band.AddToClassList("card__band");
                    band.AddToClassList(_bandClasses[idx]);
                    card.Add(band);
                    var body = new VisualElement();
                    body.AddToClassList("card__body");
                    var nl = new Label(_upgradeNames[idx]); nl.AddToClassList("card__name"); body.Add(nl);
                    var dl = new Label("効果説明テキスト"); dl.AddToClassList("card__desc"); body.Add(dl);
                    card.Add(body);
                    var footer = new VisualElement(); footer.AddToClassList("card__footer");
                    var ci = new Label("\u25C9"); ci.AddToClassList("card__cost-icon"); footer.Add(ci);
                    var cl = new Label(_upgradeCosts[idx]); cl.AddToClassList("card__cost"); footer.Add(cl);
                    card.Add(footer);
                }

                if (i == 2 && _p1Coins < 3) card.AddToClassList("card--locked");
                container.Add(card);
            }
        }

        private void UpdateCardSelection()
        {
            var cards = _leftCards.Query(className: "card").ToList();
            for (int i = 0; i < cards.Count; i++)
                cards[i].EnableInClassList("card--selected", i == _selectedCard);
        }
    }
}
