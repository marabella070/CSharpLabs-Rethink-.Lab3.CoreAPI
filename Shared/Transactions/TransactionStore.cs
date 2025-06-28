namespace Shared.Transaction;

public class TransactionStore
{
    private readonly Dictionary<int, ITransaction> _transactions = new();

    public void Add<TState>(int id, Transaction<TState> transaction) where TState : Enum
        => _transactions[id] = transaction;

    public bool TryGet<TState>(int id, out Transaction<TState>? transaction) where TState : Enum
    {
        if (_transactions.TryGetValue(id, out var baseTransaction) && baseTransaction is Transaction<TState> typed)
        {
            transaction = typed;
            return true;
        }

        transaction = null;
        return false;
    }

    public void Remove(int id) => _transactions.Remove(id);
}
