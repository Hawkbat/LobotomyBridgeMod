/**
    MIT License

    Copyright (c) 2021 Shauna Zhang

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
 */

/// Modified extensively from https://github.com/shaunabanana/unity-websocket-server
 
using System;
using System.Net.Sockets;
// For parsing the client websocket requests
using System.Text;
// For creating a thread
using System.Threading;

namespace WebSocketServer
{

    public enum WebSocketEventType
    {
        Open,
        Close,
        Message
    }

    public struct WebSocketMessage
    {
        public WebSocketMessage(WebSocketConnection connection, string text, byte[] data)
        {
            this.id = Guid.NewGuid().ToString();
            this.connection = connection;
            this.text = text;
            this.data = data;
        }

        public string id;
        public WebSocketConnection connection;
        public string text;
        public byte[] data;
    }

    public struct WebSocketEvent
    {
        public WebSocketEvent(WebSocketConnection connection, WebSocketEventType type, string text, byte[] data)
        {
            this.id = Guid.NewGuid().ToString();
            this.connection = connection;
            this.type = type;
            this.text = text;
            this.data = data;
        }

        public string id;
        public WebSocketEventType type;
        public WebSocketConnection connection;
        public string text;
        public byte[] data;
    }

    public class WebSocketConnection
    {
        public string id;
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly WebSocketServer server;
        private Thread connectionThread;
        private IWebSocketLogger logger;

        public WebSocketConnection(TcpClient client, WebSocketServer server, IWebSocketLogger logger)
        {
            this.id = Guid.NewGuid().ToString();
            this.client = client;
            this.stream = client.GetStream();
            this.server = server;
            this.logger = logger;
        }

        public bool Connected => client.Connected;

        public bool TryEstablish()
        {
            // Wait for enough bytes to be available
            var requestBuffer = new byte[10000];
            var requestText = "";
            while (requestText.Length < 10000 && !requestText.Contains("\r\n\r\n"))
            {
                int received = stream.Read(requestBuffer, 0, requestBuffer.Length);
                if (received > 0)
                {
                    var chunk = Encoding.UTF8.GetString(requestBuffer, 0, received);
                    requestText += chunk;
                }
            }

            // Translate bytes of request to a RequestHeader object
            var request = new RequestHeader(requestText);

            string validationError;
            // Check if the request complies with WebSocket protocol.
            if (WebSocketProtocol.CheckConnectionHandshake(request, out validationError))
            {
                // If so, initiate the connection by sending a reply according to protocol.
                var response = WebSocketProtocol.CreateHandshakeReply(request);
                stream.Write(response, 0, response.Length);

                // Start message handling
                connectionThread = new Thread(new ThreadStart(HandleConnection))
                {
                    IsBackground = true
                };
                connectionThread.Start();

                // Call the server callback.
                var wsEvent = new WebSocketEvent(this, WebSocketEventType.Open, null, null);
                server.events.Enqueue(wsEvent);

                logger.Log($"WebSocket client connected (client {id})");

                return true;
            }
            else
            {
                logger.Log($"WebSocket handshake failed (client {id}): {validationError}");
                return false;
            }
        }

        public void SendText(string data)
        {
            WriteDataFrameToStream(WebSocketProtocol.EncodeText(data));
        }

        public void SendBinary(byte[] data)
        {
            WriteDataFrameToStream(WebSocketProtocol.EncodeBinary(data));
        }

        public void SendClose() => SendClose(1005, null);

        public void SendClose(ushort code) => SendClose(code, null);

        public void SendClose(ushort code, string reason)
        {
            WriteDataFrameToStream(WebSocketProtocol.EncodeClose(code, reason));
        }

        public void SendPing() => SendPing(new byte[0]);

        public void SendPing(byte[] data)
        {
            WriteDataFrameToStream(WebSocketProtocol.EncodePing(data));
        }

        public void SendPong() => SendPong(new byte[0]);

        public void SendPong(byte[] data)
        {
            WriteDataFrameToStream(WebSocketProtocol.EncodePong(data));
        }

        public void Close()
        {
            client.Close();
            connectionThread.Abort();
        }

        private void HandleConnection()
        {
            try
            {
                while (true)
                {
                    var dataframe = ReadDataFrame();

                    if (dataframe.fin)
                    {
                        if (dataframe.opcode == WebSocketOpCode.Text)
                        {
                            var text = WebSocketProtocol.DecodeText(dataframe);
                            var wsEvent = new WebSocketEvent(this, WebSocketEventType.Message, text, null);
                            server.events.Enqueue(wsEvent);
                        }
                        else if (dataframe.opcode == WebSocketOpCode.Binary)
                        {
                            var data = WebSocketProtocol.DecodeBinary(dataframe);
                            var wsEvent = new WebSocketEvent(this, WebSocketEventType.Message, null, data);
                            server.events.Enqueue(wsEvent);
                        }
                        else if (dataframe.opcode == WebSocketOpCode.Close)
                        {
                            ushort code;
                            string reason;
                            WebSocketProtocol.DecodeClose(dataframe, out code, out reason);

                            try
                            {
                                SendClose(code, reason);
                            }
                            catch { }

                            // Handle closing the connection.
                            logger.Log($"Client closed the connection with code {code} and reason {reason} (client {id})");
                            // Close the connection.
                            stream.Close();
                            client.Close();
                            // Call server callback.
                            WebSocketEvent wsEvent = new WebSocketEvent(this, WebSocketEventType.Close, null, null);
                            server.events.Enqueue(wsEvent);
                            // Jump out of the loop.
                            break;
                        }
                        else if (dataframe.opcode == WebSocketOpCode.Ping)
                        {
                            logger.Log($"Received ping (client {id})");
                            var payload = WebSocketProtocol.DecodePing(dataframe);
                            SendPong(payload);
                        }
                        else if (dataframe.opcode == WebSocketOpCode.Pong)
                        {
                            logger.Log($"Received pong (client {id})");
                        }
                        else
                        {
                            logger.Log($"Unsupported data frame with fin: {dataframe.fin}, opcode {dataframe.opcode}, length {dataframe.length}");
                        }
                    }
                    else
                    {
                        logger.Log("Fragmentation encountered.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log("Error handling WebSocket connection", ex);
            }
        }


        private WebSocketDataFrame ReadDataFrame()
        {
            const int DataframeHead = 2;        // Length of dataframe head
            const int ShortPayloadLength = 2;   // Length of a short payload length field
            const int LongPayloadLength = 8;    // Length of a long payload length field
            const int Mask = 4;                 // Length of the payload mask

            // Wait for a dataframe head to be available, then read the data.
            byte[] headBytes = ReadBytesFromStream(DataframeHead);

            // Decode the message head, including FIN, OpCode, and initial byte of the payload length.
            var dataframe = WebSocketProtocol.DecodeDataFrameHead(headBytes);

            // Depending on the dataframe length, read & decode the next bytes for payload length
            var lengthBytes = new byte[0];
            if (dataframe.length == 126)
            {
                lengthBytes = ReadBytesFromStream(ShortPayloadLength); // Read the next two bytes for length
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                dataframe.length = BitConverter.ToUInt16(lengthBytes, 0);
                dataframe.offset = 4;
            }
            else if (dataframe.length == 127)
            {
                lengthBytes = ReadBytesFromStream(LongPayloadLength); // Read the next eight bytes for length
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                dataframe.length = (int)BitConverter.ToUInt64(lengthBytes, 0);
                dataframe.offset = 10;
            }

            var maskBytes = new byte[0];
            if (dataframe.mask)
            {
                maskBytes = ReadBytesFromStream(Mask); // Read the next four bytes for mask 
            }

            var payloadBytes = ReadBytesFromStream(dataframe.length); // Read the payload

            var data = new byte[headBytes.Length + lengthBytes.Length + maskBytes.Length + payloadBytes.Length];
            headBytes.CopyTo(data, 0);
            lengthBytes.CopyTo(data, headBytes.Length);
            maskBytes.CopyTo(data, headBytes.Length + lengthBytes.Length);
            payloadBytes.CopyTo(data, headBytes.Length + lengthBytes.Length + maskBytes.Length);
            dataframe.data = data;

            return dataframe;
        }

        private byte[] ReadBytesFromStream(int length)
        {
            var buffer = new byte[length];
            var index = 0;
            while (index < length)
            {
                var received = stream.Read(buffer, index, buffer.Length - index);
                index += received;
            }
            return buffer;
        }

        private void WriteDataFrameToStream(WebSocketDataFrame dataframe)
        {
            stream.Write(dataframe.data, 0, dataframe.data.Length);
        }
    }

}