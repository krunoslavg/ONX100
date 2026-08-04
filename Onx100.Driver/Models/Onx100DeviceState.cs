using System.Collections.ObjectModel;
using Onx100.Driver.Enums;

namespace Onx100.Driver.Models
{
    public sealed record Onx100DeviceState
    {
        /*************** PUBLIC PROPERTIES ********************/
        public Onx100PowerState PowerState { get; init; } = Onx100PowerState.Unknown;
        public int? SelectedInput {  get; init; }
        public int? Volume { get; init; }
        public bool? IsMuted { get; init; }
        public IReadOnlyDictionary<int, Onx100SignalState> SignalStates { get; init; } = new ReadOnlyDictionary<int, Onx100SignalState>(
            new Dictionary<int, Onx100SignalState> {
                [1] = Onx100SignalState.Unknown, [2] = Onx100SignalState.Unknown, [3] = Onx100SignalState.Unknown, [4] = Onx100SignalState.Unknown 
            }
        );            
    }
}
