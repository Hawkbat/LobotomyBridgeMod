using System;

namespace WebSocketServer
{
    public interface IWebSocketLogger
    {
        void Log(string message);
        void Log(string message, Exception exception);
    }
}
