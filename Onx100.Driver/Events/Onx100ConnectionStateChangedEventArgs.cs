using Onx100.Driver.Enums;

namespace Onx100.Driver.Events
{
    public sealed class Onx100ConnectionStateChangedEventArgs : EventArgs
    {
        /*************** PUBLIC PROPERTIES ********************/
        public Onx100ConnectionState PreviousState { get; }
        public Onx100ConnectionState CurrentState { get; }


        /*************** CONSTRUCTOR ********************/
        public Onx100ConnectionStateChangedEventArgs(Onx100ConnectionState previousState, Onx100ConnectionState currentState) {
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }
}
