using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    public bool isPlayerOnPlane = false;

    [Header("Above thie height, turn off the audio in TD")]
    public float playerHeightTrigger = 0.5f;

    private bool isPlayerOnPlane_saved = false;
    private bool activate = false;

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
    }
}