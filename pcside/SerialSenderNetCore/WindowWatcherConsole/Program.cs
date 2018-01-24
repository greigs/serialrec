using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WindowWatcher;

namespace WindowWatcherConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var ww = new WindowWatcherMicrosoft();

            while (true)
            {
                try
                {
                    var activeWindow = ww.GetActiveWindowInfo();
                    Console.WriteLine(activeWindow.Process.FileName);
                }
                catch
                {
                    
                }
                
                Thread.Sleep(1000);
            }
        }
    }
}
