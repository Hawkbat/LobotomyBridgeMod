// For String
using System;
// For dictionary
using System.Collections.Generic;
// For parsing the client websocket requests
using System.Text;
using System.Text.RegularExpressions;

namespace WebSocketServer
{
    struct WebSocketDataFrame
    {
        public bool fin;
        public bool mask;
        public WebSocketOpCode opcode;
        public int length;
        public int offset;
        public byte[] data;
    }

    class RequestHeader
    {
        static readonly Regex head = new Regex("^(GET|POST|PUT|DELETE|OPTIONS) (.+) HTTP/([0-9.]+)");
        static readonly Regex body = new Regex("([A-Za-z0-9-]+): ?([^\n^\r]+)");

        public string method = "";
        public string uri = "";
        public string version = "";
        public Dictionary<string, string> headers;

        public RequestHeader(string data)
        {
            headers = new Dictionary<string, string>();

            MatchCollection matches = head.Matches(data);
            foreach (Match match in matches)
            {
                method = match.Groups[1].Value.Trim();
                uri = match.Groups[2].Value.Trim();
                version = match.Groups[3].Value.Trim();
            }

            matches = body.Matches(data);
            foreach (Match match in matches)
            {
                headers.Add(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
            }
        }
    }

    class WebSocketProtocol
    {

        public static bool CheckConnectionHandshake(RequestHeader request, out string validationError)
        {
            // The method must be GET.
            if (!string.Equals(request.method, "GET"))
            {
                validationError = "Request does not begin with GET.";
                return false;
            }

            // TODO: Version must be greater than "1.1".

            // Must have a Host.
            if (!request.headers.ContainsKey("Host"))
            {
                validationError = "Request does not have a Host.";
                return false;
            }

            // Must have a Upgrade: websocket
            if (!request.headers.ContainsKey("Upgrade") || !string.Equals(request.headers["Upgrade"], "websocket"))
            {
                validationError = "Request does not have Upgrade: websocket.";
                return false;
            }

            // Must have a Connection: Upgrade
            if (!request.headers.ContainsKey("Connection") || request.headers["Connection"].IndexOf("Upgrade") == -1)
            {
                validationError = "Request does not have Connection: Upgrade.";
                return false;
            }

            // Must have a Sec-WebSocket-Key
            if (!request.headers.ContainsKey("Sec-WebSocket-Key"))
            {
                validationError = "Request does not have Sec-WebSocket-Key";
                return false;
            }

            // Must have a Sec-WebSocket-Version: 13
            if (!request.headers.ContainsKey("Sec-WebSocket-Version") || !string.Equals(request.headers["Sec-WebSocket-Version"], "13"))
            {
                validationError = "Request does not have Sec-WebSocket-Version: 13";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        public static byte[] CreateHandshakeReply(RequestHeader request)
        {
            const string eol = "\r\n"; // HTTP/1.1 defines the sequence CR LF as the end-of-line marker

            var response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + eol
                + "Connection: Upgrade" + eol
                + "Upgrade: websocket" + eol
                + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                    System.Security.Cryptography.SHA1.Create().ComputeHash(
                        Encoding.UTF8.GetBytes(
                            request.headers["Sec-WebSocket-Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                        )
                    )
                ) + eol
                + eol);

            return response;
        }

        public static WebSocketDataFrame DecodeDataFrameHead(byte[] bytes)
        {
            var fin = (bytes[0] & 0b10000000) != 0;
            var mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

            var opcode = (WebSocketOpCode)(bytes[0] & 0b00001111);
            var length = bytes[1] & 0b01111111;
            var offset = 2;

            var dataframe = new WebSocketDataFrame
            {
                fin = fin,
                mask = mask,
                opcode = opcode,
                length = length,
                offset = offset,
                data = bytes
            };
            return dataframe;
        }

        public static byte[] EncodeDataFrameHead(WebSocketDataFrame dataframe)
        {
            if (dataframe.length >= 128)
            {
                throw new Exception("Data frame length exceeded limits");
            }
            var bytes = new byte[2];
            bytes[0] |= (byte)(dataframe.fin ? 0b10000000 : 0);
            bytes[0] |= (byte)((byte)dataframe.opcode & 0b00001111);
            bytes[1] |= (byte)(dataframe.mask ? 0b10000000 : 0);
            bytes[1] |= (byte)(dataframe.length & 0b01111111);
            return bytes;
        }

        public static string DecodeText(WebSocketDataFrame dataframe)
        {
            if (dataframe.length == 0)
            {
                return string.Empty;
            }
            var decoded = DecodeBinary(dataframe);
            var text = Encoding.UTF8.GetString(decoded);
            return text;
        }

        public static byte[] DecodeBinary(WebSocketDataFrame dataframe)
        {
            if (dataframe.length == 0)
            {
                return new byte[0];
            }
            if (dataframe.mask)
            {
                var decoded = new byte[dataframe.length];
                var masks = new byte[4] {
                    dataframe.data[dataframe.offset],
                    dataframe.data[dataframe.offset + 1],
                    dataframe.data[dataframe.offset + 2],
                    dataframe.data[dataframe.offset + 3]
                };
                var payloadOffset = dataframe.offset + 4;

                for (int i = 0; i < dataframe.length; ++i)
                {
                    decoded[i] = (byte)(dataframe.data[payloadOffset + i] ^ masks[i % 4]);
                }
                return decoded;
            }
            else
            {
                var bytes = new byte[dataframe.length];
                Array.Copy(dataframe.data, dataframe.offset, bytes, 0, dataframe.length);
                return bytes;
            }
        }

        public static void DecodeClose(WebSocketDataFrame dataframe, out ushort code, out string reason)
        {
            if (dataframe.length >= 3)
            {
                var payload = DecodeBinary(dataframe);
                var codeBytes = new byte[2];
                Array.Copy(payload, codeBytes, 2);
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                code = BitConverter.ToUInt16(payload, 0);
                reason = Encoding.UTF8.GetString(payload, 2, payload.Length - 2);
            }
            else if (dataframe.length == 2)
            {
                var payload = DecodeBinary(dataframe);
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                code = BitConverter.ToUInt16(payload, 0);
                reason = null;
            }
            else
            {
                code = 1005;
                reason = null;
            }
        }

        public static byte[] DecodePing(WebSocketDataFrame dataframe)
        {
            return DecodeBinary(dataframe);
        }

        public static byte[] DecodePong(WebSocketDataFrame dataframe)
        {
            return DecodeBinary(dataframe);
        }

        public static void GetHeaderLengths(byte[] data, out byte length, out byte[] lengthBytes)
        {
            if (data.Length <= 125)
            {
                length = (byte)data.Length;
                lengthBytes = new byte[0];
            }
            else if (data.Length <= ushort.MaxValue)
            {
                length = 126;
                lengthBytes = BitConverter.GetBytes((ushort)data.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
            }
            else
            {
                length = 127;
                lengthBytes = BitConverter.GetBytes((ulong)data.LongLength);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
            }
        }

        public static WebSocketDataFrame EncodeText(string text)
        {
            var textBytes = Encoding.UTF8.GetBytes(text);

            byte length;
            byte[] lengthBytes;
            GetHeaderLengths(textBytes, out length, out lengthBytes);

            var dataframe = new WebSocketDataFrame
            {
                fin = true,
                mask = false,
                opcode = WebSocketOpCode.Text,
                length = length,
                offset = 2 + lengthBytes.Length,
                data = new byte[2 + lengthBytes.Length + textBytes.Length],
            };

            var headBytes = EncodeDataFrameHead(dataframe);
            headBytes.CopyTo(dataframe.data, 0);
            lengthBytes.CopyTo(dataframe.data, headBytes.Length);
            textBytes.CopyTo(dataframe.data, headBytes.Length + lengthBytes.Length);

            return dataframe;
        }

        public static WebSocketDataFrame EncodeBinary(byte[] dataBytes)
        {
            byte length;
            byte[] lengthBytes;
            GetHeaderLengths(dataBytes, out length, out lengthBytes);

            var dataframe = new WebSocketDataFrame
            {
                fin = true,
                mask = false,
                opcode = WebSocketOpCode.Binary,
                length = length,
                offset = 2 + lengthBytes.Length,
                data = new byte[2 + lengthBytes.Length + dataBytes.Length],
            };

            var headBytes = EncodeDataFrameHead(dataframe);
            headBytes.CopyTo(dataframe.data, 0);
            lengthBytes.CopyTo(dataframe.data, headBytes.Length);
            dataBytes.CopyTo(dataframe.data, headBytes.Length + lengthBytes.Length);

            return dataframe;
        }

        public static WebSocketDataFrame EncodeClose(ushort code, string reason)
        {
            var codeBytes = BitConverter.GetBytes(code);
            if (BitConverter.IsLittleEndian) Array.Reverse(codeBytes);

            var textBytes = Encoding.UTF8.GetBytes(reason);

            if (codeBytes.Length + textBytes.Length > 125)
            {
                throw new ArgumentOutOfRangeException("reason", reason.Length, "Close reason length is greater than 123 bytes, failed to produce control frame");
            }

            var dataframe = new WebSocketDataFrame
            {
                fin = true,
                mask = false,
                opcode = WebSocketOpCode.Close,
                length = codeBytes.Length + textBytes.Length,
                offset = 2,
                data = new byte[2 + codeBytes.Length + textBytes.Length],
            };

            var headBytes = EncodeDataFrameHead(dataframe);
            headBytes.CopyTo(dataframe.data, 0);
            codeBytes.CopyTo(dataframe.data, headBytes.Length);
            textBytes.CopyTo(dataframe.data, headBytes.Length + codeBytes.Length);

            return dataframe;
        }

        public static WebSocketDataFrame EncodePing() => EncodePing(new byte[0]);

        public static WebSocketDataFrame EncodePing(byte[] dataBytes)
        {
            var dataframe = new WebSocketDataFrame
            {
                fin = true,
                mask = false,
                opcode = WebSocketOpCode.Ping,
                length = dataBytes.Length,
                offset = 2,
                data = new byte[2 + dataBytes.Length],
            };

            var headBytes = EncodeDataFrameHead(dataframe);
            headBytes.CopyTo(dataframe.data, 0);
            dataBytes.CopyTo(dataframe.data, headBytes.Length);

            return dataframe;
        }

        public static WebSocketDataFrame EncodePong() => EncodePong(new byte[0]);

        public static WebSocketDataFrame EncodePong(byte[] dataBytes)
        {
            var dataframe = new WebSocketDataFrame
            {
                fin = true,
                mask = false,
                opcode = WebSocketOpCode.Pong,
                length = dataBytes.Length,
                offset = 2,
                data = new byte[2 + dataBytes.Length],
            };

            var headBytes = EncodeDataFrameHead(dataframe);
            headBytes.CopyTo(dataframe.data, 0);
            dataBytes.CopyTo(dataframe.data, headBytes.Length);

            return dataframe;
        }
    }

    enum WebSocketOpCode : byte
    {
        Continuation = 0x0,
        Text = 0x1,
        Binary = 0x2,

        Close = 0x8,
        Ping = 0x9,
        Pong = 0xA
    }

}