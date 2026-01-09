using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Region Panel")]
    [SerializeField] private GameObject regionPanel;

    [Header("Region Detail References")]
    public Text nameText;
    public Text influenceText;
    public Text stabilityText;
    public Text developmentText;
    public Text modifiersText;

    [Header("Player Resources")]
    public Text sanityText;
    public Text moneyText;
    public Text artifactsText;
    public Toggle pauseToggle;

    bool suppressPauseToggleCallback;
    bool regionPanelWarningLogged;

    enum RegionPanelMode
    {
        Hidden,
        Region,
        World
    }

    RegionPanelMode regionPanelMode = RegionPanelMode.Hidden;

    [Header("Misc UI")]
    public Text timerText;
    public GameObject endScreen;
    public Text endText;

    [Header("World Overview Labels")]
    public string worldTitle = "World";
    public string worldInfluenceLabel = "Avg Influence: ";
    public string worldStabilityLabel = "Avg Stability: ";
    public string worldDevelopmentLabel = "Avg Development: ";

    public RectTransform RegionPanelTransform
    {
        get
        {
            if (regionPanel == null)
                return null;
            RectTransform rect = regionPanel.transform as RectTransform;
            return rect != null ? rect : regionPanel.GetComponent<RectTransform>();
        }
    }

    private void Awake()
    {
        Instance = this;
        if (endScreen != null)
            endScreen.SetActive(false);

        if (pauseToggle != null)
            pauseToggle.onValueChanged.AddListener(OnPauseToggleChanged);
    }

    private void OnDestroy()
    {
        if (pauseToggle != null)
            pauseToggle.onValueChanged.RemoveListener(OnPauseToggleChanged);
    }

    public void ShowRegionInfo(Region region)
    {
        if (region == null)
        {
            HideRegionPanel();
            return;
        }

        regionPanelMode = RegionPanelMode.Region;
        ShowRegionPanel();
        ApplyRegionInfo(region);
    }

    public void UpdateSanity(int sanity)
    {
        if (sanityText != null)
            sanityText.text = sanity.ToString();
    }

    public void UpdateMoney(int money)
    {
        if (moneyText != null)
            moneyText.text = money.ToString();
    }

    public void UpdateArtifacts(int artifacts)
    {
        if (artifactsText != null)
            artifactsText.text = artifacts.ToString();
    }

    public void UpdateRegionInfo(Region region)
    {
        LevelController controller = LevelController.Instance;
        if (controller == null)
            return;

        if (regionPanelMode != RegionPanelMode.Region)
            return;

        if (controller.SelectedRegion == null)
        {
            HideRegionPanel();
            return;
        }

        if (region == controller.SelectedRegion && region != null)
        {
            ShowRegionPanel();
            ApplyRegionInfo(region);
        }
    }

    public void UpdateTimer(float timeElapsed)
    {
        if (timerText == null)
            return;

        GameDateTracker calendar = GameDateTracker.Instance;
        if (calendar != null)
        {
            timerText.text = calendar.FormatDateLabel(timeElapsed);
            return;
        }

        int minutes = Mathf.FloorToInt(timeElapsed / 60f);
        int seconds = Mathf.FloorToInt(timeElapsed % 60f);
        timerText.text = string.Format("Time: {0:00}:{1:00}", minutes, seconds);
    }

    public void RefreshSelectedRegion()
    {
        LevelController controller = LevelController.Instance;
        if (controller == null)
            return;

        switch (regionPanelMode)
        {
            case RegionPanelMode.Region:
            {
                Region selected = controller.SelectedRegion;
                if (selected == null)
                {
                    HideRegionPanel();
                    return;
                }

                ShowRegionPanel();
                ApplyRegionInfo(selected);
                return;
            }
            case RegionPanelMode.World:
                ApplyWorldStats();
                return;
            default:
                HideRegionPanel();
                return;
        }
    }

    public void ShowEndScreen(bool isWin)
    {
        if (endScreen != null)
            endScreen.SetActive(true);
        if (endText != null)
            endText.text = isWin ? "You Win!" : "You Lose!";
    }

    public void SyncPauseToggle(bool isPaused)
    {
        if (pauseToggle != null)
        {
            suppressPauseToggleCallback = true;
            pauseToggle.isOn = isPaused;
            suppressPauseToggleCallback = false;
        }

    }

    void OnPauseToggleChanged(bool isOn)
    {
        if (suppressPauseToggleCallback)
            return;

        if (LevelController.Instance != null)
            LevelController.Instance.SetPauseFromUI(isOn);
    }

    public void ShowWorldStats()
    {
        regionPanelMode = RegionPanelMode.World;
        ApplyWorldStats();
    }

    void ApplyWorldStats()
    {
        if (regionPanelMode == RegionPanelMode.Hidden)
            return;

        ShowRegionPanel();

        float influence = 0f;
        float stability = 0f;
        float development = 0f;

        LevelController controller = LevelController.Instance;
        bool hasData = controller != null && controller.TryGetWorldAverages(out influence, out stability, out development);

        if (nameText != null)
            nameText.text = worldTitle;
        if (influenceText != null)
            influenceText.text = hasData ? influence.ToString("F1") : "--";
        if (stabilityText != null)
            stabilityText.text = hasData ? stability.ToString("F1") : "--";
        if (developmentText != null)
            developmentText.text = hasData ? development.ToString("F1") : "--";

        ClearModifierText();
    }

    public void HideRegionPanel()
    {
        regionPanelMode = RegionPanelMode.Hidden;

        GameObject panel = ResolveRegionPanel();
        if (panel != null && panel.activeSelf)
            panel.SetActive(false);
    }

    void ShowRegionPanel()
    {
        if (regionPanelMode == RegionPanelMode.Hidden)
            return;

        GameObject panel = ResolveRegionPanel();
        if (panel != null && !panel.activeSelf)
            panel.SetActive(true);
    }

    void ApplyRegionInfo(Region region)
    {
        if (region == null)
            return;

        if (nameText != null)
            nameText.text = region.Name;
        if (influenceText != null)
            influenceText.text = region.Influence.ToString();
        if (stabilityText != null)
            stabilityText.text = region.Stability.ToString();
        if (developmentText != null)
            developmentText.text = region.Development.ToString();

        UpdateModifierList(region);
    }

    void UpdateModifierList(Region region)
    {
        if (modifiersText == null)
            return;

        IReadOnlyList<RegionModifier> modifiers = region.ActiveModifiers;

        if (modifiers == null || modifiers.Count == 0)
        {
            modifiersText.text = "Modifiers: none";
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Modifiers:");

        for (int i = 0; i < modifiers.Count; i++)
        {
            RegionModifier modifier = modifiers[i];
            builder.Append("- ");
            builder.Append(modifier.Name);

            float remaining = modifier.RemainingDuration;
            if (remaining >= 0f)
            {
                builder.Append(" (");
                builder.Append(Mathf.CeilToInt(remaining));
                builder.Append("s)");
            }

            if (i < modifiers.Count - 1)
                builder.AppendLine();
        }

        modifiersText.text = builder.ToString();
    }

    void ClearModifierText(bool showNA = true)
    {
        if (modifiersText != null)
            modifiersText.text = showNA ? "Modifiers: n/a" : string.Empty;
    }

    GameObject ResolveRegionPanel()
    {
        if (regionPanel != null)
            return regionPanel;

        Transform candidate = null;
        if (nameText != null)
            candidate = nameText.transform.parent;
        else if (influenceText != null)
            candidate = influenceText.transform.parent;
        else if (stabilityText != null)
            candidate = stabilityText.transform.parent;
        else if (developmentText != null)
            candidate = developmentText.transform.parent;

        regionPanel = candidate != null ? candidate.gameObject : null;

        if (regionPanel == null && !regionPanelWarningLogged)
        {
            Debug.LogWarning("UIManager: Region panel reference is not set; please assign it in the inspector.");
            regionPanelWarningLogged = true;
        }

        return regionPanel;
    }
}
