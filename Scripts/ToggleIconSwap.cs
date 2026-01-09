using UnityEngine;
using UnityEngine.UI;

public class ToggleIconSwap : MonoBehaviour
{
    [SerializeField] Toggle toggle;
    [SerializeField] GameObject playIcon;
    [SerializeField] GameObject pauseIcon;

    void Awake()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        bool isOn = toggle != null && toggle.isOn;
        UpdateIcons(isOn);

        if (toggle != null)
            toggle.onValueChanged.AddListener(UpdateIcons);
    }

    void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(UpdateIcons);
    }

    void UpdateIcons(bool isOn)
    {
        if (playIcon != null)
            playIcon.SetActive(!isOn);
        if (pauseIcon != null)
            pauseIcon.SetActive(isOn);

        LevelController controller = LevelController.Instance;
        if (controller != null)
            controller.SetPauseFromUI(isOn);
    }
}
