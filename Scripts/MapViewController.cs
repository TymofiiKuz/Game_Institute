using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управляет переключением режимов карты и связанными с ними кнопками.
/// Добавь этот компонент на объект UI и назначь ссылки на четыре кнопки.
/// </summary>
public class MapViewController : MonoBehaviour
{
    [SerializeField] private LevelController levelController;

    [Header("Buttons")]
    [SerializeField] private Button standardButton;
    [SerializeField] private Button influenceButton;
    [SerializeField] private Button stabilityButton;
    [SerializeField] private Button developmentButton;

    void Awake()
    {
        if (levelController == null)
            levelController = LevelController.Instance;
    }

    void OnEnable()
    {
        if (levelController == null)
            levelController = LevelController.Instance;

        RefreshButtonStates();
    }

    public void ShowStandardMap()    => SetMapView(LevelController.MapViewMode.Standard);
    public void ShowInfluenceMap()   => SetMapView(LevelController.MapViewMode.Influence);
    public void ShowStabilityMap()   => SetMapView(LevelController.MapViewMode.Stability);
    public void ShowDevelopmentMap() => SetMapView(LevelController.MapViewMode.Development);

    void SetMapView(LevelController.MapViewMode mode)
    {
        if (levelController == null)
            levelController = LevelController.Instance;

        if (levelController == null)
            return;

        levelController.SetMapViewMode(mode);
        RefreshButtonStates();
    }

    void RefreshButtonStates()
    {
        if (levelController == null)
            return;

        LevelController.MapViewMode mode = levelController.CurrentMapViewMode;
        SetButtonInteractable(standardButton,    mode != LevelController.MapViewMode.Standard);
        SetButtonInteractable(influenceButton,   mode != LevelController.MapViewMode.Influence);
        SetButtonInteractable(stabilityButton,   mode != LevelController.MapViewMode.Stability);
        SetButtonInteractable(developmentButton, mode != LevelController.MapViewMode.Development);
    }

    void SetButtonInteractable(Button button, bool isInteractable)
    {
        if (button != null)
            button.interactable = isInteractable;
    }
}
