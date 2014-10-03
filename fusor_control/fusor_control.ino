#include <Servo.h>
#include <PID_v1.h>

enum pins
{
  HV_OUTPUT = 22,
  PUMP_OUTPUT = 23,
  HV_INPUT = 24,
  PUMP_INPUT = 25,
  
  SERVO_OUTPUT = 2,
  REGULATOR_OUTPUT = 6,
  VOLTAGE_OUTPUT = 7,
  
  REGULATOR_INPUT = A0,
  VOLTAGE_INPUT = A1,
  CURRENT_INPUT = A2,
  PRESSURE_INPUT = A3,
  COUNT_INPUT = A4,
};

enum commands
{
  SET_REGULATOR_SETPOINT = 1,
  SET_REGULATOR_TUNINGS,
  SET_PRESSURE_SETPOINT,
  SET_PRESSURE_TUNINGS,
  SET_PRESSURE_LIMITS,
  SET_PUMP_OUTPUT,
  SET_HV_OUTPUT,
  SET_VOLTAGE_OUTPUT,
  
  GET_PUMP_INPUT,
  GET_HV_INPUT,
  GET_PRESSURE_INPUT,
  GET_VOLTAGE_INPUT,
  GET_CURRENT_INPUT,
  GET_COUNT_INPUT,
};

double RegulatorInput, RegulatorOutput, RegulatorSetpoint = 0,
PressureInput, PressureOutput, PressureSetpoint = 0, LastPressureOutput = 0;
unsigned long LastUpdate = millis();
Servo PressureServo;
PID RegulatorLoop(&RegulatorInput, &RegulatorOutput, &RegulatorSetpoint, 0, 0, 0, DIRECT);
PID PressureLoop(&PressureInput, &PressureOutput, &PressureSetpoint, 0, 0, 0, DIRECT);

void setup()
{
  pinMode(PUMP_OUTPUT, OUTPUT);
  pinMode(HV_OUTPUT, OUTPUT);
  pinMode(REGULATOR_OUTPUT, OUTPUT);
  pinMode(VOLTAGE_OUTPUT, OUTPUT);
  pinMode(PUMP_INPUT, INPUT_PULLUP);
  pinMode(HV_INPUT, INPUT_PULLUP);
  
  analogReadResolution(12);
  analogWriteResolution(12);
  
  PressureServo.attach(SERVO_OUTPUT);
  
  RegulatorLoop.SetOutputLimits(0, 4095);
  RegulatorLoop.SetMode(AUTOMATIC);
  PressureLoop.SetMode(AUTOMATIC);
  
  Serial.begin(115200);
}

void loop()
{
  RegulatorInput = analogRead(REGULATOR_INPUT);
  RegulatorLoop.Compute();
  analogWrite(REGULATOR_OUTPUT, RegulatorOutput);
  
  PressureInput = analogRead(PRESSURE_INPUT);
  PressureLoop.Compute();
  
  if (millis() - LastUpdate >= 250)
  {
    if (PressureOutput > LastPressureOutput)
    {
      PressureServo.write(++LastPressureOutput);
    } 
    else
    {
      PressureServo.write(--LastPressureOutput);
    }
    LastUpdate = millis();
  }
  
  while (Serial.available())
  {
    switch (Serial.parseInt())
    {
    case SET_REGULATOR_SETPOINT:
      RegulatorSetpoint = Serial.parseInt();
      break;
    case SET_REGULATOR_TUNINGS:
      RegulatorLoop.SetTunings(Serial.parseFloat(), Serial.parseFloat(), Serial.parseFloat());
      break;
    case SET_PRESSURE_SETPOINT:
      PressureSetpoint = Serial.parseInt();
      break;
    case SET_PRESSURE_TUNINGS:
      PressureLoop.SetTunings(Serial.parseFloat(), Serial.parseFloat(), Serial.parseFloat());
      break;
    case SET_PRESSURE_LIMITS:
      PressureLoop.SetOutputLimits(Serial.parseInt(), Serial.parseInt());
      break;
    case SET_PUMP_OUTPUT:
      digitalWrite(PUMP_OUTPUT, Serial.parseInt() ? HIGH : LOW);
      break;
    case SET_HV_OUTPUT:
      digitalWrite(HV_OUTPUT, Serial.parseInt() ? HIGH : LOW);
      break;
    case SET_VOLTAGE_OUTPUT:
      analogWrite(VOLTAGE_OUTPUT, Serial.parseInt());
      break;
    case GET_PUMP_INPUT:
      Serial.println(!digitalRead(PUMP_INPUT));
      break;
    case GET_HV_INPUT:
      Serial.println(!digitalRead(HV_INPUT));
      break;
    case GET_PRESSURE_INPUT:
      Serial.println(analogRead(PRESSURE_INPUT));
      break;
    case GET_VOLTAGE_INPUT:
      Serial.println(analogRead(VOLTAGE_INPUT));
      break;
    case GET_CURRENT_INPUT:
      Serial.println(analogRead(CURRENT_INPUT));
      break;
    case GET_COUNT_INPUT:
      Serial.println(analogRead(COUNT_INPUT));
    }
  }
}
