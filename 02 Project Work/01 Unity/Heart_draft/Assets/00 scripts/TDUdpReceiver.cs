using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

public class TDUdpReceiver : MonoBehaviour
{
    [Header("UDP")]
    public int listenPort = 9000;

    [Header("Target")]
    public Transform targetObject;

    [Header("World Mapping")]
    public Vector2 worldXRange = new Vector2(-5f, 5f);
    public Vector2 worldZRange = new Vector2(-8f, 8f);
    public Vector2 worldYRange = new Vector2(0f, 2f);

    [Header("Smoothing")]
    public float lerpSpeed = 12f;

    [Header("Options")]
    public bool requireValid = true;
    public bool printReceivedMessage = false;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running = false;

    private readonly object dataLock = new object();

    private float xNorm = 0.5f;
    private float yNorm = 0.5f;
    private float zNorm = 0.0f;
    private int valid = 0;

    void Start()
    {
        if (targetObject == null)
            targetObject = this.transform;

        try
        {
            udpClient = new UdpClient(listenPort);
            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log($"UDP listening on port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to open UDP port: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, listenPort);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data).Trim();

                if (printReceivedMessage)
                    Debug.Log("UDP RECV: " + msg);

                // expected: x,y,z,valid
                string[] parts = msg.Split(',');
                if (parts.Length < 4)
                    continue;

                if (
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float px) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float py) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float pz) &&
                    int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pvalid)
                )
                {
                    lock (dataLock)
                    {
                        xNorm = Mathf.Clamp01(px);
                        yNorm = Mathf.Clamp01(py);
                        zNorm = Mathf.Clamp01(pz);
                        valid = pvalid;
                    }
                }
            }
            catch (SocketException)
            {
                // 소켓 닫힐 때 들어올 수 있음
            }
            catch (Exception e)
            {
                Debug.LogWarning("UDP receive error: " + e.Message);
            }
        }
    }

    void Update()
    {
        float x, y, z;
        int isValid;

        lock (dataLock)
        {
            x = xNorm;
            y = yNorm;
            z = zNorm;
            isValid = valid;
        }

        if (requireValid && isValid == 0)
            return;

        float worldX = Mathf.Lerp(worldXRange.x, worldXRange.y, x);
        float worldZ = Mathf.Lerp(worldZRange.x, worldZRange.y, y);
        float worldY = Mathf.Lerp(worldYRange.x, worldYRange.y, z);

        Vector3 targetPos = new Vector3(worldX, worldY, worldZ);

        targetObject.position = Vector3.Lerp(
            targetObject.position,
            targetPos,
            Time.deltaTime * lerpSpeed
        );
    }

    void OnApplicationQuit()
    {
        running = false;

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(100);
        }
    }
}