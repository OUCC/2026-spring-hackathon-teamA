using UnityEngine;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.RuntimeUI.Documents
{
    /// <summary>
    /// Match シーンに配置する UIDocument の参照ホルダー。
    /// Awake で全名前付き要素をキャッシュし、Presenter に提供する。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchUIDocument : MonoBehaviour
    {
        private UIDocument _document;

        // --- HUD ---
        public VisualElement LeftHudRoot { get; private set; }
        public VisualElement RightHudRoot { get; private set; }

        // --- 強化オーバーレイ ---
        public VisualElement UpgradeOverlayRoot { get; private set; }

        // --- リザルト ---
        public VisualElement ResultRoot { get; private set; }

        // --- カードテンプレート ---
        [SerializeField] private VisualTreeAsset _upgradeCardTemplate;
        public VisualTreeAsset UpgradeCardTemplate => _upgradeCardTemplate;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            LeftHudRoot = root.Q("LeftHud");
            RightHudRoot = root.Q("RightHud");
            UpgradeOverlayRoot = root.Q("UpgradeOverlayRoot");
            ResultRoot = root.Q("ResultRoot");
        }
    }
}
