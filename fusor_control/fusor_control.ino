#include <stdlib.h>
#include <Servo.h>
#include <PID_v1.h>

int i = 0;
double regulator_input_value,
  regulator_output_value,
  regulator_setpoint = 960,
  pressure_input_value,
  pressure_output_value,
  pressure_setpoint = 0,
  pump_output = 2,
  regulator_output = 3,
  hv_output = 4,
  voltage_output = 5,
  pump_status_input = 6,
  hv_status_input = 7,
  servo_output = 9,
  regulator_input = 0,
  voltage_input = 1,
  current_input = 2,
  pressure_input = 3,
  scaler_input = 4;
char buffer[64];
Servo pressure_servo;
PID regulator_pid(&regulator_input_value, &regulator_output_value, &regulator_setpoint, 0, 0.2, 0, DIRECT);
PID pressure_pid(&pressure_input_value, &pressure_output_value, &pressure_setpoint, 0, 0.2, 0, DIRECT);

void setup()
{
  pinMode(pump_output, OUTPUT);
  pinMode(regulator_output, OUTPUT);
  pinMode(voltage_output, OUTPUT);
  pinMode(hv_output, OUTPUT);
  pinMode(hv_status_input, INPUT_PULLUP);
  pinMode(pump_status_input, INPUT_PULLUP);
  pressure_servo.attach(servo_output);
  pressure_pid.SetOutputLimits(0, 90);
  regulator_pid.SetMode(AUTOMATIC);
  pressure_pid.SetMode(AUTOMATIC);
  Serial.begin(57600);
}

void setRelay(int pin, const char *string)
{
  if (strtol(string, NULL, 10))
  {
    digitalWrite(pin, HIGH);
  }
  else
  {
    digitalWrite(pin, LOW);
  }
}

void loop()
{
  if (Serial.available())
  {
    buffer[i] = Serial.read();
    if (i > 0 && buffer[i] == '\n')
    {
      if (!memcmp(buffer, "get hv status", 13))
      {
        Serial.println(digitalRead(hv_status_input));
      }
      else if (!memcmp(buffer, "get voltage", 11))
      {
        Serial.println(analogRead(voltage_input));
      }
      else if (!memcmp(buffer, "get current", 11))
      {
        Serial.println(analogRead(current_input));
      }
      else if (!memcmp(buffer, "get pump status", 15))
      {
        Serial.println(digitalRead(pump_status_input));
      }
      else if (!memcmp(buffer, "get pressure", 12))
      {
        Serial.println(analogRead(pressure_input));
      }
      else if (!memcmp(buffer, "get scaler rate", 15))
      {
        Serial.println(analogRead(scaler_input));
      }
      else if (!memcmp(buffer, "set hv ", 7))
      {
        setRelay(hv_output, &buffer[7]);
      }
      else if (!memcmp(buffer, "set voltage ", 12))
      {
        analogWrite(voltage_output, strtol(&buffer[12], NULL, 10));
      }
      else if (!memcmp(buffer, "set pump ", 9))
      {
        setRelay(pump_output, &buffer[9]);
      }
      else if (!memcmp(buffer, "set pressure ", 13))
      {
        pressure_setpoint = strtol(&buffer[13], NULL, 10);
      }
      i = -1;
    }
    else if ( i == 64 )
    {
      i = -1;
    }
    i++;
  }
  regulator_input_value = analogRead(regulator_input);
  regulator_pid.Compute();
  analogWrite(regulator_output, regulator_output_value);
  pressure_input_value = analogRead(pressure_input);
  pressure_pid.Compute();
  pressure_servo.write(pressure_output_value);
}
