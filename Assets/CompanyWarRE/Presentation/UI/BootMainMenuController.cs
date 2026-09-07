using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.UI
{
    /// <summary>Runtime actions for the generated Boot main menu hierarchy.</summary>
    public sealed class BootMainMenuController : MonoBehaviour
    {
        private const string LastLevelKey = "CompanyWar.LastLevel";

        [SerializeField] private GameObject mainMenuCanvas;
        [SerializeField] private GameObject levelSelectRoot;
        [SerializeField] private GameObject mailPanel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button mailButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button closeMailButton;
        [SerializeField] private TMP_Text continueHint;
        [SerializeField] private string battleSceneName = "FormalBattle";

        public void Configure(
            GameObject menuCanvas,
            GameObject levelSelect,
            GameObject mail,
            Button start,
            Button continueGame,
            Button queryMail,
            Button quit,
            Button closeMail,
            TMP_Text hint,
            string battleScene)
        {
            mainMenuCanvas = menuCanvas;
            levelSelectRoot = levelSelect;
            mailPanel = mail;
            startButton = start;
            continueButton = continueGame;
            mailButton = queryMail;
            quitButton = quit;
            closeMailButton = closeMail;
            continueHint = hint;
            battleSceneName = string.IsNullOrWhiteSpace(battleScene) ? "FormalBattle" : battleScene;
        }

        private void Awake()
        {
            Bind(startButton, OpenLevelSelect);
            Bind(continueButton, ContinueGame);
            Bind(mailButton, ToggleMail);
            Bind(quitButton, QuitGame);
            Bind(closeMailButton, ToggleMail);
            if (mailPanel != null) mailPanel.SetActive(false);
        }

        public void ShowMainMenu()
        {
            if (levelSelectRoot != null) levelSelectRoot.SetActive(false);
            if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
        }

        public void OpenLevelSelect()
        {
            if (levelSelectRoot == null)
            {
                SetHint("选关 UI 尚未绑定，请在 Boot 场景中创建 LevelSelectCanvas 后重新生成主界面。");
                Debug.LogWarning("Boot main menu could not find LevelSelectCanvas.", this);
                return;
            }

            levelSelectRoot.SetActive(true);
            if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);
        }

        public void ContinueGame()
        {
            if (!PlayerPrefs.HasKey(LastLevelKey))
            {
                SetHint("暂无可用存档，请先开始新作战");
                return;
            }

            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(battleSceneName))
            {
                SetHint($"场景 {battleSceneName} 未加入 Build Settings");
                return;
            }

            SceneManager.LoadSceneAsync(battleSceneName, LoadSceneMode.Single);
        }

        public void ToggleMail()
        {
            if (mailPanel != null) mailPanel.SetActive(!mailPanel.activeSelf);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Quit request triggered from Boot main menu.", this);
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private void SetHint(string value)
        {
            if (continueHint != null) continueHint.text = value;
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
