using UnityEngine;

public class EnableClickToProceedUI : MonoBehaviour
{
    public GameObject overlayImage;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (TotalUIManager.Instance.enableClickProceed)
        {
            overlayImage.SetActive(false);
        }
        else
        {
            overlayImage.SetActive(true);
        }
    }
}
