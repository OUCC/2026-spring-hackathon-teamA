using UnityEngine;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.RuntimeUI.Documents
{
    /// <summary>
    /// Match シーンに配置する UIDocument の参照ホルダー。
    /// Awake で静的要素をキャッシュし、CreatePanes で N-player 分のペインを動的生成する。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchUIDocument : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset _upgradeCardTemplate;
        [SerializeField] private VisualTreeAsset _playerHudTemplate;
        [SerializeField] private VisualTreeAsset _upgradePaneTemplate;
        [SerializeField] private VisualTreeAsset _resultPaneTemplate;

        public VisualTreeAsset UpgradeCardTemplate => _upgradeCardTemplate;

        // 静的要素
        public VisualElement UpgradeOverlayRoot { get; private set; }
        public VisualElement ResultRoot { get; private set; }
        public VisualElement ImpactFlashOverlay { get; private set; }

        // 動的生成されるペイン配列 (CreatePanes 後に有効)
        public VisualElement[] HudRoots { get; private set; }
        public VisualElement[] UpgradePanes { get; private set; }
        public VisualElement[] ResultPanes { get; private set; }

        private VisualElement _matchLayer;
        private VisualElement _upgradePanesContainer;
        private VisualElement _resultPanesContainer;
        private VisualElement _root;

        private void Awake()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;

            _matchLayer = _root.Q("MatchLayer");
            UpgradeOverlayRoot = _root.Q("UpgradeOverlayRoot");
            _upgradePanesContainer = _root.Q("UpgradePanesContainer");
            ResultRoot = _root.Q("ResultRoot");
            _resultPanesContainer = _root.Q("ResultPanesContainer");
            ImpactFlashOverlay = _root.Q("ImpactFlashOverlay");
        }

        /// <summary>
        /// N-player 分の HUD / 強化 / リザルト ペインを動的に生成する。
        /// PresentationInitializer.Initialize の最初に呼ばれる。
        /// </summary>
        public void CreatePanes(int playerCount)
        {
            var playerClasses = new[] { "p1", "p2", "p3", "p4" };

            HudRoots = new VisualElement[playerCount];
            UpgradePanes = new VisualElement[playerCount];
            ResultPanes = new VisualElement[playerCount];

            // レイアウトクラス
            string layoutClass = $"layout-{playerCount}p";

            // --- HUD ペイン ---
            if (playerCount == 3)
            {
                BuildThreePlayerLayout(_matchLayer, playerClasses, layoutClass,
                    (container, idx) =>
                    {
                        var hud = InstantiateHudPane(playerClasses[idx]);
                        container.Add(hud);
                        HudRoots[idx] = hud;
                    });
            }
            else
            {
                _matchLayer.AddToClassList(layoutClass);
                for (int i = 0; i < playerCount; i++)
                {
                    if (i > 0 && playerCount == 2)
                    {
                        var divider = new VisualElement();
                        divider.AddToClassList("match-divider");
                        _matchLayer.Add(divider);
                    }
                    var hud = InstantiateHudPane(playerClasses[i]);
                    _matchLayer.Add(hud);
                    HudRoots[i] = hud;
                }
            }

            // --- 強化ペイン ---
            if (playerCount == 3)
            {
                BuildThreePlayerLayout(_upgradePanesContainer, playerClasses, layoutClass,
                    (container, idx) =>
                    {
                        var pane = InstantiateUpgradePane(playerClasses[idx]);
                        container.Add(pane);
                        UpgradePanes[idx] = pane;
                    });
            }
            else
            {
                _upgradePanesContainer.AddToClassList(layoutClass);
                for (int i = 0; i < playerCount; i++)
                {
                    if (i > 0 && playerCount == 2)
                    {
                        var divider = new VisualElement();
                        divider.AddToClassList("match-divider");
                        _upgradePanesContainer.Add(divider);
                    }
                    var pane = InstantiateUpgradePane(playerClasses[i]);
                    _upgradePanesContainer.Add(pane);
                    UpgradePanes[i] = pane;
                }
            }

            // --- リザルトペイン ---
            if (playerCount == 3)
            {
                BuildThreePlayerLayout(_resultPanesContainer, playerClasses, layoutClass,
                    (container, idx) =>
                    {
                        var pane = InstantiateResultPane(playerClasses[idx]);
                        container.Add(pane);
                        ResultPanes[idx] = pane;
                    });
            }
            else
            {
                _resultPanesContainer.AddToClassList(layoutClass);
                for (int i = 0; i < playerCount; i++)
                {
                    if (i > 0 && playerCount == 2)
                    {
                        var divider = new VisualElement();
                        divider.AddToClassList("match-divider");
                        _resultPanesContainer.Add(divider);
                    }
                    var pane = InstantiateResultPane(playerClasses[i]);
                    _resultPanesContainer.Add(pane);
                    ResultPanes[i] = pane;
                }
            }
        }

        /// <summary>
        /// 3P レイアウト: 上段 row (P1, P2) + 下段 row (P3 中央)
        /// </summary>
        private void BuildThreePlayerLayout(VisualElement parent, string[] playerClasses,
            string layoutClass, System.Action<VisualElement, int> addPane)
        {
            parent.AddToClassList(layoutClass);

            var topRow = new VisualElement();
            topRow.AddToClassList("layout-3p-row");
            parent.Add(topRow);

            addPane(topRow, 0);
            addPane(topRow, 1);

            var bottomRow = new VisualElement();
            bottomRow.AddToClassList("layout-3p-row");
            parent.Add(bottomRow);

            addPane(bottomRow, 2);
        }

        private VisualElement InstantiateHudPane(string playerClass)
        {
            var hudRoot = new VisualElement();
            hudRoot.AddToClassList("hud-root");
            hudRoot.AddToClassList($"hud-root--{playerClass}");

            if (_playerHudTemplate != null)
            {
                var instance = _playerHudTemplate.Instantiate();
                hudRoot.Add(instance);
            }

            return hudRoot;
        }

        private VisualElement InstantiateUpgradePane(string playerClass)
        {
            VisualElement pane;
            if (_upgradePaneTemplate != null)
            {
                var container = _upgradePaneTemplate.Instantiate();
                pane = container.Q(className: "upgrade-pane") ?? container;
            }
            else
            {
                pane = new VisualElement();
                pane.AddToClassList("upgrade-pane");
            }
            pane.AddToClassList($"upgrade-pane--{playerClass}");
            return pane;
        }

        private VisualElement InstantiateResultPane(string playerClass)
        {
            VisualElement pane;
            if (_resultPaneTemplate != null)
            {
                var container = _resultPaneTemplate.Instantiate();
                pane = container.Q(className: "result-pane") ?? container;
            }
            else
            {
                pane = new VisualElement();
                pane.AddToClassList("result-pane");
            }
            pane.AddToClassList($"result-pane--{playerClass}");
            return pane;
        }
    }
}
