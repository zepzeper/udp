using System;
using System.Net;
using System.Net.Sockets;
using LibData;

namespace UdpClient
{
    public enum ClientState
    {
        Initial,
        WaitingForWelcome,
        SendingDNSLookup,
        WaitingForDNSLookupReply,
        SendingAck,
        WaitingForEnd,
        Terminated
    }
    
    public class ClientContext
    {
        public ClientState CurrentState { get; set; } = ClientState.Initial;
        public Socket ClientSocket { get; set; }
        public IPEndPoint ServerEndPoint { get; set; }
        public EndPoint RemoteEndPoint { get; set; }
        public int CurrentLookupIndex { get; set; } = 0;
        public int CurrentLookupMsgId { get; set; } = 0;
        public Message? ReceivedMessage { get; set; }
        
        public ClientContext(Socket socket, IPEndPoint serverEndPoint)
        {
            ClientSocket = socket;
            ServerEndPoint = serverEndPoint;
            RemoteEndPoint = ServerEndPoint;
        }
        
        // Reset context for a new session
        public void Reset()
        {
            CurrentState = ClientState.Initial;
            CurrentLookupIndex = 0;
            CurrentLookupMsgId = 0;
            ReceivedMessage = null;
        }
    }
}
