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

    public TextMeshProUGUI buttonCounterText;
    private int buttonCounter = 0;

    public bool isInIntro = true;
    public AudioListener audioListener;


    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
         Instance = this;



        isPlayerOnPlane = (this.transform.position.y < playerHeightTrigger);
        isPlayerOnPlane_saved = isPlayerOnPlane;
        activate = true;
    }

    void Update()
    {
        isPlayerOnPlane = (this.transform.position.y < playerHeightTrigger);

        if (activate && isPlayerOnPlane != isPlayerOnPlane_saved)
        {
            if (isInIntro)
            {
                audioListener.enabled = isPlayerOnPlane;
                isPlayerOnPlane_saved = isPlayerOnPlane;
            }
            else
            {
                TDUdpManager.Instance.SendPlayerPlaneState(isPlayerOnPlane);

                Debug.Log("isPlayerOnPlane changed: " + isPlayerOnPlane);

                isPlayerOnPlane_saved = isPlayerOnPlane;
            }
        }

        setTypeUI();

    }

    void setTypeUI()
    {

        for (int i = 0; i < typeUI.Length; i++)
        {
            Color c = typeUI[i].color;

            if (serialReceiver.type == i)
            {
                c.a = 1f;
            }
            else
            {
                c.a = 0.1f;
            }

            typeUI[i].color = c;
        }
    }

    public void ButtonPressed()
    {
        // Debug.Log("Button Pressed");
        buttonCounter++;
        buttonCounterText.text = buttonCounter.ToString();


    }
    public void UpdateFaceOrientation(int faceState)
    {
        faceOrientText.text = faceState.ToString();
    }

}