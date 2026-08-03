
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Onx100.Api.Hubs;
using Onx100.Api.Models;
using Onx100.Driver;
using Onx100.Driver.Abstractions;
using Onx100.Driver.Configuration;
using Onx100.Driver.Enums;
using Onx100.Driver.Events;
using Onx100.Driver.Exceptions;
using Onx100.Driver.Models;

namespace Onx100.Api.Services;

public sealed class Onx100DeviceService : IOnx100DeviceService
{
    /******************** PRIVATE MEMBERS ********************/
    private readonly IOnx100Device device;
    private readonly IHubContext<DeviceHub> hubContext;
    private readonly ILogger<Onx100DeviceService> logger;
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private bool disposed;

    
    /******************** PUBLIC PROPERTIES ********************/
    public Onx100ConnectionState ConnectionState => device.ConnectionState;
    public Onx100DeviceState DeviceState => device.DeviceState;


    /******************** CONSRTUCTOR********************/
    public Onx100DeviceService(IOptions<Onx100Options> options, IHubContext<DeviceHub> hubContext, ILogger<Onx100DeviceService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);

        this.hubContext = hubContext;
        this.logger = logger;
        device = new Onx100Device(options.Value);

        device.Onx100ConnectionStateChanged += HandleConnectionStateChanged;
        device.Onx100DeviceStateChanged += HandleDeviceStateChanged;
    }


    /******************** PUBLIC METHODS ********************/
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(EnsureConnectedAsync, cancellationToken);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(token => device.DisconnectAsync(token), cancellationToken);
    }

    public Task PowerOnAsync(CancellationToken cancellationToken = default) => ExecuteConnectedAsync(token => ExecutePowerOperationWithRecoveryAsync(device.PowerOnAsync, token), cancellationToken);

    public Task PowerOffAsync(CancellationToken cancellationToken = default) => ExecuteConnectedAsync(token => ExecutePowerOperationWithRecoveryAsync(device.PowerOffAsync, token), cancellationToken);

    public Task SelectInputAsync(int input, CancellationToken cancellationToken = default)
    {
        return ExecuteConnectedAsync(token => SelectInputWithRecoveryAsync(input, token), cancellationToken);
    }

    public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
    {
        return ExecuteConnectedAsync(token => SetVolumeWithRecoveryAsync(volume, token), cancellationToken);
    }

    public Task SetMuteAsync(bool mute, CancellationToken cancellationToken = default)
    {
        return ExecuteConnectedAsync(token => SetMuteWithRecoveryAsync(mute, token), cancellationToken);
    }

    public Task<Onx100DeviceState> RefreshStateAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteConnectedAsync(RefreshStateWithRecoveryAsync, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await operationLock.WaitAsync();

        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            device.Onx100ConnectionStateChanged -= HandleConnectionStateChanged;
            device.Onx100DeviceStateChanged -= HandleDeviceStateChanged;
            await device.DisposeAsync();
        }
        finally
        {
            operationLock.Release();
            operationLock.Dispose();
        }
    }

    
    /******************** PRIVATE  METHODS ********************/
    private async Task<Onx100DeviceState> RefreshStateWithRecoveryAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await RefreshStateCoreAsync(cancellationToken);
        }
        catch (Onx100TimeoutException)
        {
            await EnsureConnectedAsync(cancellationToken);
            return await RefreshStateCoreAsync(cancellationToken);
        }
    }
    
    private async Task<Onx100DeviceState> RefreshStateCoreAsync(CancellationToken cancellationToken)
    {
        Onx100PowerState powerState = await device.GetPowerStateAsync(cancellationToken);

        await device.GetVolumeAsync(cancellationToken);
        await device.GetMuteAsync(cancellationToken);

        if (powerState == Onx100PowerState.On)
        {
            await device.GetSelectedInputAsync(cancellationToken);
        }

        return device.DeviceState;
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await operationLock.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();
            await operation(cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }
      
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (device.ConnectionState == Onx100ConnectionState.Connected)
        {
            return;
        }

        if (device.ConnectionState != Onx100ConnectionState.Disconnected)
        {
            throw new InvalidOperationException($"The ONX-100 device cannot connect while its connection state is {device.ConnectionState}.");
        }

        await device.ConnectAsync(cancellationToken);
    }

    private async Task ExecuteConnectedAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await operationLock.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();
            await EnsureConnectedAsync(cancellationToken);
            await operation(cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async Task<T> ExecuteConnectedAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await operationLock.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();
            await EnsureConnectedAsync(cancellationToken);
            return await operation(cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }
    
    private async Task ExecutePowerOperationWithRecoveryAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken);
        }
        catch (Onx100TimeoutException)
        {
            await EnsureConnectedAsync(cancellationToken);
            await operation(cancellationToken);
        }
    }

    private async Task SetVolumeWithRecoveryAsync(int volume, CancellationToken cancellationToken)
    {
        try
        {
            await device.SetVolumeAsync(volume, cancellationToken);
        }
        catch (Onx100TimeoutException)
        {
            await EnsureConnectedAsync(cancellationToken);

            int actualVolume = await device.GetVolumeAsync(cancellationToken);

            if (actualVolume == volume)
            {
                return;
            }

            await device.SetVolumeAsync(volume, cancellationToken);
        }
    }

    private async Task SetMuteWithRecoveryAsync(bool mute, CancellationToken cancellationToken)
    {
        try
        {
            await device.SetMuteAsync(mute, cancellationToken);
        }
        catch (Onx100TimeoutException)
        {
            await EnsureConnectedAsync(cancellationToken);

            bool actualMute = await device.GetMuteAsync(cancellationToken);

            if (actualMute == mute)
            {
                return;
            }

            await device.SetMuteAsync(mute, cancellationToken);
        }
    }
    
    private async Task SelectInputWithRecoveryAsync(int input, CancellationToken cancellationToken)
    {
        try
        {
            await device.SelectInputAsync(input, cancellationToken);
        }
        catch (Onx100TimeoutException)
        {
            await EnsureConnectedAsync(cancellationToken);

            int actualInput = await device.GetSelectedInputAsync(cancellationToken);

            if (actualInput == input)
            {
                return;
            }

            await device.SelectInputAsync(input, cancellationToken);
        }
    }

    private void HandleConnectionStateChanged(object? sender, Onx100ConnectionStateChangedEventArgs eventArgs) => _ = BroadcastStateSafelyAsync();

    private void HandleDeviceStateChanged(object? sender, Onx100DeviceStateChangedEventArgs eventArgs) => _ = BroadcastStateSafelyAsync();

    private async Task BroadcastStateSafelyAsync()
    {
        try
        {
            DeviceStateResponse response = DeviceStateResponse.From(device.ConnectionState, device.DeviceState);
            await hubContext.Clients.All.SendAsync("DeviceStateChanged", response);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to broadcast ONX-100 device state.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}