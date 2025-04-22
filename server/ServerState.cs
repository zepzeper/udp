using System;
using System.Net;
using System.Net.Sockets;
using LibData;

namespace UdpServer
{
    public enum ServerState
    {
        Waiting,
        ReceivingHello,
        SendingWelcome,
        ReceivingDNSLookup,
        ProcessingDNSLookup,
        SendingDNSLookupReply,
        SendingError,
        ReceivingAck,
        SendingEnd
    }
    
    public class ServerContext
    {
        public ServerState CurrentState { get; set; } = ServerState.Waiting;
        public Socket ServerSocket { get; set; }
        public IPEndPoint ServerEndPoint { get; set; }
        public IPEndPoint ClientEndPoint { get; set; }
        public EndPoint RemoteEndPoint { get; set; }
        public Message? ReceivedMessage { get; set; }
        public DNSRecord? LookupRecord { get; set; }
        public DNSRecord? FoundRecord { get; set; }
        public int CurrentLookupMsgId { get; set; }
        public int ReceivedLookupCount { get; set; } = 0;
        
        public ServerContext(Socket socket, IPEndPoint serverEndPoint, IPEndPoint clientEndPoint)
        {
            ServerSocket = socket;
            ServerEndPoint = serverEndPoint;
            ClientEndPoint = clientEndPoint;
            RemoteEndPoint = ClientEndPoint;
        }
        
        public void Reset()
        {
            CurrentState = ServerState.Waiting;
            ReceivedLookupCount = 0;
            RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            ReceivedMessage = null;
            LookupRecord = null;
            FoundRecord = null;
        }
    }
}
