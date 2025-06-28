namespace Shared.Transaction;

public interface ITransactionStateMachine<TState> where TState : Enum
{
    bool IsTransitionAllowed(TState from, TState to);
}