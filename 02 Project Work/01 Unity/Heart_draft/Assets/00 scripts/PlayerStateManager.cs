using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    public Esp32SppSerialReceiver serialReceiver;
    public bool isPlayerOnPlane = false;

    [Header("Above thie height, turn off the audio in TD")]
    public float playerHeightTrigger = 0.5f;

    private bool isPlayerOnPlane_saved = false;
    private bool activate = false;

    public RawImage[] typeUI = new RawImage[4];

    public TextMeshProUGUI faceOrientText;
    private int prevFace = 999;
    private bool faceInitialized = false;

    public TextMeshProUGUI buttonCounterText;

    public bool isInIntro = true;
    private bool prevIsInIntro;
    private bool introInitialized = false;
    public AudioListener audioListener;


    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        prevIsInIntro = isInIntro;
        introInitialized = true;

        isPlayerOnPlane = (this.transform.position.y < playerHeightTrigger);
        isPlayerOnPlane_saved = isPlayerOnPlane;
        activate = true;
        // if (TDUdpManager.Instance != null)
        // {
        //     TDUdpManager.Instance.SendIntroState(isInIntro);
        // }
    }

    void Update()
    {
        isPlayerOnPlane = (this.transform.position.y < playerHeightTrigger);

        if (activate && isPlayerOnPlane != isPlayerOnPlane_saved)
        {
            // if (isInIntro)
            // {
            //     audioListener.enabled = isPlayerOnPlane;
            //     isPlayerOnPlane_saved = isPlayerOnPlane;
            // }
            // else
            // {
            //     TDUdpManager.Instance.SendPlayerPlaneState(isPlayerOnPlane);

            //     Debug.Log("isPlayerOnPlane changed: " + isPlayerOnPlane);

            //     isPlayerOnPlane_saved = isPlayerOnPlane;
            // }
                TDUdpManager.Instance.SendPlayerPlaneState(isPlayerOnPlane);

                Debug.Log("isPlayerOnPlane changed: " + isPlayerOnPlane);

                isPlayerOnPlane_saved = isPlayerOnPlane;
        }

        setTypeUI();
        // if (introInitialized && isInIntro != prevIsInIntro)
        // {
        //     if (TDUdpManager.Instance != null)
        //     {
        //         TDUdpManager.Instance.SendIntroState(isInIntro);

        //     }

        //     prevIsInIntro = isInIntro;
        // }

    }

    void setTypeUI()
    {

        for (int i = 0; i < typeUI.Length; i++)
        {
            // Color c = typeUI[i].color;

            // if (serialReceiver.type == i)
            // {
            //     c.a = 1f;
            // }
            // else
            // {
            //     c.a = 0.1f;
            // }

            // typeUI[i].color = c;

            if (serialReceiver.type == i)
            {
                typeUI[i].gameObject.SetActive(true);
            }
            else
            {
                typeUI[i].gameObject.SetActive(false);
            }

        }
    }


    public void UpdateFaceOrientation(int newFace)
    {

        if (!faceInitialized)
        {
            prevFace = newFace;
            faceInitialized = true;
            return;
        }

        if (newFace != prevFace)
        {
            if (TDUdpManager.Instance != null)
                TDUdpManager.Instance.SendFaceState(newFace);

            prevFace = newFace;
        }
    }
}