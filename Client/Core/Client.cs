namespace Client_v.Core;

using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;

using Client_v.Handlers;
using Client_v.Models;
using Client_v.Interfaces;
using Shared.XML_Classes;
using Shared.ID_Management;
using Shared.Delegates;

public class Client
{
    // main
    private TcpClient? client;
    private Handlers.ClientHandler? clientHandler;

    // client parameters
    public string currentUser = string.Empty;
    private readonly string host;
    private readonly int port;

    // input/output parameters
    private string currentInput = "";
    private readonly object consoleLock = new();
    private ILogOutput? logOutput = null;

    //
    private List<ClientEntry> connectedClients = new();
    private List<int> _transactions = new();
    private readonly IdManager<GridIdGenerator> _localTransactionIdsManager;
    private readonly Dictionary<int, TaskCompletionSource<int?>> _pendingGlobalTransactionIds = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingReleases = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingExchangeResponses = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingItemReceipts = new();
    private readonly Dictionary<int, TaskCompletionSource<object?>> _pendingIncomingItems = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingReverseRequests = new();
    private readonly Dictionary<int, TaskCompletionSource<string?>> _pendingExchangeOffers = new();

    private ExchangeRequestHandler? _exchangeRequestHandler;


    // Метод для регистрации обработчика запроса
    public void RegisterExchangeRequestHandler(ExchangeRequestHandler handler)
    {
        _exchangeRequestHandler = handler;
    }

    public void SetLogOutput(ILogOutput? output)
    {
        logOutput = output;
    }

    public List<ClientEntry> GetConnectedClients() => connectedClients;

    public Client(string host, int port)
    {
        this.host = host;
        this.port = port;

        var gridIdGenerator = new GridIdGenerator("node-2");
        _localTransactionIdsManager = new IdManager<GridIdGenerator>(gridIdGenerator);
    }

    public void Run()
    {
        try
        {
            client = new TcpClient(host, port);

            clientHandler = new Handlers.ClientHandler(client);

            clientHandler.Run();

            AuthenticateUser();

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

    public bool Disconnect()
    {
        if (clientHandler == null || clientHandler.IsRunning) { return true; }

        var quitCommand = new QuitCommand { Reason = "Client requested disconnect" };
        string? quitXml = XmlHelper.SerializeToXml<QuitCommand>(quitCommand);

        if (quitXml == null)
        {
            PrintMessageToConsole("Something went wrong when sterilizing an xml document.", Models.LogTag.Error);
            return false;
        }

        SendMessageToServer(quitXml);

        return true;
    }

    private async Task<int?> AcquireGlobalTransactionIdAsync()
    {
        int pendingGlobalTransactionId = _localTransactionIdsManager.GetNextId();

        var tcs = new TaskCompletionSource<int?>();

        lock (_pendingGlobalTransactionIds)
        {
            _pendingGlobalTransactionIds[pendingGlobalTransactionId] = tcs;
        }

        _transactions.Add(pendingGlobalTransactionId);

        var transtitionIdRequestCommand = new TransitionIdRequest
        {
            ClientTransactionId = pendingGlobalTransactionId
        };

        string? xmlMessage = XmlHelper.SerializeToXml<TransitionIdRequest>(transtitionIdRequestCommand);

        if (xmlMessage == null)
        {
            PrintMessageToConsole("Something went wrong when sterilizing an xml document.", Models.LogTag.Error);
            return null;
        }

        SendMessageToServer(xmlMessage);

        return await tcs.Task; // ожидание ответа от сервера
    }

    public async Task<bool> ReleaseGlobalTransactionIdAsync(int serverTransactionId)
    {
        int pendingReleaseTransitionId = _localTransactionIdsManager.GetNextId();

        var tcs = new TaskCompletionSource<bool>();

        lock (_pendingReleases)
        {
            _pendingReleases[pendingReleaseTransitionId] = tcs;
        }

        _transactions.Add(pendingReleaseTransitionId);

        var releaseRequest = new TransitionIdRelease
        {
            ClientTransactionId = pendingReleaseTransitionId,
            ServerTransactionId = serverTransactionId
        };

        string? xmlMessage = XmlHelper.SerializeToXml(releaseRequest);
        if (xmlMessage == null)
        {
            PrintMessageToConsole("Failed to serialize release request.", Models.LogTag.Error);
            return false;
        }

        SendMessageToServer(xmlMessage);

        return await tcs.Task;
    }

    private async Task<int?> AcquireTransactionAndStartExchangeAsync()
    {
        int? serverTransactionId = await AcquireGlobalTransactionIdAsync();
        if (serverTransactionId is not int sid)
        {
            PrintMessageToConsole("Failed to acquire global transaction ID.", Models.LogTag.Error);
            return null;
        }

        return sid;
    }

    private void ConfirmItemReceipt(int sid, int toClientId)
    {
        var confirm = new ReceiptConfirmation
        {
            ServerTransactionId = sid,
            toClientId = toClientId,
            Success = true
        };

        string? message = XmlHelper.SerializeToXml(confirm);
        if (message == null)
        {
            PrintMessageToConsole("Failed to serialize receipt confirmation to XML.", Models.LogTag.Error);
            return;
        }

        SendMessageToServer(message);
    }

    private async Task<object?> SendExchangeRequestAsync<T>(int sid, int toClientId, T item)
    {
        // Сериализуем объект в JSON (используем безопасный JsonSerializer)
        string jsonData;
        try
        {
            jsonData = JsonSerializer.Serialize(item);
        }
        catch (Exception ex)
        {
            PrintMessageToConsole($"Failed to serialize {typeof(T).Name} to JSON: {ex.Message}", Models.LogTag.Error);
            return null;
        }

        // Преобразуем сериализованный JSON в строку Base64
        string base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonData));

        // Получаем имя типа
        string? typeName = typeof(T).AssemblyQualifiedName;
        if (typeName == null)
        {
            PrintMessageToConsole($"Failed to get type name for {typeof(T).Name}.", Models.LogTag.Error);
            return null;
        }

        var exchangeRequestCommand = new ExchangeRequest
        {
            ServerTransactionId = sid,
            toClientId = toClientId,
            TypeOfExchangeObject = typeName,
            XmlPayload = base64Data
        };

        string? message = XmlHelper.SerializeToXml(exchangeRequestCommand);
        if (message == null)
        {
            PrintMessageToConsole("Failed to serialize exchange request command.", Models.LogTag.Error);
            return null;
        }

        var tcs = new TaskCompletionSource<object?>();
        lock (_pendingIncomingItems)
        {
            _pendingIncomingItems[sid] = tcs;
        }

        SendMessageToServer(message);

        return await tcs.Task;
    }


    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Sender!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    public async Task<T?> ExchangeAsync<T>(int toClientId, T item)
    {
        var sid = await AcquireTransactionAndStartExchangeAsync();

        if (sid is not int transactionId) { return default; }

        // 1. Отправка запроса за обмен с клиентом toClientId, на элемент item
        // Отправка запроса на обмен

        var receivedObject = await SendExchangeRequestAsync(transactionId, toClientId, item);
        if (receivedObject == null) { return default; }

        if (receivedObject is T typedResult)
        {
            // Подтверждение получения предмета
            ConfirmItemReceipt(transactionId, toClientId);
            await ReleaseGlobalTransactionIdAsync(transactionId);
            return typedResult;
        }

        PrintMessageToConsole($"Received item type {receivedObject.GetType().Name} doesn't match expected type {typeof(T).Name}.", Models.LogTag.Error);
        await ReleaseGlobalTransactionIdAsync(transactionId);

        return default;
    }











    private async Task<bool> SendItemAsync<T>(int sid, int toClientId, T item) //+++++++++++++++++++++
    {
        // Сериализуем объект в JSON (используем безопасный JsonSerializer)
        string jsonData;
        try
        {
            jsonData = JsonSerializer.Serialize(item);
        }
        catch (Exception ex)
        {
            PrintMessageToConsole($"Failed to serialize {typeof(T).Name} to JSON: {ex.Message}", Models.LogTag.Error);
            return false;
        }

        // Преобразуем сериализованный JSON в строку Base64
        string base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonData));

        // Получаем имя типа
        string? typeName = typeof(T).AssemblyQualifiedName;
        if (typeName == null)
        {
            PrintMessageToConsole($"Failed to get type name for {typeof(T).Name}.", Models.LogTag.Error);
            return false;
        }

        var itemSendCommand = new SendItem
        {
            ServerTransactionId = sid,
            toClientId = toClientId,
            TypeName = typeName,
            XmlPayload = base64Data
        };

        string? message = XmlHelper.SerializeToXml(itemSendCommand);
        if (message == null)
        {
            PrintMessageToConsole("Failed to serialize item send command.", Models.LogTag.Error);
            return false;
        }

        var tcs = new TaskCompletionSource<bool>();
        lock (_pendingItemReceipts)
        {
            _pendingItemReceipts[sid] = tcs;
        }

        SendMessageToServer(message);

        return await tcs.Task;
    }

    // Receiver
    private async Task HandleExchangeRequest(int transactionId, int fromClientId, string TypeOfExchangeObject, object item)
    {
        if (_exchangeRequestHandler == null) { return; }

        // Получаем согласие и сам предмет
        var response = await _exchangeRequestHandler(TypeOfExchangeObject, item);

        // Пользователь отказался — ничего не делаем
        if (!response.Accept) { return; }

        if (!await SendItemAsync(transactionId, fromClientId, response.OfferedItem))
        {
            return;
        }
        return;
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
                Disconnect();
                currentInput = "";
                return true;
            }


            SendMessageToServer(currentInput);
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
                string message = XmlHelper.ReadUntilEOF(clientHandler!.Reader);

                if (string.IsNullOrWhiteSpace(message)) { continue; }

                // Remove Beginning/Ending spaces and <EOF> marker for clear check
                string cleanMessage = message.Trim().Replace("<EOF>", "");

                if (cleanMessage.TrimStart().StartsWith("<?xml") &&
                    XmlHelper.TryGetRootTagName(cleanMessage, out string? tagName))
                {
                    HandleTaggedMessage(tagName!, cleanMessage);

                    if (tagName!.ToLower() == "quit_approved") { break; }

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

    private void HandleTaggedMessage(string tagName, string xml)
    {
        switch (tagName.ToLower())
        {
            case "client_list":
                { 
                    var clients = XmlHelper.DeserializeXml<ClientListMessage>(xml);

                    if (clients == null)
                    {
                        PrintMessageToConsole("Invalid client list format", Models.LogTag.Error);
                        return;
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

                    PrintMessageToConsole(builder.ToString(), Models.LogTag.Server);
    #endif
                    break;
                }
            case "quit_approved":
                { 
                    QuitApprovedCommand? quitApprovedCommand = XmlHelper.DeserializeXml<QuitApprovedCommand>(xml);

                    if (quitApprovedCommand == null)
                    {
                        PrintMessageToConsole("Invalid quit approved command format", Models.LogTag.Error);
                        return;
                    }

                    string logMessage = $"Quit request approved by {quitApprovedCommand.ApprovedBy}, " +
                                        $"Timestamp: {quitApprovedCommand.Timestamp}";

                    PrintMessageToConsole(logMessage, Models.LogTag.Server);

                    break;
                }
            case "transition_id_request_response":
                {
                    TransitionIdRequestResponse? response = XmlHelper.DeserializeXml<TransitionIdRequestResponse>(xml);

                    if (response == null)
                    {
                        PrintMessageToConsole("Invalid transition id request response command format", Models.LogTag.Error);
                        return;
                    }

                    lock (_pendingGlobalTransactionIds)
                    {
                        if (_pendingGlobalTransactionIds.TryGetValue(response.ClientTransactionId, out var tcs))
                        {
                            tcs.SetResult(response.Success ? response.ServerTransactionId : null);

                            _pendingGlobalTransactionIds.Remove(response.ClientTransactionId);
                            _localTransactionIdsManager.ReleaseId(response.ClientTransactionId);
                        }
                    }

                    break;
                }
            case "transition_id_release_response":
                { 
                    var response = XmlHelper.DeserializeXml<TransitionIdReleaseResponse>(xml);

                    if (response == null)
                    {
                        PrintMessageToConsole("Invalid transition id release response format", Models.LogTag.Error);
                        return;
                    }

                    lock (_pendingReleases)
                    {
                        if (_pendingReleases.TryGetValue(response.ClientTransactionId, out var tcs))
                        {
                            tcs.SetResult(response.Success);
                            _pendingReleases.Remove(response.ClientTransactionId);
                            _localTransactionIdsManager.ReleaseId(response.ClientTransactionId);
                        }
                    }

                    break;
                }
            case "exchange_response_result":
                {
                    var response = XmlHelper.DeserializeXml<ExchangeResponseResult>(xml);

                    if (response == null)
                    {
                        PrintMessageToConsole("Invalid transition id release response format", Models.LogTag.Error);
                        return;
                    }

                    lock (_pendingExchangeResponses)
                    {
                        if (_pendingExchangeResponses.TryGetValue(response.ServerTransactionId, out var tcs))
                        {
                            tcs.SetResult(response.Success);
                            _pendingExchangeResponses.Remove(response.ServerTransactionId);
                        }
                    }

                    break;
                }
            case "receipt_confirmation_result":
                {
                    var response = XmlHelper.DeserializeXml<ReceiptConfirmationResult>(xml);

                    if (response == null)
                    {
                        PrintMessageToConsole("Invalid transition id release response format", Models.LogTag.Error);
                        return;
                    }

                    lock (_pendingItemReceipts)
                    {
                        if (_pendingItemReceipts.TryGetValue(response.ServerTransactionId, out var tcs))
                        {
                            tcs.SetResult(response.Success);
                            _pendingItemReceipts.Remove(response.ServerTransactionId);
                        }
                    }

                    break;
                }



            case "incoming_item":
                {
                    var incoming = XmlHelper.DeserializeXml<IncomingItem>(xml);

                    if (incoming == null || string.IsNullOrWhiteSpace(incoming.XmlPayload) || string.IsNullOrWhiteSpace(incoming.TypeName))
                    {
                        PrintMessageToConsole("Invalid incoming_item: missing payload or type name.", Models.LogTag.Error);
                        return;
                    }

                    lock (_pendingIncomingItems)
                    {
                        if (_pendingIncomingItems.TryGetValue(incoming.ServerTransactionId, out var tcsObj))
                        {
                            try
                            {
                                // Декодируем Base64 строку в байты
                                byte[] binaryData = Convert.FromBase64String(incoming.XmlPayload);

                                // Преобразуем байты обратно в строку JSON
                                string jsonData = Encoding.UTF8.GetString(binaryData);

                                // Десериализуем JSON строку обратно в объект
                                var deserialized = JsonSerializer.Deserialize(jsonData, Type.GetType(incoming.TypeName));

                                if (deserialized == null)
                                {
                                    PrintMessageToConsole("Failed to deserialize object from JSON payload.", Models.LogTag.Error);
                                }

                                tcsObj.SetResult(deserialized);
                            }
                            catch (Exception ex)
                            {
                                PrintMessageToConsole($"Exception during deserialization: {ex.Message}", Models.LogTag.Error);
                                tcsObj.SetResult(null);
                            }

                            _pendingIncomingItems.Remove(incoming.ServerTransactionId);
                        }
                        else
                        {
                            PrintMessageToConsole($"Unexpected incoming item for transaction {incoming.ServerTransactionId}", Models.LogTag.Warning);
                        }
                    }

                    break;
                }
            case "exchange_offer":
                {
                    var response = XmlHelper.DeserializeXml<ExchangeOffer>(xml);

                    if (response == null || string.IsNullOrWhiteSpace(response.XmlPayload) || string.IsNullOrWhiteSpace(response.TypeOfExchangeObject))
                    {
                        PrintMessageToConsole("Invalid incoming_item: missing payload or type name.", Models.LogTag.Error);
                        return;
                    }

                    try
                    {
                        // Декодируем Base64 строку в байты
                        byte[] binaryData = Convert.FromBase64String(response.XmlPayload);
                        
                        // Преобразуем байты обратно в строку JSON
                        string jsonData = Encoding.UTF8.GetString(binaryData);

                        // Десериализуем JSON строку обратно в объект
                        var deserialized = JsonSerializer.Deserialize(jsonData, Type.GetType(response.TypeOfExchangeObject));

                        if (deserialized == null)
                        {
                            PrintMessageToConsole("Failed to deserialize object from JSON payload.", Models.LogTag.Error);
                        }

                        // Запускаем обработку в фоне, без ожидания
                        _ = HandleExchangeRequest(response.ServerTransactionId,
                                                    response.fromClientId,
                                                    response.TypeOfExchangeObject,
                                                    deserialized!);
                    }
                    catch (Exception ex)
                    {
                        PrintMessageToConsole($"Exception during deserialization: {ex.Message}", Models.LogTag.Error);
                    }

                    break;
                }



            case "reverse_exchange_offer":
                { 
                    var response = XmlHelper.DeserializeXml<ReverseExchangeOffer>(xml);

                    if (response == null)
                    {
                        PrintMessageToConsole("Invalid transition id release response format", Models.LogTag.Error);
                        return;
                    }

                    lock (_pendingReverseRequests)
                    {
                        if (_pendingReverseRequests.TryGetValue(response.ServerTransactionId, out var tcs))
                        {
                            tcs.SetResult(true);
                            _pendingReverseRequests.Remove(response.ServerTransactionId);
                        }
                    }

                    break;
                }

            default:
                { 
                    PrintMessageToConsole($"[Unhandled XML tag: {tagName}]", Models.LogTag.Error);
                    break;

                }
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
    
    private void AuthenticateUser()
    {
#if CONSOLE_CLIENT
        // User Name Request
        PrintMessageToConsole("Enter your name:", Models.LogTag.Client);

        currentUser = Console.ReadLine()?.Trim() ?? $"user{Guid.NewGuid().ToString()[..4]}";
        Console.WriteLine();
#endif

        var authCommand = new AuthCommand { ClientName = currentUser };
        string? xmlMessage = XmlHelper.SerializeToXml<AuthCommand>(authCommand);

        if (xmlMessage == null)
        {
            PrintMessageToConsole("Something went wrong when sterilizing an xml document.", Models.LogTag.Error);
            return;
        }

        SendMessageToServer(xmlMessage);

        PrintMessageToConsole($"Connected to the server as {currentUser}...\n", Models.LogTag.Client);
    }

    private void SendMessageToServer(string message)
    {
        try
        {
            // Adding a marker for the end of the message on a new line
            string messageWithEof = $"{message}\n<EOF>";

            // Sending a message
            clientHandler?.Writer.WriteLine(messageWithEof);
        }
        catch (Exception ex)
        {
            PrintMessageToConsole($"Error sending message to server: {ex.Message}", Models.LogTag.Error);
        }
    }
}
