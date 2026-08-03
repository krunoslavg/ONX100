using Onx100.Driver.Enums;
using Onx100.Driver.Models;

namespace Onx100.Api.Models;

public sealed record DeviceStateResponse(string ConnectionState, string PowerState, int? SelectedInput, int? Volume, bool? IsMuted, IReadOnlyDictionary<int, string> SignalStates)
{
    /******************** PUBLIC METHODS ********************/
    public static DeviceStateResponse From(Onx100ConnectionState connectionState, Onx100DeviceState deviceState)
    {
        Dictionary<int, string> signalStates = deviceState.SignalStates.ToDictionary(entry => entry.Key, entry => entry.Value.ToString());

        return new DeviceStateResponse(connectionState.ToString(), deviceState.PowerState.ToString(), deviceState.SelectedInput, deviceState.Volume, deviceState.IsMuted, signalStates);
    }
}