// ---------------- Cross-Platform Serial Configuration ----------------
#if defined(__AVR_ATmega2560__)
// Arduino Mega 2560: Use hardware serial port Serial1
// Sensor TX connects to Mega Pin 18
// Sensor RX connects to Mega Pin 19
#define sensorSerial Serial1

#elif defined(ESP32)
// ESP32: Manually instantiate hardware serial port; compatible with all models including C3/S2/S3/WROOM, etc.
#include <HardwareSerial.h>
HardwareSerial mySensorSerial(1);  // Consistently use internal UART 1
#define sensorSerial mySensorSerial

#else
// Arduino UNO: Software serial port
// Sensor TX connects to UNO Pin 3
// Sensor RX connects to UNO Pin 2
#include <SoftwareSerial.h>
SoftwareSerial mySerial(2, 3);
#define sensorSerial mySerial
#endif
// ------------------------------------------------

uint8_t Com[8] = { 0x01, 0x03, 0x01, 0xF4, 0x00, 0x04, 0x04, 0x07 };   //Wind speed and direction
uint8_t Com1[8] = { 0x01, 0x03, 0x01, 0xF8, 0x00, 0x03, 0x85, 0xC6 };  //Temperature, humidity, noise
uint8_t Com2[8] = { 0x01, 0x03, 0x01, 0xFE, 0x00, 0x02, 0xA4, 0x07 };  //Light
uint8_t Com3[8] = { 0x01, 0x03, 0x01, 0xFB, 0x00, 0x03, 0x75, 0xC6 };  //PM2.5, PM10, atmospheric pressure
float tem, hum, ws, ap, db;
int wd, wdangle, pm2_5, pm10;
uint32_t lux;

void setup() {
  Serial.begin(115200);

  // Print a line of plain English text for testing. If this line appears clearly and without garbled characters,
  // it indicates that communication between the computer and the development board is fully functional! Serial.println("");
  Serial.println("--- System Start ---");

// Initialize the sensor serial port for different development boards
// (The sensor's baud rate must remain at 4800—do not change it)
#if defined(ESP32)
  // ESP32 specific pins: RX connected to 16, TX connected to 17;
  // pins can be customized based on the specific board model
  sensorSerial.begin(4800, SERIAL_8N1, 16, 17);
#else
  // UNO and Mega 2560
  sensorSerial.begin(4800);
#endif
}

void loop() {
  readHumiture_Noise();
  Serial.print("TEM = ");
  Serial.print(tem, 1);
  Serial.print("°C  ");
  Serial.print("HUM = ");
  Serial.print(hum, 1);
  Serial.print("%RH  ");
  Serial.print("dbValue = ");
  Serial.print(db, 1);
  Serial.print("dB  ");
  readLight();
  Serial.print("Lux = ");
  Serial.print(lux);
  Serial.println("(lux)  ");

  readPM2_5_10_AtmosphericPressure();
  Serial.print("PM2.5 = ");
  Serial.print(pm2_5);
  Serial.print("ug/m³  ");
  Serial.print("PM10 = ");
  Serial.print(pm10);
  Serial.print("ug/m³ ");
  Serial.print("AP = ");
  Serial.print(ap, 1);
  Serial.println("kPa ");

  readWindSpeed_WindDirection();
  Serial.print("Wind Speed = ");
  Serial.print(ws, 1);
  Serial.print("m/s  ");
  Serial.print("Wind Direction = ");
  Serial.print(wd);
  Serial.print(" WindDirection_Angle = ");
  Serial.print(wdangle);
  Serial.println("° ");
  Serial.println(" ");
  delay(2000);
}

void readWindSpeed_WindDirection(void) {
  uint8_t Data[12] = { 0 };
  uint8_t ch = 0;
  bool flag = 1;
  long timeStart = millis();
  long timeStart1 = 0;
  while (flag) {

    if ((millis() - timeStart1) > 100) {
      while (sensorSerial.available() > 0) {
        sensorSerial.read();
      }
      sensorSerial.write(Com, 8);
      timeStart1 = millis();
    }

    if ((millis() - timeStart) > 1000) {
      Serial.println("Time out");
      return;
    }

    if (readN(&ch, 1) == 1) {
      if (ch == 0x01) {
        Data[0] = ch;
        if (readN(&ch, 1) == 1) {
          if (ch == 0x03) {
            Data[1] = ch;
            if (readN(&ch, 1) == 1) {
              if (ch == 0x08) {
                Data[2] = ch;
                if (readN(&Data[3], 10) == 10) {
                  if (CRC16_2(Data, 11) == (Data[11] * 256 + Data[12])) {
                    ws = (Data[3] * 256 + Data[4]) / 100.00;
                    wd = Data[7] * 256 + Data[8];
                    wdangle = Data[9] * 256 + Data[10];
                    flag = 0;
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}


void readHumiture_Noise(void) {
  uint8_t Data[12] = { 0 };
  uint8_t ch = 0;
  bool flag = 1;
  long timeStart = millis();
  long timeStart1 = 0;
  while (flag) {

    if ((millis() - timeStart1) > 100) {
      while (sensorSerial.available() > 0) {
        sensorSerial.read();
      }
      sensorSerial.write(Com1, 8);
      timeStart1 = millis();
    }

    if ((millis() - timeStart) > 1000) {
      Serial.println("Time out1");
      return;
    }

    if (readN(&ch, 1) == 1) {
      if (ch == 0x01) {
        Data[0] = ch;
        if (readN(&ch, 1) == 1) {
          if (ch == 0x03) {
            Data[1] = ch;
            if (readN(&ch, 1) == 1) {
              if (ch == 0x06) {
                Data[2] = ch;
                if (readN(&Data[3], 8) == 8) {
                  if (CRC16_2(Data, 9) == (Data[9] * 256 + Data[10])) {
                    hum = (Data[3] * 256 + Data[4]) / 10.00;
                    tem = (Data[5] * 256 + Data[6]) / 10.00;
                    db = (Data[7] * 256 + Data[8]) / 10.00;
                    flag = 0;
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}


void readLight(void) {
  uint8_t Data[15] = { 0 };
  uint8_t ch = 0;
  long timeStart = millis();
  long timeStart1 = 0;
  bool flag = 1;
  while (flag) {

    if ((millis() - timeStart1) > 100) {
      while (sensorSerial.available() > 0) {
        sensorSerial.read();
      }
      sensorSerial.write(Com2, 8);
      timeStart1 = millis();
    }


    if ((millis() - timeStart) > 1000) {
      Serial.println("Time out2");
      return;
    }

    if (readN(&ch, 1) == 1) {
      if (ch == 0x01) {
        Data[0] = ch;
        if (readN(&ch, 1) == 1) {
          if (ch == 0x03) {
            Data[1] = ch;
            if (readN(&ch, 1) == 1) {
              if (ch == 0x04) {
                Data[2] = ch;
                if (readN(&Data[3], 6) == 6) {
                  if (CRC16_2(Data, 7) == (Data[7] * 256 + Data[8])) {
                    lux = Data[3] << 24 | Data[4] << 16 | Data[5] << 8 | Data[6];
                    flag = 0;
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}

void readPM2_5_10_AtmosphericPressure(void) {
  uint8_t Data[12] = { 0 };
  uint8_t ch = 0;
  long timeStart = millis();
  long timeStart1 = 0;
  bool flag = 1;
  while (flag) {

    if ((millis() - timeStart1) > 100) {
      while (sensorSerial.available() > 0) {
        sensorSerial.read();
      }
      sensorSerial.write(Com3, 8);
      timeStart1 = millis();
    }


    if ((millis() - timeStart) > 1000) {
      Serial.println("Time out3");
      return;
    }

    if (readN(&ch, 1) == 1) {
      if (ch == 0x01) {
        Data[0] = ch;
        if (readN(&ch, 1) == 1) {
          if (ch == 0x03) {
            Data[1] = ch;
            if (readN(&ch, 1) == 1) {
              if (ch == 0x06) {
                Data[2] = ch;
                if (readN(&Data[3], 8) == 8) {
                  if (CRC16_2(Data, 9) == (Data[9] * 256 + Data[10])) {
                    pm2_5 = Data[3] * 256 + Data[4];
                    pm10 = Data[5] * 256 + Data[6];
                    ap = (Data[7] * 256 + Data[8]) / 10.00;
                    flag = 0;
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}

uint8_t readN(uint8_t *buf, size_t len) {
  size_t offset = 0, left = len;
  int16_t Tineout = 500;
  uint8_t *buffer = buf;
  long curr = millis();
  while (left) {
    if (sensorSerial.available()) {
      buffer[offset] = sensorSerial.read();
      offset++;
      left--;
    }
    if (millis() - curr > Tineout) {
      break;
    }
  }
  return offset;
}

unsigned int CRC16_2(unsigned char *buf, int len) {
  unsigned int crc = 0xFFFF;
  for (int pos = 0; pos < len; pos++) {
    crc ^= (unsigned int)buf[pos];
    for (int i = 8; i != 0; i--) {
      if ((crc & 0x0001) != 0) {
        crc >>= 1;
        crc ^= 0xA001;
      } else {
        crc >>= 1;
      }
    }
  }

  crc = ((crc & 0x00ff) << 8) | ((crc & 0xff00) >> 8);
  return crc;
}