using System.Collections.ObjectModel;
using Onx100.Driver.Abstractions;
using Onx100.Driver.Commands;
using Onx100.Driver.Configuration;
using Onx100.Driver.Enums;
using Onx100.Driver.Events;
using Onx100.Driver.Models;
using Onx100.Driver.Protocol;
using Onx100.Driver.Transport;
using Onx100.Driver.Exceptions;


namespace Onx100.Driver
{
    public sealed class Onx100Device : IOnx100Device
    {
        /************** PRIVATE MEMBERS ****************/
        private readonly Onx100Options deviceOptions;
        private readonly IOnx100Transport transport;
        private readonly Onx100MessageFramer messageFramer;
        private readonly Onx100ProtocolParser protocolParser;
        private readonly Onx100CommandDispatcher commandDispatcher;
        private readonly SemaphoreSlim lifecycleLock = new(1, 1); 
        private readonly SemaphoreSlim powerOperationLock = new(1, 1);
        private TaskCompletionSource<Onx100PowerState>? powerStateCompletion;
        private Onx100PowerState? awaitedPowerState;
        private CancellationTokenSource? receiveLoopCancellation;
        private Task? receiveLoopTask;
        private bool disposed;
        private readonly object stateLock = new();
        private Onx100ConnectionState connectionState = Onx100ConnectionState.Disconnected;
        private Onx100DeviceState deviceState = new Onx100DeviceState();


        /************** PUBLIC PROPERTIES ****************/
        public Onx100ConnectionState ConnectionState => connectionState;
        public Onx100DeviceState DeviceState => deviceState;


        /************** PUBLIC EVENTS ****************/
        public event EventHandler<Onx100ConnectionStateChangedEventArgs>? Onx100ConnectionStateChanged;
        public event EventHandler<Onx100DeviceStateChangedEventArgs>? Onx100DeviceStateChanged;


        /************** CONSTRUCTOR ****************/
        public Onx100Device(Onx100Options? options = null) : this(options ?? new Onx100Options(), new TcpOnx100Transport())
        {
        }

        internal Onx100Device(Onx100Options options, IOnx100Transport transport)
        { 
            ArgumentNullException.ThrowIfNull(options, nameof(options));
            ArgumentNullException.ThrowIfNull(transport, nameof(transport));

            ValidateOptions(options);

            this.deviceOptions = options;
            this.transport = transport;
            this.messageFramer = new Onx100MessageFramer();
            this.protocolParser = new Onx100ProtocolParser();
            this.commandDispatcher = new Onx100CommandDispatcher(transport, options.CommandTimeout);
        }


        /************** PUBLIC INTERFACE METHODS ****************/
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (connectionState == Onx100ConnectionState.Connected)
                    return;

                SetConnectionState(Onx100ConnectionState.Connecting);
                messageFramer.Reset();

                using CancellationTokenSource? timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeoutCancellation.CancelAfter(deviceOptions.ConnectionTimeout);

                try
                {
                    await transport.ConnectAsync(deviceOptions.Host, deviceOptions.Port, timeoutCancellation.Token).ConfigureAwait(false);

                    SetConnectionState(Onx100ConnectionState.Connected);

                    receiveLoopCancellation = new CancellationTokenSource();
                    receiveLoopTask = ReceiveLoopAsync(receiveLoopCancellation.Token);
                }
                catch
                {
                    SetConnectionState(Onx100ConnectionState.Disconnected);
                    throw;
                }
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await DisconnectCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
                return;

            await lifecycleLock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (disposed)                
                    return;                

                await DisconnectCoreAsync().ConfigureAwait(false);
                await transport.DisposeAsync().ConfigureAwait(false);

                disposed = true;
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        public async Task<Onx100PowerState> GetPowerStateAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            Onx100ProtocolMessage? response = await commandDispatcher.ExecuteAsync(Onx100CommandFormatter.GetPower(), Onx100MessageKind.PowerResponse, cancellationToken: cancellationToken);
            Onx100PowerState powerState = response.PowerState ?? throw new InvalidOperationException( "Power response has no power state.");

            return powerState;
        }

        public Task PowerOnAsync(CancellationToken cancellationToken = default)
        {
            return SetPowerStateAsync(Onx100PowerState.On, Onx100PowerState.Warming, Onx100CommandFormatter.PowerOn(), cancellationToken);
        }

        public Task PowerOffAsync(CancellationToken cancellationToken = default)
        {
            return SetPowerStateAsync(Onx100PowerState.Off, Onx100PowerState.Cooling, Onx100CommandFormatter.PowerOff(), cancellationToken);
        }

        public async Task<int> GetSelectedInputAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            Onx100ProtocolMessage? response = await commandDispatcher.ExecuteAsync(Onx100CommandFormatter.GetInput(), Onx100MessageKind.InputResponse, cancellationToken: cancellationToken);
            int input = response.Input ?? throw new InvalidOperationException("Input response has no input value.");

            return input;
        }

        public async Task SelectInputAsync(int input, CancellationToken cancellationToken = default)
        {
            var command = Onx100CommandFormatter.SelectInput(input);

            EnsureConnected();

            await commandDispatcher.ExecuteAsync(command,Onx100MessageKind.OkResponse, cancellationToken: cancellationToken);

            UpdateSelectedInput(input);
        }

        public async Task<bool> GetMuteAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            Onx100ProtocolMessage? response = await commandDispatcher.ExecuteAsync(Onx100CommandFormatter.GetMute(),Onx100MessageKind.MuteResponse,cancellationToken: cancellationToken);

            var muted = response.IsMuted ?? throw new InvalidOperationException("Mute response has no mute state.");

            return muted;
        }

        public async Task SetMuteAsync(bool mute, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            await commandDispatcher.ExecuteAsync(Onx100CommandFormatter.SetMute(mute),Onx100MessageKind.OkResponse, cancellationToken: cancellationToken);

            UpdateMuteState(mute);
        }

        public async Task<int> GetVolumeAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            Onx100ProtocolMessage? response = await commandDispatcher.ExecuteAsync(Onx100CommandFormatter.GetVolume(), Onx100MessageKind.VolumeResponse, cancellationToken: cancellationToken);
            int volume = response.Volume ?? throw new InvalidOperationException("Volume response has no volume value.");
            
            return volume;        
        }

        public async Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
        {
            string? command = Onx100CommandFormatter.SetVolume(volume);

            EnsureConnected();

            await commandDispatcher.ExecuteAsync(command, Onx100MessageKind.OkResponse, cancellationToken: cancellationToken);

            UpdateVolume(volume);
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

        private void SetConnectionState(Onx100ConnectionState newState)
        {
            var previousState = connectionState;

            if (previousState == newState)
            
                return;
            

            connectionState = newState;

            Onx100ConnectionStateChanged?.Invoke(this, new Onx100ConnectionStateChangedEventArgs(previousState, newState));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await transport.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        IOException exception = new IOException("The device closed the TCP connection.");

                        commandDispatcher.TryFailPendingCommand(exception);
                        FailPowerStateWaiter(exception);
                        SetConnectionState(Onx100ConnectionState.Disconnected);

                        return;
                    }

                    IReadOnlyList<string>? rawMessages = messageFramer.Append(buffer.AsSpan(0, bytesRead));

                    foreach (string? rawMessage in rawMessages)
                    {
                        Onx100ProtocolMessage? message = protocolParser.Parse(rawMessage);

                        HandleIncomingMessage(message);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during an intentional disconnect.
            }
            catch (Exception exception)
            {
                commandDispatcher.TryFailPendingCommand(exception);
                FailPowerStateWaiter(exception);
                SetConnectionState(Onx100ConnectionState.Disconnected);
            }
        }

        private void HandleIncomingMessage(Onx100ProtocolMessage message)
        {
            switch (message.Kind)
            {
                case Onx100MessageKind.PowerResponse:
                    UpdatePowerState(message.PowerState ?? throw new InvalidOperationException("Power response has no power state."));
                    break;

                case Onx100MessageKind.InputResponse:
                    UpdateSelectedInput(message.Input ?? throw new InvalidOperationException("Input response has no input value."));
                    break;

                case Onx100MessageKind.VolumeResponse:
                    UpdateVolume(message.Volume ?? throw new InvalidOperationException("Volume response has no volume value."));
                    break;

                case Onx100MessageKind.MuteResponse:
                    UpdateMuteState(message.IsMuted ?? throw new InvalidOperationException("Mute response has no mute state."));
                    break;

                case Onx100MessageKind.PowerEvent:
                    UpdatePowerState(message.PowerState ?? throw new InvalidOperationException("Power event has no power state."));
                    break;

                case Onx100MessageKind.SignalEvent:
                    UpdateSignalState(
                        message.Input ?? throw new InvalidOperationException("Signal event has no input."),
                        message.SignalState ?? throw new InvalidOperationException("Signal event has no signal state."));
                    break;
            }

            if (commandDispatcher.TryHandleMessage(message))
            {
                return;
            }

            switch (message.Kind)
            {
                case Onx100MessageKind.Bye:
                    IOException exception = new IOException("The device ended the TCP session.");
                    commandDispatcher.TryFailPendingCommand(exception);
                    FailPowerStateWaiter(exception);
                    SetConnectionState(Onx100ConnectionState.Disconnected);
                    break;

                case Onx100MessageKind.Hello:
                case Onx100MessageKind.Busy:
                case Onx100MessageKind.Unknown:
                    break;
            }
        }
        
        private async Task DisconnectCoreAsync()
        {
            if (connectionState != Onx100ConnectionState.Disconnected)            
                SetConnectionState(Onx100ConnectionState.Disconnecting);
            

            CancellationTokenSource? receiveLoopCancellation = Interlocked.Exchange(ref this.receiveLoopCancellation, null);
            Task? receiveLoopTask = Interlocked.Exchange(ref this.receiveLoopTask, null);

            commandDispatcher.TryFailPendingCommand(new IOException("The driver was disconnected."));

            receiveLoopCancellation?.Cancel();

            try
            {
                await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);

                if (receiveLoopTask is not null)                
                    await receiveLoopTask.ConfigureAwait(false);
                
            }
            finally
            {
                receiveLoopCancellation?.Dispose();
                messageFramer.Reset();
                SetConnectionState(Onx100ConnectionState.Disconnected);
            }
        }

        private void UpdatePowerState(Onx100PowerState powerState)
        {
            UpdateDeviceState(state => state.PowerState == powerState ? state : state with { PowerState = powerState });
            CompletePowerStateWaiter(powerState);
        }

        private void UpdateSignalState(int input, Onx100SignalState signalState)
        {
            if (input is < 1 or > 4)            
                throw new ArgumentOutOfRangeException(nameof(input));
            

            UpdateDeviceState(state =>
            {
                if (state.SignalStates.TryGetValue(input, out var currentSignalState) && currentSignalState == signalState)                
                    return state;                

                Dictionary<int, Onx100SignalState>? signalStates = new Dictionary<int, Onx100SignalState>(state.SignalStates) {[input] = signalState };

                return state with
                {
                    SignalStates =new ReadOnlyDictionary<int, Onx100SignalState>(signalStates)
                };
            });
        }

        private void UpdateDeviceState(Func<Onx100DeviceState, Onx100DeviceState> update)
        {
            ArgumentNullException.ThrowIfNull(update);

            Onx100DeviceState previousState;
            Onx100DeviceState currentState;

            lock (stateLock)
            {
                previousState = deviceState;
                currentState = update(previousState);

                if (ReferenceEquals(previousState, currentState))
                {
                    return;
                }

                deviceState = currentState;
            }

            Onx100DeviceStateChanged?.Invoke(this, new Onx100DeviceStateChangedEventArgs(previousState, currentState));
        }

        private void UpdateSelectedInput(int input)
        {
            UpdateDeviceState(state => state.SelectedInput == input ? state : state with { SelectedInput = input });
        }

        private void UpdateVolume(int volume)
        {
            UpdateDeviceState(state => state.Volume == volume ? state : state with { Volume = volume });
        }

        private void UpdateMuteState(bool muted)
        {
            UpdateDeviceState(state => state.IsMuted == muted ? state : state with { IsMuted = muted });
        }

        private void EnsureConnected()
        {
            ThrowIfDisposed();

            if (connectionState != Onx100ConnectionState.Connected)            
                throw new InvalidOperationException("The ONX-100 device is not connected.");
            
        }

        private async Task SetPowerStateAsync(Onx100PowerState targetState, Onx100PowerState transitionState, string command, CancellationToken cancellationToken)
        {
            EnsureConnected();

            await powerOperationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                Onx100PowerState currentState = await GetPowerStateAsync(cancellationToken).ConfigureAwait(false);

                if (currentState == targetState)                
                    return;                

                TaskCompletionSource<Onx100PowerState> completion = CreatePowerStateWaiter(targetState);

                try
                {
                    if (currentState != transitionState)
                    {
                        await commandDispatcher.ExecuteAsync(command, Onx100MessageKind.OkResponse, cancellationToken: cancellationToken) .ConfigureAwait(false);

                        UpdateDeviceState(state => state.PowerState == targetState ? state : state with { PowerState = transitionState });
                    }

                    try
                    {
                        await completion.Task.WaitAsync(deviceOptions.PowerTransitionTimeout, cancellationToken).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        throw new Onx100TimeoutException(command.TrimEnd('\r'), deviceOptions.PowerTransitionTimeout);
                    }
                }
                finally
                {
                    ClearPowerStateWaiter(completion);
                }
            }
            finally
            {
                powerOperationLock.Release();
            }
        }

        private TaskCompletionSource<Onx100PowerState> CreatePowerStateWaiter(Onx100PowerState targetState)
        {
            TaskCompletionSource<Onx100PowerState> completion = new TaskCompletionSource<Onx100PowerState>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (stateLock)
            {
                if (powerStateCompletion is not null)                
                    throw new InvalidOperationException("Another power operation is already pending.");
                

                if (deviceState.PowerState == targetState)
                {
                    completion.TrySetResult(targetState);
                    return completion;
                }

                awaitedPowerState = targetState;
                powerStateCompletion = completion;
            }

            return completion;
        }

        private void CompletePowerStateWaiter(Onx100PowerState powerState)
        {
            TaskCompletionSource<Onx100PowerState>? completion = null;

            lock (stateLock)
            {
                if (awaitedPowerState == powerState)
                {
                    completion = powerStateCompletion;
                    powerStateCompletion = null;
                    awaitedPowerState = null;
                }
            }

            completion?.TrySetResult(powerState);
        }

        private void ClearPowerStateWaiter(TaskCompletionSource<Onx100PowerState> completion)
        {
            lock (stateLock)
            {
                if (ReferenceEquals(powerStateCompletion, completion))
                {
                    powerStateCompletion = null;
                    awaitedPowerState = null;
                }
            }
        }

        private void FailPowerStateWaiter(Exception exception)
        {
            TaskCompletionSource<Onx100PowerState>? completion;

            lock (stateLock)
            {
                completion = powerStateCompletion;
                powerStateCompletion = null;
                awaitedPowerState = null;
            }

            completion?.TrySetException(exception);
        }
    }
}
