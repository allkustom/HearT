using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;

public class serial : MonoBehaviour
{
    [Header("serial port, matches it with Arduino IDE port(turn off the serial monitor)")]
    public string portName ="/dev/cu.usbmodem1101";
    public int baudRate = 115200;

    [Header("Player object")]
    public Transform targetObject;

    public float rotationLerpSpeed = 2f;

    private int faceState = 0;

    private SerialPort serialPort;
    private Thread serialThread;
    private volatile bool isRunning = false;

    private readonly object dataLock = new object();

    void Start()
    {
        if (targetObject == null)
            targetObject = this.transform;

        OpenSerial();
    }

    void OpenSerial()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 50;
            serialPort.NewLine = "\n";
            serialPort.Open();

            isRunning = true;
            serialThread = new Thread(ReadSerialLoop);
            serialThread.Start();

            Debug.Log("Serial opened: " + portName);
        }
        catch (Exception e)
        {
            Debug.LogError("Serial port error: " + e.Message);
        }
    }

    void ReadSerialLoop()
    {
        while (isRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string line = serialPort.ReadLine().Trim();
                ParseLine(line);
                Debug.Log($"Data Arduino -> Unity: {line}");
            }
            catch (TimeoutException)
            {
            }
            catch (Exception e)
            {
                Debug.LogWarning("Serial read error: " + e.Message);
            }
        }
    }

    void ParseLine(string line)
    {
        if (!line.StartsWith("FACE:"))
            return;

        string payload = line.Substring(5);
        string[] parts = payload.Split(',');

        if (parts.Length < 1)
            return;

        if (int.TryParse(parts[0], out int parsedState))
        {
            lock (dataLock)
            {
                faceState = parsedState;
            }
        }
    }

    void Update()
    {
        int currentFace;

        lock (dataLock)
        {
            currentFace = faceState;
        }

        Quaternion targetRotation = targetObject.rotation;

        if (currentFace == 1)
        {
            // Facing top
            targetRotation = Quaternion.Euler(0f, 0f, 0f);
        }
        else if (currentFace == -1)
        {
            // Facing bottom
            targetRotation = Quaternion.Euler(0f, 0f, 180f);
        }
        else
        {
            
            return;
        }
        

        targetObject.rotation = Quaternion.Lerp(
            targetObject.rotation,
            targetRotation,
            Time.deltaTime * rotationLerpSpeed
        );
    }

    void OnApplicationQuit()
    {
        isRunning = false;

        if (serialThread != null && serialThread.IsAlive)
            serialThread.Join(200);

        if (serialPort != null && serialPort.IsOpen)
            serialPort.Close();
    }
}