using UnityEngine;
using TMPro;

public class IntroManager_Buttons : MonoBehaviour
{
    public static IntroManager_Buttons Instance { get; private set; }
    public TMP_InputField playerName;
    public TMP_InputField seedNumber;
    public GameObject nextButton;
    public int pageNum = 0;
    public GameObject pageCollection;
    public GameObject correctAnswerUI;
    public GameObject[] murmurSounds;
    private int murmurQuadrant = 0;
    public GameObject[] firstSoundFlip;
    public GameObject[] rawSoundsSample;
    public GameObject[] soundSamples_1;
    public GameObject[] soundSamples_2;
    public GameObject[] soundSamples_3;
    public GameObject[] soundSamples_4;
    public GameObject[] dashBoardRawSounds;
    public GameObject[] dashBoardConvertedSounds;

    public Esp32SppSerialReceiver serialReceiver;
    public GameObject playerObject;
    public GameObject[] pages;
    public GameObject dashboardUI;
    public GameObject dashboardPanelBlock;
    public PlayerStateManager playerStateManager;

    void Awake()
    {
        Instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pageCollection.SetActive(true);
        nextButton.SetActive(false);
        correctAnswerUI.SetActive(false);
        for (int i = 0; i < murmurSounds.Length; i++)
        {
            murmurSounds[i].SetActive(false);
            soundSamples_1[i].SetActive(false);
            soundSamples_2[i].SetActive(false);
            soundSamples_3[i].SetActive(false);
            soundSamples_4[i].SetActive(false);
        }
        for (int i = 0; i < firstSoundFlip.Length; i++)
        {
            firstSoundFlip[i].SetActive(false);
        }
        dashboardUI.SetActive(false);
        dashboardPanelBlock.SetActive(true);


        for (int i = 0; i < dashBoardRawSounds.Length; i++)
        {
            dashBoardRawSounds[i].SetActive(false);
        }
        for (int i = 0; i < dashBoardConvertedSounds.Length; i++)
        {
            dashBoardConvertedSounds[i].SetActive(false);
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (!playerStateManager.isPlayerOnPlane)
        {
            dashboardPanelBlock.SetActive(false);
        }
        else
        {
            dashboardPanelBlock.SetActive(true);
        }

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

        if (playerObject.transform.position.z > 0 && playerObject.transform.position.x < 0)
        {
            murmurQuadrant = 0;
        }
        else if (playerObject.transform.position.z > 0 && playerObject.transform.position.x > 0)
        {
            murmurQuadrant = 1;
        }
        else if (playerObject.transform.position.z < 0 && playerObject.transform.position.x < 0)
        {
            murmurQuadrant = 2;
        }
        else if (playerObject.transform.position.z < 0 && playerObject.transform.position.x > 0)
        {
            murmurQuadrant = 3;
        }

        if (pageNum == 7)
        {
            for (int i = 0; i < murmurSounds.Length; i++)
            {
                if (i == murmurQuadrant)
                {
                    murmurSounds[i].SetActive(true);
                }
                else
                {
                    murmurSounds[i].SetActive(false);
                }
            }
        }






        if (pageNum == 8)
        {
            if (serialReceiver.face == 1)
            {
                firstSoundFlip[0].SetActive(true);
                firstSoundFlip[1].SetActive(false);
            }
            else if (serialReceiver.face == -1)
            {
                firstSoundFlip[0].SetActive(false);
                firstSoundFlip[1].SetActive(true);
            }
            else
            {
                firstSoundFlip[0].SetActive(false);
                firstSoundFlip[1].SetActive(false);
            }
        }


        if (pageNum == 10)
        {
            if (serialReceiver.face == 1)
            {
                rawSoundsSample[0].SetActive(true);
                for (int i = 0; i < soundSamples_1.Length; i++)
                {
                    soundSamples_1[i].SetActive(false);
                }
            }
            else if (serialReceiver.face == -1)
            {
                rawSoundsSample[0].SetActive(false);
                for (int i = 0; i < soundSamples_1.Length; i++)
                {
                    if (i == murmurQuadrant)
                    {
                        soundSamples_1[i].SetActive(true);
                    }
                    else
                    {
                        soundSamples_1[i].SetActive(false);
                    }
                }
            }
            else
            {
                rawSoundsSample[0].SetActive(false);
                for (int i = 0; i < soundSamples_1.Length; i++)
                {
                    soundSamples_1[i].SetActive(false);
                }
            }
        }

        if (pageNum == 11)
        {
            if (serialReceiver.face == 1)
            {
                rawSoundsSample[1].SetActive(true);
                for (int i = 0; i < soundSamples_2.Length; i++)
                {
                    soundSamples_2[i].SetActive(false);
                }
            }
            else if (serialReceiver.face == -1)
            {
                rawSoundsSample[1].SetActive(false);
                for (int i = 0; i < soundSamples_2.Length; i++)
                {
                    if (i == murmurQuadrant)
                    {
                        soundSamples_2[i].SetActive(true);
                    }
                    else
                    {
                        soundSamples_2[i].SetActive(false);
                    }
                }
            }
            else
            {
                rawSoundsSample[1].SetActive(false);
                for (int i = 0; i < soundSamples_2.Length; i++)
                {
                    soundSamples_2[i].SetActive(false);
                }
            }
        }
        if (pageNum == 12)
        {
            if (serialReceiver.face == 1)
            {
                rawSoundsSample[2].SetActive(true);
                for (int i = 0; i < soundSamples_3.Length; i++)
                {
                    soundSamples_3[i].SetActive(false);
                }
            }
            else if (serialReceiver.face == -1)
            {
                rawSoundsSample[3].SetActive(false);
                for (int i = 0; i < soundSamples_3.Length; i++)
                {
                    if (i == murmurQuadrant)
                    {
                        soundSamples_3[i].SetActive(true);
                    }
                    else
                    {
                        soundSamples_3[i].SetActive(false);
                    }
                }
            }
            else
            {
                rawSoundsSample[2].SetActive(false);
                for (int i = 0; i < soundSamples_3.Length; i++)
                {
                    soundSamples_3[i].SetActive(false);
                }
            }
        }
        if (pageNum == 13)
        {
            if (serialReceiver.face == 1)
            {
                rawSoundsSample[3].SetActive(true);
                for (int i = 0; i < soundSamples_4.Length; i++)
                {
                    soundSamples_4[i].SetActive(false);
                }
            }
            else if (serialReceiver.face == -1)
            {
                rawSoundsSample[3].SetActive(false);
                for (int i = 0; i < soundSamples_4.Length; i++)
                {
                    if (i == murmurQuadrant)
                    {
                        soundSamples_4[i].SetActive(true);
                    }
                    else
                    {
                        soundSamples_4[i].SetActive(false);
                    }
                }
            }
            else
            {
                rawSoundsSample[3].SetActive(false);
                for (int i = 0; i < soundSamples_4.Length; i++)
                {
                    soundSamples_4[i].SetActive(false);
                }
            }
        }

        if (pageNum == 16)
        {
            dashboardUI.SetActive(true);
        }




    }

    public void NextScene()
    {
        pageNum++;
        if (pageNum == 4)
        {
            DisableButton();
        }
        if (pageNum == 5)
        {
            // if (PlayerDataManager.Instance.page4Answer == 1)
            // {
            //     correctAnswerUI.SetActive(true);
            // }
        }

        if (pageNum == pages.Length)
        {
            // ACTION
            // Add Dashboard enable
            playerStateManager.isInIntro = false;
            playerStateManager.audioListener.enabled = true;
            playerStateManager.isInIntro = false;
            DisableButton();
        }

        if (pageNum == 10 || pageNum == 11 || pageNum == 12 || pageNum == 13)
        {
            DisableButton();
        }

    }
    public void NameAndSeed()
    {
        PlayerDataManager.Instance.playerName = playerName.text;
        PlayerDataManager.Instance.seedNumber = int.Parse(seedNumber.text);
        nextButton.SetActive(true);

    }
    public void EnableButton()
    {
        nextButton.SetActive(true);
    }
    public void DisableButton()
    {
        nextButton.SetActive(false);
    }

    // public void Page4Answer_1()
    // {
    //     PlayerDataManager.Instance.page4Answer = 1;
    // }
    // public void Page4Answer_2()
    // {
    //     PlayerDataManager.Instance.page4Answer = 2;
    // }
    // public void Page4Answer_3()
    // {
    //     PlayerDataManager.Instance.page4Answer = 3;
    // }
    // public void Page4Answer_4()
    // {
    //     PlayerDataManager.Instance.page4Answer = 4;
    // }


    // // Add TDUDP sending function below clip setting
    // public void sound1Select_1_Apply()
    // {
    //     dashBoardConvertedSounds[0].GetComponent<AudioSource>().clip = soundSamples_1[PlayerDataManager.Instance.soundSelect_1].GetComponent<AudioSource>().clip;
    //     SendSoundSelectsToTD();
    // }
    // public void sound1Select_2_Apply()
    // {
    //     dashBoardConvertedSounds[1].GetComponent<AudioSource>().clip = soundSamples_2[PlayerDataManager.Instance.soundSelect_2].GetComponent<AudioSource>().clip;
    //     SendSoundSelectsToTD();
    // }
    // public void sound1Select_3_Apply()
    // {
    //     dashBoardConvertedSounds[2].GetComponent<AudioSource>().clip = soundSamples_3[PlayerDataManager.Instance.soundSelect_3].GetComponent<AudioSource>().clip;
    //     SendSoundSelectsToTD();
    // }
    // public void sound1Select_4_Apply()
    // {
    //     dashBoardConvertedSounds[3].GetComponent<AudioSource>().clip = soundSamples_4[PlayerDataManager.Instance.soundSelect_4].GetComponent<AudioSource>().clip;
    //     SendSoundSelectsToTD();
    // }
    // public void SendSoundSelectsToTD()
    // {
    //     if (TDUdpManager.Instance == null) return;

    //     TDUdpManager.Instance.SendSoundSelects(
    //         PlayerDataManager.Instance.soundSelect_1,
    //         PlayerDataManager.Instance.soundSelect_2,
    //         PlayerDataManager.Instance.soundSelect_3,
    //         PlayerDataManager.Instance.soundSelect_4
    //     );
    // }


    // public void sound1Select_1_1()
    // {
    //     PlayerDataManager.Instance.soundSelect_1 = 0;
    // }
    // public void sound1Select_1_2()
    // {
    //     PlayerDataManager.Instance.soundSelect_1 = 1;
    // }
    // public void sound1Select_1_3()
    // {
    //     PlayerDataManager.Instance.soundSelect_1 = 2;
    // }
    // public void sound1Select_1_4()
    // {
    //     PlayerDataManager.Instance.soundSelect_1 = 3;
    // }





    // public void sound1Select_2_1()
    // {
    //     PlayerDataManager.Instance.soundSelect_2 = 0;
    // }
    // public void sound1Select_2_2()
    // {
    //     PlayerDataManager.Instance.soundSelect_2 = 1;
    // }
    // public void sound1Select_2_3()
    // {
    //     PlayerDataManager.Instance.soundSelect_2 = 2;
    // }
    // public void sound1Select_2_4()
    // {
    //     PlayerDataManager.Instance.soundSelect_2 = 3;
    // }



    // public void sound1Select_3_1()
    // {
    //     PlayerDataManager.Instance.soundSelect_3 = 0;
    // }
    // public void sound1Select_3_2()
    // {
    //     PlayerDataManager.Instance.soundSelect_3 = 1;
    // }
    // public void sound1Select_3_3()
    // {
    //     PlayerDataManager.Instance.soundSelect_3 = 2;
    // }
    // public void sound1Select_3_4()
    // {
    //     PlayerDataManager.Instance.soundSelect_3 = 3;
    // }


    // public void sound1Select_4_1()
    // {
    //     PlayerDataManager.Instance.soundSelect_4 = 0;
    // }
    // public void sound1Select_4_2()
    // {
    //     PlayerDataManager.Instance.soundSelect_4 = 1;
    // }
    // public void sound1Select_4_3()
    // {
    //     PlayerDataManager.Instance.soundSelect_4 = 2;
    // }
    // public void sound1Select_4_4()
    // {
    //     PlayerDataManager.Instance.soundSelect_4 = 3;
    // }

    // public void playRaw_1()
    // {
    //     dashBoardRawSounds[0].SetActive(false);
    //     dashBoardRawSounds[0].SetActive(true);
    // }
    // public void playRaw_2()
    // {
    //     dashBoardRawSounds[1].SetActive(false);
    //     dashBoardRawSounds[1].SetActive(true);
    // }
    // public void playRaw_3()
    // {
    //     dashBoardRawSounds[2].SetActive(false);
    //     dashBoardRawSounds[2].SetActive(true);
    // }
    // public void playRaw_4()
    // {
    //     dashBoardRawSounds[3].SetActive(false);
    //     dashBoardRawSounds[3].SetActive(true);
    // }


    // public void playConverted_1()
    // {
    //     dashBoardConvertedSounds[0].SetActive(false);
    //     dashBoardConvertedSounds[0].SetActive(true);
    // }
    // public void playConverted_2()
    // {
    //     dashBoardConvertedSounds[1].SetActive(false);
    //     dashBoardConvertedSounds[1].SetActive(true);
    // }
    // public void playConverted_3()
    // {
    //     dashBoardConvertedSounds[2].SetActive(false);
    //     dashBoardConvertedSounds[2].SetActive(true);
    // }
    // public void playConverted_4()
    // {
    //     dashBoardConvertedSounds[3].SetActive(false);
    //     dashBoardConvertedSounds[3].SetActive(true);
    // }




}
