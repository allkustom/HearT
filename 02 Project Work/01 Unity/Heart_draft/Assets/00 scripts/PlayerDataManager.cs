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
    }

    // Update is called once per frame
    void Update()
    {
    }
}
