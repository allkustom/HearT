using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;

public class TDUdpManager : MonoBehaviour
{
    public static TDUdpManager Instance { get; private set; }
    [Header("UDP TD -> Unity")]
    public int listenPort = 9000;

    public string sendAddress = "127.0.0.1";
    [Header("UDP Unity -> TD")]
    public int sendPort = 9001;

    public Transform targetObject;
    // public float planeWidth = 11.0f;
    // public float planeHeight = 17.0f;
    

    public Vector2 worldXRange = new Vector2(-5.5f, 5.5f);
    public Vector2 worldZRange = new Vector2(-8.5f, 8.5f);
    public Vector2 worldYRange = new Vector2(0f, 4f);

    public float lerpSpeed = 12f;

    public bool requireValid = true;
    public bool printReceivedMessage = false;

    public bool printSentMessage = false;
    public bool sendEmptyFrameMessage = false;
    public string emptyFrameMessage = "none";

    private UdpClient receiveClient;
    private UdpClient sendClient;
    private Thread receiveThread;
    private bool running = false;

    private readonly object dataLock = new object();

    private float xNorm = 0.5f;
    private float yNorm = 0.5f;
    private float zNorm = 0.0f;
    private int valid = 0;

    private IPEndPoint sendEndPoint;

    public Transform PlayerTransform => targetObject;

    private struct InteractionData
    {
        public int type;
        public float distance;

        public InteractionData(int type, float distance)
        {
            this.type = type;
            this.distance = distance;
        }
    }

    private readonly Dictionary<int, InteractionData> currentInteractions = new Dictionary<int, InteractionData>();
    private readonly object interactionLock = new object();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (targetObject == null)
            targetObject = this.transform;

        try
        {
            receiveClient = new UdpClient(listenPort);
            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            sendClient = new UdpClient();
            sendEndPoint = new IPEndPoint(IPAddress.Parse(sendAddress), sendPort);

            Debug.Log($"UDP listening on port {listenPort}");
            Debug.Log($"UDP sending to {sendAddress}:{sendPort}");
        }
        catch (Exception e)
        {
            Debug.LogError("UDP init failed: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, listenPort);

        while (running)
        {
            try
            {
                byte[] data = receiveClient.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data).Trim();

                if (printReceivedMessage)
                    Debug.Log("UDP TD -> Unity: " + msg);

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


    void LateUpdate()
    {
        SendAllInteractions();
        ClearInteractions();
    }

    public void ReportInteraction(int objectId, int type, float distance)
    {
        lock (interactionLock)
        {
            currentInteractions[objectId] = new InteractionData(type, distance);
        }
    }

    public void SendRawMessage(string message)
    {
        if (sendClient == null || sendEndPoint == null || string.IsNullOrEmpty(message))
            return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            sendClient.Send(data, data.Length, sendEndPoint);

            if (printSentMessage)
                Debug.Log("UDP Unity -> TD: " + message);
        }
        catch (Exception e)
        {
            Debug.LogWarning("UDP send error: " + e.Message);
        }
    }

    public void SendPlayerPlaneState(bool isOnPlane)
    {
        string msg = isOnPlane ? "switch,1" : "switch,0";
        SendRawMessage(msg);
    }

    private void SendAllInteractions()
    {
        StringBuilder sb = new StringBuilder();

        lock (interactionLock)
        {
            bool first = true;

            foreach (var kvp in currentInteractions)
            {
                InteractionData data = kvp.Value;

                if (!first)
                    sb.Append("|");

                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1:F4}",
                    data.type,
                    data.distance
                );

                first = false;
            }
        }

        if (sb.Length > 0)
        {
            SendRawMessage(sb.ToString());
        }
        else if (sendEmptyFrameMessage)
        {
            SendRawMessage(emptyFrameMessage);
        }
    }

    private void ClearInteractions()
    {
        lock (interactionLock)
        {
            currentInteractions.Clear();
        }
    }

    void OnApplicationQuit()
    {
        running = false;

        if (receiveClient != null)
        {
            receiveClient.Close();
            receiveClient = null;
        }

        if (sendClient != null)
        {
            sendClient.Close();
            sendClient = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(100);
        }
    }


}