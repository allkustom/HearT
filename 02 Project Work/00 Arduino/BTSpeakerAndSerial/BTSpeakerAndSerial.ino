#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

#include "AudioTools.h"
#include "BluetoothA2DPSink.h"

#include <BluetoothSerial.h>

// Max98735A amp pin
static const int i2s_bclk = 26;
static const int i2s_lrck = 25;
static const int i2s_din = 27;
static const int i2s_en = 14;

I2SStream i2s;
// A2DP bluetooth speaker
BluetoothA2DPSink a2dp_sink(i2s);
// BL speaker name
static const char* BLSpeakerName = "Stethoscope-Speaker";



// BL SPP
BluetoothSerial SerialBT;
static const char* SPPName = "Stethoscope-Control";

// MPU6050 pin
static const int i2c_SDA = 32;
static const int i2c_SCL = 33;

Adafruit_MPU6050 mpu;

// Face orient state with MPU6050
enum FaceState {
  face_side = 0,
  face_up      = 1,
  face_down    = -1
};
static FaceState currentFace = face_side;
static FaceState rawFace = face_side;
static int lastSentFace = 999;

static const char useAxis = 'Z';
static const float facingThreshold = 7.0f;

static uint32_t lastFaceChangeMs = 0;
static const uint32_t faceSampleTime = 50;


// Joystick pin
static const int joystickPin_X = 34;
static const int joystickPin_Y = 35;
static const int joustickPin_SW = 23;

static int joyCx = 2048;
static int joyCy = 2048;


static bool invertX = false;
static bool invertY = true;

static const int joystickRelease  = 350;
static const int joystickTriggerThresh = 700;

enum JoyDir {
  dir_none=-1,
  dir_up=0,
  dir_right=1,
  dir_down=2,
  dir_left=3
};
static JoyDir joyLatchedDir = dir_none;
static bool buttonLatched = false;

static uint32_t lastTypeSentMs = 0;
static const uint32_t typeWaitTime = 200;

//---------------------------------------------------
//---------------------------------------------------
//---------------------------------------------------
// [Setup section]
void setupAmp() {
  pinMode(i2s_en, OUTPUT);
  digitalWrite(i2s_en, LOW);
}
static inline void ampOn(){
  digitalWrite(i2s_en, HIGH);
}
static inline void ampOff(){
  digitalWrite(i2s_en, LOW);
}

void setupA2DP() {
  auto cfg = i2s.defaultConfig();
  cfg.sample_rate = 44100;
  cfg.bits_per_sample = 16;
  cfg.channels = 2;
  cfg.pin_bck  = i2s_bclk;
  cfg.pin_ws   = i2s_lrck;
  cfg.pin_data = i2s_din;
  cfg.pin_data_rx = -1;

  i2s.begin(cfg);
  a2dp_sink.start(BLSpeakerName);
}

void setupSPP() {
  SerialBT.begin(SPPName);
  Serial.print("SPP started: ");
  Serial.println(SPPName);
}

void setupMPU() {
  Wire.begin(i2c_SDA, i2c_SCL);

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
  pinMode(joustickPin_SW, INPUT_PULLUP);
}

void calibrateJoystickCenter() {
  long sx = 0, sy = 0;
  int n = 0;
  uint32_t t0 = millis();
  while (millis() - t0 < 300) {
    sx += analogRead(joystickPin_X);
    sy += analogRead(joystickPin_Y);
    n++;
    delay(5);
  }
  joyCx = (int)(sx / n);
  joyCy = (int)(sy / n);

  Serial.print("joystick center set");
  Serial.print(joyCx);
  Serial.print(", ");
  Serial.println(joyCy);
}

// ----------------------------------------------
// ----------------------------------------------
// ----------------------------------------------
// State managing
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
  return face_side;
}

JoyDir detectJoyDir(int x, int y, FaceState face) {
  int dx = x - joyCx;
  int dy = y - joyCy;

  if (invertX) dx = -dx;
  if (invertY) dy = -dy;
  if (face == face_down) {
    dx = -dx;
    dy = -dy;
  }

  if (abs(dx) < joystickRelease && abs(dy) < joystickRelease) return dir_none;

  bool xTrig = abs(dx) >= joystickTriggerThresh;
  bool yTrig = abs(dy) >= joystickTriggerThresh;

  if (xTrig && yTrig) {
    if (abs(dx) >= abs(dy)) return (dx > 0) ? dir_right : dir_left;
    return (dy > 0) ? dir_up : dir_down;
  }

  if (xTrig) return (dx > 0) ? dir_right : dir_left;
  if (yTrig) return (dy > 0) ? dir_up : dir_down;

  return dir_none;
}


// Send msg through BL SPP
void sendLineBT(const char* key, int value) {
  if (!SerialBT.hasClient()) {
    Serial.println("NO SPP connection");
    return;
  }
  char buf[32];
  int n = snprintf(buf, sizeof(buf), "%s:%d\n", key, value);
  SerialBT.write((const uint8_t*)buf, n);
  Serial.println((String)key + " / " + (String)value);
}


static uint32_t lastMPUMs = 0;
static uint32_t lastJoyMs = 0;

void setup() {
  Serial.begin(115200);
  delay(200);

  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);

  Serial.println("BOOT");

  setupAmp();
  setupA2DP();
  Serial.println("A2DP started");

  delay(800);
  ampOn();

  setupSPP();

  setupMPU();
  setupJoystick();
  calibrateJoystickCenter();

  Serial.println("all ready");
}

void loop() {
  uint32_t now = millis();

  // MPU: 10Hz
  if (now - lastMPUMs >= 100) {
    lastMPUMs = now;

    sensors_event_t a, g, temp;
    mpu.getEvent(&a, &g, &temp);

    FaceState nf = detectFace(a.acceleration.x, a.acceleration.y, a.acceleration.z);

    if (nf != rawFace) {
      rawFace = nf;
      lastFaceChangeMs = millis();

    }

    if (millis() - lastFaceChangeMs >= faceSampleTime) {
      if (rawFace != currentFace) {
        currentFace = rawFace;

        if (currentFace == face_up || currentFace == face_down) {
          int v = (int)currentFace;
          if (v != lastSentFace) {
            lastSentFace = v;
            sendLineBT("FACE", v);
          }
        }
      }
    }
  }



  if (now - lastJoyMs >= 10) {
    lastJoyMs = now;

    int x = analogRead(joystickPin_X);
    int y = analogRead(joystickPin_Y);
    JoyDir dir = detectJoyDir(x, y, currentFace);

    if (dir == dir_none) {
      joyLatchedDir = dir_none;
    } else if (joyLatchedDir == dir_none) {
      joyLatchedDir = dir;

      uint32_t nowMs = millis();
      if (nowMs - lastTypeSentMs >= typeWaitTime) {
        lastTypeSentMs = nowMs;
        Serial.println("Joy controlled");
        sendLineBT("TYPE", (int)dir);
      }
    }

    bool pressed = (digitalRead(joustickPin_SW) == LOW);
    if (!pressed) {
      buttonLatched = false;
    } else if (!buttonLatched) {
      buttonLatched = true;
      sendLineBT("BUTTON", 0);
    }
  }

  delay(3);
}