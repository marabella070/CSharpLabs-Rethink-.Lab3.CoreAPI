using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;

using Server.Helpers;
using Server.Handlers;
using Shared.XML_Classes;

namespace Server.Core;

public class Server
{
    private TcpListener listener;
    private int port;
    private readonly ClientIdManager _idManager = new();
    private List<ClientHandler> clients = new List<ClientHandler>();

    public Server(int port)
    {
        this.port = port;
        listener = new TcpListener(IPAddress.Any, port);
    }

    public void Start()
    {
        listener.Start();
        Console.WriteLine("The server is running on the port " + port);

        while (true)
        {
            TcpClient tcpClient = listener.AcceptTcpClient();

            string userId = _idManager.GetNextId().ToString();

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

            // Processing the mandatory first message from the user
            string? initialMessage = client.Reader.ReadLine();
            if (initialMessage != null && initialMessage.StartsWith("AUTH:"))
            {
                string _clientName = initialMessage.Substring("AUTH:".Length).Trim();
                client.UserName = _clientName;

                BroadcastClientList(client);

                Console.WriteLine($"The client is connected [ID: {client.Id}, User name: {client.UserName}]");
            }

            // Send welcome message to the client
            SendMessageToClient(client, $"Welcome {client.UserName} to the server!");

            Console.WriteLine($"Client [ID: {client.Id}, User name: {client.UserName}] connected.");

            while (true)
            {
                string? msg = client.Reader.ReadLine();
                if (msg == null) { break; }

                Console.WriteLine($"Message from [ID: {client.Id}, User name: {client.UserName}]: {msg}");

                // If client sent QUIT command
                if (msg.IndexOf("QUIT", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine($"Client [ID: {client.Id}, User name: {client.UserName}] initiated disconnect.");

                    SendMessageToClient(client, $"Bye {client.UserName}, waiting for you back!");

                    BroadcastClientList(client);

                    SendMessageToClient(client, "QUIT APPROVED");

                    break;
                }
            }
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
                if (int.TryParse(client.Id, out int userId))
                {
                    _idManager.ReleaseId(userId);
                }

                client.Close();
                clients.Remove(client);
                Console.WriteLine(LogMessage);
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

            string xmlMessage;
            var serializer = new XmlSerializer(typeof(ClientListMessage));

            using (var sw = new StringWriter())
            {
                serializer.Serialize(sw, clientListMessage);
                xmlMessage = sw.ToString();
            }

            SendMessageToClient(currentClient, xmlMessage);

            Console.WriteLine($"Broadcasted XML client list to {clients.Count} clients");
        }
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