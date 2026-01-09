using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a configurable grid of region UI elements at runtime, wiring them up with the LevelController.
/// </summary>
public class RegionGridGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelController levelController;
    [SerializeField] private RectTransform regionsContainer;
    [SerializeField] private GameObject regionUIPrefab;

    [Header("Placement Settings")]
    [SerializeField] private bool useGridLayout = false;
    [SerializeField] private int rows = 3;
    [SerializeField] private int columns = 6;
    [SerializeField] private Vector2 cellSize = new Vector2(180f, 120f);
    [SerializeField] private Vector2 spacing = new Vector2(20f, 20f);
    [SerializeField] private Vector2 randomAreaPadding = new Vector2(40f, 40f);
    [SerializeField] private float separationMultiplier = 1.1f;
    [SerializeField] private int placementAttemptsPerRegion = 60;

    [Header("Voronoi Generation")]
    [SerializeField] private bool useVoronoiLayout = false;
    [SerializeField] private VoronoiRegionGenerator voronoiGenerator;
    [SerializeField] private bool syncVoronoiBoundsToContainer = true;
    [SerializeField] private Vector2 voronoiBoundsPadding = new Vector2(40f, 40f);

    [Header("Graph Generation")]
    [SerializeField] private bool generateContinentalGraphs = true;
    [SerializeField, Range(1, 8)] private int continentCount = 3;
    [SerializeField, Range(2, 12)] private int minRegionsPerContinent = 3;
    [SerializeField, Range(2, 16)] private int maxRegionsPerContinent = 6;
    [SerializeField] private Vector2 continentJitter = new Vector2(80f, 60f);
    [SerializeField] private Vector2 regionScatter = new Vector2(160f, 120f);
    [SerializeField] private int extraEdgesPerContinent = 1;
    [SerializeField, Range(0f, 1f)] private float crossContinentConnectionChance = 0.35f;
    [SerializeField, Range(0.6f, 1.4f)] private float hexSpacingFactor = 0.9f;
    [SerializeField] private float hexJitter = 12f;
    [SerializeField] private float continentInnerPadding = 14f;
    [SerializeField] private int relaxationIterations = 3;
    [SerializeField, Range(0.1f, 1f)] private float relaxationStep = 0.35f;
    [SerializeField, Range(0.8f, 1.5f)] private float continentAspectRatio = 1.15f;
    [SerializeField, Range(0.8f, 2.5f)] private float blobSpacingMultiplier = 1.15f;
    [SerializeField, Range(0.4f, 1.2f)] private float intraContinentSpacing = 0.82f;
    [SerializeField, Range(0f, 0.45f)] private float continentCenterVoid = 0.2f;
    [SerializeField] private float continentGapPadding = 60f;

    [Header("Region Styling")]
    [SerializeField] private Vector2 outlineDistance = new Vector2(2f, 2f);
    [SerializeField, Range(0f, 1f)] private float alphaHitThreshold = 0.1f;
    [SerializeField] private int textureSize = 128;
    [SerializeField, Range(0.01f, 0.2f)] private float edgeFeather = 0.06f;

    private readonly List<UnityEngine.Object> generatedAssets = new List<UnityEngine.Object>();
    private static readonly string[] RegionNamePool =
    {
        "Avalon", "Borealis", "Caldera", "Drakonia", "Elysia", "Fjorlan",
        "Galatia", "Hinterfeld", "Icarus", "Juniper", "Kryptos", "Lunaris",
        "Moravia", "Nereus", "Orinthia", "Peregrine", "Quintara", "Rivenvale",
        "Solstice", "Thaloria", "Umbra", "Valis", "Weyland", "Xebec",
        "Yonder", "Zephyria"
    };
    private static readonly string[] ContinentNamePool =
    {
        "Aldara", "Bralin", "Cyrenth", "Dalvoria", "Ekrion", "Falandis",
        "Galor", "Hesper", "Ithoria", "Jandrel", "Karev", "Lysmar",
        "Myrr", "Nhal", "Orvantis", "Pyrith", "Quorin", "Rhazor",
        "Sylene", "Tharos", "Ulvem", "Varkesh", "Wyren", "Xandria",
        "Yllin", "Zaroth"
    };

    private class RegionSpawnData
    {
        public Region Region;
        public Vector2 Position;
        public Continent Continent;

        public RegionSpawnData(Region region, Vector2 position, Continent continent)
        {
            Region = region;
            Position = position;
            Continent = continent;
        }
    }

    private void Awake()
    {
        if (levelController == null)
            levelController = GetComponent<LevelController>() ?? LevelController.Instance;

        if (regionsContainer == null && levelController != null && levelController.regionsContainer != null)
            regionsContainer = levelController.regionsContainer as RectTransform;

        if (regionUIPrefab == null && levelController != null)
            regionUIPrefab = levelController.regionUIPrefab;

        if (useVoronoiLayout && voronoiGenerator == null)
            voronoiGenerator = GetComponent<VoronoiRegionGenerator>();
    }

    public void GenerateRegions()
    {
        if (levelController == null)
            levelController = LevelController.Instance;

        if (regionsContainer == null && levelController != null && levelController.regionsContainer != null)
            regionsContainer = levelController.regionsContainer as RectTransform;

        if (regionUIPrefab == null && levelController != null)
            regionUIPrefab = levelController.regionUIPrefab;

        if (levelController == null || regionsContainer == null || regionUIPrefab == null)
        {
            Debug.LogError("RegionGridGenerator: missing reference, cannot generate regions.");
            return;
        }

        CleanupGeneratedAssets();
        ClearExistingChildren();

        levelController.AllRegions.Clear();
        if (levelController.Continents != null)
            levelController.Continents.Clear();

        List<RegionSpawnData> spawnPlan = useVoronoiLayout
            ? BuildVoronoiSpawnPlan()
            : (generateContinentalGraphs ? BuildContinentalSpawnPlan() : BuildGridSpawnPlan());

        SpawnRegions(spawnPlan);
    }

    List<RegionSpawnData> BuildGridSpawnPlan()
    {
        int totalRegions = rows * columns;
        List<RegionSpawnData> plan = new List<RegionSpawnData>(totalRegions);
        List<string> availableNames = new List<string>(RegionNamePool);

        List<Vector2> placedPositions = new List<Vector2>(totalRegions);
        List<float> placementRadii = new List<float>(totalRegions);
        RectTransform containerRect = regionsContainer;

        Continent mainland = new Continent("Mainland", Vector2.zero);
        if (levelController != null)
            levelController.Continents.Add(mainland);

        for (int index = 0; index < totalRegions; index++)
        {
            string regionName = PickRegionName(availableNames, index);
            Region region = new Region(regionName);
            mainland.AddRegion(region);

            Vector2 position = useGridLayout
                ? CalculateGridPosition(index)
                : CalculateRandomPosition(cellSize, placedPositions, placementRadii, containerRect);

            placedPositions.Add(position);
            placementRadii.Add(Mathf.Max(cellSize.x, cellSize.y) * 0.5f);

            plan.Add(new RegionSpawnData(region, position, mainland));
        }

        return plan;
    }

    List<RegionSpawnData> BuildContinentalSpawnPlan()
    {
        int totalRegions = Mathf.Max(1, rows * columns);
        int desiredContinents = Mathf.Clamp(continentCount, 1, totalRegions);

        List<RegionSpawnData> plan = new List<RegionSpawnData>(totalRegions);
        List<string> availableNames = new List<string>(RegionNamePool);
        List<Vector2> placedPositions = new List<Vector2>(totalRegions);
        List<float> placementRadii = new List<float>(totalRegions);
        RectTransform containerRect = regionsContainer;
        Dictionary<Region, Vector2> positionLookup = new Dictionary<Region, Vector2>(totalRegions);

        List<int> distribution = DistributeRegionsAcrossContinents(totalRegions, desiredContinents);
        List<Vector2> centers = BuildContinentCenters(distribution, containerRect);

        for (int c = 0; c < distribution.Count; c++)
        {
            Vector2 center = centers[c];
            string continentName = PickContinentName(c);
            Continent continent = new Continent(continentName, center);
            if (levelController != null)
                levelController.Continents.Add(continent);

            int regionsInContinent = distribution[c];
            Vector2 halfSize = ClampExtentToContainer(GetContinentExtent(regionsInContinent), containerRect);
            List<Vector2> continentPositions = GenerateContinentPositionsBlob(
                regionsInContinent,
                center,
                halfSize,
                containerRect,
                placedPositions,
                placementRadii);

            for (int r = 0; r < regionsInContinent; r++)
            {
                int globalIndex = plan.Count;
                string regionName = PickRegionName(availableNames, globalIndex);
                Region region = new Region(regionName);
                continent.AddRegion(region);

                Vector2 position = continentPositions[r];
                placedPositions.Add(position);
                placementRadii.Add(Mathf.Max(cellSize.x, cellSize.y) * 0.5f);
                positionLookup[region] = position;

                plan.Add(new RegionSpawnData(region, position, continent));
            }

            ConnectContinentRegions(continent, positionLookup);
        }

        ConnectContinents(positionLookup);
        return plan;
    }

    void SpawnRegions(List<RegionSpawnData> spawnPlan)
    {
        if (spawnPlan == null)
            return;

        for (int i = 0; i < spawnPlan.Count; i++)
        {
            RegionSpawnData data = spawnPlan[i];
            if (data == null || data.Region == null)
                continue;

            Region region = data.Region;
            if (levelController != null)
            {
                levelController.AllRegions.Add(region);
                region.StatsChanged += levelController.OnRegionStatsChanged;
            }

            GameObject uiObject = Instantiate(regionUIPrefab, regionsContainer);
            uiObject.name = $"Region_{region.Name}";

            RectTransform rectTransform = uiObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = cellSize;
                rectTransform.anchoredPosition = data.Position;
            }

            RegionUI regionUI = uiObject.GetComponent<RegionUI>();
            if (regionUI != null)
            {
                regionUI.SetRegion(region);

                if (regionUI.regionImage != null)
                {
                    Sprite sprite = CreateRandomRegionSprite();
                    ConfigureRegionImage(regionUI.regionImage, sprite);
                }
            }

            Button button = uiObject.GetComponentInChildren<Button>();
            if (button != null && regionUI != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(regionUI.OnClick);

                if (button.targetGraphic is Image buttonImage)
                    buttonImage.alphaHitTestMinimumThreshold = Mathf.Clamp01(alphaHitThreshold);
            }
        }
    }

    public int TotalRegions
    {
        get
        {
            if (useVoronoiLayout && voronoiGenerator != null)
                return Mathf.Max(0, voronoiGenerator.Regions != null ? voronoiGenerator.Regions.Count : voronoiGenerator.numClusters);
            return rows * columns;
        }
    }

    List<RegionSpawnData> BuildVoronoiSpawnPlan()
    {
        if (voronoiGenerator == null)
        {
            Debug.LogWarning("RegionGridGenerator: Voronoi generator not assigned, falling back to default layout.");
            return generateContinentalGraphs ? BuildContinentalSpawnPlan() : BuildGridSpawnPlan();
        }

        RectTransform containerRect = regionsContainer;
        if (syncVoronoiBoundsToContainer && containerRect != null)
        {
            Rect rect = containerRect.rect;
            float halfWidth = Mathf.Max(10f, rect.width * 0.5f - voronoiBoundsPadding.x);
            float halfHeight = Mathf.Max(10f, rect.height * 0.5f - voronoiBoundsPadding.y);
            voronoiGenerator.bounds = new Rect(-halfWidth, -halfHeight, halfWidth * 2f, halfHeight * 2f);
        }

        voronoiGenerator.Generate();
        IReadOnlyList<VoronoiRegionGenerator.Region> voronoiRegions = voronoiGenerator.Regions;
        if (voronoiRegions == null || voronoiRegions.Count == 0)
            return new List<RegionSpawnData>();

        List<RegionSpawnData> plan = new List<RegionSpawnData>(voronoiRegions.Count);
        List<string> availableNames = new List<string>(RegionNamePool);
        Dictionary<Region, Vector2> positionLookup = new Dictionary<Region, Vector2>(voronoiRegions.Count);
        Region[] regionLookup = new Region[voronoiRegions.Count];

        Continent continent = new Continent("Mainland", Vector2.zero);
        if (levelController != null)
            levelController.Continents.Add(continent);

        Vector2 centerSum = Vector2.zero;
        for (int i = 0; i < voronoiRegions.Count; i++)
        {
            string regionName = PickRegionName(availableNames, i);
            Region region = new Region(regionName);
            continent.AddRegion(region);

            VoronoiRegionGenerator.Region voronoiRegion = voronoiRegions[i];
            Vector2 position = voronoiRegion != null ? voronoiRegion.centroid : Vector2.zero;
            if (containerRect != null)
                position = ClampToContainer(position, containerRect, cellSize * 0.5f);

            regionLookup[i] = region;
            positionLookup[region] = position;
            centerSum += position;

            plan.Add(new RegionSpawnData(region, position, continent));
        }

        if (voronoiRegions.Count > 0)
            continent.Center = centerSum / voronoiRegions.Count;

        for (int i = 0; i < voronoiRegions.Count; i++)
        {
            VoronoiRegionGenerator.Region voronoiRegion = voronoiRegions[i];
            if (voronoiRegion == null || voronoiRegion.neighbors == null)
                continue;

            for (int n = 0; n < voronoiRegion.neighbors.Count; n++)
            {
                int neighborId = voronoiRegion.neighbors[n];
                if (neighborId <= i || neighborId < 0 || neighborId >= regionLookup.Length)
                    continue;

                Region a = regionLookup[i];
                Region b = regionLookup[neighborId];
                ConnectRegions(a, b, continent, positionLookup);
            }
        }

        return plan;
    }

    private Vector2 CalculateRegionPositionNear(
        Vector2 center,
        List<Vector2> placedPositions,
        List<float> placementRadii,
        RectTransform container)
    {
        if (container == null)
            return center;

        Rect rect = container.rect;
        float halfWidth = Mathf.Max(0f, rect.width * 0.5f - randomAreaPadding.x - cellSize.x * 0.5f);
        float halfHeight = Mathf.Max(0f, rect.height * 0.5f - randomAreaPadding.y - cellSize.y * 0.5f);
        float regionRadius = Mathf.Max(cellSize.x, cellSize.y) * 0.5f;

        for (int attempt = 0; attempt < placementAttemptsPerRegion; attempt++)
        {
            Vector2 offset = new Vector2(
                UnityEngine.Random.Range(-regionScatter.x, regionScatter.x),
                UnityEngine.Random.Range(-regionScatter.y, regionScatter.y));

            Vector2 candidate = new Vector2(
                Mathf.Clamp(center.x + offset.x, -halfWidth, halfWidth),
                Mathf.Clamp(center.y + offset.y, -halfHeight, halfHeight));

            if (!IsOverlapping(candidate, regionRadius, placedPositions, placementRadii, separationMultiplier))
                return candidate;
        }

        return new Vector2(
            Mathf.Clamp(center.x, -halfWidth, halfWidth),
            Mathf.Clamp(center.y, -halfHeight, halfHeight));
    }

    private List<int> DistributeRegionsAcrossContinents(int totalRegions, int desiredContinents)
    {
        List<int> distribution = new List<int>();
        int minPer = Mathf.Max(1, minRegionsPerContinent);
        int maxPer = Mathf.Max(minPer, maxRegionsPerContinent);

        int contCount = Mathf.Clamp(desiredContinents, 1, totalRegions);
        int maxSupportedByMin = Mathf.Max(1, totalRegions / minPer);
        contCount = Mathf.Min(contCount, maxSupportedByMin);
        contCount = Mathf.Max(1, contCount);
        int effectiveMaxPer = Mathf.Max(maxPer, Mathf.CeilToInt((float)totalRegions / contCount));

        int remaining = totalRegions;
        for (int i = 0; i < contCount; i++)
        {
            int continentsLeft = contCount - i - 1;
            int minForThis = Mathf.Min(minPer, remaining - continentsLeft * minPer);
            int maxForThis = Mathf.Min(effectiveMaxPer, remaining - continentsLeft * minPer);
            if (maxForThis < minForThis)
                maxForThis = minForThis;

            int allocation = UnityEngine.Random.Range(minForThis, maxForThis + 1);
            distribution.Add(allocation);
            remaining -= allocation;
        }

        int safety = 0;
        while (remaining > 0 && distribution.Count > 0 && safety < 512)
        {
            bool added = false;
            for (int i = 0; i < distribution.Count && remaining > 0; i++)
            {
                if (distribution[i] >= effectiveMaxPer)
                    continue;

                distribution[i]++;
                remaining--;
                added = true;
            }

            if (!added)
                break;

            safety++;
        }

        return distribution;
    }

    private List<Vector2> BuildContinentCenters(List<int> distribution, RectTransform container)
    {
        int count = distribution.Count;
        List<Vector2> centers = new List<Vector2>(count);
        if (container == null)
        {
            for (int i = 0; i < count; i++)
                centers.Add(Vector2.zero);

            return centers;
        }

        Rect rect = container.rect;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rowsCount = Mathf.CeilToInt((float)count / Mathf.Max(1, cols));
        float cellWidth = rect.width / Mathf.Max(1, cols);
        float cellHeight = rect.height / Mathf.Max(1, rowsCount);

        for (int index = 0; index < count; index++)
        {
            int row = index / cols;
            int column = index % cols;

            float baseX = -rect.width * 0.5f + cellWidth * (column + 0.5f);
            float baseY = rect.height * 0.5f - cellHeight * (row + 0.5f);

            if ((row & 1) == 1)
                baseX += cellWidth * 0.25f * Mathf.Sign(baseX == 0f ? 1f : baseX);

            Vector2 baseCenter = new Vector2(baseX, baseY);
            float minRadius = Mathf.Min(rect.width, rect.height) * 0.5f * continentCenterVoid;
            float dist = baseCenter.magnitude;
            if (dist < minRadius && dist > 0.001f)
                baseCenter = baseCenter.normalized * minRadius;
            else if (dist < minRadius)
                baseCenter = Vector2.right * minRadius;

            Vector2 extent = ClampExtentToContainer(GetContinentExtent(distribution[index]), container);
            float paddingX = randomAreaPadding.x + continentGapPadding;
            float paddingY = randomAreaPadding.y + continentGapPadding;
            float clampLeft = -rect.width * 0.5f + extent.x + paddingX;
            float clampRight = rect.width * 0.5f - extent.x - paddingX;
            float clampBottom = -rect.height * 0.5f + extent.y + paddingY;
            float clampTop = rect.height * 0.5f - extent.y - paddingY;

            if (clampLeft > clampRight)
            {
                float mid = (clampLeft + clampRight) * 0.5f;
                clampLeft = clampRight = mid;
            }

            if (clampBottom > clampTop)
            {
                float mid = (clampBottom + clampTop) * 0.5f;
                clampBottom = clampTop = mid;
            }

            Vector2 center = baseCenter;
            center += new Vector2(
                UnityEngine.Random.Range(-continentJitter.x, continentJitter.x),
                UnityEngine.Random.Range(-continentJitter.y, continentJitter.y));

            center.x = Mathf.Clamp(center.x, clampLeft, clampRight);
            center.y = Mathf.Clamp(center.y, clampBottom, clampTop);

            centers.Add(center);
        }

        return centers;
    }

    private Vector2 GetContinentExtent(int regionsInContinent)
    {
        int ring = CalculateHexRing(regionsInContinent);
        float spacing = Mathf.Max(cellSize.x, cellSize.y) * hexSpacingFactor;
        float extentX = spacing * Mathf.Sqrt(3f) * ring + cellSize.x * 0.5f + continentInnerPadding;
        float extentY = spacing * 1.5f * ring + cellSize.y * 0.5f + continentInnerPadding;
        // fallback for very small rings
        if (ring == 0)
        {
            extentX = Mathf.Max(extentX, cellSize.x * 0.75f);
            extentY = Mathf.Max(extentY, cellSize.y * 0.75f);
        }
        return new Vector2(extentX * continentAspectRatio, extentY / Mathf.Max(0.2f, continentAspectRatio));
    }

    private Vector2 ClampExtentToContainer(Vector2 extent, RectTransform container)
    {
        if (container == null)
            return extent;

        Rect rect = container.rect;
        float maxX = Mathf.Max(10f, rect.width * 0.5f - randomAreaPadding.x - cellSize.x);
        float maxY = Mathf.Max(10f, rect.height * 0.5f - randomAreaPadding.y - cellSize.y);
        extent.x = Mathf.Min(extent.x, maxX);
        extent.y = Mathf.Min(extent.y, maxY);
        return extent;
    }

    private int CalculateHexRing(int regionCount)
    {
        int ring = 0;
        while (regionCount > 1 + 3 * ring * (ring + 1))
            ring++;
        return ring;
    }

    private List<Vector2Int> BuildCompactHexCoordinates(int regionCount)
    {
        int ring = CalculateHexRing(regionCount);
        List<Vector2Int> coords = new List<Vector2Int>();

        for (int q = -ring; q <= ring; q++)
        {
            int r1 = Mathf.Max(-ring, -q - ring);
            int r2 = Mathf.Min(ring, -q + ring);
            for (int r = r1; r <= r2; r++)
                coords.Add(new Vector2Int(q, r));
        }

        float angleOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        coords.Sort((a, b) =>
        {
            int distA = HexDistance(a);
            int distB = HexDistance(b);
            if (distA != distB)
                return distA - distB;

            float angleA = Mathf.Atan2(AxialToWorld(a, 1f).y, AxialToWorld(a, 1f).x) + angleOffset;
            float angleB = Mathf.Atan2(AxialToWorld(b, 1f).y, AxialToWorld(b, 1f).x) + angleOffset;
            return angleA.CompareTo(angleB);
        });

        if (coords.Count > regionCount)
            coords.RemoveRange(regionCount, coords.Count - regionCount);

        return coords;
    }

    private int HexDistance(Vector2Int axial)
    {
        int x = Mathf.Abs(axial.x);
        int y = Mathf.Abs(axial.y);
        int z = Mathf.Abs(axial.x + axial.y);
        return (x + y + z) / 2;
    }

    private Vector2 AxialToWorld(Vector2Int axial, float spacing)
    {
        float x = spacing * (Mathf.Sqrt(3f) * axial.x + Mathf.Sqrt(3f) * 0.5f * axial.y);
        float y = spacing * (1.5f * axial.y);
        return new Vector2(x, y);
    }

    private Vector2 ClampToContainer(Vector2 position, RectTransform container, Vector2 extent)
    {
        if (container == null)
            return position;

        Rect rect = container.rect;
        float halfWidth = rect.width * 0.5f - randomAreaPadding.x;
        float halfHeight = rect.height * 0.5f - randomAreaPadding.y;

        position.x = Mathf.Clamp(position.x, -halfWidth + extent.x, halfWidth - extent.x);
        position.y = Mathf.Clamp(position.y, -halfHeight + extent.y, halfHeight - extent.y);
        return position;
    }

    private Vector2 ClampToExtent(Vector2 position, Vector2 center, Vector2 extent)
    {
        position.x = Mathf.Clamp(position.x, center.x - extent.x, center.x + extent.x);
        position.y = Mathf.Clamp(position.y, center.y - extent.y, center.y + extent.y);
        return position;
    }

    private List<Vector2> GenerateContinentPositionsBlob(
        int regionCount,
        Vector2 center,
        Vector2 extent,
        RectTransform container,
        List<Vector2> globalPositions,
        List<float> globalRadii)
    {
        List<Vector2> positions = new List<Vector2>(regionCount);
        List<float> radii = new List<float>(regionCount);
        float regionRadius = Mathf.Max(cellSize.x, cellSize.y) * 0.5f;
        float minSpacing = Mathf.Clamp(regionRadius * intraContinentSpacing, regionRadius * 0.5f, regionRadius * blobSpacingMultiplier);

        int maxAttempts = regionCount * 30;
        while (positions.Count < regionCount && maxAttempts-- > 0)
        {
            Vector2 candidate = SampleEllipse(center, extent, continentAspectRatio);
            candidate += UnityEngine.Random.insideUnitCircle * hexJitter;
            candidate = ClampToContainer(candidate, container, extent);
            candidate = ClampToExtent(candidate, center, extent);

            if (IsOverlapping(candidate, minSpacing, globalPositions, globalRadii, 1f))
                continue;
            if (IsOverlapping(candidate, minSpacing, positions, radii, 1f))
                continue;

            positions.Add(candidate);
            radii.Add(regionRadius);
        }

        if (positions.Count < regionCount)
        {
            // fill remaining slots by relaxing random points near center
            for (int i = positions.Count; i < regionCount; i++)
            {
                Vector2 pos = center + UnityEngine.Random.insideUnitCircle * Mathf.Min(extent.x, extent.y) * 0.4f;
                positions.Add(pos);
                radii.Add(regionRadius);
            }
        }

        RelaxPositions(positions, radii, center, extent, container);
        PullClusterTighter(positions, center, 0.9f);
        return positions;
    }

    private void RelaxPositions(
        List<Vector2> positions,
        List<float> radii,
        Vector2 center,
        Vector2 extent,
        RectTransform container)
    {
        float maxMove = Mathf.Max(cellSize.x, cellSize.y) * relaxationStep;

        for (int iter = 0; iter < relaxationIterations; iter++)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 pos = positions[i];
                Vector2 force = Vector2.zero;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (i == j)
                        continue;

                    float desired = (radii[i] + radii[j]) * separationMultiplier * intraContinentSpacing;
                    Vector2 delta = pos - positions[j];
                    float dist = delta.magnitude + 0.0001f;
                    float push = desired - dist;
                    if (push > 0f)
                        force += delta.normalized * push;
                }

                // pull slightly toward center to keep blob cohesive
                force += (center - pos) * 0.1f;

                if (force.sqrMagnitude > maxMove * maxMove)
                    force = force.normalized * maxMove;

                pos += force;

                // keep inside allowed extents and container
                pos = ClampToContainer(pos, container, extent);
                pos = ClampToExtent(pos, center, extent);
                positions[i] = pos;
            }
        }
    }

    private void PullClusterTighter(List<Vector2> positions, Vector2 center, float factor)
    {
        factor = Mathf.Clamp(factor, 0.7f, 1f);
        for (int i = 0; i < positions.Count; i++)
        {
            Vector2 delta = positions[i] - center;
            positions[i] = center + delta * factor;
        }
    }

    private Vector2 SampleEllipse(Vector2 center, Vector2 extent, float aspect)
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(UnityEngine.Random.value);
        float rx = extent.x * aspect * radius;
        float ry = extent.y / Mathf.Max(0.2f, aspect) * radius;
        return center + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
    }

    private string PickContinentName(int index)
    {
        if (index >= 0 && index < ContinentNamePool.Length)
            return ContinentNamePool[index];

        int extraIndex = index % ContinentNamePool.Length;
        return ContinentNamePool[extraIndex] + " " + (index / ContinentNamePool.Length + 1);
    }

    private void ConnectContinentRegions(Continent continent, Dictionary<Region, Vector2> positionLookup)
    {
        if (continent == null || continent.Regions == null || continent.Regions.Count == 0)
            return;

        List<Region> regions = continent.Regions;

        for (int i = 1; i < regions.Count; i++)
        {
            Region region = regions[i];
            Region target = regions[UnityEngine.Random.Range(0, i)];
            ConnectRegions(region, target, continent, positionLookup);
        }

        for (int extra = 0; extra < extraEdgesPerContinent; extra++)
        {
            Region start = regions[UnityEngine.Random.Range(0, regions.Count)];
            Region nearest = FindNearestNonNeighbor(start, regions, positionLookup);
            if (nearest != null)
                ConnectRegions(start, nearest, continent, positionLookup);
        }
    }

    private Region FindNearestNonNeighbor(Region source, List<Region> candidates, Dictionary<Region, Vector2> positionLookup)
    {
        if (source == null || candidates == null || candidates.Count == 0 || positionLookup == null)
            return null;

        if (!positionLookup.TryGetValue(source, out Vector2 sourcePos))
            return null;

        Region best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            Region candidate = candidates[i];
            if (candidate == null || candidate == source)
                continue;

            if (source.IsNeighbor(candidate))
                continue;

            if (!positionLookup.TryGetValue(candidate, out Vector2 candidatePos))
                continue;

            float sqr = (candidatePos - sourcePos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = candidate;
            }
        }

        return best;
    }

    private void ConnectContinents(Dictionary<Region, Vector2> positionLookup)
    {
        if (levelController == null || levelController.Continents == null)
            return;

        List<Continent> continents = levelController.Continents;
        if (continents.Count <= 1)
            return;

        for (int i = 0; i < continents.Count - 1; i++)
        {
            ConnectClosestContinents(continents[i], continents[i + 1], positionLookup);
        }

        for (int i = 0; i < continents.Count; i++)
        {
            if (UnityEngine.Random.value > crossContinentConnectionChance)
                continue;

            int targetIndex = UnityEngine.Random.Range(0, continents.Count);
            if (targetIndex == i)
                continue;

            ConnectClosestContinents(continents[i], continents[targetIndex], positionLookup);
        }
    }

    private void ConnectClosestContinents(Continent a, Continent b, Dictionary<Region, Vector2> positionLookup)
    {
        if (a == null || b == null || positionLookup == null)
            return;

        Region bestA = null;
        Region bestB = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < a.Regions.Count; i++)
        {
            Region ra = a.Regions[i];
            if (ra == null || !positionLookup.TryGetValue(ra, out Vector2 posA))
                continue;

            for (int j = 0; j < b.Regions.Count; j++)
            {
                Region rb = b.Regions[j];
                if (rb == null || ra.IsNeighbor(rb))
                    continue;

                if (!positionLookup.TryGetValue(rb, out Vector2 posB))
                    continue;

                float distance = (posA - posB).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestA = ra;
                    bestB = rb;
                }
            }
        }

        if (bestA != null && bestB != null)
            ConnectRegions(bestA, bestB, a, positionLookup, b);
    }

    private bool ConnectRegions(
        Region a,
        Region b,
        Continent primaryContinent,
        Dictionary<Region, Vector2> positionLookup,
        Continent secondaryContinent = null)
    {
        if (a == null || b == null || a == b)
            return false;

        if (a.IsNeighbor(b))
            return false;

        a.AddNeighbor(b);
        b.AddNeighbor(a);

        float distance = 0f;
        if (positionLookup != null &&
            positionLookup.TryGetValue(a, out Vector2 posA) &&
            positionLookup.TryGetValue(b, out Vector2 posB))
        {
            distance = Vector2.Distance(posA, posB);
        }

        if (primaryContinent != null)
            primaryContinent.AddConnection(a, b, distance);

        if (secondaryContinent != null && secondaryContinent != primaryContinent)
            secondaryContinent.AddConnection(a, b, distance);

        return true;
    }

    private void ClearExistingChildren()
    {
        if (regionsContainer == null)
            return;

        for (int i = regionsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(regionsContainer.GetChild(i).gameObject);
        }
    }

    private void CleanupGeneratedAssets()
    {
        for (int i = generatedAssets.Count - 1; i >= 0; i--)
        {
            UnityEngine.Object asset = generatedAssets[i];
            if (asset != null)
                Destroy(asset);
        }

        generatedAssets.Clear();
    }

    private Vector2 CalculateGridPosition(int index)
    {
        int row = index / columns;
        int column = index % columns;

        float totalWidth = columns * cellSize.x + (columns - 1) * spacing.x;
        float totalHeight = rows * cellSize.y + (rows - 1) * spacing.y;

        float startX = -totalWidth * 0.5f + cellSize.x * 0.5f;
        float startY = totalHeight * 0.5f - cellSize.y * 0.5f;

        float x = startX + column * (cellSize.x + spacing.x);
        float y = startY - row * (cellSize.y + spacing.y);

        return new Vector2(x, y);
    }

    private Vector2 CalculateRandomPosition(
        Vector2 elementSize,
        List<Vector2> placedPositions,
        List<float> placementRadii,
        RectTransform container)
    {
        if (container == null)
            return Vector2.zero;

        Rect rect = container.rect;
        float halfWidth = Mathf.Max(0f, rect.width * 0.5f - randomAreaPadding.x - elementSize.x * 0.5f);
        float halfHeight = Mathf.Max(0f, rect.height * 0.5f - randomAreaPadding.y - elementSize.y * 0.5f);

        float regionRadius = Mathf.Max(elementSize.x, elementSize.y) * 0.5f;

        for (int attempt = 0; attempt < placementAttemptsPerRegion; attempt++)
        {
            Vector2 candidate = SampleBiasedPosition(halfWidth, halfHeight);
            if (!IsOverlapping(candidate, regionRadius, placedPositions, placementRadii, separationMultiplier))
                return candidate;
        }

        // Ослабляем ограничение по дистанции, если найти позицию не удалось.
        float relaxedMultiplier = separationMultiplier * 0.85f;
        for (int attempt = 0; attempt < placementAttemptsPerRegion; attempt++)
        {
            Vector2 candidate = SampleBiasedPosition(halfWidth, halfHeight);
            if (!IsOverlapping(candidate, regionRadius, placedPositions, placementRadii, relaxedMultiplier))
                return candidate;
        }

        return SampleBiasedPosition(halfWidth, halfHeight);
    }

    private Vector2 SampleBiasedPosition(float halfWidth, float halfHeight)
    {
        if (halfWidth <= 0f || halfHeight <= 0f)
            return Vector2.zero;

        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Pow(UnityEngine.Random.value, 2f); // bias toward the center
        float x = Mathf.Cos(angle) * radius * halfWidth;
        float y = Mathf.Sin(angle) * radius * halfHeight;
        return new Vector2(x, y);
    }

    private bool IsOverlapping(
        Vector2 candidate,
        float candidateRadius,
        List<Vector2> placedPositions,
        List<float> placementRadii,
        float multiplier)
    {
        float candidateExtent = candidateRadius * multiplier;
        for (int i = 0; i < placedPositions.Count; i++)
        {
            float otherExtent = placementRadii[i] * multiplier;
            float minDistance = candidateExtent + otherExtent;
            if ((candidate - placedPositions[i]).sqrMagnitude < minDistance * minDistance)
                return true;
        }

        return false;
    }

    private void ConfigureRegionImage(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.alphaHitTestMinimumThreshold = Mathf.Clamp01(alphaHitThreshold);

        Outline outline = image.GetComponent<Outline>();
        if (outline == null)
            outline = image.gameObject.AddComponent<Outline>();

        outline.effectColor = Color.black;
        outline.effectDistance = outlineDistance;
        outline.useGraphicAlpha = false;
    }

    private Sprite CreateRandomRegionSprite()
    {
        int size = Mathf.Clamp(textureSize, 32, 512);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        System.Random rng = new System.Random(Guid.NewGuid().GetHashCode());

        float power = Mathf.Lerp(2.2f, 4.5f, (float)rng.NextDouble());
        float xScale = Mathf.Lerp(0.6f, 1f, (float)rng.NextDouble());
        float yScale = Mathf.Lerp(0.6f, 1f, (float)rng.NextDouble());
        float rotation = Mathf.Lerp(-30f, 30f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
        float noiseFrequency = Mathf.Lerp(1.2f, 3.5f, (float)rng.NextDouble());
        float noiseStrength = Mathf.Lerp(0.2f, 0.45f, (float)rng.NextDouble());
        float offsetX = (float)rng.NextDouble() * 1000f;
        float offsetY = (float)rng.NextDouble() * 1000f;
        float feather = Mathf.Clamp(edgeFeather, 0.01f, 0.2f);

        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / (size - 1);
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / (size - 1);

                // Normalized coords in range [-1, 1].
                float nx = u * 2f - 1f;
                float ny = v * 2f - 1f;

                // Apply rotation.
                float rx = nx * cos - ny * sin;
                float ry = nx * sin + ny * cos;

                float sx = Mathf.Pow(Mathf.Abs(rx) / xScale, power);
                float sy = Mathf.Pow(Mathf.Abs(ry) / yScale, power);
                float superEllipse = sx + sy;

                float noise = (Mathf.PerlinNoise(u * noiseFrequency + offsetX, v * noiseFrequency + offsetY) - 0.5f) * 2f;
                float threshold = 1f + noise * noiseStrength;
                float mask = threshold - superEllipse;

                float alpha = Mathf.Clamp01(mask / feather);
                if (alpha <= 0f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.name = "RegionTexture";

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "RegionSprite";

        generatedAssets.Add(texture);
        generatedAssets.Add(sprite);
        return sprite;
    }

    private string PickRegionName(List<string> availableNames, int fallbackIndex)
    {
        if (availableNames.Count > 0)
        {
            int pick = UnityEngine.Random.Range(0, availableNames.Count);
            string name = availableNames[pick];
            availableNames.RemoveAt(pick);
            return name;
        }

        return $"Region {fallbackIndex + 1}";
    }

    private void OnDestroy()
    {
        CleanupGeneratedAssets();
    }
}
