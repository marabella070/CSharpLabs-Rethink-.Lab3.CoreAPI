using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;

using Server.Handlers;
using Shared.XML_Classes;
using Shared.ID_Management;

namespace Server.Core;

public class Server
{
    private TcpListener listener;
    private int port;
    private readonly IdManager<SimpleIdGenerator> _clientIdManager;
    private readonly IdManager<GridIdGenerator> _transactionsIdManager;
    private List<ClientHandler> clients = new List<ClientHandler>();


    public Server(int port)
    {
        this.port = port;
        listener = new TcpListener(IPAddress.Any, port);

        var simpleIdGenerator = new SimpleIdGenerator();
        _clientIdManager = new IdManager<SimpleIdGenerator>(simpleIdGenerator);

        var gridIdGenerator = new GridIdGenerator("node-1");
        _transactionsIdManager = new IdManager<GridIdGenerator>(gridIdGenerator);
    }

    public void Start()
    {
        listener.Start();
        Console.WriteLine("The server is running on the port " + port);

        while (true)
        {
            TcpClient tcpClient = listener.AcceptTcpClient();

            int userId = _clientIdManager.GetNextId();

            ClientHandler clientHandler = new ClientHandler(tcpClient, userId);
            lock (clients) clients.Add(clientHandler);

            new Thread(() => HandleClient(clientHandler)).Start();
        }
    }

    private void HandleClient(ClientHandler client)
    {
        try
        {
            client.UserName = "Not verified";

            AuthenticateUser(client);

            ProcessMessages(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error with client [ID: {client.Id}, User name: {client.UserName}]: {ex.Message}");
        }
        finally
        {
            lock (clients)
            {
                string LogMessage = $"Client [ID: {client.Id}, User name: {client.UserName}] fully disconnected.";

                // Free up user number
                _clientIdManager.ReleaseId(client.Id);

                client.Close();
                clients.Remove(client);
                Console.WriteLine(LogMessage);
            }
        }
    }

    private void ProcessMessages(ClientHandler client)
    {
        try
        {
            while (true)
            {
                string message = XmlHelper.ReadUntilEOF(client.Reader);

                if (string.IsNullOrWhiteSpace(message)) { continue; }

                // Remove Beginning/Ending spaces and <EOF> marker for clear check
                string cleanMessage = message.Trim().Replace("<EOF>", "");

                if (cleanMessage.TrimStart().StartsWith("<?xml") &&
                    XmlHelper.TryGetRootTagName(cleanMessage, out string? tagName))
                {
                    HandleTaggedMessage(client, tagName!, cleanMessage);
                }
                else
                {
                    Console.WriteLine($"Message from [ID: {client.Id}, User name: {client.UserName}]: {message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reception error with client [ID: {client.Id}, User name: {client.UserName}]: {ex.Message}");
        }
    }

    private void HandleTaggedMessage(ClientHandler client, string tagName, string xml)
    {
        switch (tagName.ToLower())
        {
            case "quit":
                {
                    QuitCommand? reasonToDisconnect = XmlHelper.DeserializeXml<QuitCommand>(xml);

                    if (reasonToDisconnect == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User name: {client.UserName}] Invalid quit command format.");
                        return;
                    }

                    Console.WriteLine($"Client [ID: {client.Id}, User name: {client.UserName}] initiated disconnect.");
                    Console.WriteLine($"Disconnect reason: {reasonToDisconnect.Reason}, Timestamp: {reasonToDisconnect.Timestamp}");


                    SendMessageToClient(client, $"Bye {client.UserName}, waiting for you back!");

                    BroadcastClientList(client);

                    QuitApprovement(client);

                    break;
                }
            case "auth":
                { 
                    AuthCommand? authCommand = XmlHelper.DeserializeXml<AuthCommand>(xml);

                    if (authCommand == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User name: {client.UserName}] Invalid auth command format.");
                        return;
                    }

                    client.UserName = authCommand.ClientName;
                    Console.WriteLine($"The client is connected [ID: {client.Id}, User name: {client.UserName}]");

                    BroadcastClientList(client);

                    // Send welcome message to the client
                    SendMessageToClient(client, $"Welcome {client.UserName} to the server!");

                    break;
                }
            case "transition_id_request":
                { 
                    var request = XmlHelper.DeserializeXml<TransitionIdRequest>(xml);

                    if (request == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User name: {client.UserName}] sent invalid transition_id_request.");
                        return;
                    }

                    // Пытаемся сгенерировать ID
                    int? serverTransactionId = null;
                    bool success = false;

                    try
                    {
                        serverTransactionId = _transactionsIdManager.GetNextId();
                        success = true;

                        Console.WriteLine($"Generated ServerTransactionID = {serverTransactionId} for client_transaction_id = {request.ClientTransactionId} (User: {client.UserName})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to generate ID for client {client.UserName}: {ex.Message}");
                    }

                    var response = new TransitionIdRequestResponse
                    {
                        ClientTransactionId = request.ClientTransactionId,
                        ServerTransactionId = serverTransactionId,
                        Success = success
                    };

                    string? xmlMessage = XmlHelper.SerializeToXml<TransitionIdRequestResponse>(response);

                    if (xmlMessage == null)
                    {
                        Console.WriteLine("Something went wrong when sterilizing an xml document.");
                        return;
                    }

                    SendMessageToClient(client, xmlMessage);

                    break;
                }
            case "transition_id_release":
                { 
                    var release = XmlHelper.DeserializeXml<TransitionIdRelease>(xml);

                    if (release == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] sent invalid transition_id_release.");
                        return;
                    }

                    bool success = false;

                    try
                    {
                        success = _transactionsIdManager.ReleaseId(release.ServerTransactionId);
                        Console.WriteLine($"Client [ID: {client.Id}] released ServerTransactionID = {release.ServerTransactionId}, success = {success}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error releasing ID: {ex.Message}");
                    }

                    var response = new TransitionIdReleaseResponse
                    {
                        ClientTransactionId = release.ClientTransactionId,
                        Success = success
                    };

                    string? responseXml = XmlHelper.SerializeToXml<TransitionIdReleaseResponse>(response);

                    if (responseXml == null)
                    {
                        Console.WriteLine("Something went wrong when sterilizing an xml document.");
                        return;
                    }

                    SendMessageToClient(client, responseXml);

                    break;
                }
            case "exchange_request":
                {
                    var release = XmlHelper.DeserializeXml<ExchangeRequest>(xml);

                    if (release == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] sent invalid exchange_request.");
                        return;
                    }

                    ClientHandler? addressee = clients.FirstOrDefault(client => client.Id == release.toClientId);
                    if (addressee == null)
                    { 
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] There is no recipient with the transmitted ID in the database..");
                        return;
                    }

                    var response = new ExchangeOffer
                    {
                        ServerTransactionId = release.ServerTransactionId,
                        fromClientId = client.Id, 
                        TypeOfExchangeObject = release.TypeOfExchangeObject
                    };

                    string? responseXml = XmlHelper.SerializeToXml<ExchangeOffer>(response);

                    if (responseXml == null)
                    {
                        Console.WriteLine("Something went wrong when sterilizing an xml document.");
                        return;
                    }

                    SendMessageToClient(addressee, responseXml);

                    break;
                }
            case  "exchange_response":
                {
                    var release = XmlHelper.DeserializeXml<ExchangeResponse>(xml);

                    if (release == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] sent invalid exchange_request.");
                        return;
                    }

                    ClientHandler? addressee = clients.FirstOrDefault(client => client.Id == release.toClientId);
                    if (addressee == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] There is no recipient with the transmitted ID in the database..");
                        return;
                    }

                    var response = new ExchangeResponseResult
                    {
                        ServerTransactionId = release.ServerTransactionId,
                        fromClientId = client.Id,
                        Success = release.Success
                    };

                    string? responseXml = XmlHelper.SerializeToXml<ExchangeResponseResult>(response);

                    if (responseXml == null)
                    {
                        Console.WriteLine("Something went wrong when sterilizing an xml document.");
                        return;
                    }

                    SendMessageToClient(addressee, responseXml);

                    break;
                }
            case "send_item":
                {
                    var release = XmlHelper.DeserializeXml<SendItem>(xml);

                    if (release == null || string.IsNullOrWhiteSpace(release.XmlPayload) || string.IsNullOrWhiteSpace(release.TypeName))
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] Invalid incoming_item: missing payload or type name.");
                        return;
                    }

                    try
                    {
                        ClientHandler? addressee = clients.FirstOrDefault(client => client.Id == release.toClientId);
                        if (addressee == null)
                        {
                            Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] There is no recipient with the transmitted ID in the database..");
                            return;
                        }

                        var response = new IncomingItem
                        {
                            ServerTransactionId = release.ServerTransactionId,
                            fromClientId = client.Id,
                            TypeName = release.TypeName,
                            XmlPayload = release.XmlPayload
                        };

                        string? responseXml = XmlHelper.SerializeToXml<IncomingItem>(response);

                        if (responseXml == null)
                        {
                            Console.WriteLine("Something went wrong when sterilizing an xml document.");
                            return;
                        }

                        SendMessageToClient(addressee, responseXml);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception during deserialization: {ex.Message}");
                        return;
                    }

                    break;
                }
            case "receipt_confirmation":
                {
                    var release = XmlHelper.DeserializeXml<ReceiptConfirmation>(xml);

                    if (release == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] sent invalid exchange_request.");
                        return;
                    }

                    ClientHandler? addressee = clients.FirstOrDefault(client => client.Id == release.toClientId);
                    if (addressee == null)
                    { 
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] There is no recipient with the transmitted ID in the database..");
                        return;
                    }

                    var response = new ReceiptConfirmationResult
                    {
                        ServerTransactionId = release.ServerTransactionId,
                        fromClientId = client.Id, 
                        Success = release.Success
                    };

                    string? responseXml = XmlHelper.SerializeToXml<ReceiptConfirmationResult>(response);

                    if (responseXml == null)
                    {
                        Console.WriteLine("Something went wrong when sterilizing an xml document.");
                        return;
                    }

                    SendMessageToClient(addressee, responseXml);

                    break;
                }
            case "reverse_exchange_request":
                {
                    var release = XmlHelper.DeserializeXml<ReverseExchangeRequest>(xml);

                    if (release == null)
                    {
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] sent invalid exchange_request.");
                        return;
                    }

                    ClientHandler? addressee = clients.FirstOrDefault(client => client.Id == release.toClientId);
                    if (addressee == null)
                    { 
                        Console.WriteLine($"Client [ID: {client.Id}, User: {client.UserName}] There is no recipient with the transmitted ID in the database..");
                        return;
                    }

                    var response = new ReverseExchangeOffer
                    {
                        ServerTransactionId = release.ServerTransactionId,
                        fromClientId = client.Id, 
                    };

                    string? responseXml = XmlHelper.SerializeToXml<ReverseExchangeOffer>(response);

                    if (responseXml == null)
                    {
                        Console.WriteLine("Something went wrong when sterilizing an xml document.");
                        return;
                    }

                    SendMessageToClient(addressee, responseXml);

                    break;
                }

            default:
                { 
                    Console.WriteLine($"Unhandled XML tag in [ID: {client.Id}, User: {client.UserName}]: {xml}");
                    break;
                }
        }
    }

    private void BroadcastClientList(ClientHandler currentClient)
    {
        lock (clients)
        {
            var clientListMessage = new ClientListMessage();

            foreach (var client in clients)
            {
                clientListMessage.Clients.Add(new ClientEntry
                {
                    Id = client.Id,
                    UserName = client.UserName
                });
            }

            string? xmlMessage = XmlHelper.SerializeToXml<ClientListMessage>(clientListMessage);

            if (xmlMessage == null)
            {
                Console.WriteLine("Something went wrong when sterilizing an xml document.");
                return;
            }

            SendMessageToClient(currentClient, xmlMessage);

            Console.WriteLine($"Broadcasted XML client list to {clients.Count} clients");
        }
    }

    private void QuitApprovement(ClientHandler client)
    { 
        var quitApprovedCommand = new QuitApprovedCommand { ApprovedBy = "Server" };
        string? quitApprovedXml = XmlHelper.SerializeToXml<QuitApprovedCommand>(quitApprovedCommand);

        if (quitApprovedXml == null)
        {
            Console.WriteLine("Something went wrong when sterilizing an xml document.");
            return;
        }

        SendMessageToClient(client, quitApprovedXml);
    }

    private bool AuthenticateUser(ClientHandler client)
    {
        string message = XmlHelper.ReadUntilEOF(client.Reader);

        if (string.IsNullOrWhiteSpace(message)) { return false; }

        string cleanMessage = message.Trim().Replace("<EOF>", "");

        if (cleanMessage.TrimStart().StartsWith("<?xml") &&
            XmlHelper.TryGetRootTagName(cleanMessage, out string? tagName))
        {
            if (tagName!.ToLower() != "auth")
            {
                return false;
            }

            HandleTaggedMessage(client, tagName!, cleanMessage);
        }
        return true;
    }

    private void SendMessageToClient(ClientHandler client, string message)
    {
        try
        {
            // Adding a marker for the end of the message on a new line
            string messageWithEof = $"{message}\n<EOF>";

            // Sending a message
            client.Writer.WriteLine(messageWithEof);
            client.Writer.Flush();

            Console.WriteLine($"Sent message to [ID: {client.Id}, User: {client.UserName}]");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message to [ID: {client.Id}, User: {client.UserName}]: {ex.Message}");
        }
    }
}