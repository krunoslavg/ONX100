using Onx100.Driver.Enums;
using Onx100.Driver.Events;
using Onx100.Driver.Models;


namespace Onx100.Driver.Abstractions
{
    public interface IOnx100Device : IAsyncDisposable
    {
        /*************** PUBLIC PROPERTIES ********************/
        Onx100ConnectionState ConnectionState { get; }
        Onx100DeviceState DeviceState { get; }


        /*************** PUBLIC EVENTS ********************/
        event EventHandler<Onx100ConnectionStateChangedEventArgs>? Onx100ConnectionStateChanged;
        event EventHandler<Onx100DeviceStateChangedEventArgs>? Onx100DeviceStateChanged;


        /*************** PUBLIC METHODS ********************/
        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync(CancellationToken cancellationToken = default);
        Task PowerOnAsync(CancellationToken cancellationToken = default);   
        Task PowerOffAsync(CancellationToken cancellationToken = default); 
        Task <Onx100PowerState> GetPowerStateAsync(CancellationToken cancellationToken = default);
        Task<int> GetSelectedInputAsync(CancellationToken cancellationToken = default);
        Task SelectInputAsync(int input,  CancellationToken cancellationToken = default);
        Task<int> GetVolumeAsync(CancellationToken cancellationToken = default);
        Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default);
        Task<bool> GetMuteAsync(CancellationToken cancellationToken = default);
        Task SetMuteAsync(bool mute, CancellationToken cancellationToken = default);
    }
}
