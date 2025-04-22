using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LibData;

namespace UdpProtocol
{
    public static class MessageUtils
    {
        public static void SendMessage(Socket socket, Message message, IPEndPoint endPoint)
        {
            string jsonMessage = JsonSerializer.Serialize(message);
            byte[] sendData = Encoding.UTF8.GetBytes(jsonMessage);
            socket.SendTo(sendData, endPoint);
        }
        
        public static Message ReceiveMessage(Socket socket, ref IPEndPoint senderEndPoint)
        {
          byte[] buffer = new byte[1024];
          EndPoint remoteEP = senderEndPoint;

          int bytesRead = socket.ReceiveFrom(buffer, ref remoteEP);

          senderEndPoint = (IPEndPoint)remoteEP;

          string jsonMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
          return JsonSerializer.Deserialize<Message>(jsonMessage);
        }

        public static void LogInfo(string message)
        {
            Log(message, ConsoleColor.Cyan);
        }

        public static void LogSuccess(string message)
        {
            Log(message, ConsoleColor.Green);
        }

        public static void LogWarning(string message)
        {
            Log(message, ConsoleColor.Yellow);
        }

        public static void LogError(string message)
        {
            Log(message, ConsoleColor.Red);
        }

        public static void LogProtocol(string operation, Message message)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PROTOCOL | {operation}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  Message ID: {message.MsgId}");
            Console.WriteLine($"  Type: {message.MsgType}");

            if (message.Content != null)
            {
                if (message.MsgType == MessageType.DNSLookupReply && message.Content is DNSRecord record)
                {
                    Console.WriteLine("  Content: <DNSRecord>");
                    Console.WriteLine($"    Type: {record.Type}");
                    Console.WriteLine($"    Name: {record.Name}");
                    Console.WriteLine($"    Value: {record.Value}");
                    Console.WriteLine($"    TTL: {record.TTL}");
                }
                else
                {
                    Console.WriteLine($"  Content: {message.Content}");
                }
            }

            Console.ResetColor();
            Console.WriteLine();
        }

        public static void LogSeparator()
        {
            Console.WriteLine(new string('-', 60));
        }

        public static void LogDNSRecord(DNSRecord record)
        {
            if (record == null) return;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("┌─────────────── DNS RECORD ───────────────┐");
            Console.WriteLine($"│ Type: {record.Type,-36} │");
            Console.WriteLine($"│ Name: {record.Name,-36} │");
            Console.WriteLine($"│ Value: {record.Value,-35} │");
            Console.WriteLine($"│ TTL: {record.TTL,-37} │");
            Console.WriteLine("└────────────────────────────────────────┘");
            Console.ResetColor();
        }

        public static void LogStateTransition<T>(string component, T oldState, T newState)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {component} | State change: {oldState} -> {newState}");
            Console.ResetColor();
            Console.WriteLine();
        }

        // Base logging method
        private static void Log(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            Console.ResetColor();
        }

    }
}
