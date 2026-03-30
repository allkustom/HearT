//--------------------------------------------------------------------------------------
//--------------------------------------------------------------------------------------
// Documentaion:
// It's for the pen plotter machine.
// covers the tabloid size paper(as a landscape)
// Couldn't apply GRBL. And made drawing sequence manually.
// Make sure to set variables as const as much as possible, since it requires almost 100% memory of the board
// -
// Used Board:
// Arduino Uno R3
//--------------------------------------------------------------------------------------
//--------------------------------------------------------------------------------------
#include <AccelStepper.h>
#include <MultiStepper.h>
#include <Servo.h>
#include <math.h>

const int stepperCount = 2;
const int axisX = 0;
const int axisY = 1;

const int dirPin[stepperCount] = {2, 4};
const int stepPin[stepperCount] = {3, 5};
const int enPin[stepperCount] = {6, 7};

// Step control pin is wired manually
// Switching between fullstep and 1/16 step mode
const int stepControlPin = 8;
const int servoPin = 9;

const int fullStepRev = 200;
int microStepping = 1;
int stepsPerRevolution = fullStepRev * 1;

const bool stepMode_FULLSTEP = LOW;
const bool stepMode_MICROSTEP = HIGH;
int microStepSetting = 16;

const float pulleyDiameter = 38.975f;
const float pullyRadius = pulleyDiameter * PI;
float stepsPerMM = (float)200 / pullyRadius;

const float minX = 0.0f;
const float maxX = 431.8f;
const float minY = 2.5f;
const float maxY = 274.4f;

const float paperWidth = 431.8f;
const float paperHeight = 279.4f;

const float margin = 5.0f;
const float drawMinX = minX + margin;
const float drawMaxX = maxX - margin;
const float drawMinY = minY + margin;
const float drawMaxY = maxY - margin;
const bool invertDrawY = true;

const float penOffsetHalf = 12.075f;

float travelMaxSpeed = 120.0f;
float travelAccel = 50.0f;

float drawMaxSpeed = 25.0f;
float drawAccel = 25.0f;

float travelMaxSpeedSteps = 0.0f;
float travelAccelSteps = 0.0f;
float drawMaxSpeedSteps = 0.0f;
float drawAccelSteps = 0.0f;

// Servo arm wasn't staying in the mdidle when it's 90, so applied 100 for the mid/neutral posiiton
const int servoMidDeg = 100;
const int servoPen1DownDeg = 150;
const int servoPen2DownDeg = 50;
const int penSetOffset = 15;
const int servoMoveDelayMs = 250;

const float defaultCrossSize = 6.0f;
const int defaultCircleSegments = 10;

const bool invertXDir = false;
const bool invertYDir = true;

AccelStepper *st[stepperCount];
MultiStepper xySteppers;
Servo penServo;

bool systemArmed = false;
bool penIsDown = false;
int currentPen = 1;

struct PointMM
{
  float x;
  float y;
};

// Setting top-right as a starting point
// Make sure to move the plotter haed to top-right
PointMM currentHead = {maxX, maxY};

String serialLine;

//--------------------------------------------------------------------------------------
//--------------------------------------------------------------------------------------
// Utilities
//--------------------------------------------------------------------------------------
//--------------------------------------------------------------------------------------
void updateMotionScale()
{
  travelMaxSpeedSteps = travelMaxSpeed * stepsPerMM;
  travelAccelSteps = travelAccel * stepsPerMM;
  drawMaxSpeedSteps = drawMaxSpeed * stepsPerMM;
  drawAccelSteps = drawAccel * stepsPerMM;
}

static inline long mmToSteps(float mm)
{
  return lroundf(mm * stepsPerMM);
}

static inline float clampf(float v, float low, float high)
{
  if (v < low)
  {
    return low;
  }
  if (v > high)
  {
    return high;
  }
  return v;
}

static inline bool underRange(float v, float lo, float hi)
{
  return (v >= lo && v <= hi);
}

static inline bool pointInDrawArea(float x, float y)
{
  return underRange(x, drawMinX, drawMaxX) &&
         underRange(y, drawMinY, drawMaxY);
}

static inline bool pointInMachineArea(float x, float y)
{
  return underRange(x, minX, maxX) &&
         underRange(y, minY, maxY);
}

static inline float lerpf(float a, float b, float t)
{
  return a + (b - a) * t;
}

bool isNumericToken(const String &s)
{
  if (s.length() == 0)
    return false;
  bool sawDigit = false;
  bool sawDot = false;
  int start = 0;
  if (s[0] == '-' || s[0] == '+')
    start = 1;
  for (int i = start; i < s.length(); i++)
  {
    char c = s[i];
    if (c >= '0' && c <= '9')
    {
      sawDigit = true;
      continue;
    }
    if (c == '.' && !sawDot)
    {
      sawDot = true;
      continue;
    }
    return false;
  }
  return sawDigit;
}

String nextToken(String &s)
{
  s.trim();
  if (s.length() == 0)
    return "";
  int sp = s.indexOf(' ');
  if (sp < 0)
  {
    String t = s;
    s = "";
    t.trim();
    return t;
  }
  String t = s.substring(0, sp);
  s = s.substring(sp + 1);
  t.trim();
  return t;
}

String restTokens(String &s)
{
  s.trim();
  String out = s;
  s = "";
  out.trim();
  return out;
}

float penYOffsetMM(int pen)
{
  return (pen == 1) ? +penOffsetHalf : -penOffsetHalf;
}

bool penCoordToHeadCoord(float penX, float penY, int pen, PointMM &headOut)
{
  headOut.x = penX;
  headOut.y = penY - penYOffsetMM(pen);

  if (!pointInMachineArea(headOut.x, headOut.y))
    return false;
  return true;
}

void printReachableRange()
{
  // Serial.println(F("Reachable machine area in paper coordinates:"));
  // Serial.print(F("X: "));
  // Serial.print(minX, 2);
  // Serial.print(F(" to "));
  // Serial.println(maxX, 2);
  // Serial.print(F("Y: "));
  // Serial.print(minY, 2);
  // Serial.print(F(" to "));
  // Serial.println(maxY, 2);

  // Serial.println(F("Reachable pen Y range with current offset assumption:"));
  // Serial.print(F("PEN1 Y: "));
  // Serial.print(minY + penYOffsetMM(1), 2);
  // Serial.print(F(" to "));
  // Serial.println(maxY + penYOffsetMM(1), 2);
  // Serial.print(F("PEN2 Y: "));
  // Serial.print(minY + penYOffsetMM(2), 2);
  // Serial.print(F(" to "));
  // Serial.println(maxY + penYOffsetMM(2), 2);
}

void printDrawArea()
{
  // Serial.println(F("Drawable area with margin:"));
  // Serial.print(F("X: "));
  // Serial.print(drawMinX, 2);
  // Serial.print(F(" ~ "));
  // Serial.println(drawMaxX, 2);
  // Serial.print(F("Y: "));
  // Serial.print(drawMinY, 2);
  // Serial.print(F(" ~ "));
  // Serial.println(drawMaxY, 2);
}

void syncStepperPositionsToCurrentHead()
{
  if (st[axisX] == nullptr || st[axisY] == nullptr)
    return;
  st[axisX]->setCurrentPosition(invertXDir ? -mmToSteps(currentHead.x) : mmToSteps(currentHead.x));
  st[axisY]->setCurrentPosition(invertYDir ? -mmToSteps(currentHead.y) : mmToSteps(currentHead.y));
}

void setStepModeFullStep()
{
  digitalWrite(stepControlPin, stepMode_FULLSTEP);
  microStepping = 1;
  stepsPerRevolution = fullStepRev * microStepping;
  stepsPerMM = (float)stepsPerRevolution / pullyRadius;
  updateMotionScale();
  syncStepperPositionsToCurrentHead();
}

void setStepModeMicro()
{
  digitalWrite(stepControlPin, stepMode_MICROSTEP);
  microStepping = microStepSetting;
  stepsPerRevolution = fullStepRev * microStepping;
  stepsPerMM = (float)stepsPerRevolution / pullyRadius;
  updateMotionScale();
  syncStepperPositionsToCurrentHead();
}

void enableMotor(bool activate)
{
  for (int i = 0; i < stepperCount; i++)
  {
    digitalWrite(enPin[i], activate ? LOW : HIGH);
  }
}

void setAxisProfile(int axis, float maxSpeed, float accel)
{
  st[axis]->setMaxSpeed(maxSpeed);
  st[axis]->setAcceleration(accel);
}

void setBothAxisProfile(float maxSpeed, float accel)
{
  for (int i = 0; i < stepperCount; i++)
  {
    setAxisProfile(i, maxSpeed, accel);
  }
}

void penUp()
{
  penServo.write(servoMidDeg);
  delay(servoMoveDelayMs);
  penIsDown = false;
}

void penDownSelect(int pen)
{
  currentPen = (pen == 2) ? 2 : 1;
  if (currentPen == 1)
  {
    penServo.write(servoPen1DownDeg);
  }
  else
  {
    penServo.write(servoPen2DownDeg);
  }
  delay(servoMoveDelayMs);
  penIsDown = true;
}

void penSetSelect(int pen)
{
  currentPen = (pen == 2) ? 2 : 1;
  if (currentPen == 1)
  {
    penServo.write(servoPen1DownDeg - penSetOffset);
  }
  else
  {
    penServo.write(servoPen2DownDeg + penSetOffset);
  }
  delay(servoMoveDelayMs);
  penIsDown = true;
}

void startSequenceReferenceTopRight()
{
  enableMotor(true);
  // setStepModeFullStep();
  setStepModeMicro();
  setBothAxisProfile(travelMaxSpeedSteps, travelAccelSteps);

  st[axisX]->setCurrentPosition(invertXDir ? -mmToSteps(maxX) : mmToSteps(maxX));
  st[axisY]->setCurrentPosition(invertYDir ? -mmToSteps(maxY) : mmToSteps(maxY));
  currentHead.x = maxX;
  currentHead.y = maxY;

  penUp();
  systemArmed = true;

  Serial.println("Set top-right as a start point");
}

void freeSteppers()
{
  penUp();
  enableMotor(false);
  systemArmed = false;
}

void ensureArmed()
{
  if (!systemArmed)
  {
    startSequenceReferenceTopRight();
  }
}

bool moveHeadToMM(float xMM, float yMM, bool verbose = true,
                  float maxSpeed = travelMaxSpeedSteps,
                  float accel = travelAccelSteps)
{
  ensureArmed();

  if (!pointInMachineArea(xMM, yMM))
  {
    if (verbose)
    {
      // Serial.print(F("Error: head target out of machine range: "));
    }
    return false;
  }

  long targetX = invertXDir ? -mmToSteps(xMM) : mmToSteps(xMM);
  long targetY = invertYDir ? -mmToSteps(yMM) : mmToSteps(yMM);

  long dx = labs(targetX - st[axisX]->currentPosition());
  long dy = labs(targetY - st[axisY]->currentPosition());

  if (dx == 0 && dy == 0)
  {
    currentHead.x = xMM;
    currentHead.y = yMM;
    return true;
  }

  st[axisX]->setMaxSpeed(maxSpeed);
  st[axisY]->setMaxSpeed(maxSpeed);

  long targets[stepperCount];
  targets[axisX] = targetX;
  targets[axisY] = targetY;

  xySteppers.moveTo(targets);
  xySteppers.runSpeedToPosition();

  currentHead.x = xMM;
  currentHead.y = yMM;

  if (verbose)
  {
    Serial.print(F("OK HEAD -> "));
    Serial.print(currentHead.x, 2);
    Serial.print(F(", "));
    Serial.println(currentHead.y, 2);
  }
  return true;
}

bool movePenToMM(float xMM, float yMM, int pen, bool verbose = true,
                 float maxSpeed = travelMaxSpeedSteps,
                 float accel = travelAccelSteps)
{
  PointMM head;
  if (!penCoordToHeadCoord(xMM, yMM, pen, head))
  {
    if (verbose)
    {
      Serial.print(F("ERR: pen "));
      // Serial.print(pen);
      // Serial.print(F(" cannot reach point ("));
      // Serial.print(xMM, 2);
      // Serial.print(F(", "));
      // Serial.print(yMM, 2);
      // Serial.println(F(") due to pen offset / machine range."));
    }
    return false;
  }
  return moveHeadToMM(head.x, head.y, verbose, maxSpeed, accel);
}

bool movePenClampedToDrawArea(float x, float y, int pen,
                              bool verbose = false,
                              float maxSpeed = drawMaxSpeedSteps,
                              float accel = drawAccelSteps)
{
  float cx = clampf(x, drawMinX, drawMaxX);
  float cy = clampf(y, drawMinY, drawMaxY);
  return movePenToMM(cx, cy, pen, verbose, maxSpeed, accel);
}

bool drawSegmentClipped(float x1, float y1, float x2, float y2, int pen, int subdivisions = 80)
{
  ensureArmed();
  if (subdivisions < 1)
    subdivisions = 1;

  bool lastInside = false;
  bool first = true;

  for (int i = 0; i <= subdivisions; i++)
  {
    float t = (float)i / (float)subdivisions;
    float x = lerpf(x1, x2, t);
    float y = lerpf(y1, y2, t);

    bool inside = pointInDrawArea(x, y);

    if (first)
    {
      if (!movePenClampedToDrawArea(x, y, pen, false, travelMaxSpeedSteps, travelAccelSteps))
        return false;
      if (inside)
        penDownSelect(pen);
      else
        penUp();
      lastInside = inside;
      first = false;
      continue;
    }

    if (inside != lastInside)
    {
      penUp();
      if (!movePenClampedToDrawArea(x, y, pen, false, drawMaxSpeedSteps, drawAccelSteps))
        return false;
      if (inside)
        penDownSelect(pen);
      lastInside = inside;
    }
    else
    {
      if (!movePenClampedToDrawArea(x, y, pen, false, drawMaxSpeedSteps, drawAccelSteps))
        return false;
    }
  }

  penUp();
  return true;
}

bool moveDrawTo(float x, float y, int pen)
{
  return movePenClampedToDrawArea(x, y, pen, false, drawMaxSpeedSteps, drawAccelSteps);
}

bool drawClosedPolylineContinuous(PointMM *pts, int count, int pen)
{
  if (count < 2)
    return false;

  ensureArmed();

  penUp();
  if (!movePenClampedToDrawArea(pts[0].x, pts[0].y, pen, false, travelMaxSpeedSteps, travelAccelSteps))
  {
    return false;
  }

  penDownSelect(pen);

  for (int i = 1; i < count; i++)
  {
    if (!moveDrawTo(pts[i].x, pts[i].y, pen))
    {
      penUp();
      return false;
    }
  }

  if (!moveDrawTo(pts[0].x, pts[0].y, pen))
  {
    penUp();
    return false;
  }

  penUp();
  return true;
}

bool gotoInsideDrawArea(float x, float y, int pen)
{
  penUp();
  return movePenClampedToDrawArea(x, y, pen, true, travelMaxSpeedSteps, travelAccelSteps);
}

bool linePen(float x1, float y1, float x2, float y2, int pen)
{
  return drawSegmentClipped(x1, y1, x2, y2, pen, 100);
}

bool crossAt(float cx, float cy, float sizeMM, int pen)
{
  float h = sizeMM * 0.5f;
  bool ok = true;
  ok &= drawSegmentClipped(cx - h, cy, cx + h, cy, pen, 40);
  ok &= drawSegmentClipped(cx, cy - h, cx, cy + h, pen, 40);
  return ok;
}

bool rectCentered(float cx, float cy, float w, float h, int pen)
{
  float x1 = cx - w * 0.5f;
  float x2 = cx + w * 0.5f;
  float y1 = cy - h * 0.5f;
  float y2 = cy + h * 0.5f;

  bool ok = true;
  ok &= drawSegmentClipped(x1, y1, x2, y1, pen, 60);
  ok &= drawSegmentClipped(x2, y1, x2, y2, pen, 60);
  ok &= drawSegmentClipped(x2, y2, x1, y2, pen, 60);
  ok &= drawSegmentClipped(x1, y2, x1, y1, pen, 60);
  return ok;
}

bool triangleCentered(float cx, float cy, float sizeMM, int pen)
{
  float r = sizeMM * 0.5f;
  PointMM p[4];
  for (int i = 0; i < 3; i++)
  {
    float a = (-90.0f + i * 120.0f) * DEG_TO_RAD;
    p[i].x = cx + cosf(a) * r;
    p[i].y = cy - sinf(a) * r;
  }
  p[3] = p[0];

  bool ok = true;
  for (int i = 0; i < 3; i++)
  {
    ok &= drawSegmentClipped(p[i].x, p[i].y, p[i + 1].x, p[i + 1].y, pen, 60);
  }
  return ok;
}

bool circleCentered(float cx, float cy, float radiusMM, int pen, int segments = defaultCircleSegments)
{
  const int polygonSides = 30;
  PointMM pts[polygonSides];

  for (int i = 0; i < polygonSides; i++)
  {
    float a = (TWO_PI * i) / polygonSides;

    pts[i].x = cx + cosf(a) * radiusMM;
    pts[i].y = cy - sinf(a) * radiusMM;
  }

  return drawClosedPolylineContinuous(pts, polygonSides, pen);
}

bool starCentered(float cx, float cy, float outerR, float innerR, int pen)
{
  PointMM p[11];
  for (int i = 0; i < 10; i++)
  {
    float a = (-90.0f + i * 36.0f) * DEG_TO_RAD;
    float r = (i % 2 == 0) ? outerR : innerR;
    p[i].x = cx + cosf(a) * r;
    p[i].y = cy - sinf(a) * r;
  }
  p[10] = p[0];

  bool ok = true;
  for (int i = 0; i < 10; i++)
  {
    ok &= drawSegmentClipped(p[i].x, p[i].y, p[i + 1].x, p[i + 1].y, pen, 30);
  }
  return ok;
}

bool goHome0()
{
  penUp();
  return moveHeadToMM(maxX, maxY, true, travelMaxSpeedSteps, travelAccelSteps);
}

bool goHome1()
{
  penUp();
  return moveHeadToMM(drawMaxX, drawMaxY, true, travelMaxSpeedSteps, travelAccelSteps);
}

bool goHome2()
{
  penUp();
  float centerX = (minX + maxX) * 0.5f;
  float centerY = (minY + maxY) * 0.5f;
  return moveHeadToMM(centerX, centerY, true, travelMaxSpeedSteps, travelAccelSteps);
}

float glyphOx, glyphOy, glyphW, glyphH;
int glyphPen = 1;
bool glyphStrokeActive = false;
float glyphLastX = 0.0f;
float glyphLastY = 0.0f;

static inline bool glyphSamePoint(float ax, float ay, float bx, float by)
{
  return fabs(ax - bx) < 0.01f && fabs(ay - by) < 0.01f;
}

void glyphBegin(int pen)
{
  glyphPen = pen;
  glyphStrokeActive = false;
  glyphLastX = 0.0f;
  glyphLastY = 0.0f;
  penUp();
}

void glyphEnd()
{
  penUp();
  glyphStrokeActive = false;
}

bool glyphLine(float x1, float y1, float x2, float y2)
{
  float ax = glyphOx + x1 * glyphW;
  float ay = glyphOy + (1.0f - y1) * glyphH;
  float bx = glyphOx + x2 * glyphW;
  float by = glyphOy + (1.0f - y2) * glyphH;

  bool reverseSegment = false;
  if (glyphStrokeActive)
  {
    if (glyphSamePoint(ax, ay, glyphLastX, glyphLastY))
    {
      reverseSegment = false;
    }
    else if (glyphSamePoint(bx, by, glyphLastX, glyphLastY))
    {
      reverseSegment = true;
    }
    else
    {
      penUp();
      if (!movePenClampedToDrawArea(ax, ay, glyphPen, false, travelMaxSpeedSteps, travelAccelSteps))
        return false;
      penDownSelect(glyphPen);
    }
  }
  else
  {
    penUp();
    if (!movePenClampedToDrawArea(ax, ay, glyphPen, false, travelMaxSpeedSteps, travelAccelSteps))
      return false;
    penDownSelect(glyphPen);
  }

  if (reverseSegment)
  {
    if (!moveDrawTo(ax, ay, glyphPen))
    {
      penUp();
      glyphStrokeActive = false;
      return false;
    }
    glyphLastX = ax;
    glyphLastY = ay;
  }
  else
  {
    if (!moveDrawTo(bx, by, glyphPen))
    {
      penUp();
      glyphStrokeActive = false;
      return false;
    }
    glyphLastX = bx;
    glyphLastY = by;
  }

  glyphStrokeActive = true;
  return true;
}

bool drawGlyph(char c, float ox, float oy, float w, float h, int pen)
{
  glyphOx = ox;
  glyphOy = oy;
  glyphW = w;
  glyphH = h;
  glyphPen = pen;

  glyphBegin(pen);

  if (c >= 'a' && c <= 'z')
    c = c - 'a' + 'A';

  bool ok = true;
  switch (c)
  {
  case 'A':
    ok &= glyphLine(0.0, 1.0, 0.5, 0.0);
    ok &= glyphLine(1.0, 1.0, 0.5, 0.0);
    ok &= glyphLine(0.2, 0.55, 0.8, 0.55);
    break;
  case 'B':
    ok &= glyphLine(0.0, 0.0, 0.0, 1.0);
    ok &= glyphLine(0.0, 0.0, 0.75, 0.1);
    ok &= glyphLine(0.75, 0.1, 0.75, 0.45);
    ok &= glyphLine(0.75, 0.45, 0.0, 0.5);
    ok &= glyphLine(0.0, 0.5, 0.75, 0.55);
    ok &= glyphLine(0.75, 0.55, 0.75, 0.9);
    ok &= glyphLine(0.75, 0.9, 0.0, 1.0);
    break;
  case 'C':
    ok &= glyphLine(0.9, 0.1, 0.2, 0.0);
    ok &= glyphLine(0.2, 0.0, 0.0, 0.2);
    ok &= glyphLine(0.0, 0.2, 0.0, 0.8);
    ok &= glyphLine(0.0, 0.8, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.9, 0.9);
    break;
  case 'D':
    ok &= glyphLine(0.0, 0.0, 0.0, 1.0);
    ok &= glyphLine(0.0, 0.0, 0.75, 0.15);
    ok &= glyphLine(0.75, 0.15, 0.9, 0.5);
    ok &= glyphLine(0.9, 0.5, 0.75, 0.85);
    ok &= glyphLine(0.75, 0.85, 0.0, 1.0);
    break;
  case 'E':
    ok &= glyphLine(0.9, 0.0, 0.0, 0.0);
    ok &= glyphLine(0.0, 0.0, 0.0, 1.0);
    ok &= glyphLine(0.0, 0.5, 0.7, 0.5);
    ok &= glyphLine(0.0, 1.0, 0.9, 1.0);
    break;
  case 'F':
    ok &= glyphLine(0.0, 0.0, 0.0, 1.0);
    ok &= glyphLine(0.0, 0.0, 0.9, 0.0);
    ok &= glyphLine(0.0, 0.5, 0.7, 0.5);
    break;
  case 'G':
    ok &= glyphLine(0.9, 0.2, 0.7, 0.0);
    ok &= glyphLine(0.7, 0.0, 0.2, 0.0);
    ok &= glyphLine(0.2, 0.0, 0.0, 0.2);
    ok &= glyphLine(0.0, 0.2, 0.0, 0.8);
    ok &= glyphLine(0.0, 0.8, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.9, 1.0);
    ok &= glyphLine(0.9, 1.0, 0.9, 0.6);
    ok &= glyphLine(0.9, 0.6, 0.5, 0.6);
    break;
  case 'H':
    ok &= glyphLine(0.0, 0.0, 0.0, 1.0);
    ok &= glyphLine(1.0, 0.0, 1.0, 1.0);
    ok &= glyphLine(0.0, 0.5, 1.0, 0.5);
    break;
  case 'I':
    ok &= glyphLine(0.1, 0.0, 0.9, 0.0);
    ok &= glyphLine(0.5, 0.0, 0.5, 1.0);
    ok &= glyphLine(0.1, 1.0, 0.9, 1.0);
    break;
  case 'J':
    ok &= glyphLine(0.1, 0.0, 0.9, 0.0);
    ok &= glyphLine(0.5, 0.0, 0.5, 0.85);
    ok &= glyphLine(0.5, 0.85, 0.3, 1.0);
    ok &= glyphLine(0.3, 1.0, 0.0, 0.85);
    break;
  case 'K':
    ok &= glyphLine(0.0, 0.0, 0.0, 1.0);
    ok &= glyphLine(0.95, 0.0, 0.0, 0.55);
    ok &= glyphLine(0.2, 0.45, 1.0, 1.0);
    break;
  case 'L':
    ok &= glyphLine(0.0, 0.0, 0.0, 1.0);
    ok &= glyphLine(0.0, 1.0, 0.9, 1.0);
    break;
  case 'M':
    ok &= glyphLine(0.0, 1.0, 0.0, 0.0);
    ok &= glyphLine(0.0, 0.0, 0.5, 0.45);
    ok &= glyphLine(0.5, 0.45, 1.0, 0.0);
    ok &= glyphLine(1.0, 0.0, 1.0, 1.0);
    break;
  case 'N':
    ok &= glyphLine(0.0, 1.0, 0.0, 0.0);
    ok &= glyphLine(0.0, 0.0, 1.0, 1.0);
    ok &= glyphLine(1.0, 1.0, 1.0, 0.0);
    break;
  case 'O':
    ok &= glyphLine(0.2, 0.0, 0.8, 0.0);
    ok &= glyphLine(0.8, 0.0, 1.0, 0.2);
    ok &= glyphLine(1.0, 0.2, 1.0, 0.8);
    ok &= glyphLine(1.0, 0.8, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.0, 0.8);
    ok &= glyphLine(0.0, 0.8, 0.0, 0.2);
    ok &= glyphLine(0.0, 0.2, 0.2, 0.0);
    break;
  case 'P':
    ok &= glyphLine(0.0, 1.0, 0.0, 0.0);
    ok &= glyphLine(0.0, 0.0, 0.8, 0.0);
    ok &= glyphLine(0.8, 0.0, 0.9, 0.2);
    ok &= glyphLine(0.9, 0.2, 0.8, 0.45);
    ok &= glyphLine(0.8, 0.45, 0.0, 0.45);
    break;
  case 'Q':
    ok &= glyphLine(0.2, 0.0, 0.8, 0.0);
    ok &= glyphLine(0.8, 0.0, 1.0, 0.2);
    ok &= glyphLine(1.0, 0.2, 1.0, 0.8);
    ok &= glyphLine(1.0, 0.8, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.0, 0.8);
    ok &= glyphLine(0.0, 0.8, 0.0, 0.2);
    ok &= glyphLine(0.0, 0.2, 0.2, 0.0);
    ok &= glyphLine(0.55, 0.6, 1.0, 1.0);
    break;
  case 'R':
    ok &= glyphLine(0.0, 1.0, 0.0, 0.0);
    ok &= glyphLine(0.0, 0.0, 0.8, 0.0);
    ok &= glyphLine(0.8, 0.0, 0.9, 0.2);
    ok &= glyphLine(0.9, 0.2, 0.8, 0.45);
    ok &= glyphLine(0.8, 0.45, 0.0, 0.45);
    ok &= glyphLine(0.3, 0.45, 1.0, 1.0);
    break;
  case 'S':
    ok &= glyphLine(0.9, 0.1, 0.2, 0.0);
    ok &= glyphLine(0.2, 0.0, 0.0, 0.25);
    ok &= glyphLine(0.0, 0.25, 0.8, 0.5);
    ok &= glyphLine(0.8, 0.5, 1.0, 0.75);
    ok &= glyphLine(1.0, 0.75, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 0.1, 0.9);
    break;
  case 'T':
    ok &= glyphLine(0.0, 0.0, 1.0, 0.0);
    ok &= glyphLine(0.5, 0.0, 0.5, 1.0);
    break;
  case 'U':
    ok &= glyphLine(0.0, 0.0, 0.0, 0.8);
    ok &= glyphLine(0.0, 0.8, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 1.0, 0.8);
    ok &= glyphLine(1.0, 0.8, 1.0, 0.0);
    break;
  case 'V':
    ok &= glyphLine(0.0, 0.0, 0.5, 1.0);
    ok &= glyphLine(0.5, 1.0, 1.0, 0.0);
    break;
  case 'W':
    ok &= glyphLine(0.0, 0.0, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.5, 0.55);
    ok &= glyphLine(0.5, 0.55, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 1.0, 0.0);
    break;
  case 'X':
    ok &= glyphLine(0.0, 0.0, 1.0, 1.0);
    ok &= glyphLine(1.0, 0.0, 0.0, 1.0);
    break;
  case 'Y':
    ok &= glyphLine(0.0, 0.0, 0.5, 0.5);
    ok &= glyphLine(1.0, 0.0, 0.5, 0.5);
    ok &= glyphLine(0.5, 0.5, 0.5, 1.0);
    break;
  case 'Z':
    ok &= glyphLine(0.0, 0.0, 1.0, 0.0);
    ok &= glyphLine(1.0, 0.0, 0.0, 1.0);
    ok &= glyphLine(0.0, 1.0, 1.0, 1.0);
    break;
  case '0':
    ok &= glyphLine(0.2, 0.0, 0.8, 0.0);
    ok &= glyphLine(0.8, 0.0, 1.0, 0.2);
    ok &= glyphLine(1.0, 0.2, 1.0, 0.8);
    ok &= glyphLine(1.0, 0.8, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.0, 0.8);
    ok &= glyphLine(0.0, 0.8, 0.0, 0.2);
    ok &= glyphLine(0.0, 0.2, 0.2, 0.0);
    ok &= glyphLine(0.25, 0.85, 0.75, 0.15);
    break;
  case '1':
    ok &= glyphLine(0.5, 0.0, 0.5, 1.0);
    ok &= glyphLine(0.35, 0.15, 0.5, 0.0);
    ok &= glyphLine(0.3, 1.0, 0.7, 1.0);
    break;
  case '2':
    ok &= glyphLine(0.1, 0.2, 0.3, 0.0);
    ok &= glyphLine(0.3, 0.0, 0.8, 0.0);
    ok &= glyphLine(0.8, 0.0, 1.0, 0.2);
    ok &= glyphLine(1.0, 0.2, 0.0, 1.0);
    ok &= glyphLine(0.0, 1.0, 1.0, 1.0);
    break;
  case '3':
    ok &= glyphLine(0.1, 0.0, 0.9, 0.0);
    ok &= glyphLine(0.9, 0.0, 0.55, 0.5);
    ok &= glyphLine(0.55, 0.5, 0.9, 1.0);
    ok &= glyphLine(0.1, 1.0, 0.9, 1.0);
    ok &= glyphLine(0.3, 0.5, 0.7, 0.5);
    break;
  case '4':
    ok &= glyphLine(0.8, 0.0, 0.8, 1.0);
    ok &= glyphLine(0.0, 0.55, 1.0, 0.55);
    ok &= glyphLine(0.0, 0.55, 0.6, 0.0);
    break;
  case '5':
    ok &= glyphLine(1.0, 0.0, 0.1, 0.0);
    ok &= glyphLine(0.1, 0.0, 0.1, 0.5);
    ok &= glyphLine(0.1, 0.5, 0.8, 0.5);
    ok &= glyphLine(0.8, 0.5, 1.0, 0.7);
    ok &= glyphLine(1.0, 0.7, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 0.1, 1.0);
    break;
  case '6':
    ok &= glyphLine(0.9, 0.1, 0.7, 0.0);
    ok &= glyphLine(0.7, 0.0, 0.2, 0.0);
    ok &= glyphLine(0.2, 0.0, 0.0, 0.4);
    ok &= glyphLine(0.0, 0.4, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 1.0, 0.8);
    ok &= glyphLine(1.0, 0.8, 0.8, 0.5);
    ok &= glyphLine(0.8, 0.5, 0.0, 0.5);
    break;
  case '7':
    ok &= glyphLine(0.0, 0.0, 1.0, 0.0);
    ok &= glyphLine(1.0, 0.0, 0.35, 1.0);
    break;
  case '8':
    ok &= glyphLine(0.2, 0.0, 0.8, 0.0);
    ok &= glyphLine(0.8, 0.0, 1.0, 0.2);
    ok &= glyphLine(1.0, 0.2, 0.8, 0.5);
    ok &= glyphLine(0.8, 0.5, 0.2, 0.5);
    ok &= glyphLine(0.2, 0.5, 0.0, 0.2);
    ok &= glyphLine(0.0, 0.2, 0.2, 0.0);
    ok &= glyphLine(0.2, 0.5, 0.0, 0.8);
    ok &= glyphLine(0.0, 0.8, 0.2, 1.0);
    ok &= glyphLine(0.2, 1.0, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 1.0, 0.8);
    ok &= glyphLine(1.0, 0.8, 0.8, 0.5);
    break;
  case '9':
    ok &= glyphLine(1.0, 0.6, 0.8, 0.0);
    ok &= glyphLine(0.8, 0.0, 0.2, 0.0);
    ok &= glyphLine(0.2, 0.0, 0.0, 0.2);
    ok &= glyphLine(0.0, 0.2, 0.2, 0.5);
    ok &= glyphLine(0.2, 0.5, 1.0, 0.5);
    ok &= glyphLine(1.0, 0.5, 0.8, 1.0);
    ok &= glyphLine(0.8, 1.0, 0.2, 1.0);
    break;
  case '-':
    ok &= glyphLine(0.15, 0.5, 0.85, 0.5);
    break;
  case '_':
    ok &= glyphLine(0.05, 1.0, 0.95, 1.0);
    break;
  case '/':
    ok &= glyphLine(0.15, 1.0, 0.85, 0.0);
    break;
  case ' ':
    break;
  default:
    ok &= glyphLine(0.1, 0.1, 0.9, 0.1);
    ok &= glyphLine(0.9, 0.1, 0.9, 0.9);
    ok &= glyphLine(0.9, 0.9, 0.1, 0.9);
    ok &= glyphLine(0.1, 0.9, 0.1, 0.1);
    break;
  }

  glyphEnd();
  return ok;
}

bool textInBox(float x1, float y1, float x2, float y2, String txt, int pen)
{
  float left = max(min(x1, x2), drawMinX);
  float right = min(max(x1, x2), drawMaxX);
  float bottom = min(max(y1, y2), drawMaxY);
  float top = max(min(y1, y2), drawMinY);

  if (right <= left || bottom <= top)
  {
    Serial.println(F("ERR: BOXTEXT box is outside drawable area."));
    return false;
  }

  float boxW = right - left;
  float boxH = bottom - top;

  txt.replace("_", " ");
  txt.trim();
  if (txt.length() == 0)
    return true;

  float margin = 2.0f;
  float innerW = max(1.0f, boxW - margin * 2.0f);
  float innerH = max(1.0f, boxH - margin * 2.0f);

  float n = (float)txt.length();
  float charW_byWidth = innerW / (n * 0.80f);
  float charH_byHeight = innerH;

  float charH = min(charH_byHeight, charW_byWidth / 0.62f);
  float charW = charH * 0.62f;
  float spacing = charW * 0.18f;

  float totalW = n * charW + (n - 1.0f) * spacing;
  float startX = left + (boxW - totalW) * 0.5f;
  float startY = top + (boxH - charH) * 0.5f;

  bool ok = true;
  for (int i = 0; i < txt.length(); i++)
  {
    float ox = startX + i * (charW + spacing);
    ok &= drawGlyph(txt[i], ox, startY, charW, charH, pen);
  }
  return ok;
}

void printHelp()
{
  // Serial.println(F("CMD START                            -> assume current physical position = top-right"));
  // Serial.println(F("CMD FREE                             -> pen up + disable motors"));
  // Serial.println(F("CMD STATUS                           -> print status"));
  // Serial.println(F("CMD x y [P1|P2]                      -> pen-up move to XY (mm, clamped to drawable area)"));
  // Serial.println(F("CMD GOTO x y [P1|P2]                 -> same as above"));
  // Serial.println(F("CMD PENUP                            -> servo neutral 90"));
  // Serial.println(F("CMD PEN1                             -> pen1 down"));
  // Serial.println(F("CMD PEN2                             -> pen2 down"));
  // Serial.println(F("CMD FULLSTEP                         -> step mode full"));
  // Serial.println(F("CMD MICRO                          -> step mode 1/16"));
  // Serial.println(F("CMD LINE x1 y1 x2 y2 [P1|P2]"));
  // Serial.println(F("CMD CROSS x y size [P1|P2]"));
  // Serial.println(F("CMD CIRCLE x y radius [P1|P2]"));
  // Serial.println(F("CMD RECT x y w h [P1|P2]             -> centered rectangle"));
  // Serial.println(F("CMD TRI x y size [P1|P2]             -> centered triangle"));
  // Serial.println(F("CMD STAR x y outer inner [P1|P2]"));
  // Serial.println(F("CMD BOXTEXT x1 y1 x2 y2 TEXT [P1|P2]"));
}

void printStatus()
{

  // Serial.print(F("Paper size = "));
  // Serial.print(paperWidth, 2);
  // Serial.print(F(" x "));
  // Serial.print(paperHeight, 2);
  // Serial.println(F(" mm"));

  // Serial.print(F("Reachable range = ("));
  // Serial.print(minX, 2);
  // Serial.print(F(", "));
  // Serial.print(minY, 2);
  // Serial.print(F(") to ("));
  // Serial.print(maxX, 2);
  // Serial.print(F(", "));
  // Serial.print(maxY, 2);
  // Serial.println(F(")"));

  // Serial.print(F("Head center mm: "));
  // Serial.print(currentHead.x, 2);
  // Serial.print(F(", "));
  // Serial.println(currentHead.y, 2);

  // Serial.print(F("Head steps: "));
  // Serial.print(st[axisX]->currentPosition());
  // Serial.print(F(", "));
  // Serial.println(st[axisY]->currentPosition());

  // Serial.print(F("Current pen: P"));
  // Serial.println(currentPen);

  // Serial.print(F("Pen state: "));
  // Serial.println(penIsDown ? F("DOWN") : F("UP"));

  // Serial.print(F("Step mode: "));
  // Serial.println((microStepping == 1) ? F("FULL STEP") : F("1/16 STEP"));

  // Serial.print(F("stepsPerMM: "));
  // Serial.println(stepsPerMM, 6);

  printReachableRange();
  printDrawArea();
}

int parsePenToken(const String &token)
{
  String t = token;
  t.trim();
  if (t.length() == 0)
    return currentPen;
  t.toUpperCase();
  if (t == "P2" || t == "2")
    return 2;
  if (t == "P1" || t == "1")
    return 1;
  return currentPen;
}

bool parseFloatFromToken(String token, float &out)
{
  token.trim();
  if (!isNumericToken(token))
    return false;
  out = token.toFloat();
  return true;
}

bool handleCmdLine(String line)
{
  line.trim();
  if (line.length() == 0)
    return false;

  if (line.startsWith("CMD"))
  {
    line = line.substring(3);
  }
  line.trim();
  if (line.length() == 0)
  {
    printHelp();
    return false;
  }

  String peek = line;
  String first = nextToken(peek);

  if (isNumericToken(first))
  {
    String second = nextToken(peek);
    if (!isNumericToken(second))
    {
      Serial.println(F("ERR: expected second numeric token for direct move."));
      return false;
    }

    float x = first.toFloat();
    float y = second.toFloat();
    String maybePen = nextToken(peek);
    int pen = (maybePen.length() > 0) ? parsePenToken(maybePen) : currentPen;

    gotoInsideDrawArea(x, y, pen);
    return true;
  }

  String work = line;
  String cmd = nextToken(work);
  cmd.toUpperCase();

  if (cmd == "HOME0")
  {
    goHome0();
    return true;
  }

  if (cmd == "HOME1")
  {
    goHome1();
    return true;
  }

  if (cmd == "HOME2")
  {
    goHome2();
    return true;
  }

  if (cmd == "START")
  {
    startSequenceReferenceTopRight();
    return true;
  }

  if (cmd == "FREE")
  {
    freeSteppers();
    return true;
  }

  if (cmd == "MICRO")
  {
    setStepModeMicro();
    Serial.print(F("Step mode set to 1/"));
    Serial.print(microStepSetting);
    Serial.println(F(" STEP"));
    return true;
  }
  if (cmd == "PENUP")
  {
    penUp();
    return true;
  }
  if (cmd == "PEN1")
  {
    penDownSelect(1);
    return true;
  }
  if (cmd == "PEN2")
  {
    penDownSelect(2);
    return true;
  }
  if (cmd == "PENSET1")
  {
    penSetSelect(1);
    return true;
  }
  if (cmd == "PENSET2")
  {
    penSetSelect(2);
    return true;
  }

  if (cmd == "LINE")
  {
    float x1, y1, x2, y2;
    if (!parseFloatFromToken(nextToken(work), x1) || !parseFloatFromToken(nextToken(work), y1) ||
        !parseFloatFromToken(nextToken(work), x2) || !parseFloatFromToken(nextToken(work), y2))
    {
      return false;
    }
    int pen = parsePenToken(nextToken(work));

    linePen(x1, y1, x2, y2, pen);

    return true;
  }

  if (cmd == "CROSS")
  {
    float x, y, s;
    if (!parseFloatFromToken(nextToken(work), x) || !parseFloatFromToken(nextToken(work), y) ||
        !parseFloatFromToken(nextToken(work), s))
    {
      return false;
    }
    int pen = parsePenToken(nextToken(work));
    crossAt(x, y, s, pen);
    return true;
  }

  if (cmd == "CIRCLE")
  {
    float x, y, r;
    if (!parseFloatFromToken(nextToken(work), x) || !parseFloatFromToken(nextToken(work), y) ||
        !parseFloatFromToken(nextToken(work), r))
    {
      return false;
    }
    int pen = parsePenToken(nextToken(work));
    circleCentered(x, y, r, pen, defaultCircleSegments);
    return true;
  }

  if (cmd == "RECT")
  {
    float x, y, w, h;
    if (!parseFloatFromToken(nextToken(work), x) || !parseFloatFromToken(nextToken(work), y) ||
        !parseFloatFromToken(nextToken(work), w) || !parseFloatFromToken(nextToken(work), h))
    {
      return false;
    }
    int pen = parsePenToken(nextToken(work));
    rectCentered(x, y, w, h, pen);
    return true;
  }

  if (cmd == "TRI")
  {
    float x, y, s;
    if (!parseFloatFromToken(nextToken(work), x) || !parseFloatFromToken(nextToken(work), y) ||
        !parseFloatFromToken(nextToken(work), s))
    {
      return false;
    }
    int pen = parsePenToken(nextToken(work));
    triangleCentered(x, y, s, pen);
    return true;
  }

  if (cmd == "STAR")
  {
    float x, y, ro, ri;
    if (!parseFloatFromToken(nextToken(work), x) || !parseFloatFromToken(nextToken(work), y) ||
        !parseFloatFromToken(nextToken(work), ro) || !parseFloatFromToken(nextToken(work), ri))
    {
      return false;
    }
    int pen = parsePenToken(nextToken(work));
    starCentered(x, y, ro, ri, pen);
    return true;
  }

  if (cmd == "BOXTEXT")
  {
    float x1, y1, x2, y2;
    if (!parseFloatFromToken(nextToken(work), x1) || !parseFloatFromToken(nextToken(work), y1) ||
        !parseFloatFromToken(nextToken(work), x2) || !parseFloatFromToken(nextToken(work), y2))
    {
      return false;
    }

    String rest = restTokens(work);
    rest.trim();
    int pen = currentPen;

    int lastSpace = rest.lastIndexOf(' ');
    if (lastSpace > 0)
    {
      String maybePen = rest.substring(lastSpace + 1);
      String upperPen = maybePen;
      upperPen.toUpperCase();
      if (upperPen == "P1" || upperPen == "P2" || upperPen == "1" || upperPen == "2")
      {
        pen = parsePenToken(maybePen);
        rest = rest.substring(0, lastSpace);
        rest.trim();
      }
    }

    if (rest.length() == 0)
    {
      return false;
    }

    textInBox(x1, y1, x2, y2, rest, pen);
    return true;
  }

  Serial.print(F("ERR: unknown CMD -> "));
  Serial.println(cmd);
  return false;
}

void setup()
{
  Serial.begin(115200);

  for (int i = 0; i < stepperCount; i++)
  {
    pinMode(dirPin[i], OUTPUT);
    pinMode(stepPin[i], OUTPUT);
    pinMode(enPin[i], OUTPUT);
    digitalWrite(enPin[i], HIGH);
  }

  pinMode(stepControlPin, OUTPUT);
  // setStepModeFullStep();
  setStepModeMicro();

  for (int i = 0; i < stepperCount; i++)
  {
    st[i] = new AccelStepper(AccelStepper::DRIVER, stepPin[i], dirPin[i]);
    st[i]->setMinPulseWidth(3);
    st[i]->setMaxSpeed(travelMaxSpeedSteps);
    st[i]->setAcceleration(travelAccelSteps);
  }

  xySteppers.addStepper(*st[axisX]);
  xySteppers.addStepper(*st[axisY]);
  syncStepperPositionsToCurrentHead();

  penServo.attach(servoPin);
  penUp();
  enableMotor(false);

  delay(500);

  printReachableRange();
  printDrawArea();
}

void loop()
{
  while (Serial.available() > 0)
  {
    char c = (char)Serial.read();
    if (c == '\n')
    {
      if (serialLine.length() > 0)
      {
        bool ok = handleCmdLine(serialLine);
        if (ok)
        {
          Serial.println(F("OK"));
        }
        serialLine = "";
      }
    }
    else if (c != '\r')
    {
      serialLine += c;
    }
  }
}