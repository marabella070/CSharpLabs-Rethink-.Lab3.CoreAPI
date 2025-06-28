namespace Shared.Transaction;

// public enum ExchangeRecipientState
// {
//     IncomingExchangeOfferReceived,        // 0. Получено предложение на обмен
//     ExchangeOfferResponded,               // 1. Ответ отправлен: согласие/отказ
//     ItemReceivedFromSender,               // 2. Получен предмет от клиента 1
//     ItemReceiptConfirmedToSender,         // 3. Подтверждение отправлено
//     ReverseExchangeRequestedBySender,     // 4. Получен запрос на обратный обмен
//     ItemSentToSender,                     // 5. Отправлен предмет клиенту 1
//     ItemReceiptConfirmedBySender,         // 6. Получено подтверждение от клиента 1
//     CompletedSuccessfully,                // 7. Успешное завершение
//     Failed                                // 7. Неуспешное завершение
// }

// public class ExchangeRecipientStateMachine : ITransactionStateMachine<ExchangeRecipientState>
// {
//     public ExchangeRecipientState? GetNextState(ExchangeRecipientState current)
//     {
//         if (ValidTransitions.TryGetValue(current, out var transitions))
//         {
//             // Возвращаем первый валидный, не "Failed", не терминальный
//             return transitions.FirstOrDefault(s => !s.Equals((ExchangeRecipientState)(object)ExchangeRecipientState.Failed));
//         }

//         return default;
//     }

//     private static readonly Dictionary<ExchangeRecipientState, ExchangeRecipientState[]> ValidTransitions = new()
//     {
//         [ExchangeRecipientState.IncomingExchangeOfferReceived] = new[] {
//             ExchangeRecipientState.ExchangeOfferResponded,
//             ExchangeRecipientState.Failed
//         },

//         [ExchangeRecipientState.ExchangeOfferResponded] = new[] {
//             ExchangeRecipientState.ItemReceivedFromSender,
//             ExchangeRecipientState.Failed
//         },

//         [ExchangeRecipientState.ItemReceivedFromSender] = new[] {
//             ExchangeRecipientState.ItemReceiptConfirmedToSender,
//             ExchangeRecipientState.Failed
//         },

//         [ExchangeRecipientState.ItemReceiptConfirmedToSender] = new[] {
//             ExchangeRecipientState.ReverseExchangeRequestedBySender,
//             ExchangeRecipientState.Failed
//         },

//         [ExchangeRecipientState.ReverseExchangeRequestedBySender] = new[] {
//             ExchangeRecipientState.ItemSentToSender,
//             ExchangeRecipientState.Failed
//         },

//         [ExchangeRecipientState.ItemSentToSender] = new[] {
//             ExchangeRecipientState.ItemReceiptConfirmedBySender,
//             ExchangeRecipientState.Failed
//         },

//         [ExchangeRecipientState.ItemReceiptConfirmedBySender] = new[] {
//             ExchangeRecipientState.CompletedSuccessfully,
//             ExchangeRecipientState.Failed
//         },

//         // Терминальные состояния
//         [ExchangeRecipientState.CompletedSuccessfully] = Array.Empty<ExchangeRecipientState>(),
//         [ExchangeRecipientState.Failed] = Array.Empty<ExchangeRecipientState>()
//     };

//     public bool IsTransitionAllowed(ExchangeRecipientState from, ExchangeRecipientState to)
//     {
//         return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
//     }
// }
