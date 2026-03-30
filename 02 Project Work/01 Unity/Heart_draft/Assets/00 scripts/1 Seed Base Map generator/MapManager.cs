using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{

    public static MapManager Instance { get; private set; }
    [Header("Map Size")]
    public float mapWidth = 17.0f;
    public float mapHeight = 11.0f;
    public float edgePadding = 0.25f;

    [Header("Enermy Generation")]
    public GameObject symptomPrefab;
    public bool generateMapCheck = false;

    [Header("Random Symptom Count")]
    public int minSymptomCount = 3;
    public int maxSymptomCount = 6;
    [Header("Difficulty Settings - Easy")]
    public int easyMinSymptomCount = 3;
    public int easyMaxSymptomCount = 4;
    [Header("Difficulty Settings - Hard")]
    public int hardMinSymptomCount = 5;
    public int hardMaxSymptomCount = 7;

    public int minType = 0;
    public int maxType = 2;

    public float minRange = 2.0f;
    public float maxRange = 4.0f;
    public float minDistanceBetweenSymptoms = 2.0f;
    public int maxPositionTryCount = 50;

    [Header("Debug")]
    public int debugSeed = 4567;
    public bool showSeedOnScreen = true;

    private readonly List<GameObject> spawned = new();
    private int currentSeed = 0;

    public List<GameObject> GetSpawnedSymptoms()
    {
        return spawned;
    }

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }


    void Update()
    {
        if (generateMapCheck)
        {
            generateMapCheck = false;
            GenerateMap();
        }
    }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        ClearSpawned();

        currentSeed = GetSeedFromPlayerData();

        Random.InitState(currentSeed);

        if (symptomPrefab == null)
        {
            return;
        }

        int symptomCount = Random.Range(minSymptomCount, maxSymptomCount + 1);

        List<int> generatedTypes = GenerateTypes(symptomCount);
        List<Vector2> placedPoints = new List<Vector2>();

        for (int i = 0; i < symptomCount; i++)
        {
            Vector2 mapPos;
            bool found = TryGetRandomPointInMap(placedPoints, out mapPos);

            if (!found)
            {
                Debug.LogWarning($"[MapManager] Could not find valid spawn position for symptom {i} with min distance {minDistanceBetweenSymptoms}");
                continue;
            }

            placedPoints.Add(mapPos);

            Vector3 worldPos = MapToWorld(mapPos);

            GameObject go = Instantiate(symptomPrefab, worldPos, Quaternion.identity);
            go.name = $"Symptom_{i:00}_T{generatedTypes[i]}";

            SymptomManager em = go.GetComponent<SymptomManager>();
            if (em != null)
            {
                em.type = generatedTypes[i];
                em.range = Random.Range(minRange, maxRange);
            }
            else
            {
                Debug.LogWarning($"[MapManager] Spawned prefab has no SymptomManager: {go.name}");
            }

            spawned.Add(go);

            Debug.Log($"[MapManager] Spawned {go.name} at {worldPos}, range={(em != null ? em.range : 0f):F2}");
        }

        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.symptomCount = spawned.Count;
            PlayerDataManager.Instance.ResizeDiagnosisPoints();
        }
        Debug.Log($"[MapManager] Generated map with seed {currentSeed}, symptomCount={spawned.Count}");
    }

    int GetSeedFromPlayerData()
    {
        // if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.seedNumber >= 1000)
        // {
        //     return PlayerDataManager.Instance.seedNumber;
        // }

        if (PlayerDataManager.Instance != null)
        {

            // Difficulty function, deactivated at Mar 18th after the Discord discussion
            // if (PlayerDataManager.Instance.seedNumber >= 5000)
            // {
            //     minSymptomCount = hardMinSymptomCount;
            //     maxSymptomCount = hardMaxSymptomCount;
            // }
            // else
            // {
            //     minSymptomCount = easyMinSymptomCount;
            //     maxSymptomCount = easyMaxSymptomCount;
            // }

            return PlayerDataManager.Instance.seedNumber;
        }

        return debugSeed;
    }

    List<int> GenerateTypes(int count)
    {
        List<int> result = new List<int>();

        for (int i = 0; i < count; i++)
        {
            result.Add(Random.Range(minType, maxType + 1));
        }

        bool allSame = true;
        for (int i = 1; i < result.Count; i++)
        {
            if (result[i] != result[0])
            {
                allSame = false;
                break;
            }
        }

        if (allSame && result.Count > 1)
        {
            int randomIndex = Random.Range(0, result.Count);
            int newType = result[randomIndex];

            while (newType == result[0])
            {
                newType = Random.Range(minType, maxType + 1);
            }

            result[randomIndex] = newType;
        }

        return result;
    }

    Vector2 RandomPointInMap()
    {
        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        float x = Random.Range(-halfWidth + edgePadding, halfWidth - edgePadding);
        float y = Random.Range(-halfHeight + edgePadding, halfHeight - edgePadding);

        return new Vector2(x, y);
    }

    bool ApplyMinDistance(Vector2 assignedPoints, List<Vector2> placedPoints, float minDistance)
    {
        float minDistanceSqr = minDistance * minDistance;

        for (int i = 0; i < placedPoints.Count; i++)
        {
            if ((assignedPoints - placedPoints[i]).sqrMagnitude < minDistanceSqr)
            {
                return false;
            }
        }
        return true;
    }
    bool TryGetRandomPointInMap(List<Vector2> placedPoints, out Vector2 result)
    {
        for (int i = 0; i < maxPositionTryCount; i++)
        {
            Vector2 assignedPoints = RandomPointInMap();

            if (ApplyMinDistance(assignedPoints, placedPoints, minDistanceBetweenSymptoms))
            {
                result = assignedPoints;
                return true;
            }
        }

        result = Vector2.zero;
        return false;
    }
    Vector3 MapToWorld(Vector2 mapPos)
    {
        return new Vector3(mapPos.x, 0f, mapPos.y);
    }

    void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                Destroy(spawned[i]);
            }
        }

        spawned.Clear();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        Vector3 bl = new Vector3(-halfWidth, 0f, -halfHeight);
        Vector3 br = new Vector3(halfWidth, 0f, -halfHeight);
        Vector3 tr = new Vector3(halfWidth, 0f, halfHeight);
        Vector3 tl = new Vector3(-halfWidth, 0f, halfHeight);

        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }

    void OnGUI()
    {
        if (!showSeedOnScreen) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.normal.textColor = Color.black;

        GUI.Label(new Rect(20, 20, 300, 40), "Seed: " + currentSeed, style);
    }
}