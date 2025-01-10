using System.Collections.Generic;
using WebSocketServer;
using System;
using BridgeMod.BridgeMessages;
using SimpleJson;

namespace BridgeMod
{
    public class BridgeServer : WebSocketServer.WebSocketServer
    {
        Dictionary<string, Type> messageTypes = new Dictionary<string, Type>();

        public BridgeServer(string address, int port) : base(address, port, new Logger())
        {

        }

        protected override void OnOpen(WebSocketConnection connection)
        {
            BridgeMod.Log($"Connection Opened (client {connection.id})");
            Send(new Ready(), connection.id, null);
        }

        protected override void OnClose(WebSocketConnection connection)
        {
            BridgeMod.Log($"Connection Closed (client {connection.id})");
        }

        protected override void OnError(WebSocketConnection connection)
        {
            BridgeMod.Log($"Connection Error (client {connection.id})");
        }

        protected override void OnMessage(WebSocketMessage message)
        {
            BridgeMod.Log($"Connection Message (client {message.connection.id}): {message.text}");
            var genericMessage = FromJson<BridgeMessage>(message.text);
            if (!messageTypes.TryGetValue(genericMessage.type, out var subType))
            {
                foreach (var t in GetType().Assembly.GetTypes())
                {
                    if (t.Name == genericMessage.type)
                    {
                        subType = t;
                        messageTypes.Add(genericMessage.type, subType);
                        break;
                    }
                }
            }
            if (subType != null)
            {
                var specificMessage = (BridgeMessage)FromJson(message.text, subType);
                specificMessage.PopulateFromReceive(message.connection.id);
                BridgeMod.GetInstance().OnBridgeMessage(specificMessage);
            }
            else
            {
                genericMessage.PopulateFromReceive(message.connection.id);
                BridgeMod.GetInstance().OnBridgeMessage(genericMessage);
            }
        }

        public void Broadcast(BridgeMessage message)
        {
            message.PopulateToSend(null, null);
            BridgeMod.Log($"Broadcasting Message {ToJson(message)}");
            BroadcastText(ToJson(message));
        }

        public void Send<T>(T message, string clientID, string replyTo) where T : BridgeMessage
        {
            message.PopulateToSend(clientID, replyTo);
            BridgeMod.Log($"Sending Message {ToJson(message)} (client {clientID})");
            SendText(ToJson(message), clientID);
        }

        private string ToJson<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public object FromJson(string json, Type type)
        {
            return JsonConvert.DeserializeObject(json, type);
        }

        public class Logger : IWebSocketLogger
        {
            public void Log(string message)
            {
                BridgeMod.Log(message);
            }

            public void Log(string message, Exception exception)
            {
                BridgeMod.Log(message, exception);
            }
        }
    }

}