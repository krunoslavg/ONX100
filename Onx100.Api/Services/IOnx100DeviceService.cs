using Onx100.Driver.Enums;
using Onx100.Driver.Models;

namespace Onx100.Api.Services;

public interface IOnx100DeviceService : IAsyncDisposable
{
    /******************** PUBLIC PROPERTIES ********************/
    Onx100ConnectionState ConnectionState { get; }
    Onx100DeviceState DeviceState { get; }


    /******************** PUBLIC METHODS ********************/
    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task PowerOnAsync(CancellationToken cancellationToken = default);

    Task PowerOffAsync(CancellationToken cancellationToken = default);

    Task SetInputAsync(int input, CancellationToken cancellationToken = default);

    Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default);

    Task SetMuteAsync(bool mute, CancellationToken cancellationToken = default);

    Task<Onx100DeviceState> RefreshStateAsync(CancellationToken cancellationToken = default);
}