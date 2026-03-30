using UnityEngine;

public class TypeUI : MonoBehaviour
{
    public Esp32SppSerialReceiver serialReceiver;


    public GameObject[] TypeUIs;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < TypeUIs.Length; i++)
        {
            if (serialReceiver.type < TypeUIs.Length)
            {

                if (serialReceiver.type == i)
                {
                    TypeUIs[i].gameObject.SetActive(true);
                }
                else
                {
                    TypeUIs[i].gameObject.SetActive(false);
                }

            }

        }

    }
}
