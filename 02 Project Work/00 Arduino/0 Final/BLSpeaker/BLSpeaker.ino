//--------------------------------------------------------------------------------------
//--------------------------------------------------------------------------------------
// Documentaion:
// It's for the bluetooth speaker.
// Used ESP32 Devkit C4, which contains the Bluetooth Classic.
// Make sure to use the Bluetooth Classic, not BLE, since BLE is not supported by the A2DP protocol.
// -
// Used Board:
// ESP32 Devkit C4
//--------------------------------------------------------------------------------------
//--------------------------------------------------------------------------------------
#include "AudioTools.h"
#include "BluetoothA2DPSink.h"

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



void setup() {
  Serial.begin(115200);
  delay(200);
  
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);
  setupAmp();
  setupA2DP();
  Serial.println("A2DP started");

  delay(800);
  ampOn();


}

void loop() {

}
