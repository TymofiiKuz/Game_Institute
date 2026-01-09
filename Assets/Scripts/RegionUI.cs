using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RegionUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI")]
    public Text regionNameText;
    public Image regionImage;         // назначи в инспекторе
    public Color hoverColor = new Color(0.65f, 0.65f, 0.65f, 0.35f);

    [Header("Data")]
    private Region region;

    // Цвета, чтобы корректно восстанавливать внешний вид
    private Color _initialColor = Color.white;
    private Color _baseColor = Color.white;
    private bool _hasInitialColor;

    void Awake()
    {
        if (regionImage != null)
        {
            _initialColor = regionImage.color;
            _baseColor = _initialColor;
            _hasInitialColor = true;
        }
    }

    public void SetRegion(Region newRegion)
    {
        if (region == newRegion)
        {
            RefreshVisual();
            return;
        }

        if (region != null)
            region.StatsChanged -= OnRegionStatsChanged;

        region = newRegion;

        if (region != null)
            region.StatsChanged += OnRegionStatsChanged;

        if (LevelController.Instance != null)
            LevelController.Instance.RegisterRegionUI(this);

        RefreshVisual();
    }

    // Старый клик оставляю как “обычный выбор” региона.
    // Он нужен, когда нет таргетинга.
    public void OnClick()
    {
        if (region == null) return;
        Debug.Log("Region clicked: " + region.Name);
        if (LevelController.Instance != null)
            LevelController.Instance.SelectRegion(region);
    }

    // Hover
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHover(true, hoverColor);

        // Показ тултипа при наведении
        if (Tooltip.Instance != null && region != null)
            Tooltip.Instance.Show(region.GetTooltip());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHover(false);

        if (Tooltip.Instance != null)
            Tooltip.Instance.Hide();
    }

    // Клик по UI-региону: если включен таргетинг, отдаём выбор менеджеру.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (region == null) return;

        OnClick();
    }

    // Удобный вызов для внешней подсветки (если когда-нибудь понадобится)
    public void SetHover(bool on)
    {
        SetHover(on, hoverColor);
    }

    public void SetHover(bool on, Color color)
    {
        if (regionImage == null) return;
        if (on)
        {
            // Лёгкая подсветка поверх базового цвета
            Color highlight = Color.Lerp(_baseColor, color, 0.5f);
            highlight.a = Mathf.Max(_baseColor.a, highlight.a);
            regionImage.color = highlight;
        }
        else
        {
            regionImage.color = _baseColor;
        }
    }

    public Region GetRegion()
    {
        return region;
    }

    public void RefreshVisual()
    {
        LevelController controller = LevelController.Instance;
        LevelController.MapViewMode mode = controller != null
            ? controller.CurrentMapViewMode
            : LevelController.MapViewMode.Standard;

        RefreshVisual(mode);
    }

    public void RefreshVisual(LevelController.MapViewMode mode)
    {
        if (regionImage != null)
        {
            _baseColor = CalculateRegionColor(mode);
            regionImage.color = _baseColor;
        }

        if (regionNameText != null)
            regionNameText.text = BuildRegionLabel(mode);
    }

    Color CalculateRegionColor(LevelController.MapViewMode mode)
    {
        if (region == null)
            return _hasInitialColor ? _initialColor : Color.white;

        switch (mode)
        {
            case LevelController.MapViewMode.Influence:
                return GetGradientColor(region.Influence);
            case LevelController.MapViewMode.Stability:
                return GetGradientColor(region.Stability);
            case LevelController.MapViewMode.Development:
                return GetGradientColor(region.Development);
            default:
                return _hasInitialColor ? _initialColor : Color.white;
        }
    }

    string BuildRegionLabel(LevelController.MapViewMode mode)
    {
        if (region == null)
            return string.Empty;

        switch (mode)
        {
            case LevelController.MapViewMode.Influence:
                return $"{region.Name}\nInfluence: {region.Influence}";
            case LevelController.MapViewMode.Stability:
                return $"{region.Name}\nStability: {region.Stability}";
            case LevelController.MapViewMode.Development:
                return $"{region.Name}\nDevelopment: {region.Development}";
            default:
                return region.Name;
        }
    }

    Color GetGradientColor(int value)
    {
        float t = Mathf.InverseLerp(0f, 20f, Mathf.Clamp(value, 0, 20));
        return Color.Lerp(Color.red, Color.green, t);
    }

    void OnRegionStatsChanged(Region changedRegion)
    {
        if (changedRegion != region)
            return;

        RefreshVisual();
    }

    void OnDestroy()
    {
        if (region != null)
            region.StatsChanged -= OnRegionStatsChanged;

        if (LevelController.Instance != null)
            LevelController.Instance.UnregisterRegionUI(this);
    }
}
