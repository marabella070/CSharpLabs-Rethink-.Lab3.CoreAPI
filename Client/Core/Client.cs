namespace Client_v.Core;

using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using Client_v.Handlers;
using Client_v.Models;
using Client_v.Interfaces;
using Shared.XML_Classes;

public class Client
{
    private TcpClient? client;
    public string currentUser = string.Empty;
    private Handlers.ClientHandler? clientHandler;
    private readonly string host;
    private readonly int port;
    private List<ClientEntry> connectedClients = new();
    private string currentInput = "";
    private readonly object consoleLock = new();
    private ILogOutput? logOutput = null;

    public void SetLogOutput(ILogOutput? output)
    {
        logOutput = output;
    }

    public Client(string host, int port)
    {
        this.host = host;
        this.port = port;
    }

    public void Run()
    {
        try
        {
            client = new TcpClient(host, port);

            clientHandler = new Handlers.ClientHandler(client);

#if CONSOLE_CLIENT
            // User Name Request
            PrintMessageToConsole("Enter your name:", Models.LogTag.Client);

            currentUser = Console.ReadLine()?.Trim() ?? $"user{Guid.NewGuid().ToString()[..4]}";
            Console.WriteLine();
#endif

            // Sending the name to the server
            clientHandler.Writer.WriteLine($"AUTH:{currentUser}");

            PrintMessageToConsole($"Connected to the server as {currentUser}...\n", Models.LogTag.Client);

            Thread receiveThread = new(ReceiveMessages)
            {
                IsBackground = true
            };
            receiveThread.Start();

#if CONSOLE_CLIENT
            currentInput = "";
            Console.Write($"{currentUser}> ");
#endif

#if CONSOLE_CLIENT
            // User Input thread
            Thread inputThread = new(ProcessUserInput);
            inputThread.Start();
            inputThread.Join(); // Waiting for user input to complete
#endif

            receiveThread.Join(); // Waiting for receive input to complete
        }
        catch (Exception ex)
        {
            PrintMessageToConsole(ex.Message, Models.LogTag.Error);
        }
        finally
        {
            clientHandler?.Close();
            PrintMessageToConsole("Connection closed.", Models.LogTag.Client);
        }
    }

    public void Disconnect()
    { 
        clientHandler?.Writer.WriteLine("QUIT");
    }

    private void ProcessUserInput()
    {
        while (true)
        {
            ConsoleKeyInfo? key = null;

            // Waiting for a keystroke with timeout 100 ms
            for (int i = 0; i < 10; i++) // 10 iterations of 100 ms = 1 second timeout
            {
                if (Console.KeyAvailable)
                {
                    key = Console.ReadKey(intercept: true);
                    break;
                }
                Thread.Sleep(100); // Waiting for 100 ms before the next check
            }

            lock (consoleLock)
            {
                if (key.HasValue)
                {
                    bool quit = ProcessKey(key.Value);
                    if (quit) { break; }
                }
                RedrawInputLine();
            }
        }
    }

    private bool ProcessKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Enter)
        {
            if (string.IsNullOrWhiteSpace(currentInput)) { return false; }

            Console.WriteLine("\n");

            if (currentInput.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                clientHandler!.Writer.WriteLine("QUIT");
                currentInput = "";
                return true;
            }

            clientHandler!.Writer.WriteLine(currentInput);
            currentInput = "";
        }
        else if (key.Key == ConsoleKey.Backspace)
        {
            if (currentInput.Length > 0)
            {
                currentInput = currentInput[..^1];
            }
        }
        else
        {
            currentInput += key.KeyChar;
        }
        return false;
    }

    private void RedrawInputLine()
    {
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth)); // clear the line
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write($"{currentUser}> {currentInput}");
    }

    private void ReceiveMessages()
    {
        try
        {
            while (true)
            {
                string message = ReadUntilEOF();

                if (string.IsNullOrWhiteSpace(message)) { continue; }

                // Remove Beginning/Ending spaces and <EOF> marker for clear check
                string cleanMessage = message.Trim().Replace("<EOF>", "");

                if (cleanMessage.TrimStart().StartsWith("<?xml") && TryGetRootTagName(cleanMessage, out string? tagName))
                {
                    string? processedMessage = HandleTaggedMessage(tagName!, cleanMessage);

#if CONSOLE_CLIENT
                    PrintMessageToConsole(processedMessage, Models.LogTag.Server);
#endif
                }
                else if (cleanMessage.IndexOf("QUIT APPROVED", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }
                else
                {
                    PrintMessageToConsole(cleanMessage, Models.LogTag.Server);
                }
            }
        }
        catch (Exception ex)
        {
            PrintMessageToConsole($"Reception error: {ex.Message}", Models.LogTag.Error);
        }
    }

    private string ReadUntilEOF()
    {
        var builder = new StringBuilder();
        string? line;

        while ((line = clientHandler!.Reader.ReadLine()) != null)
        {
            if (line.Trim() == "<EOF>") { break; }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    private bool TryGetRootTagName(string xml, out string? tagName)
    {
        tagName = null;

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            tagName = xmlDoc.DocumentElement?.Name;
            return tagName != null;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private string? HandleTaggedMessage(string tagName, string xml)
    {
        switch (tagName.ToLower())
        {
            case "client_list":
                var clients = DeserializeXml<ClientListMessage>(xml);

                if (clients == null)
                {
                    PrintMessageToConsole("Invalid client list format", Models.LogTag.Error);
                    return null;
                }

                connectedClients = clients.Clients;

#if CONSOLE_CLIENT
                var builder = new StringBuilder();
                builder.AppendLine($"Connected Clients ({clients.Count}):");

                foreach (var client in clients.Clients) 
                {
                    client.UserName += (client.UserName == currentUser) ? " (You)" : "";

                    builder.AppendLine($"• [{client.Id}] {client.UserName}");
                }

                return builder.ToString();
#else
                return null;
#endif

            default:
                PrintMessageToConsole($"[Unhandled XML tag: {tagName}]", Models.LogTag.Error);
                return null;
        }
    }

    private T? DeserializeXml<T>(string xml) where T : class
    {
        try
        {
            var serializer = new XmlSerializer(typeof(T));
            using var reader = new StringReader(xml);
            return serializer.Deserialize(reader) as T;
        }
        catch (Exception ex)
        {
            PrintMessageToConsole($"XML parsing error: {ex.Message}", Models.LogTag.Error);
            return null;
        }
    }

    private void PrintMessageToConsole(string? message, Models.LogTag? tag = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        lock (consoleLock)
        {
            // Generating message
            var taggedMessage = new StringBuilder();

            if (tag is not null)
            {
                taggedMessage.Append($"[{tag}]: ");
            }

            taggedMessage.Append(message + "\n");

            if (tag == Models.LogTag.Server)
            {
                taggedMessage.AppendLine();
            }


#if CONSOLE_CLIENT
            // Memorizing the current cursor position
            int inputCursorLeft = Console.CursorLeft;
            int inputCursorTop = Console.CursorTop;

            // Return to the beginning of the current line
            Console.SetCursorPosition(0, inputCursorTop);

            // Erase entire line with spaces
            Console.Write(new string(' ', Console.WindowWidth - 1));

            // Go back to the beginning of the line again
            Console.SetCursorPosition(0, inputCursorTop);

            Console.WriteLine(taggedMessage.ToString());

            // Move the cursor to a new line if the message does not fit.
            if (Console.CursorLeft >= Console.WindowWidth - 1)
            {
                Console.WriteLine();
            }

            RedrawInputLine();
#endif

            logOutput?.Print(taggedMessage.ToString());
        }
    }
}
