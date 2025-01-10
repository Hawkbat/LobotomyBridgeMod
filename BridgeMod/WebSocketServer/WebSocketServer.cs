using System;
// Networking libs
using System.Net;
using System.Net.Sockets;
// For creating a thread
using System.Threading;
// For List & ConcurrentQueue
using System.Collections.Generic;
using DisruptorUnity3d;

namespace WebSocketServer
{
    public class WebSocketServer : IDisposable
    {
        // The tcpListenerThread listens for incoming WebSocket connections, then assigns the client to handler threads;
        private TcpListener tcpListener;
        private Thread tcpListenerThread;

        protected List<WebSocketConnection> connections;
        public RingBuffer<WebSocketEvent> events;

        string address;
        int port;
        IWebSocketLogger logger;

        public WebSocketServer(string address, int port, IWebSocketLogger logger)
        {
            this.address = address;
            this.port = port;
            this.logger = logger;
            connections = new List<WebSocketConnection>();
            events = new RingBuffer<WebSocketEvent>(1000);
            tcpListenerThread = new Thread(new ThreadStart(ListenForTcpConnection))
            {
                IsBackground = true
            };
            tcpListenerThread.Start();
        }

        public void Dispose()
        {
            foreach (var client in connections)
            {
                client.Close();
            }
            tcpListener.Stop();
            tcpListenerThread.Abort();
        }

        public void PumpEvents()
        {
            while (events.TryDequeue(out WebSocketEvent wsEvent))
            {
                if (wsEvent.type == WebSocketEventType.Open)
                {
                    OnOpen(wsEvent.connection);
                }
                else if (wsEvent.type == WebSocketEventType.Close)
                {
                    OnClose(wsEvent.connection);
                }
                else if (wsEvent.type == WebSocketEventType.Message)
                {
                    WebSocketMessage message = new WebSocketMessage(wsEvent.connection, wsEvent.text, wsEvent.data);
                    OnMessage(message);
                }
            }
        }

        private void ListenForTcpConnection()
        {
            try
            {
                // Create listener on <address>:<port>.
                tcpListener = new TcpListener(address == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(address), port);
                tcpListener.Start();
                logger.Log($"WebSocket server is listening on {address}:{port} for incoming connections.");
                while (true)
                {
                    // Accept a new client, then open a stream for reading and writing.
                    var connectedTcpClient = tcpListener.AcceptTcpClient();
                    logger.Log("Establishing WebSocket client connection...");
                    // Create a new connection
                    WebSocketConnection connection = new WebSocketConnection(connectedTcpClient, this, logger);
                    // Establish connection
                    if (connection.TryEstablish())
                    {
                        connections.Add(connection);
                    }
                    else
                    {
                        connectedTcpClient.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                BridgeMod.BridgeMod.Log("Error establishing WebSocket connection", ex);
            }
        }

        protected virtual void OnOpen(WebSocketConnection connection) { }

        protected virtual void OnMessage(WebSocketMessage message) { }

        protected virtual void OnClose(WebSocketConnection connection) { }

        protected virtual void OnError(WebSocketConnection connection) { }

        protected void BroadcastText(string data)
        {
            connections.RemoveAll(c => !c.Connected);

            foreach (var connection in connections)
            {
                try
                {
                    connection.SendText(data);
                }
                catch (SocketException socketException)
                {
                    logger.Log("Socket exception: " + socketException);
                }
            }
        }

        protected void BroadcastBinary(byte[] data)
        {
            connections.RemoveAll(c => !c.Connected);

            foreach (var connection in connections)
            {
                try
                {
                    connection.SendBinary(data);
                }
                catch (SocketException socketException)
                {
                    logger.Log("Socket exception: " + socketException);
                }
            }
        }

        protected void SendText(string data, string connectionID)
        {
            var connection = connections.Find(c => c.id == connectionID);
            connection.SendText(data);
        }

        protected void SendBinary(byte[] data, string connectionID)
        {
            var connection = connections.Find(c => c.id == connectionID);
            connection.SendBinary(data);
        }
    }
}

