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

    public RawImage[] modeUI = new RawImage[4];

    public TextMeshProUGUI faceOrientText;

    public TextMeshProUGUI buttonCounterText;
    private int buttonCounter = 0;

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }



        Instance = this;
        DontDestroyOnLoad(gameObject);

        isPlayerOnPlane = (this.transform.position.y < playerHeightTrigger);
        isPlayerOnPlane_saved = isPlayerOnPlane;
        activate = true;
    }

    void Update()
    {
        isPlayerOnPlane = (this.transform.position.y < playerHeightTrigger);

        if (activate && isPlayerOnPlane != isPlayerOnPlane_saved)
        {
            TDUdpManager.Instance.SendPlayerPlaneState(isPlayerOnPlane);

            Debug.Log("isPlayerOnPlane changed: " + isPlayerOnPlane);

            isPlayerOnPlane_saved = isPlayerOnPlane;
        }

        TypeModeUI();

    }

    void TypeModeUI()
    {
        for (int i = 0; i < modeUI.Length; i++)
        {
            if (serialReceiver.type== i)
            {
                modeUI[i].color = Color.white;  
            }
            else
            {
                modeUI[i].color = Color.gray;
            }
        }
    }

    public void ButtonPressed()
    {
        Debug.Log("Button Pressed");
        buttonCounter++;
        buttonCounterText.text = buttonCounter.ToString();
    }
    public void UpdateFaceOrientation(int faceState)
    {
        faceOrientText.text = faceState.ToString();
    }

}