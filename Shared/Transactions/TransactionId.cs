namespace Shared.Transaction;

public enum TransactionIdAcquisitionState
{
    Pending,       // Запрос готовится/отправляется
    Completed,     // Успешный ответ от сервера
    Failed         // Ошибка или отрицательный ответ
}

public class TransactionIdAcquisitionStateMachine : ITransactionStateMachine<TransactionIdAcquisitionState>
{
    private static readonly Dictionary<TransactionIdAcquisitionState, TransactionIdAcquisitionState[]> ValidTransitions = new()
    {
        [TransactionIdAcquisitionState.Pending] = new[] {
            TransactionIdAcquisitionState.Completed,
            TransactionIdAcquisitionState.Failed
        },
        // Терминальные состояния
        [TransactionIdAcquisitionState.Completed] = Array.Empty<TransactionIdAcquisitionState>(),
        [TransactionIdAcquisitionState.Failed] = Array.Empty<TransactionIdAcquisitionState>()
    };

    public bool IsTransitionAllowed(TransactionIdAcquisitionState from, TransactionIdAcquisitionState to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}


public enum TransactionIdReleaseState
{
    Pending,     // Запрос на освобождение в процессе
    Released,    // Успешное освобождение
    Failed       // Ошибка освобождения
}

public class TransactionIdReleaseStateMachine : ITransactionStateMachine<TransactionIdReleaseState>
{
    private static readonly Dictionary<TransactionIdReleaseState, TransactionIdReleaseState[]> ValidTransitions = new()
    {
        [TransactionIdReleaseState.Pending] = new[] {
            TransactionIdReleaseState.Released,
            TransactionIdReleaseState.Failed
        },
        // Терминальные состояния
        [TransactionIdReleaseState.Released] = Array.Empty<TransactionIdReleaseState>(),
        [TransactionIdReleaseState.Failed] = Array.Empty<TransactionIdReleaseState>()
    };

    public bool IsTransitionAllowed(TransactionIdReleaseState from, TransactionIdReleaseState to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}
