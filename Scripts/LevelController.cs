using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class LevelController : MonoBehaviour
{
    public static LevelController Instance;

    public enum MapViewMode
    {
        Standard,
        Influence,
        Stability,
        Development
    }

    public Transform regionsContainer; // Родитель для UI регионов
    public GameObject regionUIPrefab; // Префаб UI-элемента региона
    public Sprite[] regionSprites;
    public RegionGridGenerator regionGridGenerator;
    public GraphicRaycaster uiRaycaster;
    public EventSystem eventSystem;
    [Header("Click Filtering")]
    [SerializeField] private RectTransform worldClickSurface;

    public Region SelectedRegion;
    [System.NonSerialized, HideInInspector] public List<Region> AllRegions = new List<Region>();
    [System.NonSerialized, HideInInspector] public List<Continent> Continents = new List<Continent>();

    [Header("Map View")]
    [SerializeField] private MapViewMode currentMapViewMode = MapViewMode.Standard;
    private readonly List<RegionUI> regionUIElements = new List<RegionUI>();

    public int Sanity = 100;
    public int Money = 100;
    public int Artifacts = 5;
    public float sanityRegenRate = 1f; // сколько разума добавлять в секунду
    private float sanityRegenTimer = 0f;
    public Text finalMessage;
    private PointerEventData pointerEventData;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private bool isPaused;
    private bool isGameOver;

    private void Awake()
    {
        Instance = this;

        if (regionGridGenerator == null)
            regionGridGenerator = GetComponent<RegionGridGenerator>();

        if (regionGridGenerator == null && regionsContainer != null)
            regionGridGenerator = regionsContainer.GetComponent<RegionGridGenerator>();

        if (uiRaycaster == null)
        {
            UIManager uiManager = UIManager.Instance != null ? UIManager.Instance : FindFirstObjectByType<UIManager>();
            if (uiManager != null)
                uiRaycaster = uiManager.GetComponentInParent<GraphicRaycaster>();

            if (uiRaycaster == null)
                uiRaycaster = FindFirstObjectByType<GraphicRaycaster>();
        }

        if (eventSystem == null)
            eventSystem = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();

        if (worldClickSurface == null && regionsContainer != null)
        {
            worldClickSurface = regionsContainer as RectTransform;
            if (worldClickSurface == null)
                worldClickSurface = regionsContainer.GetComponent<RectTransform>();
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;
        isPaused = false;
        isGameOver = false;
        GenerateRegions();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateSanity(Sanity);
            UIManager.Instance.UpdateMoney(Money);
            UIManager.Instance.UpdateArtifacts(Artifacts);
            UIManager.Instance.HideRegionPanel();
            UIManager.Instance.SyncPauseToggle(false);
        }
        SelectedRegion = null;
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        for (int i = 0; i < AllRegions.Count; i++)
        {
            AllRegions[i].UpdateModifiers(deltaTime);
        }

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshSelectedRegion();

        if (Input.GetMouseButtonDown(0) && !IsPointerOverRegionPanel() && !IsPointerOverRegionUI())
        {
            DeselectRegion();
            if (UIManager.Instance != null)
                UIManager.Instance.HideRegionPanel();
        }

        // Время с последнего обновления
        sanityRegenTimer += deltaTime;
        if (sanityRegenTimer >= 10f) // каждые 10 секунду
        {
            ChangeSanity((int)sanityRegenRate); // если нужно не дробное, а целое значение
            sanityRegenTimer = 0f;
        }

    }

    void GenerateRegions()
    {
        ClearRegionUIRegistry();
        if (regionGridGenerator != null)
        {
            regionGridGenerator.GenerateRegions();
            RefreshAllRegionVisuals();
            return;
        }

        Debug.LogWarning("LevelController: RegionGridGenerator not assigned, regions will not be generated.");
    }

    public void SelectRegion(Region region)
    {
        SelectedRegion = region;
        if (UIManager.Instance != null)
            UIManager.Instance.ShowRegionInfo(region);
    }

    public void DeselectRegion()
    {
        SelectedRegion = null;
        if (UIManager.Instance != null)
            UIManager.Instance.HideRegionPanel();
    }

    public void ChangeSanity(int amount)
    {
        Sanity = Mathf.Clamp(Sanity + amount, 0, 100);
        UIManager.Instance.UpdateSanity(Sanity);
        CheckGameOver();
    }

    public void ChangeMoney(int amount)
    {
        Money = Mathf.Max(0, Money + amount);
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateMoney(Money);
    }

    public void ChangeArtifacts(int amount)
    {
        Artifacts = Mathf.Max(0, Artifacts + amount);
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateArtifacts(Artifacts);
    }

    public void ShowWorldStats()
    {
        SelectedRegion = null;
        if (UIManager.Instance == null)
            return;

        UIManager.Instance.ShowWorldStats();
    }

    public void SetPauseFromUI(bool pause)
    {
        if (isGameOver)
        {
            SyncPauseState(true);
            return;
        }

        SyncPauseState(pause);
    }

    public void SyncPauseState(bool pause)
    {
        isPaused = pause;
        Time.timeScale = isPaused ? 0f : 1f;

        if (UIManager.Instance != null)
            UIManager.Instance.SyncPauseToggle(isPaused);
    }

    public bool TryGetWorldAverages(out float influence, out float stability, out float development)
    {
        influence = 0f;
        stability = 0f;
        development = 0f;

        int count = AllRegions.Count;
        if (count == 0)
            return false;

        for (int i = 0; i < count; i++)
        {
            Region region = AllRegions[i];
            influence += region.Influence;
            stability += region.Stability;
            development += region.Development;
        }

        float invCount = 1f / count;
        influence *= invCount;
        stability *= invCount;
        development *= invCount;
        return true;
    }


    bool IsPointerOverRegionPanel()
    {
        Transform regionPanelTransform = UIManager.Instance != null ? UIManager.Instance.RegionPanelTransform : null;
        if (regionPanelTransform == null)
            return false;

        GameObject panelObject = regionPanelTransform.gameObject;
        if (!panelObject.activeInHierarchy)
            return false;

        RectTransform panelRect = regionPanelTransform as RectTransform;
        if (panelRect == null)
            return false;

        Camera eventCamera = GetCameraForRect(panelRect);
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, eventCamera);
    }

    bool IsPointerOverRegionUI()
    {
        EventSystem activeEventSystem = eventSystem != null ? eventSystem : EventSystem.current;

        if (activeEventSystem == null)
            return false;

        if (pointerEventData == null)
            pointerEventData = new PointerEventData(activeEventSystem);

        pointerEventData.Reset();
        pointerEventData.position = Input.mousePosition;

        raycastResults.Clear();
        activeEventSystem.RaycastAll(pointerEventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            GameObject go = raycastResults[i].gameObject;
            if (go == null)
                continue;

            if (go.GetComponentInParent<RegionUI>() != null)
                return true;
        }

        return false;
    }

    bool IsPointerOverWorldSurface()
    {
        Camera eventCamera = GetEventCamera();

        // Primary check: configured surface (usually the regions container).
        if (worldClickSurface != null &&
            RectTransformUtility.RectangleContainsScreenPoint(worldClickSurface, Input.mousePosition, eventCamera))
            return true;

        // Fallback to the full canvas so clicks anywhere on the background can deselect.
        RectTransform canvasRect = null;
        if (uiRaycaster != null)
            canvasRect = uiRaycaster.transform as RectTransform;
        if (canvasRect == null && worldClickSurface != null)
            canvasRect = worldClickSurface.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();

        if (canvasRect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(canvasRect, Input.mousePosition, eventCamera))
            return true;

        // If nothing is set up, allow the click by default.
        return worldClickSurface == null;
    }

    bool IsInteractiveUIElement(GameObject go)
    {
        if (go.GetComponentInParent<RegionUI>() != null)
            return true;

        if (go.GetComponentInParent<Selectable>() != null)
            return true;

        return go.GetComponentInParent<IPointerClickHandler>() != null;
    }

    Camera GetEventCamera()
    {
        if (uiRaycaster != null && uiRaycaster.eventCamera != null)
            return uiRaycaster.eventCamera;

        return Camera.main;
    }

    Camera GetCameraForRect(RectTransform rect)
    {
        if (rect == null)
            return GetEventCamera();

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
            return GetEventCamera();

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
            return canvas.worldCamera;

        return GetEventCamera();
    }

    void ClearRegionUIRegistry()
    {
        regionUIElements.Clear();
    }

    public MapViewMode CurrentMapViewMode => currentMapViewMode;

    public void RegisterRegionUI(RegionUI regionUI)
    {
        if (regionUI == null || regionUIElements.Contains(regionUI))
            return;

        regionUIElements.Add(regionUI);
        regionUI.RefreshVisual(currentMapViewMode);
    }

    public void UnregisterRegionUI(RegionUI regionUI)
    {
        if (regionUI == null)
            return;

        regionUIElements.Remove(regionUI);
    }

    public void SetMapViewMode(MapViewMode mode)
    {
        if (currentMapViewMode == mode)
            return;

        currentMapViewMode = mode;
        RefreshAllRegionVisuals();
    }

    public void RefreshAllRegionVisuals()
    {
        for (int i = 0; i < regionUIElements.Count; i++)
        {
            RegionUI regionUI = regionUIElements[i];
            if (regionUI != null)
                regionUI.RefreshVisual(currentMapViewMode);
        }
    }

    public void OnRegionStatsChanged(Region region)
    {
        if (region == null)
            return;

        for (int i = 0; i < regionUIElements.Count; i++)
        {
            RegionUI regionUI = regionUIElements[i];
            if (regionUI != null && regionUI.GetRegion() == region)
                regionUI.RefreshVisual(currentMapViewMode);
        }

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateRegionInfo(region);
    }

    public void CheckGameOver()
    {
        int sanity = Sanity;
        // Check for victory
        float totalInfluence = 0f, totalStability = 0f, totalDevelopment = 0f;
        int count = AllRegions.Count;

        foreach (var region in AllRegions)
        {
            totalInfluence += region.Influence;
            totalStability += region.Stability;
            totalDevelopment += region.Development;
        }

        float avgInfluence = totalInfluence / count;
        float avgStability = totalStability / count;
        float avgDevelopment = totalDevelopment / count;

        if (Mathf.Approximately(avgInfluence, 20f) && Mathf.Approximately(avgStability, 20f) && Mathf.Approximately(avgDevelopment, 20f))
        {
            GameWin();
            return;
        }

        // Check for defeat
        if (sanity <= 0)
        {
            GameLose();
        }
    }

    public void GameWin()
    {
        Debug.Log("You win!");
        UIManager.Instance.ShowEndScreen(true);
        Time.timeScale = 0;
        isPaused = true;
        isGameOver = true;
        UIManager.Instance.SyncPauseToggle(true);
    }

    public void GameLose()
    {
        Debug.Log("You lose!");
        UIManager.Instance.ShowEndScreen(false);
        Time.timeScale = 0;
        isPaused = true;
        isGameOver = true;
        UIManager.Instance.SyncPauseToggle(true);
    }
}
