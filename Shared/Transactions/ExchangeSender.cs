namespace Shared.Transaction;

public enum ExchangeSenderState
{
    PendingTransactionId,                 // 0. Запрошен ServerTransactionId, ожидается ответ
    ExchangeRequestSent,                  // 1. Отправлен ExchangeRequest клиенту 2
    ExchangeResponseReceived,             // 2. Получен ответ от клиента 2
    ItemSent,                             // 3. Отправлен предмет от клиента 1
    ItemReceiptConfirmedByRecipient,      // 4. Получено подтверждение от клиента 2
    WaitingReverseExchangeRequest,        // 5. Запрос обратного обмена отправлен
    ItemReceivedFromRecipient,            // 6. Получен предмет от клиента 2
    ItemReceiptConfirmedToRecipient,      // 7. Подтверждено получение предмета
    CompletedSuccessfully,                // 8. Успешное завершение
    Failed                                // 8. Неуспешное завершение
}

public class ExchangeSenderStateMachine : ITransactionStateMachine<ExchangeSenderState>
{
    private static readonly Dictionary<ExchangeSenderState, ExchangeSenderState[]> ValidTransitions = new()
    {
        [ExchangeSenderState.PendingTransactionId] = new[] {
            ExchangeSenderState.ExchangeRequestSent,
            ExchangeSenderState.Failed
        },

        [ExchangeSenderState.ExchangeRequestSent] = new[] {
            ExchangeSenderState.ExchangeResponseReceived,
            ExchangeSenderState.Failed
        },

        [ExchangeSenderState.ExchangeResponseReceived] = new[] {
            ExchangeSenderState.ItemSent,
            ExchangeSenderState.Failed
        },

        [ExchangeSenderState.ItemSent] = new[] {
            ExchangeSenderState.ItemReceiptConfirmedByRecipient,
            ExchangeSenderState.Failed
        },

        [ExchangeSenderState.ItemReceiptConfirmedByRecipient] = new[] {
            ExchangeSenderState.WaitingReverseExchangeRequest,
            ExchangeSenderState.Failed
        },

        [ExchangeSenderState.WaitingReverseExchangeRequest] = new[] {
            ExchangeSenderState.ItemReceivedFromRecipient,
            ExchangeSenderState.Failed
        },

        [ExchangeSenderState.ItemReceivedFromRecipient] = new[] {
            ExchangeSenderState.ItemReceiptConfirmedToRecipient,
            ExchangeSenderState.Failed
        },

        [ExchangeSenderState.ItemReceiptConfirmedToRecipient] = new[] {
            ExchangeSenderState.CompletedSuccessfully,
            ExchangeSenderState.Failed
        },

        // Терминальные состояния
        [ExchangeSenderState.CompletedSuccessfully] = Array.Empty<ExchangeSenderState>(),
        [ExchangeSenderState.Failed] = Array.Empty<ExchangeSenderState>()
    };

    public bool IsTransitionAllowed(ExchangeSenderState from, ExchangeSenderState to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}

