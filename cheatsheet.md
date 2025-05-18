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


# Netwerkconcepten in de UDP Client-Server Applicatie

Dit document bouwt voort op de eerdere uitleg en duikt dieper in de fundamentele netwerkconcepten die in de C# UDP client-server applicatie worden gebruikt, met name sockets en hun relatie tot het OSI-model.

## Sockets: De Poort naar het Netwerk

Een **socket** is een softwarematig eindpunt dat dient als een communicatiekanaal tussen twee processen op een netwerk. Je kunt het zien als een "deur" of "interface" waardoor een applicatie data kan verzenden en ontvangen. In de .NET-omgeving wordt dit gefaciliteerd door de `System.Net.Sockets.Socket` klasse.

**Kernaspecten van Sockets in deze applicatie:**

1.  **Creatie**:
    * Een socket wordt aangemaakt met specificaties voor de netwerkfamilie, het sockettype en het protocol.
        ```csharp
        // Voorbeeld:
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        ```
        * `AddressFamily.InterNetwork`: Geeft aan dat IPv4 wordt gebruikt.
        * `SocketType.Dgram`: Specificeert dat het een datagram-socket is, wat kenmerkend is voor UDP. Elk bericht wordt als een losse eenheid (datagram) behandeld.
        * `ProtocolType.Udp`: Definieert expliciet het User Datagram Protocol.

2.  **IP-adres en Poortnummer (`IPEndPoint`)**:
    * Een socket alleen is niet genoeg; het moet weten *waar* het moet luisteren of *waar* het data naartoe moet sturen. Dit wordt gedaan met een `IPEndPoint`, een combinatie van een IP-adres (de machine op het netwerk) en een poortnummer (de specifieke applicatie/dienst op die machine).
    * `IPAddress.Any` (vaak gebruikt bij het binden van een server-socket of client-socket): Luister/verzend via elk beschikbaar lokaal IP-adres.
    * `IPAddress.Parse("127.0.0.1")`: Gebruik een specifiek IP-adres.

3.  **Binden (`Bind`)**:
    * `socket.Bind(localEndPoint);`
    * Dit associeert de socket met een lokaal IP-adres en poortnummer.
        * **Server**: Essentieel. De server bindt zich aan een bekend `IPEndPoint` zodat clients weten waar ze hun berichten naartoe moeten sturen.
        * **Client**: De client bindt zich ook aan een lokaal `IPEndPoint`. Hoewel het OS een efemere (tijdelijke) poort kan toewijzen als de client niet expliciet bindt, geeft het binden controle over de gebruikte clientpoort.

4.  **Data Versturen (`SendTo`)**:
    * `socket.SendTo(byte[] buffer, EndPoint remoteEP);` (gebruikt in `MessageUtils.SendMessage`)
    * Verstuurt een byte-array (`buffer`) naar een specifiek extern `IPEndPoint` (`remoteEP`). Omdat UDP connectionless is, moet bij elk verzonden pakket het bestemmingsadres worden opgegeven.

5.  **Data Ontvangen (`ReceiveFrom`)**:
    * `socket.ReceiveFrom(byte[] buffer, ref EndPoint remoteEP);` (gebruikt in `MessageUtils.ReceiveMessage`)
    * Wacht op en ontvangt data. De `buffer` wordt gevuld met de ontvangen bytes. Cruciaal is `ref remoteEP`: dit `IPEndPoint`-object wordt gevuld met het IP-adres en poortnummer van de *afzender*. Hierdoor weet een UDP-server wie het bericht stuurde en waar een eventueel antwoord naartoe moet.

6.  **Sluiten (`Close`)**:
    * `socket.Close();`
    * Geeft de door de socket gebruikte systeembronnen vrij, zoals het gebonden poortnummer.

## Het OSI-Model en de Relatie met Sockets

Het **OSI-model (Open Systems Interconnection model)** is een conceptueel raamwerk dat de functies van een telecommunicatie- of computersysteem verdeelt in zeven abstractielagen. Elke laag bedient de laag erboven en wordt bediend door de laag eronder.

Laten we kijken hoe de functionaliteit van de applicatie zich verhoudt tot deze lagen:

1.  **Laag 7: Applicatielaag (Application Layer)**
    * Dit is de laag die het dichtst bij de eindgebruiker staat. Het biedt de interface tussen de applicaties en de netwerkdiensten.
    * **In de code**:
        * Het **eigen protocol** gedefinieerd door `MessageType` (Hello, Welcome, DNSLookup, Ack, End) en de structuur van de `Message`-objecten.
        * De **logica van de client en server** (de state machines, het verwerken van DNS-verzoeken, het opbouwen van antwoorden).
        * De `MessageUtils` klasse voor logging en het faciliteren van de communicatie.

2.  **Laag 6: Presentatielaag (Presentation Layer)**
    * Verantwoordelijk voor datavertaling, -codering, -compressie en -encryptie. Het zorgt ervoor dat data die door de applicatielaag van het ene systeem wordt verzonden, leesbaar is voor de applicatielaag van een ander systeem.
    * **In de code**:
        * **JSON Serialisatie/Deserialisatie**: Het omzetten van `Message`-objecten en `DNSRecord`-objecten naar JSON-strings (en vice versa) met `System.Text.Json.JsonSerializer`. Dit is de "presentatie" van de applicatiedata.
        * **Encoding**: Het omzetten van strings naar byte-arrays (bijv. met `Encoding.UTF8.GetBytes()`) voor verzending over het netwerk.

3.  **Laag 5: Sessielaag (Session Layer)**
    * Beheert het opzetten, onderhouden en beëindigen van sessies (verbindingen) tussen applicaties.
    * **Voor UDP**: Omdat UDP een **connectionless** protocol is, is de rol van de sessielaag minimaal in de context van UDP zelf. Er wordt geen formele sessie opgezet of onderhouden op transportniveau.
    * **In de code (simulatie)**: De client-server interactie *simuleert* een soort sessie door de state machines. De server onthoudt bijvoorbeeld van welke client hij berichten verwacht binnen een bepaalde "transactie" (van `Hello` tot `End`). Dit is echter een applicatielaag-constructie, niet een UDP-sessie.

4.  **Laag 4: Transportlaag (Transport Layer)**
    * Zorgt voor end-to-end communicatie en datatransport tussen hosts. Het segmenteert data van de hogere lagen en voegt poortnummers toe.
    * **In de code**:
        * **UDP (User Datagram Protocol)**: Expliciet gekozen (`ProtocolType.Udp`, `SocketType.Dgram`). UDP levert een "best-effort" datagramdienst zonder garanties voor aflevering, volgorde of duplicatiepreventie.
        * **Poortnummers**: De `IPEndPoint`-objecten specificeren de bron- en bestemmingspoorten. Deze poorten stellen de transportlaag in staat om data te multiplexen en demultiplexen, d.w.z. data te routeren naar de juiste socket/applicatie op de host. De `Socket`-klasse is de API van de applicatie naar deze laag.

5.  **Laag 3: Netwerklaag (Network Layer)**
    * Verantwoordelijk voor logische adressering (IP-adressen), routering en het bepalen van het pad dat datapakketten door het netwerk afleggen.
    * **In de code**:
        * **IP (Internet Protocol)**: Impliciet gebruikt door `AddressFamily.InterNetwork` (IPv4). De `IPAddress`-objecten (bijv. `IPAddress.Parse(setting.ServerIPAddress)`) zijn adressen op deze laag.
        * De socket-implementatie van het besturingssysteem werkt samen met de IP-stack om IP-pakketten te construeren met bron- en bestemmings-IP-adressen.

6.  **Laag 2: Datalinklaag (Data Link Layer)**
    * Verzorgt de communicatie tussen direct verbonden netwerknodes (bijv. binnen hetzelfde lokale netwerk). Het gebruikt fysieke adressen (MAC-adressen) en definieert protocollen zoals Ethernet.
    * **Niet direct in de C# code**: Dit wordt afgehandeld door het besturingssysteem en de netwerkhardware (netwerkkaart). De IP-pakketten van Laag 3 worden ingekapseld in frames van Laag 2.

7.  **Laag 1: Fysieke Laag (Physical Layer)**
    * Definieert de elektrische, mechanische en procedurele specificaties voor het activeren, onderhouden en deactiveren van de fysieke link tussen systemen. Het gaat om de daadwerkelijke transmissie van bits over een medium (kabels, radiogolven).
    * **Niet direct in de C# code**: Dit wordt volledig afgehandeld door de netwerkhardware.

### Hoe Sockets en het OSI-model Samenwerken

Sockets zijn de brug waarmee applicaties (voornamelijk op de Applicatie-, Presentatie- en soms Sessielaag) toegang krijgen tot de diensten van de Transportlaag (en indirect de onderliggende lagen).

**Verzendproces (vereenvoudigd):**

1.  **Applicatie (Laag 7)**: De C# code creëert een `Message`-object.
2.  **Presentatie (Laag 6)**: Het `Message`-object wordt geserialiseerd naar JSON, vervolgens naar een `byte[]`.
3.  **Socket API**: De `socket.SendTo()` methode wordt aangeroepen. Dit is de overdracht naar de netwerkstack van het OS.
4.  **Transport (Laag 4 - UDP)**: Het besturingssysteem voegt een UDP-header toe aan de `byte[]` (met bron- en bestemmingspoort). Dit vormt een UDP-datagram.
5.  **Netwerk (Laag 3 - IP)**: Het OS voegt een IP-header toe (met bron- en bestemmings-IP-adres). Dit vormt een IP-pakket.
6.  **Datalink & Fysiek (Laag 2 & 1)**: Het IP-pakket wordt verder ingekapseld in een frame (bijv. Ethernet) en als elektrische signalen/radiogolven over het netwerkmedium verzonden.

**Ontvangstproces:** Het omgekeerde gebeurt. De data beweegt omhoog door de lagen, waarbij elke laag zijn header verwijdert en de payload doorgeeft aan de hogere laag, totdat de `byte[]` via de socket API bij de applicatie aankomt, die het vervolgens deserialiseert.

Door gebruik te maken van de `Socket`-klasse abstraheert de C# code veel van de complexiteit van de lagere OSI-lagen, waardoor de ontwikkelaar zich kan concentreren op de applicatielogica en het dataformaat, terwijl het besturingssysteem de details van UDP/IP-communicatie en netwerkinteractie afhandelt.
