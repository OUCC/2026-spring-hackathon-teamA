using UnityEngine;
using UnityEngine.SceneManagement;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.UI.RuntimeUI.Documents;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// タイトル画面のボタンイベントと BGM を管理する Presenter。
    /// TitleUIDocument の Awake 後に生成される。
    /// </summary>
    public sealed class TitlePresenter
    {
        public TitlePresenter(TitleUIDocument doc, IAudioService audio)
        {
            // BGM 再生
            audio?.PlayBgm(SfxIds.BgmTitle);

            // 2P 対戦
            doc.ModeButton2P?.RegisterCallback<UnityEngine.UIElements.ClickEvent>(_ =>
            {
                audio?.StopBgm(0.5f);
                SceneManager.LoadScene("Match");
            });

            // 1P / CPU — 無効 (Coming Soon)
            doc.ModeButton1P?.SetEnabled(false);
            doc.ModeButtonCPU?.SetEnabled(false);

            // 終了
            doc.QuitButton?.RegisterCallback<UnityEngine.UIElements.ClickEvent>(_ =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
    }
}
