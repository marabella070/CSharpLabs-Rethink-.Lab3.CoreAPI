namespace Client_v.Core;

using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using Client_v.Handlers;
using Client_v.Models;
using Client_v.Interfaces;
using Shared.XML_Classes;
using Shared.ID_Management;
using Shared.Transaction;

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
    private TransactionStore transactionStore = new();
    private readonly IdManager<GridIdGenerator> _localTransactionIdsManager;
    private readonly Dictionary<int, TaskCompletionSource<int?>> _pendingGlobalTransactionIds = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingReleases = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingExchangeResponses = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingItemReceipts = new();
    private readonly Dictionary<int, TaskCompletionSource<T?>> _pendingIncomingItems = new();

    public void SetLogOutput(ILogOutput? output)
    {
        logOutput = output;
    }

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

    public void Disconnect()
    {
        var quitCommand = new QuitCommand { Reason = "Client requested disconnect" };
        string? quitXml = XmlHelper.SerializeToXml<QuitCommand>(quitCommand);

        if (quitXml == null)
        {
            PrintMessageToConsole("Something went wrong when sterilizing an xml document.", Models.LogTag.Error);
            return;
        }

        SendMessageToServer(quitXml);
    }

    private async Task<int?> AcquireGlobalTransactionIdAsync()
    {
        int pendingGlobalTransactionId = _localTransactionIdsManager.GetNextId();

        var tcs = new TaskCompletionSource<int?>();

        lock (_pendingGlobalTransactionIds)
        {
            _pendingGlobalTransactionIds[pendingGlobalTransactionId] = tcs;
        }

        // Добавляем создание и регистрацию транзакции
        var transaction = new Transaction<TransactionIdAcquisitionState>(
            TransactionIdAcquisitionState.Pending,
            new TransactionIdAcquisitionStateMachine()
        );

        transactionStore.Add(pendingGlobalTransactionId, transaction);

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

        // Добавляем создание и регистрацию транзакции
        var transaction = new Transaction<TransactionIdReleaseState>(
            TransactionIdReleaseState.Pending,
            new TransactionIdReleaseStateMachine()
        );

        transactionStore.Add(pendingReleaseTransitionId, transaction);

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










    private async Task<(int? serverTransactionId, Transaction<ExchangeSenderState>? transaction)> AcquireTransactionAndStartExchangeAsync() //+++++++++++++++++++++
    {
        int? serverTransactionId = await AcquireGlobalTransactionIdAsync();
        if (serverTransactionId is not int sid)
        {
            PrintMessageToConsole("Failed to acquire global transaction ID.", Models.LogTag.Error);
            return (null, null);
        }

        var transaction = new Transaction<ExchangeSenderState>(
            ExchangeSenderState.PendingTransactionId,
            new ExchangeSenderStateMachine()
        );
        transactionStore.Add(sid, transaction); //!!!!!!!! УДАЛИТЬ ЕГО КОГДА ЗАКОНЧУ
        transaction.TransitionTo(ExchangeSenderState.ExchangeRequestSent);

        return (sid, transaction);
    }

    private async Task<bool> SendExchangeRequestAsync<T>(int sid, int toClientId, T item) //+++++++++++++++++++++
    {
        var exchangeRequestCommand = new ExchangeRequest
        {
            serverTransactionId = sid,
            toClientId = toClientId,
            TypeOfExchangeObject = item!.GetType().Name
        };

        string? message = XmlHelper.SerializeToXml(exchangeRequestCommand);
        if (message == null)
        {
            PrintMessageToConsole("Failed to serialize exchange request.", Models.LogTag.Error);
            return false;
        }

        var tcs = new TaskCompletionSource<bool>();
        lock (_pendingExchangeResponses)
        {
            _pendingExchangeResponses[sid] = tcs;
        }

        SendMessageToServer(message);

        return await tcs.Task;
    }

    private async Task<bool> SendItemAsync<T>(int sid, T item) //+++++++++++++++++++++
    {
        var itemSendCommand = new SendItem<T>
        {
            serverTransactionId = sid,
            Item = item
        };

        string? message = XmlHelper.SerializeToXml(itemSendCommand);
        if (message == null)
        {
            PrintMessageToConsole("Failed to serialize item.", Models.LogTag.Error);
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

    private async Task<T?> ReceiveItemAsync<T>(int sid) //+++++++++++++++++++++
    {
        var reverseRequest = new ReverseExchangeRequest
        {
            serverTransactionId = sid
        };

        string? message = XmlHelper.SerializeToXml(reverseRequest);
        if (message == null)
        {
            PrintMessageToConsole("Failed to serialize reverse exchange request.", Models.LogTag.Error);
            return default;
        }

        var tcs = new TaskCompletionSource<T?>();
        lock (_pendingIncomingItems)
        {
            _pendingIncomingItems[sid] = tcs;
        }

        SendMessageToServer(message);

        return await tcs.Task;
    }

    private void ConfirmItemReceipt(int sid)
    {
        var confirm = new ReceiptConfirmation
        {
            serverTransactionId = sid
        };

        string? message = XmlHelper.SerializeToXml(confirm);
        if (message == null)
        {
            PrintMessageToConsole("Failed to serialize item receipt confirmation.", Models.LogTag.Error);
            return;
        }

        SendMessageToServer(message);
    }



    public async Task<T?> ExchangeAsync<T>(int toClientId, T item)
    {
        var (sid, transaction) = await AcquireTransactionAndStartExchangeAsync();

        if (sid is not int transactionId || transaction == null)
        {
            return default;
        }

        if (!await SendExchangeRequestAsync(transactionId, toClientId, item))
        {
            transaction.TransitionTo(ExchangeSenderState.Failed);
            await ReleaseGlobalTransactionIdAsync(transactionId);
            return default;
        }

        transaction.TransitionTo(ExchangeSenderState.ItemSent);

        if (!await SendItemAsync(transactionId, item))
        {
            transaction.TransitionTo(ExchangeSenderState.Failed);
            await ReleaseGlobalTransactionIdAsync(transactionId);
            return default;
        }

        transaction.TransitionTo(ExchangeSenderState.WaitingReverseExchangeRequest);

        T? receivedItem = await ReceiveItemAsync<T>(transactionId);
        if (receivedItem == null)
        {
            transaction.TransitionTo(ExchangeSenderState.Failed);
            await ReleaseGlobalTransactionIdAsync(transactionId);
            return default;
        }

        ConfirmItemReceipt(transactionId);
        transaction.TransitionTo(ExchangeSenderState.ItemReceiptConfirmedToRecipient);
        transaction.TransitionTo(ExchangeSenderState.CompletedSuccessfully);

        await ReleaseGlobalTransactionIdAsync(transactionId);

        return receivedItem;
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

                    // Получаем и обновляем состояние транзакции
                    if (transactionStore.TryGet<TransactionIdAcquisitionState>(response.ClientTransactionId, out var transaction))
                    {
                        var newState = response.Success
                            ? TransactionIdAcquisitionState.Completed
                            : TransactionIdAcquisitionState.Failed;

                        if (!transaction!.TransitionTo(newState))
                        {
                            PrintMessageToConsole($"Invalid state transition for transaction {response.ClientTransactionId}", Models.LogTag.Error);
                            return;
                        }

                        transactionStore.Remove(response.ClientTransactionId);
                    }
                    else
                    {
                        PrintMessageToConsole($"Transaction {response.ClientTransactionId} not found", Models.LogTag.Warning);
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

                    // Получаем и обновляем состояние транзакции
                    if (transactionStore.TryGet<TransactionIdReleaseState>(response.ClientTransactionId, out var transaction))
                    {
                        var newState = response.Success
                            ? TransactionIdReleaseState.Released
                            : TransactionIdReleaseState.Failed;

                        if (!transaction!.TransitionTo(newState))
                        {
                            PrintMessageToConsole($"Invalid state transition for transaction {response.ClientTransactionId}", Models.LogTag.Error);
                            return;
                        }

                        transactionStore.Remove(response.ClientTransactionId);
                    }
                    else
                    {
                        PrintMessageToConsole($"Transaction {response.ClientTransactionId} not found", Models.LogTag.Warning);
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
                    var response = XmlHelper.DeserializeXml<TransitionIdReleaseResponse>(xml);

                    if (response == null)
                    {
                        PrintMessageToConsole("Invalid transition id release response format", Models.LogTag.Error);
                        return;
                    }

                    // Получаем и обновляем состояние транзакции
                    if (transactionStore.TryGet<ExchangeSenderState>(response.ServerTransactionId, out var transaction))
                    {
                        var newState = response.Success
                            ? ExchangeSenderState.ExchangeResponseReceived
                            : ExchangeSenderState.Failed;

                        if (!transaction!.TransitionTo(newState))
                        {
                            PrintMessageToConsole($"Invalid state transition for transaction {response.ServerTransactionId}", Models.LogTag.Error);
                            return;
                        }
                    }
                    else
                    {
                        PrintMessageToConsole($"Transaction {response.ServerTransactionId} not found", Models.LogTag.Warning);
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

                    // Получаем и обновляем состояние транзакции
                    if (transactionStore.TryGet<ExchangeSenderState>(response.ServerTransactionId, out var transaction))
                    {
                        var newState = response.Success
                            ? ExchangeSenderState.ItemReceiptConfirmedByRecipient
                            : ExchangeSenderState.Failed;

                        if (!transaction!.TransitionTo(newState))
                        {
                            PrintMessageToConsole($"Invalid state transition for transaction {response.ServerTransactionId}", Models.LogTag.Error);
                            return;
                        }
                    }
                    else
                    {
                        PrintMessageToConsole($"Transaction {response.ServerTransactionId} not found", Models.LogTag.Warning);
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
                    IncomingItem? incoming = XmlHelper.DeserializeXml<IncomingItem>(xml);

                    if (incoming == null || incoming.Item == null)
                    {
                        PrintMessageToConsole("Invalid incoming_item format or missing item data.", Models.LogTag.Error);
                        return;
                    }

                    // Получаем и обновляем состояние транзакции
                    if (transactionStore.TryGet<ExchangeSenderState>(response.ServerTransactionId, out var transaction))
                    {
                        var newState = response.Success
                            ? ExchangeSenderState.ItemReceivedFromRecipient
                            : ExchangeSenderState.Failed;

                        if (!transaction!.TransitionTo(newState))
                        {
                            PrintMessageToConsole($"Invalid state transition for transaction {response.ServerTransactionId}", Models.LogTag.Error);
                            return;
                        }
                    }
                    else
                    {
                        PrintMessageToConsole($"Transaction {response.ServerTransactionId} not found", Models.LogTag.Warning);
                        return;
                    }

                    lock (_pendingIncomingItems)
                    {
                        if (_pendingIncomingItems.TryGetValue(incoming.serverTransactionId, out var tcsObj))
                        {
                            // Пытаемся десериализовать объект item как T (в зависимости от ожидаемого типа)
                            try
                            {
                                Type? expectedType = tcsObj.Task.GetType().GenericTypeArguments.FirstOrDefault(); // получаем тип T из Task<T>

                                if (expectedType == null)
                                {
                                    PrintMessageToConsole("Cannot determine expected type for item.", Models.LogTag.Error);
                                    tcsObj.SetResult(null);
                                }
                                else
                                {
                                    object? deserialized = XmlHelper.DeserializeXmlFromElement(expectedType, incoming.Item);
                                    tcsObj.SetResult(deserialized);
                                }
                            }
                            catch (Exception ex)
                            {
                                PrintMessageToConsole($"Failed to deserialize incoming item: {ex.Message}", Models.LogTag.Error);
                                tcsObj.SetResult(null);
                            }

                            _pendingIncomingItems.Remove(incoming.serverTransactionId);
                        }
                        else
                        {
                            PrintMessageToConsole($"Unexpected incoming item for transaction {incoming.serverTransactionId}", Models.LogTag.Warning);
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
