using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using LibData;
using UdpClient;
using UdpProtocol;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);
    static int msgIdCounter = 1; 

    public static void start()
    {
        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        clientSocket.Bind(clientEndPoint);
        
        MessageUtils.LogSuccess($"Client started at {setting.ClientIPAddress}:{setting.ClientPortNumber}");
        MessageUtils.LogInfo($"Server address: {setting.ServerIPAddress}:{setting.ServerPortNumber}");
        MessageUtils.LogSeparator();
        
        ClientContext context = new ClientContext(clientSocket, serverEndPoint);

        try
        {
            while (context.CurrentState != ClientState.Terminated)
            {
                // Process current state
                ProcessState(context);
            }
            
            MessageUtils.LogSuccess("Client terminating...");
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
            clientSocket.Close();
        }
    }

    private static void ProcessState(ClientContext context)
    {
        switch (context.CurrentState)
        {
            case ClientState.Initial:
                HandleInitialState(context);
                break;
            case ClientState.WaitingForWelcome:
                HandleWaitingForWelcomeState(context);
                break;
            case ClientState.SendingDNSLookup:
                HandleSendingDNSLookupState(context);
                break;
            case ClientState.WaitingForDNSLookupReply:
                HandleWaitingForDNSLookupReplyState(context);
                break;
            case ClientState.SendingAck:
                HandleSendingAckState(context);
                break;
            case ClientState.WaitingForEnd:
                HandleWaitingForEndState(context);
                break;
        }
    }

    private static void HandleInitialState(ClientContext context)
    {
        Message helloMessage = new Message
        {
            MsgId = msgIdCounter++,
            MsgType = MessageType.Hello,
            Content = "Hello from client"
        };
        
        MessageUtils.LogProtocol("SENDING HELLO", helloMessage);
        MessageUtils.SendMessage(context.ClientSocket, helloMessage, context.ServerEndPoint);
        
        var oldState = context.CurrentState;
        context.CurrentState = ClientState.WaitingForWelcome;
        MessageUtils.LogStateTransition("CLIENT", oldState, context.CurrentState);
    }

    private static void HandleWaitingForWelcomeState(ClientContext context)
    {
        context.ReceivedMessage = MessageUtils.ReceiveMessage(context.ClientSocket, context.RemoteEndPoint);
        
        if (context.ReceivedMessage?.MsgType == MessageType.Welcome)
        {
            MessageUtils.LogProtocol("RECEIVED WELCOME", context.ReceivedMessage);
            
            var oldState = context.CurrentState;
            context.CurrentState = ClientState.SendingDNSLookup;
            MessageUtils.LogStateTransition("CLIENT", oldState, context.CurrentState);
        }
        else
        {
            MessageUtils.LogError($"Expected Welcome message, received: {context.ReceivedMessage?.MsgType}\n");
            MessageUtils.LogError($"Shutting down...");

            context.CurrentState = ClientState.Terminated;
        }
    }

    private static void HandleSendingDNSLookupState(ClientContext context)
    {
        var testCases = new[]
        {
            ("A", "www.outlook.com"),          // Correct case 1
            ("A", "mail.example.com"),         // Correct case 2
            ("XYZ", ""),        // Incorrect case 1 - invalid type
            ("", "nonexistentdomain.com") // Incorrect case 2 - domain not found
        };

        if (context.CurrentLookupIndex >= testCases.Length)
        {
            // All test cases covered early return.
            MessageUtils.LogSuccess("All DNS lookup test cases completed");
            
            var oldState = context.CurrentState;
            context.CurrentState = ClientState.WaitingForEnd;
            MessageUtils.LogStateTransition("CLIENT", oldState, context.CurrentState);
            return;
        }

        var (recordType, recordName) = testCases[context.CurrentLookupIndex];
        MessageUtils.LogInfo($"Sending DNS lookup #{context.CurrentLookupIndex + 1}/4: {recordType} record for {recordName}");

        DNSRecord dnsRecord = new DNSRecord
        {
            Type = recordType,
            Name = recordName
        };

        Message dnsLookupMessage = new Message
        {
            MsgId = msgIdCounter++,
            MsgType = MessageType.DNSLookup,
            Content = dnsRecord
        };
    
        // Store the message ID for acknowledgment
        context.CurrentLookupMsgId = dnsLookupMessage.MsgId;

        MessageUtils.LogProtocol("SENDING DNS LOOKUP", dnsLookupMessage);
        MessageUtils.SendMessage(context.ClientSocket, dnsLookupMessage, context.ServerEndPoint);

        var prevState = context.CurrentState;
        context.CurrentState = ClientState.WaitingForDNSLookupReply;
        MessageUtils.LogStateTransition("CLIENT", prevState, context.CurrentState);
    }

    private static void HandleWaitingForDNSLookupReplyState(ClientContext context)
    {
        context.ReceivedMessage = MessageUtils.ReceiveMessage(context.ClientSocket, context.RemoteEndPoint);

        if (context.ReceivedMessage?.MsgType == MessageType.DNSLookupReply)
        {
            MessageUtils.LogProtocol("RECEIVED DNS LOOKUP REPLY", context.ReceivedMessage);
            
            string? jsonContent = context.ReceivedMessage.Content?.ToString();
            DNSRecord? receivedRecord = JsonSerializer.Deserialize<DNSRecord>(jsonContent);

            if (receivedRecord != null)
            {
                MessageUtils.LogDNSRecord(receivedRecord);
            }

            var oldState = context.CurrentState;
            context.CurrentState = ClientState.SendingAck;
            MessageUtils.LogStateTransition("CLIENT", oldState, context.CurrentState);
        }
        else if (context.ReceivedMessage?.MsgType == MessageType.Error)
        {
            MessageUtils.LogProtocol("RECEIVED ERROR", context.ReceivedMessage);
            
            var oldState = context.CurrentState;
            context.CurrentState = ClientState.SendingAck;
            MessageUtils.LogStateTransition("CLIENT", oldState, context.CurrentState);
        }
        else if (context.ReceivedMessage?.MsgType == MessageType.End)
        {
            MessageUtils.LogProtocol("RECEIVED END", context.ReceivedMessage);
            
            var oldState = context.CurrentState;
            context.CurrentState = ClientState.Terminated;
            MessageUtils.LogStateTransition("CLIENT", oldState, context.CurrentState);
        }
        else
        {
            MessageUtils.LogError($"Unexpected message type: {context.ReceivedMessage?.MsgType}");
        }
    }

    private static void HandleSendingAckState(ClientContext context)
    {
        Message ackMessage = new Message
        {
            MsgId = msgIdCounter++,
            MsgType = MessageType.Ack,
            Content = context.CurrentLookupMsgId
        };

        MessageUtils.LogProtocol("SENDING ACK", ackMessage);
        MessageUtils.SendMessage(context.ClientSocket, ackMessage, context.ServerEndPoint);

        context.CurrentLookupIndex++;
        
        var oldState = context.CurrentState;
        context.CurrentState = ClientState.SendingDNSLookup;
        MessageUtils.LogStateTransition("CLIENT", oldState, context.CurrentState);
    }


    private static void HandleWaitingForEndState(ClientContext context)
    {
        MessageUtils.LogInfo("Waiting for End message from server...");
        context.ReceivedMessage = MessageUtils.ReceiveMessage(context.ClientSocket, context.RemoteEndPoint);

        if (context.ReceivedMessage?.MsgType == MessageType.End)
        {
            MessageUtils.LogProtocol("RECEIVED END", context.ReceivedMessage);
            
            var oldState = context.CurrentState;
            context.CurrentState = ClientState.Terminated;
            MessageUtils.LogStateTransition("CLIENT", oldState, context.CurrentState);
        } 
        else
        {
            MessageUtils.LogError($"Expected End message, received: {context.ReceivedMessage?.MsgType}");
        }
    }
}
