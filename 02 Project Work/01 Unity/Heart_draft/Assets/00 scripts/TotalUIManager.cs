using UnityEngine;
using TMPro;
using UnityEditor;

public class TotalUIManager : MonoBehaviour
{

    public static TotalUIManager Instance { get; private set; }
    public TMP_InputField playerName;
    public TMP_InputField seedNumber;
    public int pageNum = 0;
    public GameObject pageCollection;
    public GameObject[] pages;
    public GameObject dashboardUI;
    public PlayerStateManager playerStateManager;
    public TextMeshProUGUI diagnosisCountText;
    public TextMeshProUGUI symptomCountsText;
    public bool enableClickProceed = false;
    public bool isTutorialInteract = false;
    public GameObject[] tutorialMaps;
    public GameObject[] sectionCovers;
    public GameObject endingPage;
    public TextMeshProUGUI endingPageScore;


    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
        pageCollection.SetActive(true);
        dashboardUI.SetActive(false);

        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(true);
        }

        tutorialMaps[0].SetActive(false);
        tutorialMaps[1].SetActive(false);
        endingPage.SetActive(false);


    }

    void Update()
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (i == pageNum)
            {
                pages[i].SetActive(true);
            }
            else
            {
                pages[i].SetActive(false);
            }
        }
        if (pageNum == 5)
        {
            if (Esp32SppSerialReceiver.Instance.face == 1)
            {
                sectionCovers[0].SetActive(false);
                sectionCovers[1].SetActive(true);
            }
            else if (Esp32SppSerialReceiver.Instance.face == -1)
            {
                sectionCovers[0].SetActive(true);
                sectionCovers[1].SetActive(false);
            }
        }

        diagnosisCountText.text = PlayerDataManager.Instance.diagnosisCount.ToString();
        symptomCountsText.text = PlayerDataManager.Instance.symptomCount.ToString();
                endingPageScore.text = PlayerDataManager.Instance.playerScore.ToString();


    }
    public void NextPage()
    {
        pageNum++;

        if (pageNum == pages.Length)
        {
            // ACTION
            // Add Dashboard enable
            playerStateManager.isInIntro = false;
            pageCollection.SetActive(false);
        }
        if (pageNum == 1 && !enableClickProceed)
        {
            enableClickProceed = true;
        }
        if (pageNum == 4 && !isTutorialInteract)
        {
            tutorialMaps[0].SetActive(true);
            isTutorialInteract = true;
            enableClickProceed = false;
        }
        if (pageNum == 5)
        {
            tutorialMaps[0].SetActive(false);

        }
        if (pageNum == 6 && !isTutorialInteract)
        {
            tutorialMaps[1].SetActive(true);
            isTutorialInteract = true;
            enableClickProceed = false;
        }
        if (pageNum == 7)
        {
            tutorialMaps[1].SetActive(false);

        }

        // Tutorial Ends, game starts
        if (pageNum == pages.Length)
        {
            dashboardUI.SetActive(true);
            MapManager.Instance.GenerateMap();
            PlayerStateManager.Instance.isInIntro = false;
        }

    }

    public void NameAndSeed()
    {
        PlayerDataManager.Instance.playerName = playerName.text;
        PlayerDataManager.Instance.seedNumber = int.Parse(seedNumber.text);

    }

    public void EndingPage()
    {
        endingPage.SetActive(true);
        // endingPageScore.text = PlayerDataManager.Instance.playerScore.ToString();
    }


}
