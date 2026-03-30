using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

[System.Serializable]
public struct DiagnosisPointData
{
    public Vector3 position;
    public int type;

    public DiagnosisPointData(Vector3 position, int type)
    {
        this.position = position;
        this.type = type;
    }
}

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }

    public string playerName = "Player";
    public int seedNumber = 0;

    public int diagnosisCount = 0;
    public DiagnosisPointData[] diagnosisPoints;

    public int symptomCount = 0;
    public int playerScore = 100;
    public GameObject lastPage;

    public PlotterSerialSender plotterSender;

    [Header("Plotter Shape Size (mm)")]
    public float minShapeSizeMm = 10f;
    public float maxShapeSizeMm = 30f;

    [Header("Plotter Cross Size (mm)")]
    public float crossSizeMm = 15f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (lastPage != null)
            lastPage.SetActive(false);
    }

    void Update()
    {
    }

    public void ResizeDiagnosisPoints()
    {
        diagnosisPoints = new DiagnosisPointData[symptomCount];
        diagnosisCount = 0;
    }

    public void addDiagnosisPoint(int type)
    {
        if (diagnosisPoints == null || diagnosisPoints.Length == 0)
        {
            Debug.LogWarning("diagnosisPoints is null or empty.");
            return;
        }

        if (diagnosisCount < 0 || diagnosisCount >= diagnosisPoints.Length)
        {
            Debug.LogWarning("diagnosisCount is out of range.");
            return;
        }

        diagnosisPoints[diagnosisCount] = new DiagnosisPointData(
            PlayerStateManager.Instance.transform.position,
            type
        );

        diagnosisCount++;

        if (diagnosisCount >= diagnosisPoints.Length)
        {
            if (TotalUIManager.Instance != null)
            {
                CalculatePlayerScore();
                TotalUIManager.Instance.EndingPage();
            }
        }
    }

    public void undoDiagnosisPoint()
    {
        if (diagnosisPoints == null || diagnosisCount <= 0)
            return;

        diagnosisCount--;
        diagnosisPoints[diagnosisCount] = new DiagnosisPointData(Vector3.zero, 0);
    }

    public void submitDiagnosis()
    {
        // CalculatePlayerScore();

        string plotterSequence = BuildPlotterSequence();

        if (plotterSender != null)
        {
            if (!string.IsNullOrEmpty(plotterSequence))
            {
                plotterSender.SendPlotterJob(plotterSequence);
                Debug.Log("Plotter job sent.");
            }
            else
            {
                Debug.LogWarning("Plotter sequence is empty.");
            }
        }
        else
        {
            Debug.LogWarning("PlotterSerialSender is not assigned.");
        }

        if (lastPage != null)
        {
            lastPage.SetActive(true);
        }
    }

    public void CalculatePlayerScore()
    {
        if (MapManager.Instance == null)
        {
            Debug.LogWarning("MapManager.Instance is null.");
            playerScore = 0;
            return;
        }

        List<GameObject> spawnedSymptoms = MapManager.Instance.GetSpawnedSymptoms();
        if (spawnedSymptoms == null || spawnedSymptoms.Count == 0)
        {
            Debug.LogWarning("No spawned symptoms found.");
            playerScore = 0;
            return;
        }

        if (diagnosisPoints == null || diagnosisCount <= 0)
        {
            Debug.LogWarning("No diagnosis points to score.");
            playerScore = 0;
            return;
        }

        int validDiagnosisCount = Mathf.Min(diagnosisCount, diagnosisPoints.Length);

        float totalScore = 0f;
        int scoredSymptomCount = 0;

        for (int i = 0; i < spawnedSymptoms.Count; i++)
        {
            GameObject go = spawnedSymptoms[i];
            if (go == null) continue;

            SymptomManager symptom = go.GetComponent<SymptomManager>();
            if (symptom == null) continue;

            DiagnosisPointData nearestDiagnosis = FindNearestDiagnosisPoint(symptom.transform.position, validDiagnosisCount);
            float score = CalculateSymptomScore(symptom, nearestDiagnosis);

            totalScore += score;
            scoredSymptomCount++;

            Debug.Log(
                $"Symptom {i}: symptomType={symptom.type}, symptomPos={symptom.transform.position}, " +
                $"diagnosisType={nearestDiagnosis.type}, diagnosisPos={nearestDiagnosis.position}, " +
                $"range={symptom.range:F2}, score={score:F2}"
            );
        }

        if (scoredSymptomCount > 0)
        {
            playerScore = Mathf.RoundToInt(totalScore / scoredSymptomCount);
        }
        else
        {
            playerScore = 0;
        }

        Debug.Log($"Total Score = {totalScore:F2}, scoredSymptomCount = {scoredSymptomCount}, playerScore = {playerScore}");
    }

    private float CalculateSymptomScore(SymptomManager symptom, DiagnosisPointData diagnosis)
    {
        if (diagnosis.type != symptom.type)
        {
            return 0f;
        }

        Vector2 symptom2D = new Vector2(symptom.transform.position.x, symptom.transform.position.z);
        Vector2 diagnosis2D = new Vector2(diagnosis.position.x, diagnosis.position.z);

        float distance = Vector2.Distance(symptom2D, diagnosis2D);
        float range = symptom.range;

        if (range <= 0f)
        {
            return 20f;
        }

        float perfectDistance = range * 0.2f;
        float distanceScore = 0f;

        if (distance <= perfectDistance)
        {
            distanceScore = 80f;
        }
        else if (distance >= range)
        {
            distanceScore = 0f;
        }
        else
        {
            float t = (distance - perfectDistance) / (range - perfectDistance);
            distanceScore = Mathf.Lerp(80f, 0f, t);
        }

        return 20f + distanceScore;
    }

    public string BuildPlotterSequence()
    {
        if (MapManager.Instance == null)
        {
            Debug.LogWarning("MapManager.Instance is null.");
            return "";
        }

        List<GameObject> spawnedSymptoms = MapManager.Instance.GetSpawnedSymptoms();
        if (spawnedSymptoms == null || spawnedSymptoms.Count == 0)
        {
            Debug.LogWarning("No spawned symptoms found.");
            return "";
        }

        if (diagnosisPoints == null || diagnosisCount <= 0)
        {
            Debug.LogWarning("No diagnosis points available.");
            return "";
        }

        int validDiagnosisCount = Mathf.Min(diagnosisCount, diagnosisPoints.Length);

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("START");
        sb.AppendLine("HOME2");
        sb.AppendLine("MICRO");

        for (int i = 0; i < spawnedSymptoms.Count; i++)
        {
            GameObject go = spawnedSymptoms[i];
            if (go == null) continue;

            SymptomManager symptom = go.GetComponent<SymptomManager>();
            if (symptom == null) continue;

            DiagnosisPointData nearestDiagnosis = FindNearestDiagnosisPoint(symptom.transform.position, validDiagnosisCount);

            Vector2 symptomPlot = WorldToPlotterMm(symptom.transform.position);
            Vector2 diagnosisPlot = WorldToPlotterMm(nearestDiagnosis.position);

            float shapeSizeMm = RangeToShapeSizeMm(symptom.range);
            float circleRadiusMm = shapeSizeMm * 0.5f;
            float starOuterMm = shapeSizeMm * 0.25f;
            float starInnerMm = shapeSizeMm * 0.10f;

            int sx = Mathf.RoundToInt(symptomPlot.x);
            int sy = Mathf.RoundToInt(symptomPlot.y);
            int dx = Mathf.RoundToInt(diagnosisPlot.x);
            int dy = Mathf.RoundToInt(diagnosisPlot.y);

            int cross = Mathf.RoundToInt(crossSizeMm);
            int shapeSize = Mathf.RoundToInt(shapeSizeMm);
            int circleRadius = Mathf.RoundToInt(circleRadiusMm);
            int starOuter = Mathf.RoundToInt(starOuterMm);
            int starInner = Mathf.RoundToInt(starInnerMm);

            sb.AppendLine($"CROSS {sx} {sy} {cross} P1");
            sb.AppendLine(BuildShapeCommand(symptom.type, sx, sy, shapeSize, circleRadius, starOuter, starInner, "P1"));

            sb.AppendLine($"LINE {sx} {sy} {dx} {dy} P1");

            sb.AppendLine($"CROSS {dx} {dy} {cross} P2");
            sb.AppendLine(BuildShapeCommand(nearestDiagnosis.type, dx, dy, shapeSize, circleRadius, starOuter, starInner, "P2"));

        }

        string safeName = SanitizePlotterText(playerName);
        string scoreText = $"{playerScore}/100";

        sb.AppendLine($"BOXTEXT 25 30 110 45 {safeName} P2");
        sb.AppendLine($"BOXTEXT 25 15 110 30 {scoreText} P2");
        sb.AppendLine("HOME1");
        sb.AppendLine("FREE");

        string result = sb.ToString();
        Debug.Log(result);
        return result;
    }

    private DiagnosisPointData FindNearestDiagnosisPoint(Vector3 symptomPos, int validDiagnosisCount)
    {
        DiagnosisPointData best = diagnosisPoints[0];
        float bestDistance = float.MaxValue;

        Vector2 symptom2D = new Vector2(symptomPos.x, symptomPos.z);

        for (int i = 0; i < validDiagnosisCount; i++)
        {
            DiagnosisPointData dp = diagnosisPoints[i];
            Vector2 diagnosis2D = new Vector2(dp.position.x, dp.position.z);

            float dist = Vector2.Distance(symptom2D, diagnosis2D);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                best = dp;
            }
        }

        return best;
    }

    private Vector2 WorldToPlotterMm(Vector3 worldPos)
    {
        float halfWidth = MapManager.Instance.mapWidth * 0.5f;
        float halfHeight = MapManager.Instance.mapHeight * 0.5f;

        float xMm = (worldPos.x + halfWidth) * 25.4f;
        float yMm = (worldPos.z + halfHeight) * 25.4f;

        return new Vector2(xMm, yMm);
    }

    private float RangeToShapeSizeMm(float symptomRange)
    {
        float minRange = MapManager.Instance.minRange;
        float maxRange = MapManager.Instance.maxRange;

        if (Mathf.Approximately(minRange, maxRange))
            return minShapeSizeMm;

        float t = Mathf.InverseLerp(minRange, maxRange, symptomRange);
        return Mathf.Lerp(minShapeSizeMm, maxShapeSizeMm, t);
    }

    private string BuildShapeCommand(int type, int x, int y, int size, int circleRadius, int starOuter, int starInner, string pen)
    {
        switch (type)
        {
            case 0:
                return $"RECT {x} {y} {size} {size} {pen}";
            case 1:
                return $"TRI {x} {y} {size} {pen}";
            case 2:
                return $"STAR {x} {y} {starOuter} {starInner} {pen}";
            case 3:
                return $"CIRCLE {x} {y} {circleRadius} {pen}";
            default:
                return $"CIRCLE {x} {y} {circleRadius} {pen}";
        }
    }

    private string SanitizePlotterText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "PLAYER";

        string cleaned = input.Trim().ToUpperInvariant();
        cleaned = cleaned.Replace(" ", "_");
        return cleaned;
    }
}