using System;

namespace WindowWatcherCore
{
    public interface IWindowWatcherService
    {
        string StartService();
        void StopService();
        event EventHandler<string> WindowSelected;
    }
}
