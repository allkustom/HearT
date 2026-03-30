using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class Esp32SppSerialReceiver : MonoBehaviour
{

    public static Esp32SppSerialReceiver Instance { get; private set; }

    // use ls /dev/cu.* to check the port
    public string portName = "";

    // SPP requires baudRate to assign fake port
    public int baudRate = 115200;

    public bool receiveDebug = true;

    public int face = 0;
    public int type = 0;
    // public bool button = false;

    // public event Action<int> OnFace;
    // public event Action<int> OnType;
    // public event Action OnButton;

    private SerialPort sppPort;
    private Thread sppThread;
    private volatile bool sppRunning;

    private readonly ConcurrentQueue<string> sppQueue = new ConcurrentQueue<string>();

    private Coroutine reconnectCoroutine;
    private volatile bool reconnectRequested = false;
    private volatile bool connected = false;

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        TryConnectOnce();

        if (!connected)
        {
            StartReconnectLoop();
        }
    }

    void OnDestroy()
    {
        StopReconnectLoop();

        sppRunning = false;
        try
        {
            sppThread?.Join(300);
        }
        catch
        {

        }
        try
        {
            if (sppPort != null && sppPort.IsOpen)
            {
                sppPort.Close();
            }
        }
        catch { }
    }

    private void StartReconnectLoop()
    {
        reconnectRequested = true;
        if (reconnectCoroutine == null)
        {
            reconnectCoroutine = StartCoroutine(ReconnectLoop());
        }
    }

    private void StopReconnectLoop()
    {
        reconnectRequested = false;
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
    }

    private System.Collections.IEnumerator ReconnectLoop()
    {
        while (reconnectRequested)
        {
            if (!connected)
            {
                TryConnectOnce();
            }
            yield return new WaitForSeconds(1.0f);
        }
    }

    private void TryConnectOnce()
    {
        string portFound = portName;
        if (string.IsNullOrEmpty(portFound))
        {
            portFound = AutoPickPort();
            Debug.Log($"SPP auto pick {portFound}");
        }

        if (string.IsNullOrEmpty(portFound))
        {
            Debug.Log("None detected");
            connected = false;
            return;
        }

        try
        {
            if (sppPort != null)
            {
                try
                {
                    if (sppPort.IsOpen) sppPort.Close();
                }
                catch { }
                sppPort = null;
            }

            sppPort = new SerialPort(portFound, baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 500,
                DtrEnable = false,
                RtsEnable = false
            };

            sppPort.Open();
            Debug.Log($"SPP opend: {portFound}");

            sppRunning = true;
            sppThread = new Thread(ReadLoop) { IsBackground = true };
            sppThread.Start();

            connected = true;
        }
        catch (Exception e)
        {
            Debug.Log($"SPP failed: {portFound}, {e}");
            connected = false;
        }
    }

    private void ReadLoop()
    {
        try
        {
            while (sppRunning && sppPort != null && sppPort.IsOpen)
            {
                try
                {
                    string line = sppPort.ReadLine();
                    if (string.IsNullOrEmpty(line)) continue;
                    sppQueue.Enqueue(line.Trim());
                }
                catch (TimeoutException)
                {

                }
                catch (Exception e)
                {
                    Debug.Log($"SPP Read error: {e}");
                    Thread.Sleep(200);
                    break;
                }
            }
        }
        finally
        {
            connected = false;
            sppRunning = false;
        }
    }

    void Update()
    {
        if (!connected && reconnectCoroutine == null)
        {
            StartReconnectLoop();
        }

        while (sppQueue.TryDequeue(out string s))
        {
            if (receiveDebug)
            {
                Debug.Log($"SPP received: {s}");
            }

            int colon = s.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            string key = s.Substring(0, colon).Trim().ToUpperInvariant();
            string valStr = s.Substring(colon + 1).Trim();


            // for the function trigger
            if (key == "FACE" && int.TryParse(valStr, out int fv))
            {
                face = fv;
                PlayerStateManager.Instance.UpdateFaceOrientation(face);
                Debug.Log("Face updated: " + face);
                // OnFace?.Invoke(fv);
            }
            // else if (key == "TYPE" && int.TryParse(valStr, out int tv))
            // {
            //     type = tv;
            //     Debug.Log("Type updated: " + type);
            //     // OnType?.Invoke(tv);
            // }
            else if (key == "TYPE" && int.TryParse(valStr, out int tv))
            {
                // Left side
                if (tv == 3)
                {
                    type -= 1;
                    if (type < 0) type = 3;
                    Debug.Log("Type moved LEFT -> " + type);
                }
                // Right side
                else if (tv == 1)
                {
                    type += 1;
                    if (type > 3) type = 0;
                    Debug.Log("Type moved RIGHT -> " + type);
                }
            }
            else if (key == "BUTTON")
            {
                if (TotalUIManager.Instance.pageNum == 1)
                {
                    TotalUIManager.Instance.NextPage();

                }

                if (PlayerStateManager.Instance.isPlayerOnPlane)
                {
                    if (PlayerStateManager.Instance.isInIntro)
                    {
                        if (TotalUIManager.Instance.enableClickProceed)
                        {
                            TotalUIManager.Instance.NextPage();
                        }
                        else
                        {
                            if (TotalUIManager.Instance.isTutorialInteract)
                            {
                                bool triggered = tutorialSoundExample.TriggerMode0ByButton();

                                if (triggered)
                                {
                                    Debug.Log("Tutorial interaction triggered by button.");
                                }
                                else
                                {
                                    Debug.Log("No mode 0 tutorial object in 20% range.");
                                }
                            }
                        }
                    }
                    else
                    {
                        PlayerDataManager.Instance.addDiagnosisPoint(type);
                    }
                }
            }
        }
    }

    private string AutoPickPort()
    {
        string[] ports = SerialPort.GetPortNames();

        foreach (var p in ports)
            if (p.StartsWith("/dev/cu.", StringComparison.OrdinalIgnoreCase) &&
                p.IndexOf("Stethoscope", StringComparison.OrdinalIgnoreCase) >= 0)
                return p;

        foreach (var p in ports)
            if (p.StartsWith("/dev/tty.", StringComparison.OrdinalIgnoreCase) &&
                p.IndexOf("Stethoscope", StringComparison.OrdinalIgnoreCase) >= 0)
                return p;

        return ports.Length > 0 ? ports[0] : "";
    }
}