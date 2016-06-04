// USB Virtual Serial Receive Speed Benchmark
//
// This program receives data as rapidly as possible
// using Serial.available() and Serial.read() to read
// data 1 byte at a time.


// use one of these to define
// the USB virual serial name
//
#define USBSERIAL Serial      // Leonardo, Teensy, Fubarino
//#define USBSERIAL SerialUSB   // Arduino Due, Maple

void setup() {
  USBSERIAL.begin(115200);
  //USBSERIAL.begin();  // for Maple
  pinMode(2, OUTPUT);  // frequency is kbytes/sec
}

byte pinstate=LOW;

void loop() {
  char buf[500];
  int count=0;
  int n;
  
  // receive 500 bytes
  for (count=0; count < 500; count++) {
    while (!USBSERIAL.available()) ;
    buf[count] = USBSERIAL.read();
  }
  
  // toggle pin 2, so the frequency is kbytes/sec
  if (pinstate == LOW) {
    digitalWrite(2, HIGH);
    pinstate = HIGH;
  } else {
    digitalWrite(2, LOW);
    pinstate = LOW;
  }
}
