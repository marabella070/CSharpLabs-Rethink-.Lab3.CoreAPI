using System.Net;
using System.Net.Sockets;
using System.Text;

using Server.Helpers;
using Server.Handlers;

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
            client.Writer.WriteLine($"Welcome {client.UserName} to the server!");
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

                    client.Writer.WriteLine($"Bye {client.UserName}, waiting for you back!");

                    BroadcastClientList(client);

                    client.Writer.WriteLine("QUIT APPROVED");

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
            // Creating a list of clients in the format:
            // CLIENT_LIST (Clients count)
            // user1
            // user2
            // ...
            var clientList = new StringBuilder();

            // Adding the opening tag
            clientList.AppendLine("<client_list>");

            clientList.AppendLine($"CLIENT_LIST");
            clientList.AppendLine($"Client count: ({clients.Count})");

            // List of clients

            int count = 1;

            foreach (var client in clients)
            {
                clientList.AppendLine($"Client #{count++}: {client.UserName}");
            }

            // Adding a closing tag
            clientList.AppendLine("</client_list>");

            string message = clientList.ToString();

            // Sending message to current client all connected clients

            try
            {
                currentClient.Writer.WriteLine(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending client list to [ID: {currentClient.Id}, User name: {currentClient.UserName}]: {ex.Message}");
            }

            Console.WriteLine($"Broadcasted client list to {clients.Count} clients");
        }
    }
}