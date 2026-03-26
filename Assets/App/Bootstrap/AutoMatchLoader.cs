using UnityEngine;
using UnityEngine.SceneManagement;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Title シーンに配置する一時的な自動遷移スクリプト。
    /// ProjectLifetimeScope の初期化完了後に Match シーンへ遷移する。
    /// Title UI が実装されたら削除する。
    /// </summary>
    public sealed class AutoMatchLoader : MonoBehaviour
    {
        [SerializeField] private float _delay = 0.5f;

        private void Start()
        {
            Invoke(nameof(LoadMatch), _delay);
        }

        private void LoadMatch()
        {
            SceneManager.LoadScene("Match");
        }
    }
}
