namespace Shared.Transaction;

// public class Transaction<TState> : ITransaction where TState : Enum
// {
//     public TState State { get; private set; }

//     Enum ITransaction.GetState() => State;

//     public bool Advance()
//     {
//         var next = _stateMachine.GetNextState(State);
//         if (next == null)
//             return false;

//         return TransitionTo(next.Value);
//     }

//     private readonly ITransactionStateMachine<TState> _stateMachine;

//     public Transaction(TState initialState, ITransactionStateMachine<TState> stateMachine)
//     {
//         State = initialState;
//         _stateMachine = stateMachine;
//     }

//     public bool IsTransitionAllowed(TState to)
//     {
//         return _stateMachine.IsTransitionAllowed(State, to);
//     }

//     public bool TransitionTo(TState to)
//     {
//         if (IsTransitionAllowed(to))
//         {
//             State = to;
//             return true;
//         }
//         return false;
//     }
// }