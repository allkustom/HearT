using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR_OSX || UNITY_IOS
using UnityCoreBluetooth;

public class BLESerial : MonoBehaviour
{
    public Text text;

    // Target device + UUIDs (lowercase/uppercase 섞여도 비교를 case-insensitive로 처리)
    private const string TARGET_NAME = "ESP32Stethoscope";
    private const string SERVICE_UUID = "24c50306-c436-4966-bc4d-232c8b705701";
    private const string CHAR_NOTIFY_UUID = "c96c5759-8585-4cf8-b2d9-36b1f3993f0b";

    private CoreBluetoothManager manager;
    private CoreBluetoothCharacteristic notifyCharacteristic;

    private bool flag = false;
    private byte[] value = Array.Empty<byte>();

    // Demo motion (샘플 코드 유지)
    private float vy = 0.0f;

    // Parsed result you actually want
    public int FaceState { get; private set; } = 0; // -1,0,1

    void Start()
    {
        manager = CoreBluetoothManager.Shared;

        manager.OnUpdateState((string state) =>
        {
            Debug.Log("state: " + state);
            if (state != "poweredOn") return;
            manager.StartScan();
        });

        manager.OnDiscoverPeripheral((CoreBluetoothPeripheral peripheral) =>
        {
            // name이 null/empty일 수 있음
            var name = peripheral.name ?? "";
            if (name.Length > 0)
                Debug.Log("discover peripheral name: " + name);

            if (!string.Equals(name, TARGET_NAME, StringComparison.Ordinal)) return;

            Debug.Log("Target found. Stop scan and connect.");
            manager.StopScan();
            manager.ConnectToPeripheral(peripheral);
        });

        manager.OnConnectPeripheral((CoreBluetoothPeripheral peripheral) =>
        {
            Debug.Log("connected peripheral name: " + peripheral.name);
            peripheral.discoverServices();
        });

        manager.OnDiscoverService((CoreBluetoothService service) =>
        {
            Debug.Log("discover service uuid: " + service.uuid);

            // 일부 플러그인은 uuid를 대문자/축약으로 줄 수 있어서 case-insensitive 비교
            if (!string.Equals(service.uuid, SERVICE_UUID, StringComparison.OrdinalIgnoreCase)) return;

            Debug.Log("Target service matched. Discover characteristics.");
            service.discoverCharacteristics();
        });

        manager.OnDiscoverCharacteristic((CoreBluetoothCharacteristic characteristic) =>
        {
            string uuid = characteristic.Uuid;
            string[] props = characteristic.Propertis ?? Array.Empty<string>();

            Debug.Log($"discover characteristic uuid: {uuid}");

            // Notify characteristic만 잡기
            if (!string.Equals(uuid, CHAR_NOTIFY_UUID, StringComparison.OrdinalIgnoreCase))
                return;

            // notify 지원 여부 확인
            bool canNotify = false;
            for (int i = 0; i < props.Length; i++)
            {
                if (string.Equals(props[i], "notify", StringComparison.OrdinalIgnoreCase))
                    canNotify = true;
            }

            if (!canNotify)
            {
                Debug.LogWarning("Target characteristic found but it does not advertise notify property.");
                return;
            }

            notifyCharacteristic = characteristic;
            Debug.Log("Enabling notify on target characteristic...");
            notifyCharacteristic.SetNotifyValue(true);
        });

        manager.OnUpdateValue((CoreBluetoothCharacteristic characteristic, byte[] data) =>
        {
            // 우리가 구독한 char만 처리
            if (notifyCharacteristic == null) return;
            if (!string.Equals(characteristic.Uuid, CHAR_NOTIFY_UUID, StringComparison.OrdinalIgnoreCase)) return;

            value = data ?? Array.Empty<byte>();
            flag = true;
        });

        manager.Start();
    }

    void Update()
    {
        // 샘플 동작 유지
        if (this.transform.position.y < 0)
        {
            vy = 0.0f;
            transform.position = new Vector3(0, 0, 0);
        }
        else
        {
            vy -= 0.006f;
            transform.position += new Vector3(0, vy, 0);
        }
        this.transform.Rotate(2, -3, 4);

        if (!flag) return;
        flag = false;

        // bytes -> UTF8 string
        string s = Encoding.UTF8.GetString(value).Trim(); // "FACE:1" 같은 형태
        Debug.Log("Notify raw: " + s);

        // Parse FACE:<int>
        // 예: FACE:-1 / FACE:0 / FACE:1
        if (s.StartsWith("FACE:", StringComparison.OrdinalIgnoreCase))
        {
            string num = s.Substring(5).Trim();
            if (int.TryParse(num, out int face))
            {
                FaceState = face;
                text.text = $"FACE: {FaceState}";
                vy += 0.1f;
                transform.position += new Vector3(0, vy, 0);
                return;
            }
        }

        // 예상 포맷이 아니면 raw 출력
        text.text = $"Notify: {s}";
    }

    void OnDestroy()
    {
        manager.Stop();
    }

    // 지금은 ESP32로 Write 안 써도 됨 (나중에 필요하면 UUID 6e400002 같은 RX char 추가해서 사용)
    public void Write()
    {
        if (notifyCharacteristic == null) return;
        // notifyCharacteristic.Write(...) 는 RX characteristic에서 해야 하는 게 일반적이라 일단 비워둠
    }
}
#endif