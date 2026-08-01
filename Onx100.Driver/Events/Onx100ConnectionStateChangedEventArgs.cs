using Onx100.Driver.Enums;

namespace Onx100.Driver.Events
{
    public sealed class Onx100ConnectionStateChangedEventArgs : EventArgs
    {
        public Onx100ConnectionState PreviousState { get; }
        public Onx100ConnectionState CurrentState { get; }


        public Onx100ConnectionStateChangedEventArgs(Onx100ConnectionState previousState, Onx100ConnectionState currentState) {
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }
}
