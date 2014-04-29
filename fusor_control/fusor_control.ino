#include <Servo.h>
#include <PID_v1.h>

#define PUMP_OUTPUT             3
#define HV_OUTPUT               4
#define REGULATOR_OUTPUT        5
#define VOLTAGE_OUTPUT          6
#define PUMP_INPUT              7
#define HV_INPUT                8
#define SERVO_OUTPUT            9

#define REGULATOR_INPUT         A0
#define PRESSURE_INPUT          A1
#define VOLTAGE_INPUT           A2
#define CURRENT_INPUT           A3
#define COUNT_INPUT             A4

#define SET_REGULATOR_SETPOINT  1
#define SET_REGULATOR_TUNINGS   2
#define SET_PRESSURE_SETPOINT   3
#define SET_PRESSURE_TUNINGS    4
#define SET_PRESSURE_LIMITS     5
#define SET_PUMP_OUTPUT         6
#define SET_HV_OUTPUT           7
#define SET_VOLTAGE_OUTPUT      8

#define GET_PUMP_INPUT          9
#define GET_HV_INPUT            10
#define GET_PRESSURE_INPUT      11
#define GET_VOLTAGE_INPUT       12
#define GET_CURRENT_INPUT       13
#define GET_COUNT_INPUT         14

int buffer_index = 0;
double regulator_input_value, regulator_output_value, regulator_setpoint = 0,
pressure_input_value, pressure_output_value, pressure_setpoint = 0, last_pressure_output_value = 0;
unsigned long last_update = millis();
Servo pressure_servo;
PID regulator_pid(&regulator_input_value, &regulator_output_value, &regulator_setpoint, 0, 0, 0, DIRECT);
PID pressure_pid(&pressure_input_value, &pressure_output_value, &pressure_setpoint, 0, 0, 0, DIRECT);

void setup()
{
  pinMode(PUMP_OUTPUT, OUTPUT);
  pinMode(HV_OUTPUT, OUTPUT);
  pinMode(REGULATOR_OUTPUT, OUTPUT);
  pinMode(VOLTAGE_OUTPUT, OUTPUT);
  pinMode(PUMP_INPUT, INPUT_PULLUP);
  pinMode(HV_INPUT, INPUT_PULLUP);

  TCCR0B = TCCR0B & 0b11111000 | 0x01;

  pressure_servo.attach(SERVO_OUTPUT);
  regulator_pid.SetMode(AUTOMATIC);
  pressure_pid.SetMode(AUTOMATIC);

  Serial.begin(115200);
}

void loop()
{
  regulator_input_value = analogRead(REGULATOR_INPUT);
  regulator_pid.Compute();
  analogWrite(REGULATOR_OUTPUT, regulator_output_value);

  pressure_input_value = analogRead(PRESSURE_INPUT);
  pressure_pid.Compute();

  if (millis() - last_update >= 3000)
  {
    if (pressure_output_value > last_pressure_output_value)
    {
      pressure_servo.write(++last_pressure_output_value);
    } 
    else
    {
      pressure_servo.write(--last_pressure_output_value);
    }
    last_update = millis();
  }

  while (Serial.available())
  {
    switch (Serial.parseInt())
    {
    case SET_REGULATOR_SETPOINT:
      pressure_setpoint = Serial.parseInt();
      break;
    case SET_REGULATOR_TUNINGS:
      regulator_pid.SetTunings(Serial.parseInt(), Serial.parseInt(), Serial.parseInt());
      break;
    case SET_PRESSURE_SETPOINT:
      pressure_setpoint = Serial.parseInt();
      break;
    case SET_PRESSURE_TUNINGS:
      pressure_pid.SetTunings(Serial.parseInt(), Serial.parseInt(), Serial.parseInt());
      break;
    case SET_PRESSURE_LIMITS:
      pressure_pid.SetOutputLimits(Serial.parseInt(), Serial.parseInt());
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
