// #include <WiFi.h>
// #include <WiFiUdp.h>

// #include <Wire.h>
// #include <Adafruit_MPU6050.h>
// #include <Adafruit_Sensor.h>

// #include "AudioTools.h"
// #include "BluetoothA2DPSink.h"

// // =====================
// // USER CONFIG
// // =====================
// static const char* WIFI_SSID = "TMOBILE-52BE";
// static const char* WIFI_PASS = "b7znh53sdft";

// // Unity가 실행 중인 맥북 IP
// static IPAddress UNITY_IP(192, 168, 12, 123);
// static const uint16_t UNITY_PORT = 9002;

// // =====================
// // A2DP (BT Speaker) + MAX98357A I2S pins
// // =====================
// static const int I2S_BCLK = 26;
// static const int I2S_LRCK = 25;
// static const int I2S_DOUT = 27;

// I2SStream i2s;
// BluetoothA2DPSink a2dp_sink(i2s);
// static const char* BLSpeakerName = "Stethoscope-Speaker";

// // MAX98357A SD/EN pin (popping fix)
// static const int AMP_EN = 14;  // <-- 요청대로 14

// // =====================
// // MPU6050 (I2C) pins
// // =====================
// static const int I2C_SDA = 32;
// static const int I2C_SCL = 33;

// Adafruit_MPU6050 mpu;

// enum FaceState {
//   face_neutral = 0,
//   face_up      = 1,
//   face_down    = -1
// };

// static FaceState currentFace = face_neutral;
// static const char useAxis = 'Z';
// static const float facingThreshold = 7.0f;

// // Face send gating (no duplicates)
// static int lastSentFace = 999;
// static FaceState rawFace = face_neutral;
// static uint32_t lastFaceChangeMs = 0;
// static const uint32_t FACE_STABLE_MS = 200;  // 상태가 이 시간 이상 유지되면 확정

// // =====================
// // Joystick pins
// // =====================
// static const int JOY_X_PIN = 34;
// static const int JOY_Y_PIN = 35;
// static const int JOY_SW_PIN = 23;   // 내부 풀업 가능

// // Joystick calibration / invert
// static int joyCx = 2048;
// static int joyCy = 2048;
// static bool joyCalibrated = false;

// // 방향이 안 잡힐 때 이 두 값을 바꾸면 됨
// static bool invertX = false;
// static bool invertY = true;   // <-- UP/RIGHT가 안 잡히는 케이스에서 매우 자주 해결됨

// static const int JOY_RELEASE = 350;   // 센터 복귀
// static const int JOY_TRIGGER2 = 700;  // 방향 트리거

// static uint32_t lastTypeSentMs = 0;
// static const uint32_t TYPE_COOLDOWN_MS = 200;

// enum JoyDir { DIR_NONE=-1, DIR_UP=0, DIR_RIGHT=1, DIR_DOWN=2, DIR_LEFT=3 };
// static JoyDir joyLatchedDir = DIR_NONE;
// static bool buttonLatched = false;

// // =====================
// // UDP
// // =====================
// WiFiUDP udp;

// // 버튼/타입은 이벤트성이라 2~3회 반복이 유리하지만,
// // A2DP 안정성 위해 일단 1회로 두는 걸 추천. 필요하면 2~3으로 올려도 됨.
// static const int EVENT_REPEATS = 1;
// static const uint16_t REPEAT_GAP_MS = 25;

// // =====================
// // Helpers
// // =====================
// FaceState detectFace(float ax, float ay, float az) {
//   float v = 0.0f;
//   switch (useAxis) {
//     case 'X': v = ax; break;
//     case 'Y': v = ay; break;
//     case 'Z': v = az; break;
//     default:  v = az; break;
//   }
//   if (v > facingThreshold)  return face_up;
//   if (v < -facingThreshold) return face_down;
//   return face_neutral;
// }

// JoyDir detectJoyDir(int x, int y) {
//   int dx = x - joyCx;
//   int dy = y - joyCy;

//   if (invertX) dx = -dx;
//   if (invertY) dy = -dy;

//   // 센터 복귀
//   if (abs(dx) < JOY_RELEASE && abs(dy) < JOY_RELEASE) return DIR_NONE;

//   bool xTrig = abs(dx) >= JOY_TRIGGER2;
//   bool yTrig = abs(dy) >= JOY_TRIGGER2;

//   // 둘 다 트리거면 더 큰 쪽(대각선 처리)
//   if (xTrig && yTrig) {
//     if (abs(dx) >= abs(dy)) {
//       return (dx > 0) ? DIR_RIGHT : DIR_LEFT;
//     } else {
//       return (dy > 0) ? DIR_UP : DIR_DOWN;
//     }
//   }

//   if (xTrig) return (dx > 0) ? DIR_RIGHT : DIR_LEFT;
//   if (yTrig) return (dy > 0) ? DIR_UP : DIR_DOWN;

//   return DIR_NONE;
// }

// // "KEY:val\n" 한 줄을 한 UDP 패킷으로 전송
// void sendLineOnce(const char* key, int value) {
//   if (WiFi.status() != WL_CONNECTED) return;

//   char buf[32];
//   int n = snprintf(buf, sizeof(buf), "%s:%d\n", key, value);

//   udp.beginPacket(UNITY_IP, UNITY_PORT);
//   udp.write((const uint8_t*)buf, n);
//   udp.endPacket();

//   // Serial.print("UDP> ");
//   // Serial.print(buf);
// }

// // 이벤트성(TYPE/BUTTON)은 반복 송신 옵션
// void sendLineEvent(const char* key, int value) {
//   for (int i = 0; i < EVENT_REPEATS; i++) {
//     sendLineOnce(key, value);
//     if (EVENT_REPEATS > 1) delay(REPEAT_GAP_MS);
//   }
// }

// // =====================
// // Setup blocks
// // =====================
// void setupAmp() {
//   pinMode(AMP_EN, OUTPUT);
//   digitalWrite(AMP_EN, LOW);  // mute first
// }

// void ampOn()  { digitalWrite(AMP_EN, HIGH); }
// void ampOff() { digitalWrite(AMP_EN, LOW);  }

// void setupA2DP() {
//   auto cfg = i2s.defaultConfig();
//   cfg.sample_rate = 44100;
//   cfg.bits_per_sample = 16;
//   cfg.channels = 2;
//   cfg.pin_bck  = I2S_BCLK;
//   cfg.pin_ws   = I2S_LRCK;
//   cfg.pin_data = I2S_DOUT;
//   cfg.pin_data_rx = -1;

//   i2s.begin(cfg);
//   a2dp_sink.start(BLSpeakerName);
// }

// void setupWiFi() {
//   WiFi.mode(WIFI_STA);
//   WiFi.begin(WIFI_SSID, WIFI_PASS);
//   Serial.println("WiFi begin()");
// }

// void setupMPU() {
//   Wire.begin(I2C_SDA, I2C_SCL);

//   if (!mpu.begin()) {
//     Serial.println("ERROR: None MPU6050");
//     while (1) delay(50);
//   }

//   mpu.setAccelerometerRange(MPU6050_RANGE_2_G);
//   mpu.setGyroRange(MPU6050_RANGE_250_DEG);
//   mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);

//   delay(200);
//   Serial.println("MPU6050 ready");
// }

// void setupJoystick() {
//   pinMode(JOY_SW_PIN, INPUT_PULLUP);
// }

// void calibrateJoystickCenter() {
//   const uint32_t t0 = millis();
//   long sx = 0, sy = 0;
//   int n = 0;

//   while (millis() - t0 < 300) {
//     sx += analogRead(JOY_X_PIN);
//     sy += analogRead(JOY_Y_PIN);
//     n++;
//     delay(5);
//   }

//   joyCx = (int)(sx / n);
//   joyCy = (int)(sy / n);
//   joyCalibrated = true;

//   Serial.print("JOY center calibrated: ");
//   Serial.print(joyCx);
//   Serial.print(", ");
//   Serial.println(joyCy);
// }

// // =====================
// // Main
// // =====================
// static uint32_t lastMPUMs = 0;
// static uint32_t lastJoyMs = 0;
// static bool wifiPrinted = false;

// void setup() {
//   Serial.begin(115200);
//   delay(200);

//   Serial.println("BOOT");

//   setupAmp();          // mute 먼저
//   setupA2DP();
//   Serial.println("A2DP started");

//   // A2DP 시작 직후 팝 방지: 잠깐 뒤에 앰프 ON
//   delay(800);
//   ampOn();

//   setupWiFi();

//   setupMPU();
//   setupJoystick();
//   calibrateJoystickCenter();

//   Serial.println("READY: A2DP + WiFi(UDP) + MPU + Joystick");
// }

// void loop() {
//   // Wi-Fi 연결 상태 출력(1회) + sleep off
//   if (!wifiPrinted && WiFi.status() == WL_CONNECTED) {
//     wifiPrinted = true;
//     // WiFi.setSleep(false);
//     Serial.print("WiFi connected. IP=");
//     Serial.println(WiFi.localIP());
//   }

//   uint32_t now = millis();


//   if (now - lastMPUMs >= 200) {
//     lastMPUMs = now;

//     sensors_event_t a, g, temp;
//     mpu.getEvent(&a, &g, &temp);

//     FaceState newFace = detectFace(a.acceleration.x, a.acceleration.y, a.acceleration.z);

//     // 안정화: 변화 감지 시점 업데이트
//     if (newFace != rawFace) {
//       rawFace = newFace;
//       lastFaceChangeMs = millis();
//     }

//     // 안정화 시간 이후 확정
//     if (millis() - lastFaceChangeMs >= FACE_STABLE_MS) {
//       if (rawFace != currentFace) {
//         currentFace = rawFace;

//         // 0은 보내지 않음, ±1만 보내기
//         if (currentFace == face_up || currentFace == face_down) {
//           // 같은 값은 절대 재전송 금지
//           if ((int)currentFace != lastSentFace) {
//             lastSentFace = (int)currentFace;
//             sendLineOnce("FACE", lastSentFace);
//           }
//         }
//       }
//     }
//   }

//   if (now - lastJoyMs >= 100) {
//     lastJoyMs = now;

//     int x = analogRead(JOY_X_PIN);
//     int y = analogRead(JOY_Y_PIN);

//     JoyDir dir = detectJoyDir(x, y);

//     // 방향 latch: 센터로 복귀해야 다시 트리거
//     if (dir == DIR_NONE) {
//       joyLatchedDir = DIR_NONE;
//     } else if (joyLatchedDir == DIR_NONE) {
//       joyLatchedDir = dir;

//       uint32_t nowMs = millis();
//       if (nowMs - lastTypeSentMs >= TYPE_COOLDOWN_MS) {
//         lastTypeSentMs = nowMs;
//         sendLineEvent("TYPE", (int)dir);
//       }
//     }

//     // 버튼: 눌림(LOW) 최초 1회만
//     bool pressed = (digitalRead(JOY_SW_PIN) == LOW);
//     if (!pressed) {
//       buttonLatched = false;
//     } else if (!buttonLatched) {
//       buttonLatched = true;
//       sendLineEvent("BUTTON", 0);
//     }

//   }

//   // BT/Wi-Fi task 양보
//   delay(3);

  
// }


#include <WiFi.h>
#include <WiFiUdp.h>

#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

#include "AudioTools.h"
#include "BluetoothA2DPSink.h"

// =====================
// USER CONFIG
// =====================
static const char* WIFI_SSID = "TMOBILE-52BE";
static const char* WIFI_PASS = "b7znh53sdft";

static IPAddress UNITY_IP(192, 168, 12, 123);
static const uint16_t UNITY_PORT = 9002;

// =====================
// A2DP (BT Speaker) + MAX98357A I2S pins
// =====================
static const int I2S_BCLK = 26;
static const int I2S_LRCK = 25;
static const int I2S_DOUT = 27;

I2SStream i2s;
BluetoothA2DPSink a2dp_sink(i2s);
static const char* BLSpeakerName = "Stethoscope-Speaker";

// MAX98357A SD/EN pin
static const int AMP_EN = 14;

// =====================
// MPU6050 (I2C) pins
// =====================
static const int I2C_SDA = 32;
static const int I2C_SCL = 33;

Adafruit_MPU6050 mpu;

enum FaceState {
  face_neutral = 0,
  face_up      = 1,
  face_down    = -1
};

static FaceState currentFace = face_neutral;
static FaceState rawFace = face_neutral;
static int lastSentFace = 999;

static const char useAxis = 'Z';
static const float facingThreshold = 7.0f;
static uint32_t lastFaceChangeMs = 0;
static const uint32_t FACE_STABLE_MS = 200;

// =====================
// Joystick pins
// =====================
static const int JOY_X_PIN = 34;
static const int JOY_Y_PIN = 35;
static const int JOY_SW_PIN = 23;

static int joyCx = 2048;
static int joyCy = 2048;
static bool invertX = false;
static bool invertY = true;

static const int JOY_RELEASE = 350;
static const int JOY_TRIGGER2 = 700;

enum JoyDir { DIR_NONE=-1, DIR_UP=0, DIR_RIGHT=1, DIR_DOWN=2, DIR_LEFT=3 };
static JoyDir joyLatchedDir = DIR_NONE;
static bool buttonLatched = false;

static uint32_t lastTypeSentMs = 0;
static const uint32_t TYPE_COOLDOWN_MS = 200;

// =====================
// UDP + WiFi
// =====================
WiFiUDP udp;
static bool wifiPrinted = false;

// =====================
// Task handle
// =====================
TaskHandle_t sensorTaskHandle = nullptr;

// =====================
// Helpers
// =====================
void setupAmp() {
  pinMode(AMP_EN, OUTPUT);
  digitalWrite(AMP_EN, LOW);  // mute first
}
static inline void ampOn()  { digitalWrite(AMP_EN, HIGH); }
static inline void ampOff() { digitalWrite(AMP_EN, LOW);  }

void setupA2DP() {
  auto cfg = i2s.defaultConfig();
  cfg.sample_rate = 44100;
  cfg.bits_per_sample = 16;
  cfg.channels = 2;
  cfg.pin_bck  = I2S_BCLK;
  cfg.pin_ws   = I2S_LRCK;
  cfg.pin_data = I2S_DOUT;
  cfg.pin_data_rx = -1;

  i2s.begin(cfg);
  a2dp_sink.start(BLSpeakerName);
}

void setupWiFi() {
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  // 연결 후 1회 처리: WiFi.setSleep(false) 는 연결 확인 후에 호출
}

void setupMPU() {
  Wire.begin(I2C_SDA, I2C_SCL);

  if (!mpu.begin()) {
    Serial.println("ERROR: None MPU6050");
    while (1) delay(50);
  }

  mpu.setAccelerometerRange(MPU6050_RANGE_2_G);
  mpu.setGyroRange(MPU6050_RANGE_250_DEG);
  mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);

  delay(100);
  Serial.println("MPU6050 ready");
}

void setupJoystick() {
  pinMode(JOY_SW_PIN, INPUT_PULLUP);
}

// center calibration
void calibrateJoystickCenter() {
  long sx = 0, sy = 0;
  int n = 0;
  uint32_t t0 = millis();
  while (millis() - t0 < 300) {
    sx += analogRead(JOY_X_PIN);
    sy += analogRead(JOY_Y_PIN);
    n++;
    delay(5);
  }
  joyCx = (int)(sx / n);
  joyCy = (int)(sy / n);

  Serial.print("JOY center calibrated: ");
  Serial.print(joyCx);
  Serial.print(", ");
  Serial.println(joyCy);
}

FaceState detectFace(float ax, float ay, float az) {
  float v = 0.0f;
  switch (useAxis) {
    case 'X': v = ax; break;
    case 'Y': v = ay; break;
    case 'Z': v = az; break;
    default:  v = az; break;
  }
  if (v > facingThreshold)  return face_up;
  if (v < -facingThreshold) return face_down;
  return face_neutral;
}

JoyDir detectJoyDir(int x, int y) {
  int dx = x - joyCx;
  int dy = y - joyCy;

  if (invertX) dx = -dx;
  if (invertY) dy = -dy;

  if (abs(dx) < JOY_RELEASE && abs(dy) < JOY_RELEASE) return DIR_NONE;

  bool xTrig = abs(dx) >= JOY_TRIGGER2;
  bool yTrig = abs(dy) >= JOY_TRIGGER2;

  // 대각선이면 더 큰 축 선택
  if (xTrig && yTrig) {
    if (abs(dx) >= abs(dy)) return (dx > 0) ? DIR_RIGHT : DIR_LEFT;
    return (dy > 0) ? DIR_UP : DIR_DOWN;
  }

  if (xTrig) return (dx > 0) ? DIR_RIGHT : DIR_LEFT;
  if (yTrig) return (dy > 0) ? DIR_UP : DIR_DOWN;

  return DIR_NONE;
}

void sendLineOnce(const char* key, int value) {
  if (WiFi.status() != WL_CONNECTED) return;

  char buf[32];
  int n = snprintf(buf, sizeof(buf), "%s:%d\n", key, value);

  udp.beginPacket(UNITY_IP, UNITY_PORT);
  udp.write((const uint8_t*)buf, n);
  udp.endPacket();

  // Serial 출력은 A2DP 안정성 위해 최소화 권장
  // Serial.print("UDP> "); Serial.print(buf);
}

// =====================
// Sensor task (Core 1)
// =====================
void sensorTask(void* pv) {
  // 주기 관리
  const TickType_t mpuPeriod = pdMS_TO_TICKS(100);  // 10Hz
  const TickType_t joyPeriod = pdMS_TO_TICKS(50);   // 20Hz
  TickType_t lastMpuTick = xTaskGetTickCount();
  TickType_t lastJoyTick = xTaskGetTickCount();

  while (true) {
    // ---- MPU (10Hz) ----
    if (xTaskGetTickCount() - lastMpuTick >= mpuPeriod) {
      lastMpuTick += mpuPeriod;

      sensors_event_t a, g, temp;
      mpu.getEvent(&a, &g, &temp);

      FaceState nf = detectFace(a.acceleration.x, a.acceleration.y, a.acceleration.z);

      if (nf != rawFace) {
        rawFace = nf;
        lastFaceChangeMs = millis();
      }

      if (millis() - lastFaceChangeMs >= FACE_STABLE_MS) {
        if (rawFace != currentFace) {
          currentFace = rawFace;

          // 0은 안 보냄, ±1만, 같은 값 재전송 금지
          if (currentFace == face_up || currentFace == face_down) {
            int v = (int)currentFace;
            if (v != lastSentFace) {
              lastSentFace = v;
              sendLineOnce("FACE", v);
            }
          }
        }
      }
    }

    // ---- Joystick (20Hz) ----
    if (xTaskGetTickCount() - lastJoyTick >= joyPeriod) {
      lastJoyTick += joyPeriod;

      int x = analogRead(JOY_X_PIN);
      int y = analogRead(JOY_Y_PIN);
      JoyDir dir = detectJoyDir(x, y);

      if (dir == DIR_NONE) {
        joyLatchedDir = DIR_NONE;
      } else if (joyLatchedDir == DIR_NONE) {
        joyLatchedDir = dir;

        uint32_t nowMs = millis();
        if (nowMs - lastTypeSentMs >= TYPE_COOLDOWN_MS) {
          lastTypeSentMs = nowMs;
          sendLineOnce("TYPE", (int)dir);
        }
      }

      bool pressed = (digitalRead(JOY_SW_PIN) == LOW);
      if (!pressed) {
        buttonLatched = false;
      } else if (!buttonLatched) {
        buttonLatched = true;
        sendLineOnce("BUTTON", 0);
      }
    }

    // Core 1에서도 반드시 양보
    vTaskDelay(pdMS_TO_TICKS(2));
  }
}

// =====================
// Arduino entry
// =====================
void setup() {
  Serial.begin(115200);
  delay(200);
  Serial.println("BOOT");

  // ADC 안정화
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);

  // A2DP + amp mute/unmute
  setupAmp();
  setupA2DP();
  Serial.println("A2DP started");
  delay(800);
  ampOn();

  // WiFi
  setupWiFi();

  // Sensors
  setupMPU();
  setupJoystick();
  calibrateJoystickCenter();

  Serial.println("READY: A2DP + WiFi(UDP) + MPU + Joystick (Task on Core1)");
  
  // Create sensor task pinned to Core 1
  xTaskCreatePinnedToCore(
    sensorTask,
    "sensorTask",
    4096,          // stack
    nullptr,
    1,             // priority (low)
    &sensorTaskHandle,
    1              // core 1
  );
}

void loop() {
  // Wi-Fi 연결 로그/튜닝은 메인 loop에서 가볍게 처리
  if (!wifiPrinted && WiFi.status() == WL_CONNECTED) {
    wifiPrinted = true;
    // WiFi.setSleep(false);  // 안정성
    Serial.print("WiFi connected. IP=");
    Serial.println(WiFi.localIP());
  }

  // loop는 최대한 비우고 BT/WiFi에 양보
  delay(10);
}