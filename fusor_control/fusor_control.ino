#include <stdlib.h>
#include <Servo.h>
#include <PID_v1.h>

const short
pump_output = 2,
voltage_output = 3,
hv_output = 4,
regulator_output = 5,
pump_status_input = 6,
hv_status_input = 7,
servo_output = 9,
regulator_input = A0,
pressure_input = A1,
voltage_input = A2,
current_input = A3,
scaler_input = A4;
short i = 0;
double
regulator_input_value, regulator_output_value, regulator_setpoint = 965, last_regulator_output_value = 0,
pressure_input_value, pressure_output_value, pressure_setpoint = 760, last_pressure_output_value = 60;
unsigned long last_update = millis();
char buffer[64];
Servo pressure_servo;
PID regulator_pid(&regulator_input_value, &regulator_output_value, &regulator_setpoint, 0, 0.1, 0, DIRECT);
PID pressure_pid(&pressure_input_value, &pressure_output_value, &pressure_setpoint, 3, 0, 0, DIRECT);

void setup() {
  pinMode(pump_output, OUTPUT);
  pinMode(voltage_output, OUTPUT);
  pinMode(hv_output, OUTPUT);
  pinMode(regulator_output, OUTPUT);
  pinMode(hv_status_input, INPUT_PULLUP);
  pinMode(pump_status_input, INPUT_PULLUP);

  TCCR0B = TCCR0B & 0b11111000 | 0x01;
  pressure_servo.attach(servo_output);
  regulator_pid.SetMode(AUTOMATIC);
  pressure_pid.SetMode(AUTOMATIC);
  pressure_pid.SetOutputLimits(0, 60);

  Serial.begin(57600);
}

void setRealy(const short output, const char *string) {
  if (strtol(string, NULL, 10)) {
    digitalWrite(output, HIGH);
  } 
  else {
    digitalWrite(output, LOW);
  } 
}

void loop() {
  regulator_input_value = analogRead(regulator_input);
  regulator_pid.Compute();
  analogWrite(regulator_output, regulator_output_value);

  pressure_input_value = analogRead(pressure_input);
  pressure_pid.Compute();

  if (millis() - last_update > 3000) {
    if (pressure_output_value > last_pressure_output_value) {
      pressure_servo.write(++last_pressure_output_value);
    } 
    else {
      pressure_servo.write(--last_pressure_output_value);
    }
    last_update = millis();
  }

  while (Serial.available()) {
    buffer[i] = Serial.read();
    i++;
    if (i > 1 && buffer[i - 1] == '\n') {
      if (!memcmp(buffer, "get pump status", 15)) {
        Serial.println((!digitalRead(pump_status_input)));
      } 
      else if (!memcmp(buffer, "get pressure", 12)) {
        Serial.println(analogRead(pressure_input));
      } 
      else if (!memcmp(buffer, "get hv status", 13)) {
        Serial.println((!digitalRead(hv_status_input)));
      } 
      else if (!memcmp(buffer, "get voltage", 11)) {
        Serial.println(analogRead(voltage_input));
      } 
      else if (!memcmp(buffer, "get current", 11)) {
        Serial.println(analogRead(current_input));
      } 
      else if (!memcmp(buffer, "get scaler rate", 15)) {
        Serial.println(analogRead(scaler_input));
      } 
      else if (!memcmp(buffer, "set pump ", 9)) {
        setRealy(pump_output, &buffer[9]);
      } 
      else if (!memcmp(buffer, "set pressure ", 13)) {
        pressure_setpoint = strtol(&buffer[13], NULL, 10);
      }
      else if (!memcmp(buffer, "set hv ", 7)) {
        setRealy(hv_output, &buffer[7]);
      } 
      else if (!memcmp(buffer, "set voltage ", 12)) {
        analogWrite(voltage_output, strtol(&buffer[12], NULL, 10));
      } 
      i = 0;
    } 
    else if (i >= 64) {
      i = 0;
    }
  }
}







