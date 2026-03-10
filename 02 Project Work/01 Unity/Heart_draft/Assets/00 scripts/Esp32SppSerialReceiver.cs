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

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        string portFound = portName;
        if (string.IsNullOrEmpty(portFound))
        {
            portFound = AutoPickPort();
            Debug.Log($"SPP auto pick {portFound}");
        }

        if (string.IsNullOrEmpty(portFound))
        {
            Debug.Log("None detected");
            return;
        }

        sppPort = new SerialPort(portFound, baudRate)
        {
            NewLine = "\n",
            ReadTimeout = 500,
            DtrEnable = false,
            RtsEnable = false
        };

        try
        {
            sppPort.Open();
            Debug.Log($"SPP opend: {portFound}");
        }
        catch (Exception e)
        {
            Debug.Log($"SPP failed: {portFound}, {e}");
            return;
        }

        sppRunning = true;
        sppThread = new Thread(ReadLoop) { IsBackground = true };
        sppThread.Start();
    }

    void OnDestroy()
    {
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

    private void ReadLoop()
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
            }
        }
    }

    void Update()
    {
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


            // Here for the function trigger
            if (key == "FACE" && int.TryParse(valStr, out int fv))
            {
                face = fv;
                PlayerStateManager.Instance.UpdateFaceOrientation(face);
                Debug.Log("Face updated: " + face);
                // OnFace?.Invoke(fv);
            }
            else if (key == "TYPE" && int.TryParse(valStr, out int tv))
            {
                type = tv;
                Debug.Log("Type updated: " + type);
                // OnType?.Invoke(tv);
            }
            else if (key == "BUTTON")
            {
                if(PlayerDataManager.Instance.diagnosisCount < PlayerDataManager.Instance.dignosisPoints.Length)
                {
                    Debug.Log("Button pressed");
                    PlayerDataManager.Instance.addDiagnosisPoint();
                }

                
                // OnButton?.Invoke();
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