// USB Virtual Serial Receive Speed Benchmark
//
// This program receives data using Serial.readBytes()
// to read 500 bytes at a time and attempts to emulate
// overhead of doing work which requires 1 microsecond
// to "use" each byte.  The purpose is to measure the
// data rate when the processor spends much of the CPU
// time performing other work.


// use one of these to define
// the USB virual serial name
//
#define USBSERIAL Serial      // Arduino Leonardo, Teensy, Fubarino
//#define USBSERIAL SerialUSB   // Arduino Due, Maple


void setup() {
  USBSERIAL.begin(115200);
  USBSERIAL.setTimeout(0);
  pinMode(2, OUTPUT);  // frequency is kbytes/sec
}

byte pinstate=LOW;

void loop() {
  char buf[500];
  int count=0;
  int n;
  
  // receive 500 bytes, using Serial.readBytes
  // as many times as necessary until all 500
  while (count < 500) {
    n = USBSERIAL.readBytes(buf+count, 500-count);
    count = count + n;
  }
  
  // toggle pin 2, so the frequency is kbytes/sec
  if (pinstate == LOW) {
    digitalWrite(2, HIGH);
    pinstate = HIGH;
  } else {
    digitalWrite(2, LOW);
    pinstate = LOW;
  }
  
  // Delay for 500 microseconds, to simulate doing
  // something useful with the received data
  unsigned long beginMicros = micros();
  while (micros() - beginMicros <= 500) ; // wait 500 us
}
