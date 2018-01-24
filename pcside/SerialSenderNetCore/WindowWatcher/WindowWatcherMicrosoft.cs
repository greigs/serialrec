using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WindowWatcherCore;

namespace WindowWatcher
{
    public class WindowWatcherMicrosoft : IWindowWatcherService
    {
        private bool running = false;
        private Task serviceTask;
        private string activeAppPath;

        public string StartService()
        {
            string result = null;
            int tries = 0;
            bool success = false;
            while (!success && tries < 10)
            {
                try
                {
                    result = GetActiveWindowInfo().Process.FileName;
                    success = true;
                }
                catch
                {
                    tries++;
                }
            }

            running = true;
            serviceTask = new Task(() =>
            {
                while (running)
                {
                    try
                    {
                        var activeWindow = GetActiveWindowInfo();
                        if (activeWindow.Process.FileName != activeAppPath)
                        {
                            WindowSelected?.Invoke(this, activeWindow.Process.FileName);
                        }
                        activeAppPath = activeWindow.Process.FileName;
                    }
                    catch
                    {

                    }

                    Thread.Sleep(50);
                }
            });
            serviceTask.Start();
            return result;
        }

        public void StopService()
        {
            running = false;
            serviceTask.Wait();
        }

        private WindowInfo GetActiveWindowInfo()
        {
            IntPtr hWnd = WindowWatcherInterop.GetForegroundWindow();
            WindowWatcherInterop.GetWindowThreadProcessId(hWnd, out var procId);
            var proc = Process.GetProcessById((int)procId);
            return new WindowInfo()
            {
                Process = proc.MainModule
            };
        }

        internal static class WindowWatcherInterop
        {
            [DllImport("user32.dll", SetLastError = true)]
            internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();
        }

        public event EventHandler<string> WindowSelected;
    }
}