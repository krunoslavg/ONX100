using Onx100.Driver.Enums;
using Onx100.Driver.Models;

namespace Onx100.Driver.Events
{
    public sealed class Onx100DeviceStateChangedEventArgs : EventArgs
    {
        /*************** PUBLIC PROPERTIES ********************/
        public Onx100DeviceState PreviousState { get; }
        public Onx100DeviceState CurrentState { get; }


        /*************** CONSTRUCTOR ********************/
        public Onx100DeviceStateChangedEventArgs(Onx100DeviceState previousState, Onx100DeviceState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }
}
