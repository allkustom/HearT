#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

Adafruit_MPU6050 mpu;

// 
enum FaceState {
  face_neutral = 0,
  face_up = 1,
  face_down = -1
};

FaceState currentState = face_neutral;

const char useAxis = 'Z';

const float facingThreshold = 7.0f;  

void setup() {
  Serial.begin(115200);

  Wire.begin();

  if (!mpu.begin()) {
    Serial.println("None MPU6050");
    while (1) delay(10);
  }

  mpu.setAccelerometerRange(MPU6050_RANGE_2_G);
  mpu.setGyroRange(MPU6050_RANGE_250_DEG);
  mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);

  delay(300);
}

FaceState detectFace(float ax, float ay, float az) {
  float value = 0.0f;

  switch (useAxis) {
    case 'X': value = ax; break;
    case 'Y': value = ay; break;
    case 'Z': value = az; break;
  }

  if (value > facingThreshold) {
    return face_up;
  }
  else if (value < -facingThreshold) {
    return face_down;
  }
  else {
    return face_neutral;
  }
}

void loop() {
  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  float ax = a.acceleration.x;
  float ay = a.acceleration.y;
  float az = a.acceleration.z;

  FaceState newState = detectFace(ax, ay, az);

  if (newState != currentState) {
    currentState = newState;

    Serial.print("FACE:");
    Serial.print((int)currentState);
    Serial.print(",");
    Serial.print(ax, 3); Serial.print(",");
    Serial.print(ay, 3); Serial.print(",");
    Serial.println(az, 3);
  }

  delay(20);
}