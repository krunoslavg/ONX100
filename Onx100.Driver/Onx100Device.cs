using Onx100.Driver.Abstractions;
using Onx100.Driver.Enums;
using Onx100.Driver.Models;
using Onx100.Driver.Events;
using Onx100.Driver.Configuration;

namespace Onx100.Driver
{
    public sealed class Onx100Device : IOnx100Device
    {
        /************** PRIVATE MEMBERS ****************/
        private readonly Onx100Options deviceOptions;
        private Onx100ConnectionState connectionState = Onx100ConnectionState.Disconnected;
        private Onx100DeviceState deviceState = new Onx100DeviceState();

        /************** PUBLIC PROPERTIES ****************/
        public Onx100ConnectionState ConnectionState => connectionState;
        public Onx100DeviceState DeviceState => deviceState;

        /************** PUBLIC EVENTS ****************/
        public event EventHandler<Onx100ConnectionStateChangedEventArgs>? Onx100ConnectionStateChanged;
        public event EventHandler<Onx100DeviceStateChangedEventArgs>? Onx100DeviceStateChanged;

        /************** CONSTRUCTOR ****************/
        public Onx100Device(Onx100Options? options = null)
        {
            deviceOptions = options ?? new Onx100Options();

            ValidateOptions(deviceOptions);
        }


        /************** PUBLIC INTERFACE METHODS ****************/
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task GetMuteAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Onx100PowerState> GetPowerStateAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetSelectedInputAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetVolumeAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task PowerOffAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task PowerOnAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SelectInputAsync(int input, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SetMuteAsync(bool mute, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }


        /********************* PRIVATE METHODS **********************/
        private static void ValidateOptions(Onx100Options onx100Options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(onx100Options.Host);

            if (onx100Options.Port is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(onx100Options.Port), onx100Options.Port, "Port must be between 1 and 65535");

            if (onx100Options.ConnectionTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(onx100Options.ConnectionTimeout));

            if (onx100Options.CommandTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(Onx100Options.CommandTimeout));

            if (onx100Options.PowerTransitionTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(onx100Options.PowerTransitionTimeout));

        }
    }
}
