using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ReturnToMenu : MonoBehaviour
{
    [SerializeField] Button menuButton;
    [SerializeField] string mainMenuSceneName = "MainMenu";

    void Awake()
    {
        if (menuButton == null)
            menuButton = GetComponent<Button>();

        if (menuButton != null)
            menuButton.onClick.AddListener(GoToMainMenu);
    }

    void OnDestroy()
    {
        if (menuButton != null)
            menuButton.onClick.RemoveListener(GoToMainMenu);
    }

    void GoToMainMenu()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }
}
