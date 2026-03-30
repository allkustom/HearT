using UnityEngine;

public class SymptomManager : MonoBehaviour
{
    [Range(0, 3)]
    public int type = 0;
    private Color[] typeColors = new Color[] { Color.blue, Color.green, Color.red, Color.gray, Color.magenta };

    [Range(0.0f, 5.0f)]
    public float range = 2f;
    private tutorialSoundExample tutorialExample;

    private void Awake()
    {
        tutorialExample = GetComponent<tutorialSoundExample>();
    }
    void Update()
    {

        Transform player = TDUdpManager.Instance.PlayerTransform;

        float distance = GetDistanceToPlayer(player);

        if (distance <= range)
        {
            TDUdpManager.Instance.ReportInteraction(
                gameObject.GetInstanceID(),
                type,
                distance
            );
        }
        if (tutorialExample != null)
        {
            tutorialExample.ApplyByDistance(distance, range);
        }
    }

    private float GetDistanceToPlayer(Transform player)
    {
        Vector2 a = new Vector2(transform.position.x, transform.position.z);
        Vector2 b = new Vector2(player.position.x, player.position.z);
        return Vector2.Distance(a, b);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = typeColors[type];
        Gizmos.DrawWireSphere(transform.position, range);
    }
}