using System;
using WindowWatcher;

namespace SerialSenderNetCore
{
    public class StartupConfig
    {
        public Type OutputStreamFactory { get; set; } =  typeof(SerialStreamFactory);
        public Type WindowWatcherService { get; set; } = typeof(WindowWatcherMicrosoft);
    }
}