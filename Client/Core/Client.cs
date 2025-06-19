namespace Client_v.Core;

using System.Net.Sockets;
using System.Text;

using Client_v.Handlers;
using Client_v.Models;
using Client_v.Intrefaces;

public class Client
{
    private TcpClient? client;
    public string currentUser = string.Empty;
    private Handlers.ClientHandler? clientHandler;
    private readonly string host;
    private readonly int port;
    private readonly List<string> connectedClients = new();
    private string currentInput = "";
    private readonly object consoleLock = new();
    private ILogOutput? logOutput = null;

    public void SetLogStream(ILogOutput output)
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
            clientHandler!.Close();
            PrintMessageToConsole("Connection closed.", Models.LogTag.Client);
        }
    }

    public void Disconnect()
    { 
        clientHandler!.Writer.WriteLine("QUIT");
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
                string? line = clientHandler!.Reader.ReadLine();

                if (line == null) break;

                if (IsOpeningTag(line, out string? tagName))
                {
                    string tagContent = ReadTagContent(line, tagName!, clientHandler.Reader);
                    string? message = HandleTaggedMessage(tagName!, tagContent.ToString());

                    PrintMessageToConsole(message, Models.LogTag.Server);
                }
                else if (line.IndexOf("QUIT APPROVED", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }
                else
                {
                    PrintMessageToConsole(line, Models.LogTag.Server);
                }
            }
        }
        catch (Exception ex)
        {
            PrintMessageToConsole($"Reception error: {ex.Message}", Models.LogTag.Error);
        }
    }

    private string ReadTagContent(string openingLine, string tagName, StreamReader reader)
    {
        var tagContent = new StringBuilder();
        tagContent.AppendLine(openingLine);

        string endTag = $"</{tagName}>";
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            tagContent.AppendLine(line);
            if (line.Trim() == endTag) { break; }
        }

        return tagContent.ToString();
    }

    private bool IsOpeningTag(string line, out string? tagName)
    {
        tagName = null;
        line = line.Trim();

        if (line.StartsWith('<') && line.EndsWith('>') && !line.StartsWith("</"))
        {
            tagName = line[1..^1];
            return true;
        }

        return false;
    }

    private string? HandleTaggedMessage(string tagName, string block)
    {
        switch (tagName)
        {
            case "client_list":
                return ProcessClientList(block);

            default:
                PrintMessageToConsole($"Unknown tag <{tagName}> received", Models.LogTag.Error);
                PrintMessageToConsole(block);
                return null;
        }
    }

    private string? ProcessClientList(string block)
    {
        using StringReader stringReader = new(block);
        string? line;

        // Expecting for the opening tag
        line = stringReader.ReadLine();
        if (line?.Trim() != "<client_list>")
        {
            PrintMessageToConsole("Expected <client_list> tag", Models.LogTag.Error);
            return null;
        }

        string? header = stringReader.ReadLine();
        if (header?.Trim() != "CLIENT_LIST")
        {
            PrintMessageToConsole("Expected CLIENT_LIST header", Models.LogTag.Error);
            return null;
        }

        string? countLine = stringReader.ReadLine();
        int expectedCount = 0;
        if (countLine != null && int.TryParse(countLine.Replace("Client count: (", "").Replace(")", ""), out expectedCount))
        {
            // ok
        }

        var outputBuffer = new StringBuilder();
        outputBuffer.AppendLine($"\n--- List of connected clients ({expectedCount}) ---");

        connectedClients.Clear();

        // Reading client list
        while ((line = stringReader.ReadLine()) != null)
        {
            if (line.Trim() == "</client_list>")
                break;

            if (string.IsNullOrWhiteSpace(line)) continue;

            // Extracting the client's name from a string like "Client #X: name"
            string userName = line;
            int colonIndex = line.IndexOf(':');
            if (colonIndex >= 0)
            {
                userName = line.Substring(colonIndex + 1).Trim();
            }

            connectedClients.Add(userName);

            outputBuffer.AppendLine(userName == currentUser ? $"{userName} (You)" : userName);
        }

        outputBuffer.Append("----------------------------------");

        return outputBuffer.ToString();
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
