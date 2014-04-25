#include <Servo.h>
#include <PID_v1.h>

#define BUFFER_SIZE         6

#define PUMP_OUTPUT         2
#define VOLTAGE_OUTPUT      3
#define HV_OUTPUT           4
#define REGULATOR_OUTPUT    5
#define PUMP_STATUS_INPUT   6
#define HV_STATUS_INPUT     7
#define SERVO_OUTPUT        9

#define REGULATOR_INPUT     A0
#define PRESSURE_INPUT      A1
#define VOLTAGE_INPUT       A2
#define CURRENT_INPUT       A3
#define SCALER_INPUT        A4

short buffer_index = 0;
double
regulator_input_value, regulator_output_value, regulator_setpoint = 770,
pressure_input_value, pressure_output_value, pressure_setpoint = 999, last_pressure_output_value = 60;
unsigned long last_update = millis();
char buffer[BUFFER_SIZE];
Servo pressure_servo;
PID regulator_pid(&regulator_input_value, &regulator_output_value, &regulator_setpoint, 0, 0.1, 0, DIRECT);
PID pressure_pid(&pressure_input_value, &pressure_output_value, &pressure_setpoint, 3, 0, 0, DIRECT);

void setPwmFrequency(const short pin, const short divisor)
{
  byte setting;

  if (pin == 5 || pin == 6 || pin == 9 || pin == 10)
  {
    switch (divisor)
    {
    case 1: 
      setting = 0x01; 
      break;
    case 8: 
      setting = 0x02; 
      break;
    case 64: 
      setting = 0x03; 
      break;
    case 256: 
      setting = 0x04; 
      break;
    case 1024: 
      setting = 0x05;
      break;
    default:
      return;
    }

    if (pin == 5 || pin == 6)
    {
      TCCR0B = TCCR0B & 0b11111000 | setting;
    }
    else
    {
      TCCR1B = TCCR1B & 0b11111000 | setting;
    }
  }
  else
  {
    switch (divisor)
    {
    case 1: 
      setting = 0x01; 
      break;
    case 8: 
      setting = 0x02; 
      break;
    case 32: 
      setting = 0x03; 
      break;
    case 64: 
      setting = 0x04; 
      break;
    case 128: 
      setting = 0x05; 
      break;
    case 256: 
      setting = 0x06; 
      break;
    case 1024: 
      setting = 0x07; 
      break;
    default: 
      return;
    }
    TCCR2B = TCCR2B & 0b11111000 | setting;
  }
}

void setup()
{
  pinMode(PUMP_OUTPUT, OUTPUT);
  pinMode(VOLTAGE_OUTPUT, OUTPUT);
  pinMode(HV_OUTPUT, OUTPUT);
  pinMode(REGULATOR_OUTPUT, OUTPUT);
  pinMode(HV_STATUS_INPUT, INPUT_PULLUP);
  pinMode(PUMP_STATUS_INPUT, INPUT_PULLUP);

  setPwmFrequency(VOLTAGE_OUTPUT, 8);
  setPwmFrequency(REGULATOR_OUTPUT, 1);

  pressure_servo.attach(SERVO_OUTPUT);
  regulator_pid.SetMode(AUTOMATIC);
  pressure_pid.SetMode(AUTOMATIC);
  pressure_pid.SetOutputLimits(0, 60);

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
    buffer[buffer_index] = Serial.read();
    buffer_index++;
    if (buffer_index > 1 && buffer[buffer_index - 1] == '\n')
    {
      switch (atol(buffer))
      {
      case 0:
        Serial.println(!digitalRead(PUMP_STATUS_INPUT));
        break;
      case 1:
        Serial.println(analogRead(PRESSURE_INPUT));
        break;
      case 2:
        Serial.println(!digitalRead(HV_STATUS_INPUT));
        break;
      case 3:
        Serial.println(analogRead(VOLTAGE_INPUT));
        break;
      case 4:
        Serial.println(analogRead(CURRENT_INPUT));
        break;
      case 5:
        Serial.println(analogRead(SCALER_INPUT));
        break;
      case 6:
        digitalWrite(PUMP_OUTPUT, atol(&buffer[2]) ? HIGH : LOW);
        break;
      case 7:
        pressure_setpoint = atol(&buffer[2]);
        break;
      case 8:
        digitalWrite(HV_OUTPUT, atol(&buffer[2]) ? HIGH : LOW);
        break;
      case 9:
        analogWrite(VOLTAGE_OUTPUT, atol(&buffer[2]));
      } 
      buffer_index = 0;
    } 
    else if (buffer_index >= BUFFER_SIZE)
    {
      buffer_index = 0;
    }
  }
}
