using System;
using System.Collections.Generic;
using UnityEngine;

public class VoronoiRegionGenerator : MonoBehaviour
{
    [Serializable]
    public class SeedPoint
    {
        public Vector2 pos;
        public int clusterId;

        public SeedPoint(Vector2 pos, int clusterId)
        {
            this.pos = pos;
            this.clusterId = clusterId;
        }
    }

    [Serializable]
    public class Region
    {
        public int id;
        public int clusterId;
        public List<Vector2> polygon = new List<Vector2>();
        public List<int> neighbors = new List<int>();
        public float area;
        public Vector2 centroid;
    }

    [Header("Generation")]
    public int seed = 12345;
    public Rect bounds = new Rect(-5f, -5f, 10f, 10f);
    [Min(1)] public int numClusters = 5;
    [Min(1)] public int pointsPerCluster = 10;
    [Min(0f)] public float clusterSpread = 2f;
    [Range(0.2f, 1f)] public float clusterCenterScale = 0.8f;
    [Range(0, 3)] public int lloydIterations = 0;
    public bool enforceMinDistance = true;
    [Min(0f)] public float minDistance = 0.6f;
    [Min(5)] public int maxAttemptsPerPoint = 40;
    [Range(0, 12)] public int overlapRelaxIterations = 6;
    public bool autoGenerateOnStart = true;

    [Header("Visualization")]
    public bool drawGizmos = true;
    public bool drawBounds = true;
    public bool drawSeeds = true;
    public bool drawCells = false;
    public bool drawRegions = true;
    [Min(0.01f)] public float seedGizmoRadius = 0.08f;

    [Header("Colliders (Optional)")]
    public bool generatePolygonColliders2D = false;
    public bool generateMeshColliders = false;
    public bool clearPreviousColliders = true;
    public Transform colliderParent;

    [Header("Output (Read Only)")]
    [SerializeField] private List<SeedPoint> seedPoints = new List<SeedPoint>();
    [SerializeField] private List<Region> regions = new List<Region>();

    private readonly List<GameObject> generatedObjects = new List<GameObject>();
    private List<List<Vector2>> cellPolygons = new List<List<Vector2>>();

    public IReadOnlyList<SeedPoint> SeedPoints => seedPoints;
    public IReadOnlyList<Region> Regions => regions;

    void Start()
    {
        if (autoGenerateOnStart)
            Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        System.Random rng = new System.Random(seed);
        seedPoints = GenerateSeedPoints(rng);

        for (int i = 0; i < lloydIterations; i++)
        {
            List<int>[] neighbors = BuildDelaunayNeighbors(seedPoints);
            List<List<Vector2>> cells = BuildVoronoiCells(seedPoints, neighbors);
            RelaxSeedPoints(seedPoints, cells);
        }

        if (enforceMinDistance && minDistance > 0f)
            ResolvePointOverlaps(seedPoints, rng);

        List<int>[] finalNeighbors = BuildDelaunayNeighbors(seedPoints);
        cellPolygons = BuildVoronoiCells(seedPoints, finalNeighbors);
        FixDisconnectedClusters(seedPoints, finalNeighbors);

        regions = BuildRegions(seedPoints, cellPolygons, finalNeighbors, numClusters);

        if (generatePolygonColliders2D || generateMeshColliders)
            GenerateColliders(regions);
    }

    List<SeedPoint> GenerateSeedPoints(System.Random rng)
    {
        int clusterCount = Mathf.Max(1, numClusters);
        int pointsPer = Mathf.Max(1, pointsPerCluster);
        List<SeedPoint> points = new List<SeedPoint>(clusterCount * pointsPer);
        List<Vector2> centers = new List<Vector2>(clusterCount);

        Rect centerBounds = ScaleRect(bounds, clusterCenterScale);
        for (int i = 0; i < clusterCount; i++)
            centers.Add(RandomPointInRect(rng, centerBounds));

        float minDistanceSq = minDistance * minDistance;

        for (int c = 0; c < clusterCount; c++)
        {
            Vector2 center = centers[c];
            for (int p = 0; p < pointsPer; p++)
            {
                Vector2 point = center;
                bool placed = false;
                for (int attempt = 0; attempt < maxAttemptsPerPoint; attempt++)
                {
                    point = center + new Vector2(NextGaussian(rng), NextGaussian(rng)) * clusterSpread;
                    if (!bounds.Contains(point))
                        continue;

                    if (enforceMinDistance && !IsFarEnough(points, point, minDistanceSq))
                        continue;

                    placed = true;
                    break;
                }

                if (!placed)
                {
                    point = ClampToBounds(point);
                }

                points.Add(new SeedPoint(point, c));
            }
        }

        if (enforceMinDistance && minDistance > 0f)
            ResolvePointOverlaps(points, rng);

        return points;
    }

    static bool IsFarEnough(List<SeedPoint> points, Vector2 candidate, float minDistanceSq)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if ((points[i].pos - candidate).sqrMagnitude < minDistanceSq)
                return false;
        }

        return true;
    }

    static bool IsFarEnoughExcept(List<SeedPoint> points, int index, Vector2 candidate, float minDistanceSq)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (i == index)
                continue;

            if ((points[i].pos - candidate).sqrMagnitude < minDistanceSq)
                return false;
        }

        return true;
    }

    void RelaxSeedPoints(List<SeedPoint> points, List<List<Vector2>> cells)
    {
        for (int i = 0; i < points.Count && i < cells.Count; i++)
        {
            List<Vector2> poly = cells[i];
            if (poly == null || poly.Count < 3)
                continue;

            Vector2 centroid = ComputeCentroid(poly);
            points[i].pos = ClampToBounds(centroid);
        }
    }

    void ResolvePointOverlaps(List<SeedPoint> points, System.Random rng)
    {
        if (points == null || points.Count < 2)
            return;

        float targetDistance = Mathf.Max(0f, minDistance);
        float targetDistanceSq = targetDistance * targetDistance;
        int iterations = Mathf.Max(0, overlapRelaxIterations);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            bool anyOverlap = false;
            Vector2[] offsets = new Vector2[points.Count];

            for (int i = 0; i < points.Count - 1; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    Vector2 delta = points[i].pos - points[j].pos;
                    float distSq = delta.sqrMagnitude;

                    if (distSq >= targetDistanceSq)
                        continue;

                    anyOverlap = true;
                    if (distSq < 1e-6f)
                    {
                        delta = RandomUnit(rng);
                        distSq = 1f;
                    }

                    float dist = Mathf.Sqrt(distSq);
                    float push = (targetDistance - dist) / dist * 0.5f;
                    Vector2 move = delta * push;
                    offsets[i] += move;
                    offsets[j] -= move;
                }
            }

            if (!anyOverlap)
                return;

            for (int i = 0; i < points.Count; i++)
            {
                if (offsets[i] != Vector2.zero)
                    points[i].pos = ClampToBounds(points[i].pos + offsets[i]);
            }
        }

        int extraAttempts = Mathf.Max(1, maxAttemptsPerPoint * 4);
        for (int i = 0; i < points.Count; i++)
        {
            if (!HasOverlap(points, i, targetDistanceSq))
                continue;

            Vector2 candidate = points[i].pos;
            bool placed = false;
            for (int attempt = 0; attempt < extraAttempts; attempt++)
            {
                candidate = RandomPointInRect(rng, bounds);
                if (IsFarEnoughExcept(points, i, candidate, targetDistanceSq))
                {
                    placed = true;
                    break;
                }
            }

            if (placed)
                points[i].pos = candidate;
            else
                Debug.LogWarning("VoronoiRegionGenerator: minDistance too large for bounds; overlaps may remain.");
        }
    }

    static bool HasOverlap(List<SeedPoint> points, int index, float minDistanceSq)
    {
        Vector2 pos = points[index].pos;
        for (int i = 0; i < points.Count; i++)
        {
            if (i == index)
                continue;

            if ((points[i].pos - pos).sqrMagnitude < minDistanceSq)
                return true;
        }

        return false;
    }

    void FixDisconnectedClusters(List<SeedPoint> points, List<int>[] neighbors)
    {
        if (neighbors == null || points.Count == 0)
            return;

        int clusterCount = Mathf.Max(1, numClusters);
        bool changed = false;

        for (int pass = 0; pass < 3; pass++)
        {
            Vector2[] centroids = new Vector2[clusterCount];
            int[] counts = new int[clusterCount];

            for (int i = 0; i < points.Count; i++)
            {
                int clusterId = Mathf.Clamp(points[i].clusterId, 0, clusterCount - 1);
                points[i].clusterId = clusterId;
                centroids[clusterId] += points[i].pos;
                counts[clusterId]++;
            }

            for (int c = 0; c < clusterCount; c++)
            {
                if (counts[c] > 0)
                    centroids[c] /= counts[c];
            }

            changed = false;
            List<int>[] clusterPoints = new List<int>[clusterCount];
            for (int c = 0; c < clusterCount; c++)
                clusterPoints[c] = new List<int>();

            for (int i = 0; i < points.Count; i++)
                clusterPoints[points[i].clusterId].Add(i);

            for (int c = 0; c < clusterCount; c++)
            {
                List<int> indices = clusterPoints[c];
                if (indices.Count <= 1)
                    continue;

                List<List<int>> components = BuildClusterComponents(indices, points, neighbors, c);
                if (components.Count <= 1)
                    continue;

                int largestIndex = 0;
                for (int i = 1; i < components.Count; i++)
                {
                    if (components[i].Count > components[largestIndex].Count)
                        largestIndex = i;
                }

                for (int i = 0; i < components.Count; i++)
                {
                    if (i == largestIndex)
                        continue;

                    Vector2 componentCenter = ComputeAveragePosition(points, components[i]);
                    int targetCluster = FindNearestCluster(componentCenter, centroids, counts, c);

                    if (targetCluster == c)
                        continue;

                    List<int> component = components[i];
                    for (int k = 0; k < component.Count; k++)
                        points[component[k]].clusterId = targetCluster;

                    changed = true;
                }
            }

            if (!changed)
                break;
        }
    }

    static List<List<int>> BuildClusterComponents(
        List<int> indices,
        List<SeedPoint> points,
        List<int>[] neighbors,
        int clusterId)
    {
        HashSet<int> indexSet = new HashSet<int>(indices);
        HashSet<int> visited = new HashSet<int>();
        List<List<int>> components = new List<List<int>>();

        for (int i = 0; i < indices.Count; i++)
        {
            int start = indices[i];
            if (visited.Contains(start))
                continue;

            List<int> component = new List<int>();
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                component.Add(current);

                List<int> neighborList = neighbors[current];
                for (int n = 0; n < neighborList.Count; n++)
                {
                    int next = neighborList[n];
                    if (!indexSet.Contains(next))
                        continue;
                    if (visited.Contains(next))
                        continue;
                    if (points[next].clusterId != clusterId)
                        continue;

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            components.Add(component);
        }

        return components;
    }

    static int FindNearestCluster(Vector2 position, Vector2[] centroids, int[] counts, int exclude)
    {
        int best = exclude;
        float bestDist = float.MaxValue;

        for (int i = 0; i < centroids.Length; i++)
        {
            if (i == exclude || counts[i] == 0)
                continue;

            float dist = (centroids[i] - position).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    static Vector2 ComputeAveragePosition(List<SeedPoint> points, List<int> indices)
    {
        if (indices.Count == 0)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;
        for (int i = 0; i < indices.Count; i++)
            sum += points[indices[i]].pos;

        return sum / indices.Count;
    }

    List<Region> BuildRegions(
        List<SeedPoint> points,
        List<List<Vector2>> cells,
        List<int>[] neighbors,
        int clusterCount)
    {
        List<Region> output = new List<Region>(clusterCount);
        List<HashSet<int>> regionNeighbors = new List<HashSet<int>>(clusterCount);
        List<List<List<Vector2>>> clusterCells = new List<List<List<Vector2>>>(clusterCount);

        for (int i = 0; i < clusterCount; i++)
        {
            regionNeighbors.Add(new HashSet<int>());
            clusterCells.Add(new List<List<Vector2>>());
        }

        for (int i = 0; i < points.Count && i < cells.Count; i++)
            clusterCells[points[i].clusterId].Add(cells[i]);

        for (int i = 0; i < points.Count; i++)
        {
            int clusterA = points[i].clusterId;
            List<int> neighborList = neighbors[i];
            for (int n = 0; n < neighborList.Count; n++)
            {
                int clusterB = points[neighborList[n]].clusterId;
                if (clusterA == clusterB)
                    continue;

                regionNeighbors[clusterA].Add(clusterB);
            }
        }

        for (int c = 0; c < clusterCount; c++)
        {
            List<List<Vector2>> loops = BuildBoundaryLoops(clusterCells[c]);
            List<Vector2> mainPolygon = new List<Vector2>();
            float areaSum = 0f;
            float bestArea = float.MinValue;

            for (int i = 0; i < loops.Count; i++)
            {
                float area = Mathf.Abs(ComputeSignedArea(loops[i]));
                areaSum += area;
                if (area > bestArea)
                {
                    bestArea = area;
                    mainPolygon = loops[i];
                }
            }

            Region region = new Region
            {
                id = c,
                clusterId = c,
                polygon = mainPolygon,
                area = areaSum,
                centroid = mainPolygon.Count >= 3 ? ComputeCentroid(mainPolygon) : Vector2.zero
            };

            foreach (int neighbor in regionNeighbors[c])
                region.neighbors.Add(neighbor);

            output.Add(region);
        }

        return output;
    }

    List<List<Vector2>> BuildBoundaryLoops(List<List<Vector2>> polygons)
    {
        Dictionary<EdgeKey2, EdgeData> edgeUsage = new Dictionary<EdgeKey2, EdgeData>();

        for (int p = 0; p < polygons.Count; p++)
        {
            List<Vector2> poly = polygons[p];
            if (poly == null || poly.Count < 3)
                continue;

            EnsureCounterClockwise(poly);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Count];
                if ((a - b).sqrMagnitude < 1e-6f)
                    continue;

                Vector2Key keyA = Vector2Key.FromVector(a);
                Vector2Key keyB = Vector2Key.FromVector(b);
                if (keyA.Equals(keyB))
                    continue;

                EdgeKey2 key = new EdgeKey2(keyA, keyB);
                if (edgeUsage.TryGetValue(key, out EdgeData data))
                {
                    data.count++;
                    edgeUsage[key] = data;
                }
                else
                {
                    edgeUsage[key] = new EdgeData
                    {
                        count = 1,
                        from = a,
                        to = b,
                        fromKey = keyA,
                        toKey = keyB
                    };
                }
            }
        }

        List<EdgeData> boundaryEdges = new List<EdgeData>();
        foreach (KeyValuePair<EdgeKey2, EdgeData> pair in edgeUsage)
        {
            if (pair.Value.count == 1)
                boundaryEdges.Add(pair.Value);
        }

        Dictionary<Vector2Key, List<int>> startLookup = new Dictionary<Vector2Key, List<int>>();
        for (int i = 0; i < boundaryEdges.Count; i++)
        {
            EdgeData edge = boundaryEdges[i];
            if (!startLookup.TryGetValue(edge.fromKey, out List<int> list))
            {
                list = new List<int>();
                startLookup[edge.fromKey] = list;
            }
            list.Add(i);
        }

        List<List<Vector2>> loops = new List<List<Vector2>>();
        bool[] visited = new bool[boundaryEdges.Count];

        for (int i = 0; i < boundaryEdges.Count; i++)
        {
            if (visited[i])
                continue;

            List<Vector2> loop = new List<Vector2>();
            EdgeData current = boundaryEdges[i];
            Vector2Key startKey = current.fromKey;
            Vector2Key currentKey = current.toKey;

            loop.Add(current.from);
            loop.Add(current.to);
            visited[i] = true;

            int guard = 0;
            while (!currentKey.Equals(startKey) && guard < 10000)
            {
                guard++;
                if (!startLookup.TryGetValue(currentKey, out List<int> nextEdges))
                    break;

                int nextIndex = -1;
                for (int n = 0; n < nextEdges.Count; n++)
                {
                    int candidate = nextEdges[n];
                    if (visited[candidate])
                        continue;

                    nextIndex = candidate;
                    break;
                }

                if (nextIndex == -1)
                    break;

                EdgeData next = boundaryEdges[nextIndex];
                visited[nextIndex] = true;
                loop.Add(next.to);
                currentKey = next.toKey;
            }

            CleanPolygon(loop);
            if (loop.Count >= 3)
                loops.Add(loop);
        }

        return loops;
    }

    void GenerateColliders(List<Region> regionList)
    {
        if (clearPreviousColliders)
            ClearGeneratedObjects();

        Transform parent = colliderParent != null ? colliderParent : transform;

        for (int i = 0; i < regionList.Count; i++)
        {
            List<Vector2> poly = regionList[i].polygon;
            if (poly == null || poly.Count < 3)
                continue;

            GameObject go = new GameObject($"RegionCollider_{regionList[i].id}");
            go.transform.SetParent(parent, false);

            if (generatePolygonColliders2D)
            {
                PolygonCollider2D collider = go.AddComponent<PolygonCollider2D>();
                collider.points = poly.ToArray();
            }

            if (generateMeshColliders)
            {
                MeshCollider collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = BuildMesh(poly);
            }

            generatedObjects.Add(go);
        }
    }

    void ClearGeneratedObjects()
    {
        for (int i = generatedObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = generatedObjects[i];
            if (go == null)
                continue;

            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        generatedObjects.Clear();
    }

    Mesh BuildMesh(List<Vector2> polygon)
    {
        EnsureCounterClockwise(polygon);
        List<int> triangles = Triangulate(polygon);
        Vector3[] vertices = new Vector3[polygon.Count];

        for (int i = 0; i < polygon.Count; i++)
            vertices[i] = new Vector3(polygon[i].x, polygon[i].y, 0f);

        Mesh mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles.ToArray()
        };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    static List<int> Triangulate(List<Vector2> polygon)
    {
        List<int> indices = new List<int>();
        if (polygon.Count < 3)
            return indices;

        List<int> verts = new List<int>();
        for (int i = 0; i < polygon.Count; i++)
            verts.Add(i);

        int guard = 0;
        while (verts.Count >= 3 && guard < 5000)
        {
            bool earFound = false;
            for (int i = 0; i < verts.Count; i++)
            {
                int prev = verts[(i - 1 + verts.Count) % verts.Count];
                int curr = verts[i];
                int next = verts[(i + 1) % verts.Count];

                if (!IsConvex(polygon[prev], polygon[curr], polygon[next]))
                    continue;

                if (ContainsAnyPoint(polygon, verts, prev, curr, next))
                    continue;

                indices.Add(prev);
                indices.Add(curr);
                indices.Add(next);
                verts.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
                break;

            guard++;
        }

        return indices;
    }

    static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(b - a, c - b) > 0f;
    }

    static bool ContainsAnyPoint(List<Vector2> polygon, List<int> verts, int a, int b, int c)
    {
        for (int i = 0; i < verts.Count; i++)
        {
            int index = verts[i];
            if (index == a || index == b || index == c)
                continue;

            if (PointInTriangle(polygon[index], polygon[a], polygon[b], polygon[c]))
                return true;
        }

        return false;
    }

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = Cross(b - a, p - a);
        float c2 = Cross(c - b, p - b);
        float c3 = Cross(a - c, p - c);

        bool hasNeg = (c1 < 0f) || (c2 < 0f) || (c3 < 0f);
        bool hasPos = (c1 > 0f) || (c2 > 0f) || (c3 > 0f);
        return !(hasNeg && hasPos);
    }

    static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    List<int>[] BuildDelaunayNeighbors(List<SeedPoint> points)
    {
        int count = points.Count;
        List<int>[] neighbors = new List<int>[count];
        for (int i = 0; i < count; i++)
            neighbors[i] = new List<int>();

        if (count < 2)
            return neighbors;

        List<Vector2> positions = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
            positions.Add(points[i].pos);

        DelaunayTriangulation triangulation = DelaunayTriangulation.Build(positions);
        foreach (DelaunayTriangle tri in triangulation.triangles)
        {
            AddNeighbor(neighbors, tri.a, tri.b);
            AddNeighbor(neighbors, tri.b, tri.c);
            AddNeighbor(neighbors, tri.c, tri.a);
        }

        return neighbors;
    }

    static void AddNeighbor(List<int>[] neighbors, int a, int b)
    {
        if (!neighbors[a].Contains(b))
            neighbors[a].Add(b);
        if (!neighbors[b].Contains(a))
            neighbors[b].Add(a);
    }

    List<List<Vector2>> BuildVoronoiCells(List<SeedPoint> points, List<int>[] neighbors)
    {
        List<List<Vector2>> cells = new List<List<Vector2>>(points.Count);

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 site = points[i].pos;
            List<Vector2> poly = BuildCellPolygon(site, points, neighbors[i]);
            CleanPolygon(poly);
            cells.Add(poly);
        }

        return cells;
    }

    List<Vector2> BuildCellPolygon(Vector2 site, List<SeedPoint> points, List<int> neighborList)
    {
        List<Vector2> polygon = new List<Vector2>
        {
            new Vector2(bounds.xMin, bounds.yMin),
            new Vector2(bounds.xMax, bounds.yMin),
            new Vector2(bounds.xMax, bounds.yMax),
            new Vector2(bounds.xMin, bounds.yMax)
        };

        for (int i = 0; i < neighborList.Count; i++)
        {
            Vector2 other = points[neighborList[i]].pos;
            Vector2 mid = (site + other) * 0.5f;
            Vector2 dir = other - site;
            if (dir.sqrMagnitude < 1e-6f)
                continue;

            Vector2 normal = new Vector2(dir.y, -dir.x);
            if (Vector2.Dot(normal, site - mid) < 0f)
                normal = -normal;

            polygon = ClipPolygon(polygon, mid, normal);
            if (polygon.Count == 0)
                break;
        }

        return polygon;
    }

    static List<Vector2> ClipPolygon(List<Vector2> polygon, Vector2 linePoint, Vector2 lineNormal)
    {
        const float epsilon = 1e-6f;
        List<Vector2> output = new List<Vector2>();
        if (polygon.Count == 0)
            return output;

        Vector2 prev = polygon[polygon.Count - 1];
        bool prevInside = Vector2.Dot(lineNormal, prev - linePoint) >= -epsilon;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 curr = polygon[i];
            bool currInside = Vector2.Dot(lineNormal, curr - linePoint) >= -epsilon;

            if (currInside)
            {
                if (!prevInside)
                {
                    Vector2 intersection = LineIntersection(prev, curr, linePoint, lineNormal);
                    output.Add(intersection);
                }

                output.Add(curr);
            }
            else if (prevInside)
            {
                Vector2 intersection = LineIntersection(prev, curr, linePoint, lineNormal);
                output.Add(intersection);
            }

            prev = curr;
            prevInside = currInside;
        }

        return output;
    }

    static Vector2 LineIntersection(Vector2 a, Vector2 b, Vector2 linePoint, Vector2 lineNormal)
    {
        Vector2 segment = b - a;
        float denom = Vector2.Dot(lineNormal, segment);
        if (Mathf.Abs(denom) < 1e-6f)
            return a;

        float t = Vector2.Dot(lineNormal, linePoint - a) / denom;
        return a + segment * t;
    }

    static void CleanPolygon(List<Vector2> polygon)
    {
        if (polygon == null || polygon.Count < 3)
            return;

        RemoveDuplicatePoints(polygon);
        RemoveCollinearPoints(polygon);
        EnsureCounterClockwise(polygon);
    }

    static void RemoveDuplicatePoints(List<Vector2> polygon)
    {
        const float epsilon = 1e-5f;
        for (int i = polygon.Count - 1; i >= 0; i--)
        {
            int prevIndex = (i - 1 + polygon.Count) % polygon.Count;
            if ((polygon[i] - polygon[prevIndex]).sqrMagnitude < epsilon)
                polygon.RemoveAt(i);
        }
    }

    static void RemoveCollinearPoints(List<Vector2> polygon)
    {
        const float epsilon = 1e-6f;
        if (polygon.Count < 3)
            return;

        bool removed = true;
        int guard = 0;
        while (removed && guard < 1000)
        {
            removed = false;
            for (int i = polygon.Count - 1; i >= 0; i--)
            {
                Vector2 prev = polygon[(i - 1 + polygon.Count) % polygon.Count];
                Vector2 curr = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];

                if (Mathf.Abs(Cross(curr - prev, next - curr)) < epsilon)
                {
                    polygon.RemoveAt(i);
                    removed = true;
                    break;
                }
            }
            guard++;
        }
    }

    static void EnsureCounterClockwise(List<Vector2> polygon)
    {
        if (polygon == null || polygon.Count < 3)
            return;

        if (ComputeSignedArea(polygon) < 0f)
            polygon.Reverse();
    }

    static float ComputeSignedArea(List<Vector2> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            area += (a.x * b.y) - (b.x * a.y);
        }

        return area * 0.5f;
    }

    static Vector2 ComputeCentroid(List<Vector2> polygon)
    {
        float area = ComputeSignedArea(polygon);
        if (Mathf.Abs(area) < 1e-6f)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < polygon.Count; i++)
                sum += polygon[i];
            return sum / Mathf.Max(1, polygon.Count);
        }

        float cx = 0f;
        float cy = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            float cross = (a.x * b.y) - (b.x * a.y);
            cx += (a.x + b.x) * cross;
            cy += (a.y + b.y) * cross;
        }

        float factor = 1f / (6f * area);
        return new Vector2(cx * factor, cy * factor);
    }

    static float NextGaussian(System.Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
    }

    static Vector2 RandomPointInRect(System.Random rng, Rect rect)
    {
        float x = rect.xMin + (float)rng.NextDouble() * rect.width;
        float y = rect.yMin + (float)rng.NextDouble() * rect.height;
        return new Vector2(x, y);
    }

    static Rect ScaleRect(Rect rect, float scale)
    {
        scale = Mathf.Clamp(scale, 0.2f, 1f);
        Vector2 center = rect.center;
        Vector2 size = rect.size * scale;
        return new Rect(center - size * 0.5f, size);
    }

    static Vector2 RandomUnit(System.Random rng)
    {
        double angle = rng.NextDouble() * Math.PI * 2.0;
        return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
    }

    Vector2 ClampToBounds(Vector2 point)
    {
        float x = Mathf.Clamp(point.x, bounds.xMin, bounds.xMax);
        float y = Mathf.Clamp(point.y, bounds.yMin, bounds.yMax);
        return new Vector2(x, y);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        if (drawBounds)
        {
            Gizmos.color = Color.gray;
            Vector3 a = ToWorld(new Vector2(bounds.xMin, bounds.yMin));
            Vector3 b = ToWorld(new Vector2(bounds.xMax, bounds.yMin));
            Vector3 c = ToWorld(new Vector2(bounds.xMax, bounds.yMax));
            Vector3 d = ToWorld(new Vector2(bounds.xMin, bounds.yMax));
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        if (drawCells && cellPolygons != null)
        {
            for (int i = 0; i < cellPolygons.Count; i++)
                DrawPolygon(cellPolygons[i], Color.gray);
        }

        if (drawRegions && regions != null)
        {
            for (int i = 0; i < regions.Count; i++)
                DrawPolygon(regions[i].polygon, ClusterColor(regions[i].clusterId));
        }

        if (drawSeeds && seedPoints != null)
        {
            for (int i = 0; i < seedPoints.Count; i++)
            {
                Gizmos.color = ClusterColor(seedPoints[i].clusterId);
                Gizmos.DrawSphere(ToWorld(seedPoints[i].pos), seedGizmoRadius);
            }
        }
    }

    void DrawPolygon(List<Vector2> polygon, Color color)
    {
        if (polygon == null || polygon.Count < 2)
            return;

        Gizmos.color = color;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 a = ToWorld(polygon[i]);
            Vector3 b = ToWorld(polygon[(i + 1) % polygon.Count]);
            Gizmos.DrawLine(a, b);
        }
    }

    Vector3 ToWorld(Vector2 local)
    {
        return transform.TransformPoint(new Vector3(local.x, local.y, 0f));
    }

    static Color ClusterColor(int clusterId)
    {
        float hue = Mathf.Repeat(clusterId * 0.27f, 1f);
        return Color.HSVToRGB(hue, 0.7f, 0.9f);
    }

    private struct Vector2Key : IEquatable<Vector2Key>
    {
        private const float Snap = 1000f;
        public readonly int x;
        public readonly int y;

        public Vector2Key(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2Key FromVector(Vector2 v)
        {
            return new Vector2Key(Mathf.RoundToInt(v.x * Snap), Mathf.RoundToInt(v.y * Snap));
        }

        public bool Equals(Vector2Key other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector2Key other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ y;
            }
        }
    }

    private struct EdgeKey2 : IEquatable<EdgeKey2>
    {
        public readonly Vector2Key a;
        public readonly Vector2Key b;

        public EdgeKey2(Vector2Key a, Vector2Key b)
        {
            if (Compare(a, b) <= 0)
            {
                this.a = a;
                this.b = b;
            }
            else
            {
                this.a = b;
                this.b = a;
            }
        }

        public bool Equals(EdgeKey2 other)
        {
            return a.Equals(other.a) && b.Equals(other.b);
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (a.GetHashCode() * 397) ^ b.GetHashCode();
            }
        }

        static int Compare(Vector2Key left, Vector2Key right)
        {
            int cmp = left.x.CompareTo(right.x);
            if (cmp != 0)
                return cmp;
            return left.y.CompareTo(right.y);
        }
    }

    private struct EdgeData
    {
        public int count;
        public Vector2 from;
        public Vector2 to;
        public Vector2Key fromKey;
        public Vector2Key toKey;
    }

    private struct Circle
    {
        public Vector2 center;
        public float radiusSq;
    }

    private class DelaunayTriangle
    {
        public int a;
        public int b;
        public int c;
        public Circle circumcircle;

        public DelaunayTriangle(int a, int b, int c, List<Vector2> points)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            circumcircle = DelaunayTriangulation.ComputeCircumcircle(points[a], points[b], points[c]);
        }

        public bool ContainsVertex(int index)
        {
            return a == index || b == index || c == index;
        }
    }

    private struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly int a;
        public readonly int b;

        public EdgeKey(int a, int b)
        {
            if (a <= b)
            {
                this.a = a;
                this.b = b;
            }
            else
            {
                this.a = b;
                this.b = a;
            }
        }

        public bool Equals(EdgeKey other)
        {
            return a == other.a && b == other.b;
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (a * 397) ^ b;
            }
        }
    }

    private struct Edge
    {
        public int a;
        public int b;

        public Edge(int a, int b)
        {
            this.a = a;
            this.b = b;
        }
    }

    private class DelaunayTriangulation
    {
        public List<DelaunayTriangle> triangles = new List<DelaunayTriangle>();

        public static DelaunayTriangulation Build(List<Vector2> points)
        {
            DelaunayTriangulation result = new DelaunayTriangulation();
            if (points == null || points.Count < 3)
                return result;

            List<Vector2> allPoints = new List<Vector2>(points);
            CreateSuperTriangle(points, out Vector2 p1, out Vector2 p2, out Vector2 p3);
            int i1 = allPoints.Count;
            int i2 = allPoints.Count + 1;
            int i3 = allPoints.Count + 2;
            allPoints.Add(p1);
            allPoints.Add(p2);
            allPoints.Add(p3);

            List<DelaunayTriangle> triangles = new List<DelaunayTriangle>
            {
                new DelaunayTriangle(i1, i2, i3, allPoints)
            };

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = allPoints[i];
                List<DelaunayTriangle> badTriangles = new List<DelaunayTriangle>();

                for (int t = 0; t < triangles.Count; t++)
                {
                    if (PointInsideCircumcircle(point, triangles[t]))
                        badTriangles.Add(triangles[t]);
                }

                Dictionary<EdgeKey, Edge> polygon = new Dictionary<EdgeKey, Edge>();
                Dictionary<EdgeKey, int> edgeCounts = new Dictionary<EdgeKey, int>();

                for (int t = 0; t < badTriangles.Count; t++)
                {
                    DelaunayTriangle tri = badTriangles[t];
                    AddEdge(tri.a, tri.b, polygon, edgeCounts);
                    AddEdge(tri.b, tri.c, polygon, edgeCounts);
                    AddEdge(tri.c, tri.a, polygon, edgeCounts);
                }

                for (int t = triangles.Count - 1; t >= 0; t--)
                {
                    if (badTriangles.Contains(triangles[t]))
                        triangles.RemoveAt(t);
                }

                foreach (KeyValuePair<EdgeKey, Edge> pair in polygon)
                {
                    if (!edgeCounts.TryGetValue(pair.Key, out int count) || count != 1)
                        continue;

                    Edge edge = pair.Value;
                    triangles.Add(new DelaunayTriangle(edge.a, edge.b, i, allPoints));
                }
            }

            for (int t = triangles.Count - 1; t >= 0; t--)
            {
                DelaunayTriangle tri = triangles[t];
                if (tri.ContainsVertex(i1) || tri.ContainsVertex(i2) || tri.ContainsVertex(i3))
                    triangles.RemoveAt(t);
            }

            result.triangles = triangles;
            return result;
        }

        static void AddEdge(int a, int b, Dictionary<EdgeKey, Edge> polygon, Dictionary<EdgeKey, int> edgeCounts)
        {
            EdgeKey key = new EdgeKey(a, b);
            if (edgeCounts.TryGetValue(key, out int count))
                edgeCounts[key] = count + 1;
            else
                edgeCounts[key] = 1;

            if (!polygon.ContainsKey(key))
                polygon[key] = new Edge(a, b);
        }

        static bool PointInsideCircumcircle(Vector2 point, DelaunayTriangle tri)
        {
            float distSq = (point - tri.circumcircle.center).sqrMagnitude;
            return distSq <= tri.circumcircle.radiusSq * 1.0001f;
        }

        static void CreateSuperTriangle(List<Vector2> points, out Vector2 p1, out Vector2 p2, out Vector2 p3)
        {
            float minX = points[0].x;
            float minY = points[0].y;
            float maxX = points[0].x;
            float maxY = points[0].y;

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            float dx = maxX - minX;
            float dy = maxY - minY;
            float delta = Mathf.Max(dx, dy);
            Vector2 mid = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

            p1 = mid + new Vector2(-2f * delta, -delta);
            p2 = mid + new Vector2(0f, 2f * delta);
            p3 = mid + new Vector2(2f * delta, -delta);
        }

        internal static Circle ComputeCircumcircle(Vector2 a, Vector2 b, Vector2 c)
        {
            double ax = a.x;
            double ay = a.y;
            double bx = b.x;
            double by = b.y;
            double cx = c.x;
            double cy = c.y;

            double d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-12)
            {
                return new Circle
                {
                    center = (a + b + c) / 3f,
                    radiusSq = float.PositiveInfinity
                };
            }

            double ax2ay2 = ax * ax + ay * ay;
            double bx2by2 = bx * bx + by * by;
            double cx2cy2 = cx * cx + cy * cy;

            double ux = (ax2ay2 * (by - cy) + bx2by2 * (cy - ay) + cx2cy2 * (ay - by)) / d;
            double uy = (ax2ay2 * (cx - bx) + bx2by2 * (ax - cx) + cx2cy2 * (bx - ax)) / d;

            Vector2 center = new Vector2((float)ux, (float)uy);
            float radiusSq = (center - a).sqrMagnitude;

            return new Circle
            {
                center = center,
                radiusSq = radiusSq
            };
        }
    }
}
