using System.Collections;
using System.IO.Ports;
using UnityEngine;

public class PlotterSerialSender : MonoBehaviour
{
    public string portName = "/dev/cu.usbserial-10";
    public int baudRate = 115200;
    public float startupDelay = 2f;
    public float ackTimeout = 200f;

    [Header("Test")]
    public bool sendTestOnStart = true;

    private SerialPort serialPort;

    // private string testJob =
    //     "START\n" +
    //     "HOME2\n" +
    //     "MICRO\n" +

    //     "BOXTEXT 100 150 200 165 Minkyu P1\n" +
    //     "BOXTEXT 100 135 200 150 50/100 P2\n" +
    //     "HOME1\n" +
    //     "FREE\n";
    private string testJob =
"START\n" +
"HOME2\n" +
"MICRO\n" +
"CROSS 161.5 211.1 15 P1\n" +
"RECT 161.5 211.1 30 30 P1\n" +
"LINE 161.5 211.1 151.4 177.3 P1\n" +
"CROSS 151.4 177.3 15 P2\n" +
"RECT 151.4 177.3 30 30 P2\n" +
"CROSS 236.5 25.1 15 P1\n" +
"STAR 236.5 25.1 15 7.5 P1\n" +
"LINE 236.5 25.1 206.8 50.8 P1\n" +
"CROSS 206.8 50.8 15 P2\n" +
"TRI 206.8 50.8 30 P2\n" +
"CROSS 377.2 102.1 15 P1\n" +
"CIRCLE 377.2 102.1 15 P1\n" +
"LINE 377.2 102.1 399.0 43.7 P1\n" +
"CROSS 399.0 43.7 15 P2\n" +
"CIRCLE 399.0 43.7 15 P2\n" +
"CROSS 315.5 238.8 15 P1\n" +
"TRI 315.5 238.8 30 P1\n" +
"LINE 315.5 238.8 225.3 213.1 P1\n" +
"CROSS 225.3 213.1 15 P2\n" +
"TRI 225.3 213.1 30 P2\n" +
"BOXTEXT 25 30 110 45 NAME P2\n" +
"BOXTEXT 25 15 110 30 74/100 P2\n" +
"HOME1\n" +
"FREE\n";

    IEnumerator Start()
    {
        TryOpenPort();
        yield return new WaitForSeconds(startupDelay);

        if (sendTestOnStart)
        {
            SendPlotterJob(testJob);
        }
    }

    void OnDestroy()
    {
        ClosePort();
    }

    public void TryOpenPort()
    {
        try
        {
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Close();

            serialPort = new SerialPort(portName, baudRate);
            serialPort.NewLine = "\n";
            serialPort.ReadTimeout = 200;
            serialPort.WriteTimeout = 500;
            serialPort.DtrEnable = false;
            serialPort.RtsEnable = false;
            serialPort.Open();

            Debug.Log("Serial opened: " + portName);

            StartCoroutine(ClearStartupBuffer());
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to open serial port: " + e.Message);
        }
    }

    private IEnumerator ClearStartupBuffer()
    {
        yield return new WaitForSeconds(startupDelay);

        if (serialPort == null || !serialPort.IsOpen)
            yield break;

        try
        {
            serialPort.DiscardInBuffer();
        }
        catch
        {
        }
    }

    public void ClosePort()
    {
        try
        {
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Close();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to close serial port: " + e.Message);
        }
    }

    public void SendPlotterJob(string jobText)
    {
        StartCoroutine(SendPlotterJobWithAck(jobText));
    }

    private IEnumerator SendPlotterJobWithAck(string jobText)
    {
        if (serialPort == null || !serialPort.IsOpen)
        {
            Debug.LogWarning("Serial port is not open.");
            yield break;
        }

        string[] lines = jobText.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            try
            {
                serialPort.Write(line + "\n");
                serialPort.BaseStream.Flush();
                Debug.Log("Sent: " + line);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Send failed: " + line + " / " + e.Message);
                yield break;
            }

            float startTime = Time.time;
            bool gotAck = false;

            while (Time.time - startTime < ackTimeout)
            {
                string incoming = null;

                try
                {
                    incoming = serialPort.ReadLine().Trim();
                }
                catch (System.TimeoutException)
                {
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Read ack failed: " + e.Message);
                    yield break;
                }

                if (!string.IsNullOrEmpty(incoming))
                {
                    Debug.Log("Arduino: " + incoming);

                    if (incoming == "OK")
                    {
                        gotAck = true;
                        break;
                    }

                    if (incoming.StartsWith("ERR"))
                    {
                        Debug.LogError("Arduino error after line: " + line + " / " + incoming);
                        yield break;
                    }
                }

                yield return null;
            }

            if (!gotAck)
            {
                Debug.LogError("Ack timeout after line: " + line);
                yield break;
            }
        }

        Debug.Log("Plotter job complete.");
    }
}