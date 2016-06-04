OS = LINUX
#OS = MACOSX
#OS = WINDOWS

ifeq ($(OS), LINUX)
CC = gcc
CFLAGS = -Wall -O2 -DLINUX
TARGET = receive_test

else ifeq ($(OS), MACOSX)
CC = gcc
CFLAGS = -Wall -O2 -DMACOSX
TARGET = receive_test

else ifeq ($(OS), WINDOWS)
CC = i586-mingw32msvc-gcc
CFLAGS = -Wall -O2 -DWINDOWS
TARGET = receive_test.exe
endif

all: $(TARGET)

$(TARGET): receive_test.c Makefile
	$(CC) $(CFLAGS) -o $(TARGET) receive_test.c

clean:
	rm -f receive_test receive_test.exe

