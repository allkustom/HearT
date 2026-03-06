using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;
public class ESP32UDPManager : MonoBehaviour
{
    [Header("UDP")]
    public int port = 9002;
    public bool logRaw = true;

    [Header("Latest States")]
    public int faceState = 0;   // -1,0,1
    public int typeState = -1;  // 0..3 (or -1 none yet)
    public int buttonState = 0; // always 0, but use OnButton event

    public event Action<int> OnFace;
    public event Action<int> OnType;
    public event Action OnButton;

    private UdpClient _udp;
    private Thread _thread;
    private volatile bool _running;

    public TextMeshProUGUI faceText;
    public TextMeshProUGUI typeText;
    public TextMeshProUGUI buttonText;

    void Start()
    {
        _running = true;
        _udp = new UdpClient(port);
        _udp.Client.ReceiveTimeout = 1000; // so thread can exit cleanly
        _thread = new Thread(ReceiveLoop) { IsBackground = true };
        _thread.Start();
        Debug.Log($"UDP ESP32 -> Unity 0.0.0.0:{port}");
    }

    void OnDestroy()
    {
        _running = false;
        try { _udp?.Close(); } catch { }
        try { _thread?.Join(300); } catch { }
    }

    private void ReceiveLoop()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);

        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref ep);
                if (data == null || data.Length == 0) continue;

                string s = Encoding.UTF8.GetString(data).Trim(); // e.g. "FACE:1"
                if (logRaw) Debug.Log($"[UDP] RX {ep.Address}:{ep.Port} -> {s}");

                // Parse KEY:VALUE
                int colon = s.IndexOf(':');
                if (colon <= 0) continue;

                string key = s.Substring(0, colon).Trim().ToUpperInvariant();
                string valStr = s.Substring(colon + 1).Trim();

                if (key == "FACE")
                {
                    if (int.TryParse(valStr, out int v))
                    {
                        faceState = v;
                        // Main-thread safe invoke: queue to Unity thread
                        UnityMainThread(() => OnFace?.Invoke(v));
                        
                    }
                }
                else if (key == "TYPE")
                {
                    if (int.TryParse(valStr, out int v))
                    {
                        typeState = v;
                        UnityMainThread(() => OnType?.Invoke(v));
                    }
                }
                else if (key == "BUTTON")
                {
                    // value is always 0, but we treat as event
                    buttonState = 0;
                    UnityMainThread(() => OnButton?.Invoke());
                }
            }
            catch (SocketException)
            {
                // timeout or close
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogError("[UDP] ReceiveLoop error: " + e);
            }
        }
    }

    // Minimal main-thread dispatcher (no external package)
    private static readonly System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadQueue
        = new System.Collections.Concurrent.ConcurrentQueue<Action>();

    private static void UnityMainThread(Action a)
    {
        if (a != null) _mainThreadQueue.Enqueue(a);
    }

    void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var a))
        {
            a?.Invoke();
        }
        faceText.text = faceState.ToString();
        typeText.text = typeState.ToString();
        buttonText.text = buttonState == 0 ? "Idle" : "Pressed";
    }
        
}