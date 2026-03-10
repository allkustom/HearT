using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }
    public string playerName = "Player";
    public int seedNumber = 0;
    public int gameScore = 0;
    public int page4Answer = 0;
    public int soundSelect_1 = 0;
    public int soundSelect_2 = 0;
    public int soundSelect_3 = 0;
    public int soundSelect_4 = 0;
    public int diagnosisCount = 0;
    public TextMeshProUGUI diagnosisCountText;
    public Vector3[] dignosisPoints;
    public int symptomCount = 0;
    public TextMeshProUGUI symptomCountsText;
    public GameObject lastPage;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(this.gameObject);
        lastPage.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        diagnosisCountText.text = diagnosisCount.ToString();   
        symptomCountsText.text = symptomCount.ToString();
    }

    public void addDiagnosisPoint()
    {
        dignosisPoints[diagnosisCount] = PlayerStateManager.Instance.transform.position;
        diagnosisCount++;

    }
    public void undoDiagnosisPoint()
    {
        diagnosisCount--;
        dignosisPoints[diagnosisCount] = Vector3.zero;

    }

    public void submitDiagnosis()
    {
        lastPage.SetActive(true);
        // Add more actions
        // Scoring systems

    }
}
