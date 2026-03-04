using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class TDOsc : MonoBehaviour
{
    public int listenPort = 9000;
    public Transform target;

    [Header("World Mapping")]
    public Vector2 worldXRange = new Vector2(-5f, 5f);
    public Vector2 worldZRange = new Vector2(-8f, 8f);
    public Vector2 worldYRange = new Vector2(0f, 2f);

    [Header("Smoothing")]
    public float lerpSpeed = 12f;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running = false;

    private readonly object dataLock = new object();

    private float xNorm = 0.5f;
    private float yNorm = 0.5f;
    private float zNorm = 0.0f;
    private int touchState = 0;
    private int flipState = 0;

    void Start()
    {
        if (target == null) target = transform;

        udpClient = new UdpClient(listenPort);
        running = true;

        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
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

                // expected: x,y,z,touch,flip
                string[] parts = msg.Split(',');
                if (parts.Length < 5) continue;

                if (float.TryParse(parts[0], out float px) &&
                    float.TryParse(parts[1], out float py) &&
                    float.TryParse(parts[2], out float pz) &&
                    int.TryParse(parts[3], out int pt) &&
                    int.TryParse(parts[4], out int pf))
                {
                    lock (dataLock)
                    {
                        xNorm = Mathf.Clamp01(px);
                        yNorm = Mathf.Clamp01(py);
                        zNorm = Mathf.Clamp01(pz);
                        touchState = pt;
                        flipState = pf;
                    }
                }
            }
            catch (Exception)
            {
                // ignore temporary receive errors
            }
        }
    }

    void Update()
    {
        float x, y, z;
        int touch, flip;

        lock (dataLock)
        {
            x = xNorm;
            y = yNorm;
            z = zNorm;
            touch = touchState;
            flip = flipState;
        }

        float worldX = Mathf.Lerp(worldXRange.x, worldXRange.y, x);
        float worldZ = Mathf.Lerp(worldZRange.x, worldZRange.y, y);
        float worldY = Mathf.Lerp(worldYRange.x, worldYRange.y, z);

        Vector3 targetPos = new Vector3(worldX, worldY, worldZ);
        target.position = Vector3.Lerp(target.position, targetPos, Time.deltaTime * lerpSpeed);

        // 예시: flip 상태에 따라 뒤집기
        if (flip == 1)
        {
            target.rotation = Quaternion.Lerp(
                target.rotation,
                Quaternion.Euler(0f, 180f, 0f),
                Time.deltaTime * lerpSpeed
            );
        }
        else
        {
            target.rotation = Quaternion.Lerp(
                target.rotation,
                Quaternion.Euler(0f, 0f, 0f),
                Time.deltaTime * lerpSpeed
            );
        }
    }

    void OnApplicationQuit()
    {
        running = false;

        if (udpClient != null)
        {
            udpClient.Close();
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(100);
        }
    }
}