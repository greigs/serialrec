using System;
using System.Threading;
using WindowWatcher;
using WindowWatcherCore;

namespace TestNetCoreApp
{
    class Program
    {
        private static IWindowWatcherService _windowWatcherService;
        static void Main(string[] args)
        {
            _windowWatcherService = new WindowWatcherMicrosoft();
            _windowWatcherService.WindowSelected += WindowWatcherService_WindowSelected;
            _windowWatcherService.StartService();
            Thread.Sleep(TimeSpan.FromSeconds(10));
            _windowWatcherService.StopService();
        }

        private static void WindowWatcherService_WindowSelected(object sender, string e)
        {
            Console.WriteLine(e);
        }
    }
}
