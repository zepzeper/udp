using System;
using System.Data;
using System.Data.SqlTypes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LibData;
using UdpServer;
using UdpProtocol;

// ReceiveFrom();
class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}


class ServerUDP
{
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);
    static int msgIdCounter = 1; 

    static string dnsRecordsFile = @"./DNSRecords.json";

    public static void start()
    {
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        serverSocket.Bind(serverEndPoint);

        MessageUtils.LogSuccess($"Server started at {setting.ServerIPAddress}:{setting.ServerPortNumber}");
        MessageUtils.LogSeparator();

        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
        ServerContext context = new ServerContext(serverSocket, serverEndPoint, clientEndPoint);

        try
        {
            while (true)
            {
                // Process current state
                ProcessState(context);
            }
        }
        catch (SocketException e)
        {
            MessageUtils.LogError($"Socket error: {e.Message}");
        }
        catch (Exception e)
        {
            MessageUtils.LogError($"Error: {e.Message}");
        }
        finally
        {
            context.Reset();
        }
    }

    private static void ProcessState(ServerContext context)
    {
        switch (context.CurrentState)
        {
            case ServerState.Waiting:
                HandleWaitingState(context);
                break;
            case ServerState.ReceivingHello:
                HandleReceivingHelloState(context);
                break;
            case ServerState.SendingWelcome:
                HandleSendingWelcomeState(context);
                break;
            case ServerState.ReceivingDNSLookup:
                HandleReceivingDNSLookupState(context);
                break;
            case ServerState.ProcessingDNSLookup:
                HandleProcessingDNSLookupState(context);
                break;
            case ServerState.SendingDNSLookupReply:
                HandleSendingDNSLookupReplyState(context);
                break;
            case ServerState.SendingError:
                HandleSendingErrorState(context);
                break;
            case ServerState.ReceivingAck:
                HandleReceivingAckState(context);
                break;
            case ServerState.SendingEnd:
                HandleSendingEndState(context);
                break;
        }
    }

    private static void HandleWaitingState(ServerContext context)
    {
        MessageUtils.LogInfo("Waiting for client connections...");

        var oldState = context.CurrentState;
        context.Reset();
        context.CurrentState = ServerState.ReceivingHello;
        MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
    }

    private static void HandleReceivingHelloState(ServerContext context)
    {
        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

        context.ReceivedMessage = MessageUtils.ReceiveMessage(context.ServerSocket, ref clientEndPoint);

        context.ClientEndPoint = clientEndPoint;

        if (context.ReceivedMessage?.MsgType == MessageType.Hello)
        {
            MessageUtils.LogProtocol("RECEIVED HELLO", context.ReceivedMessage);
            
            var oldState = context.CurrentState;
            context.CurrentState = ServerState.SendingWelcome;
            MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
        }
        else
        {
            MessageUtils.LogError($"Expected Hello message, received: {context.ReceivedMessage?.MsgType}");
            
            var oldState = context.CurrentState;
            context.CurrentState = ServerState.Waiting;
            MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
        }
    }
    
    private static void HandleSendingWelcomeState(ServerContext context)
    {
        Message welcomeMessage = new Message
        {
            MsgId = msgIdCounter++,
            MsgType = MessageType.Welcome,
            Content = "Welcome from server"
        };

        MessageUtils.LogProtocol("SENDING WELCOME", welcomeMessage);
        MessageUtils.SendMessage(context.ServerSocket, welcomeMessage, context.ClientEndPoint);

        var oldState = context.CurrentState;
        context.CurrentState = ServerState.ReceivingDNSLookup;
        MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
    }

    private static void HandleReceivingDNSLookupState(ServerContext context)
    {
        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

        context.ReceivedMessage = MessageUtils.ReceiveMessage(context.ServerSocket, ref clientEndPoint);

        if (context.ReceivedMessage?.MsgType == MessageType.DNSLookup)
        {
            context.ReceivedLookupCount++;
            context.CurrentLookupMsgId = context.ReceivedMessage.MsgId;

            MessageUtils.LogProtocol($"RECEIVED DNS LOOKUP #{context.ReceivedLookupCount}", context.ReceivedMessage);

            var oldState = context.CurrentState;
            context.CurrentState = ServerState.ProcessingDNSLookup;
            MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
        }
        else
        {
            MessageUtils.LogError($"Expected DNSLookup message, received: {context.ReceivedMessage?.MsgType}"); 
        }
    }
    private static void HandleProcessingDNSLookupState(ServerContext context)
    {
        string? jsonContent = context.ReceivedMessage?.Content?.ToString();

        context.LookupRecord = JsonSerializer.Deserialize<DNSRecord>(jsonContent);

        try
        {
            if (context.LookupRecord != null)
            {

                if (context.LookupRecord.Type == "" || context.LookupRecord.Name == "")
                {
                    MessageUtils.LogWarning("Received incomplete DNS lookup data");
                    context.CurrentState = ServerState.SendingError;
                    return;
                }

                MessageUtils.LogInfo($"Processing lookup for {context.LookupRecord.Type} record of {context.LookupRecord.Name}");

                context.FoundRecord = QueryDNSRecord(context.LookupRecord.Type, context.LookupRecord.Name);

                if (context.FoundRecord != null)
                {
                    MessageUtils.LogSuccess("DNS Record found:");
                    MessageUtils.LogDNSRecord(context.FoundRecord);

                    var oldState = context.CurrentState;
                    context.CurrentState = ServerState.SendingDNSLookupReply;
                    MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
                    return;
                }
            }
        }
        catch (JsonException e)
        {
            MessageUtils.LogError($"Error parsing DNS lookup data: {e.Message}");
        
            var oldState = context.CurrentState;
            context.CurrentState = ServerState.SendingError;
            MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
            return;
        }

        MessageUtils.LogWarning("DNS Record not found");
        
        var prevState = context.CurrentState;
        context.CurrentState = ServerState.SendingError;
        MessageUtils.LogStateTransition("SERVER", prevState, context.CurrentState);
    }

    private static void HandleSendingDNSLookupReplyState(ServerContext context)
    {
        Message reply = new Message
        {
            MsgId = context.CurrentLookupMsgId,
            MsgType = MessageType.DNSLookupReply,
            Content = context.FoundRecord
        };

        MessageUtils.LogProtocol("SENDING DNS LOOKUP REPLY", reply);
        MessageUtils.SendMessage(context.ServerSocket, reply, context.ClientEndPoint);

        var oldState = context.CurrentState;
        context.CurrentState = ServerState.ReceivingAck;
        MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
    }

    private static void HandleSendingErrorState(ServerContext context)
    {
        Message errorMessage = new Message
        {
            MsgId = context.CurrentLookupMsgId,
            MsgType = MessageType.Error,
            Content = $"DNS record not found for type {context.LookupRecord?.Type} and name {context.LookupRecord?.Name}"
        };

        MessageUtils.LogProtocol("SENDING ERROR", errorMessage);
        MessageUtils.SendMessage(context.ServerSocket, errorMessage, context.ClientEndPoint);

        var oldState = context.CurrentState;
        context.CurrentState = ServerState.ReceivingAck;
        MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
    }

    private static void HandleReceivingAckState(ServerContext context)
    {
        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

        context.ReceivedMessage = MessageUtils.ReceiveMessage(context.ServerSocket, ref clientEndPoint);

        if (context.ReceivedMessage?.MsgType == MessageType.Ack)
        {
            MessageUtils.LogProtocol("RECEIVED ACK", context.ReceivedMessage);

            var oldState = context.CurrentState;
            
            if (context.ReceivedLookupCount >= 4)
            {
                context.CurrentState = ServerState.SendingEnd;
                MessageUtils.LogSuccess("All required DNS lookups completed");
            }
            else
            {
                context.CurrentState = ServerState.ReceivingDNSLookup;
                MessageUtils.LogInfo($"Waiting for more DNS lookups ({context.ReceivedLookupCount}/4 completed)");
            }
            
            MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
        }
        else
        {
            MessageUtils.LogError($"Expected Ack message, received: {context.ReceivedMessage?.MsgType}");
        }
    }


    private static void HandleSendingEndState(ServerContext context)
    {
        // Send End message
        Message endMessage = new Message
        {
            MsgId = msgIdCounter++,
            MsgType = MessageType.End,
            Content = "End of communication"
        };

        MessageUtils.LogProtocol("SENDING END", endMessage);
        MessageUtils.SendMessage(context.ServerSocket, endMessage, context.ClientEndPoint);

        // Reset state machine for next client
        MessageUtils.LogSuccess("Session with client completed. Ready for new client.");
        MessageUtils.LogSeparator();
        
        var oldState = context.CurrentState;
        context.CurrentState = ServerState.Waiting;
        MessageUtils.LogStateTransition("SERVER", oldState, context.CurrentState);
    }

    private static List<DNSRecord> ReadDnsRecords()
    {
        try {
            string jsonContent = File.ReadAllText(dnsRecordsFile);
            List<DNSRecord> records = JsonSerializer.Deserialize<List<DNSRecord>>(jsonContent);
            return records ?? new List<DNSRecord>();
        } catch (Exception e)
        {
            MessageUtils.LogError($"Error reading DNS records file: {e.Message}");
        }

        return new List<DNSRecord>();
    }


    private static DNSRecord? QueryDNSRecord(string type, string name)
    {
        List<DNSRecord> records = ReadDnsRecords();

        foreach (var record in records)
        {
            if (record.Type == type && record.Name == name)
            {
                return record;
            }
        }

        return null;
    }
}
