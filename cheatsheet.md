# UDP-Based DNS Client-Server Program - Key Components Explained

## Network Components

### IPEndPoint
- **Purpose**: Represents a network endpoint as an IP address and port number
- **Usage in the program**: 
  - `clientEndPoint`: Represents the client's local binding address and port
  - `serverEndPoint`: Represents the server's binding address and port
  - When set to `IPAddress.Any` (0.0.0.0), it can receive data from any IP address on the specified port
  - Used as parameters to the Socket's `SendTo()` and `ReceiveFrom()` methods

### Socket
- **Purpose**: Low-level network interface that provides communication over the network
- **Parameters in the program**:
  - `AddressFamily.InterNetwork`: Specifies IPv4
  - `SocketType.Dgram`: Specifies UDP datagram communication
  - `ProtocolType.Udp`: Specifies the UDP protocol
- **Key methods used**:
  - `Bind()`: Associates the socket with a local endpoint
  - `SendTo()`: Sends data to a specific endpoint
  - `ReceiveFrom()`: Receives data and captures the sender's endpoint

### Bind() Method
- **Purpose**: Associates a socket with a local endpoint (IP address and port)
- **Importance**: 
  - Must be called before receiving data
  - Determines which network interface and port the application listens on
  - Using `IPAddress.Any` allows receiving from any network interface
- **Example**: `clientSocket.Bind(clientEndPoint);`

## Context Classes

### ServerContext
- **Purpose**: Maintains the server's state and stores all necessary data during a session
- **Key properties**:
  - `CurrentState`: Tracks the server's current state in the state machine
  - `ServerSocket`: The socket used for communication
  - `ServerEndPoint`: The server's local endpoint
  - `ClientEndPoint`: The client's endpoint, updated when receiving messages
  - `ReceivedMessage`: The last message received from the client
  - `LookupRecord`: The DNS record requested by the client
  - `FoundRecord`: The DNS record found during lookup
  - `ReceivedLookupCount`: Tracks how many lookups have been processed

### ClientContext
- **Purpose**: Maintains the client's state and stores all necessary data during a session
- **Key properties**:
  - `CurrentState`: Tracks the client's current state in the state machine
  - `ClientSocket`: The socket used for communication
  - `ServerEndPoint`: The server's endpoint to send messages to
  - `RemoteEndPoint`: Used to track the actual sender's endpoint when receiving messages
  - `CurrentLookupIndex`: Tracks the current DNS lookup test case
  - `CurrentLookupMsgId`: Stores the message ID for acknowledgments

## State Machines

### ServerState Enum
- **Purpose**: Defines the possible states of the server in the state machine
- **States**:
  - `Waiting`: Server is idle, waiting for a new client
  - `ReceivingHello`: Server is waiting for a Hello message
  - `SendingWelcome`: Server is sending a Welcome message
  - `ReceivingDNSLookup`: Server is waiting for a DNS lookup request
  - `ProcessingDNSLookup`: Server is processing a DNS lookup
  - `SendingDNSLookupReply`: Server is sending a DNS lookup reply
  - `SendingError`: Server is sending an error message
  - `ReceivingAck`: Server is waiting for an acknowledgment
  - `SendingEnd`: Server is sending an End message

### ClientState Enum
- **Purpose**: Defines the possible states of the client in the state machine
- **States**:
  - `Initial`: Client is starting a new session
  - `WaitingForWelcome`: Client is waiting for a Welcome message
  - `SendingDNSLookup`: Client is sending a DNS lookup request
  - `WaitingForDNSLookupReply`: Client is waiting for a DNS lookup reply
  - `SendingAck`: Client is sending an acknowledgment
  - `WaitingForEnd`: Client is waiting for an End message
  - `Terminated`: Client has ended the session

## MessageUtils Class

### ReceiveMessage Method
- **Purpose**: Receives a message from the network and captures the sender's endpoint
- **Key parameters**:
  - `socket`: The socket to receive from
  - `ref senderEndPoint`: Reference to an endpoint that gets updated with the actual sender's information
- **How it works**:
  - Creates a buffer for receiving data
  - Uses `socket.ReceiveFrom()` which populates the provided endpoint reference
  - Updates the original `senderEndPoint` with the actual sender's IP and port
  - Deserializes the received JSON data into a Message object
- **Importance**: Critical for properly tracking the remote IP address

### SendMessage Method
- **Purpose**: Sends a message to a specific endpoint
- **Key parameters**:
  - `socket`: The socket to send from
  - `message`: The message to send
  - `endPoint`: The destination endpoint
- **How it works**:
  - Serializes the message to JSON
  - Converts the JSON to bytes
  - Uses `socket.SendTo()` to send the data to the specified endpoint

## Communication Flow

### Server-Side Flow
1. Server binds to configured IP and port
2. Server waits for a Hello message from any client
3. When a Hello is received, the client's IP and port are captured
4. Server sends Welcome message back to the captured client endpoint
5. Server processes DNS lookups from the client and sends responses
6. After processing all lookups, server sends End message

### Client-Side Flow
1. Client binds to configured IP and port
2. Client sends Hello message to the server
3. Client receives Welcome message, capturing the server's actual IP
4. Client sends DNS lookup requests and processes responses
5. Client acknowledges each response
6. Client receives End message and terminates

## Error Handling

### Socket Exceptions
- **Common issues**:
  - `Can't assign requested address`: Occurs when trying to bind to an unavailable IP address
  - Solution: Use `IPAddress.Any` for binding to allow use of any available interface
- **Handling**:
  - Both client and server catch socket exceptions in their main loops
  - Error messages are logged with `MessageUtils.LogError()`

### DNS Record Handling
- **Error cases**:
  - Invalid record types
  - Empty record fields
  - Non-existent records
- **Handling**:
  - Server validates record types and field values
  - Server sends appropriate error messages for invalid requests
  - Client processes both successful responses and error messages

## IP Address Management

### Tracking Remote Endpoints
- **Server-side**: 
  - `context.ClientEndPoint` is updated with each received message's source
  - All responses are sent to this updated endpoint
- **Client-side**:
  - `context.RemoteEndPoint` is updated with each received message's source
  - Used to ensure replies go to the actual server address
