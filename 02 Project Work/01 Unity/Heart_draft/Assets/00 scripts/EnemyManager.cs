using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Range(0, 3)]
    public int type = 0;
    private Color[] typeColors = new Color[] { Color.blue, Color.green, Color.red, Color.gray, Color.magenta };

    [Range(0.0f, 5.0f)]
    public float range = 2f;

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
    }

    private float GetDistanceToPlayer(Transform player)
    {
        return Vector3.Distance(transform.position, player.position);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = typeColors[type];
        Gizmos.DrawWireSphere(transform.position, range);
    }
}