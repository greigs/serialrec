/*  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, version 3 of the License.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <sys/time.h>
#include <fcntl.h>
#include <unistd.h>
#include <errno.h>

// One of these must be defined, usually via the Makefile
//#define MACOSX
//#define LINUX
#define WINDOWS

#if defined(MACOSX) || defined(LINUX)
#include <termios.h>
#include <sys/select.h>
#define PORTTYPE int
#define BAUD B115200
#if defined(LINUX)
#include <sys/ioctl.h>
#include <linux/serial.h>
#endif
#elif defined(WINDOWS)
#include <windows.h>
#define PORTTYPE HANDLE
#define BAUD 115200
#else
#error "You must define the operating system\n"
#endif

// function prototypes
PORTTYPE open_port_and_set_baud_or_die(const char *name, long baud);
int transmit_bytes(PORTTYPE port, const char *data, int len);
void close_port(PORTTYPE port);
void delay(double sec);
void die(const char *format, ...) __attribute__ ((format (printf, 1, 2)));

char buffer[100000];

int main(int argc, char **argv)
{
	PORTTYPE port;
	struct timeval begin, end;
	int size = 30000;
	int i, n, sent, count=0;
	double elapsed, speed, sum=0.0;

	if (argc < 2) die("Usage: receive_test <comport>\n       receive_test <blocksize> <comport>\n");
	if (argc == 2) {
		port = open_port_and_set_baud_or_die(argv[1], BAUD);
		printf("port %s opened\n", argv[1]);
	} else {
		if (sscanf(argv[1], "%d", &size) != 1 ||
		  size < 1 || size > sizeof(buffer)) {
			die("Usage: receive_test <blocksize> <comport>\n");
		}
		port = open_port_and_set_baud_or_die(argv[2], BAUD);
		printf("port %s opened\n", argv[2]);
	}

	for (i=0; i < size; i++) {
		buffer[i] = rand();
	}
	gettimeofday(&begin, NULL);
	for (count = 0; count < 20; count++) {
		for (sent = 0; sent < 50000; sent += size) {
			n = transmit_bytes(port, buffer, size);
			if (n != size) die("errors transmitting data\n");
		}
		gettimeofday(&end, NULL);
		if (count < 5) {
			printf("ignoring startup buffering...\n");
		} else {
			elapsed = (double)(end.tv_sec - begin.tv_sec);
			elapsed += (double)(end.tv_usec - begin.tv_usec) / 1000000.0;
			speed = (double)sent / elapsed;
			printf("Bytes per second = %.0lf\n", speed);
			sum += speed;
		}
		begin.tv_sec = end.tv_sec;
		begin.tv_usec = end.tv_usec;
	}
	close_port(port);
	speed = sum / 15.0;
	printf("Average bytes per second = %.0lf\n", speed);
	return 0;
}


/**********************************/
/*  Serial Port Functions         */
/**********************************/


PORTTYPE open_port_and_set_baud_or_die(const char *name, long baud)
{
	PORTTYPE fd;
#if defined(MACOSX)
	struct termios tinfo;
	fd = open(name, O_RDWR | O_NONBLOCK);
	if (fd < 0) die("unable to open port %s\n", name);
	if (tcgetattr(fd, &tinfo) < 0) die("unable to get serial parms\n");
	cfmakeraw(&tinfo);
	if (cfsetspeed(&tinfo, baud) < 0) die("error in cfsetspeed\n");
	tinfo.c_cflag |= CLOCAL;
	if (tcsetattr(fd, TCSANOW, &tinfo) < 0) die("unable to set baud rate\n");
	fcntl(fd, F_SETFL, fcntl(fd, F_GETFL) & ~O_NONBLOCK);
#elif defined(LINUX)
	struct termios tinfo;
	struct serial_struct kernel_serial_settings;
	int r;
	fd = open(name, O_RDWR);
	if (fd < 0) die("unable to open port %s\n", name);
	if (tcgetattr(fd, &tinfo) < 0) die("unable to get serial parms\n");
	cfmakeraw(&tinfo);
	if (cfsetspeed(&tinfo, baud) < 0) die("error in cfsetspeed\n");
	if (tcsetattr(fd, TCSANOW, &tinfo) < 0) die("unable to set baud rate\n");
	r = ioctl(fd, TIOCGSERIAL, &kernel_serial_settings);
	if (r >= 0) {
		kernel_serial_settings.flags |= ASYNC_LOW_LATENCY;
		r = ioctl(fd, TIOCSSERIAL, &kernel_serial_settings);
		if (r >= 0) printf("set linux low latency mode\n");
	}
#elif defined(WINDOWS)
	COMMCONFIG cfg;
	COMMTIMEOUTS timeout;
	DWORD n;
	char portname[256];
	int num;
	if (sscanf(name, "COM%d", &num) == 1) {
		sprintf(portname, "\\\\.\\COM%d", num); // Microsoft KB115831
	} else {
		strncpy(portname, name, sizeof(portname)-1);
		portname[n-1] = 0;
	}
	fd = CreateFile(portname, GENERIC_READ | GENERIC_WRITE,
		0, 0, OPEN_EXISTING, 0, NULL);
	if (fd == INVALID_HANDLE_VALUE) die("unable to open port %s\n", name);
	GetCommConfig(fd, &cfg, &n);
	//cfg.dcb.BaudRate = baud;
	cfg.dcb.BaudRate = 115200;
	cfg.dcb.fBinary = TRUE;
	cfg.dcb.fParity = FALSE;
	cfg.dcb.fOutxCtsFlow = FALSE;
	cfg.dcb.fOutxDsrFlow = FALSE;
	cfg.dcb.fOutX = FALSE;
	cfg.dcb.fInX = FALSE;
	cfg.dcb.fErrorChar = FALSE;
	cfg.dcb.fNull = FALSE;
	cfg.dcb.fRtsControl = RTS_CONTROL_ENABLE;
	cfg.dcb.fAbortOnError = FALSE;
	cfg.dcb.ByteSize = 8;
	cfg.dcb.Parity = NOPARITY;
	cfg.dcb.StopBits = ONESTOPBIT;
	cfg.dcb.fDtrControl = DTR_CONTROL_ENABLE;
	SetCommConfig(fd, &cfg, n);
	GetCommTimeouts(fd, &timeout);
	timeout.ReadIntervalTimeout = 0;
	timeout.ReadTotalTimeoutMultiplier = 0;
	timeout.ReadTotalTimeoutConstant = 1000;
	timeout.WriteTotalTimeoutConstant = 0;
	timeout.WriteTotalTimeoutMultiplier = 0;
	SetCommTimeouts(fd, &timeout);
#endif
	return fd;

}

int transmit_bytes(PORTTYPE port, const char *data, int len)
{
#if defined(MACOSX) || defined(LINUX)
	return write(port, data, len);
#elif defined(WINDOWS)
	DWORD n;
	BOOL r;
	r = WriteFile(port, data, len, &n, NULL);
	if (!r) return 0;
	return n;
#endif
}


void close_port(PORTTYPE port)
{
#if defined(MACOSX) || defined(LINUX)
	close(port);
#elif defined(WINDOWS)
	CloseHandle(port);
#endif
}


/**********************************/
/*  Misc. Functions               */
/**********************************/

void delay(double sec)
{
#if defined(MACOSX) || defined(LINUX)
	usleep(sec * 1000000);
#elif defined(WINDOWS)
	Sleep(sec * 1000);
#endif
}


void die(const char *format, ...)
{
	va_list args;
	va_start(args, format);
	vfprintf(stderr, format, args);
	exit(1);
}

