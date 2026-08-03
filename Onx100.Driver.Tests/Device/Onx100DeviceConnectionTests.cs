using Onx100.Driver.Configuration;
using Onx100.Driver.Enums;
using Onx100.Driver.Events;
using Onx100.Driver.Tests.TestDoubles;


namespace Onx100.Driver.Tests.Device
{
    public sealed class Onx100DeviceConnectionTests
    {
        /***************** PUBLIC TEST METHODS ********************/
        [Fact]
        public async Task ConnectAsync_ConnectsTransportAndUpdatesConnectionState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);

            await device.ConnectAsync();

            Assert.True(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Connected, device.ConnectionState);
        }

        [Fact]
        public async Task ConnectAsync_RaisesConnectingAndConnectedEvents()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);
            List<Onx100ConnectionState> observedStates = [];

            device.Onx100ConnectionStateChanged += (_, eventArgs) => observedStates.Add(eventArgs.CurrentState);

            await device.ConnectAsync();

            Assert.Equal([Onx100ConnectionState.Connecting, Onx100ConnectionState.Connected], observedStates);
        }

        [Fact]
        public async Task DisconnectAsync_DisconnectsTransportAndUpdatesConnectionState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);

            await device.ConnectAsync();
            await device.DisconnectAsync();

            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }

        [Fact]
        public async Task DisconnectAsync_RaisesDisconnectingAndDisconnectedEvents()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);
            List<Onx100ConnectionStateChangedEventArgs> observedEvents = [];

            device.Onx100ConnectionStateChanged += (_, eventArgs) => observedEvents.Add(eventArgs);

            await device.ConnectAsync();
            observedEvents.Clear();

            await device.DisconnectAsync();

            Assert.Collection(
                observedEvents,
                firstEvent =>
                {
                    Assert.Equal(Onx100ConnectionState.Connected, firstEvent.PreviousState);
                    Assert.Equal(Onx100ConnectionState.Disconnecting, firstEvent.CurrentState);
                },
                secondEvent =>
                {
                    Assert.Equal(Onx100ConnectionState.Disconnecting, secondEvent.PreviousState);
                    Assert.Equal(Onx100ConnectionState.Disconnected, secondEvent.CurrentState);
                });
        }

        [Fact]
        public async Task ConnectAsync_WhenAlreadyConnected_RemainsConnected()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);

            await device.ConnectAsync();
            await device.ConnectAsync();

            Assert.True(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Connected, device.ConnectionState);
        }

        [Fact]
        public async Task ReceiveLoop_RemoteDisconnect_UpdatesConnectionState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);

            await device.ConnectAsync();

            transport.QueueRemoteDisconnect();

            await WaitUntilAsync(() => device.ConnectionState == Onx100ConnectionState.Disconnected, TimeSpan.FromSeconds(1));

            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }

        [Fact]
        public async Task ConnectAsync_AfterRemoteDisconnect_ReconnectsTransport()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);

            await device.ConnectAsync();

            transport.QueueRemoteDisconnect();

            await WaitUntilAsync(() => device.ConnectionState == Onx100ConnectionState.Disconnected, TimeSpan.FromSeconds(1));

            await device.ConnectAsync();

            Assert.True(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Connected, device.ConnectionState);
        }

        [Fact]
        public async Task GetVolumeAsync_WhenRemoteDisconnectOccursWhileWaiting_ThrowsIOException()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);
            TaskCompletionSource<bool> commandSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            transport.OnSendAsync = (_, _) =>
            {
                commandSent.TrySetResult(true);
                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            Task<int> queryTask = device.GetVolumeAsync();

            await commandSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            transport.QueueRemoteDisconnect();

            IOException exception = await Assert.ThrowsAsync<IOException>(() => queryTask);

            Assert.Equal("The device closed the TCP connection.", exception.Message);

            await WaitUntilAsync(() => device.ConnectionState == Onx100ConnectionState.Disconnected, TimeSpan.FromSeconds(1));

            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }

        [Fact]
        public async Task GetMuteAsync_WhenByeArrivesWhileWaiting_DisconnectsSessionAndThrowsIOException()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);
            TaskCompletionSource<bool> commandSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            transport.OnSendAsync = (_, _) =>
            {
                commandSent.TrySetResult(true);
                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            Task<bool> queryTask = device.GetMuteAsync();

            await commandSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            transport.QueueIncoming("BYE\r\n");

            IOException exception = await Assert.ThrowsAsync<IOException>(() => queryTask);

            await WaitUntilAsync(() => device.ConnectionState == Onx100ConnectionState.Disconnected, TimeSpan.FromSeconds(1));

            Assert.Equal("The device ended the TCP session.", exception.Message);
            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }

        [Fact]
        public async Task ReceiveLoop_BusyMessage_DisconnectsRejectedSession()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);

            await device.ConnectAsync();

            transport.QueueIncoming("*BUSY\r\n");

            await WaitUntilAsync(() => device.ConnectionState == Onx100ConnectionState.Disconnected, TimeSpan.FromSeconds(1));

            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }

        [Fact]
        public async Task ConnectAsync_WhenConnectionStateChangedSubscriberThrows_StillConnectsAndNotifiesOtherSubscribers()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);
            List<Onx100ConnectionState> observedStates = [];

            device.Onx100ConnectionStateChanged += (_, _) => throw new InvalidOperationException("Subscriber failure.");
            device.Onx100ConnectionStateChanged += (_, eventArgs) => observedStates.Add(eventArgs.CurrentState);

            await device.ConnectAsync();

            Assert.True(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Connected, device.ConnectionState);
            Assert.Equal([Onx100ConnectionState.Connecting, Onx100ConnectionState.Connected], observedStates);
        }

        [Fact]
        public async Task DisposeAsync_WhileCommandIsPending_FailsCommandAndDisconnectsTransport()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Device device = new Onx100Device(CreateOptions(), transport);
            TaskCompletionSource<bool> commandSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            transport.OnSendAsync = (_, _) =>
            {
                commandSent.TrySetResult(true);
                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            Task<int> queryTask = device.GetVolumeAsync();

            await commandSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await device.DisposeAsync();

            IOException exception = await Assert.ThrowsAsync<IOException>(() => queryTask);

            Assert.Equal("The driver was disconnected.", exception.Message);
            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }

        [Fact]
        public async Task ConnectDisconnectAsync_RepeatedCycles_RemainsReusable()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("PWR OFF\r\n");
                return Task.CompletedTask;
            };

            for (int cycle = 0; cycle < 10; cycle++)
            {
                await device.ConnectAsync();

                Assert.True(transport.IsConnected);
                Assert.Equal(Onx100ConnectionState.Connected, device.ConnectionState);

                Onx100PowerState powerState = await device.GetPowerStateAsync();

                Assert.Equal(Onx100PowerState.Off, powerState);

                await device.DisconnectAsync();

                Assert.False(transport.IsConnected);
                Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
            }

            Assert.Equal(10, transport.GetSentCommands().Length);
        }

        [Fact]
        public async Task ConnectAsync_WhenDeviceRespondsBusy_ThrowsAndLeavesDisconnected()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport
            {
                ConnectResponse = "*BUSY\r\n"
            };

            Onx100Options options = CreateOptions();
            await using Onx100Device device = new Onx100Device(options, transport);

            IOException exception = await Assert.ThrowsAsync<IOException>(() => device.ConnectAsync());

            Assert.Equal("The device rejected the TCP session because it is busy.", exception.Message);
            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }

        [Fact]
        public async Task ConnectAsync_WhenHandshakeDoesNotArrive_TimesOutAndLeavesDisconnected()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport
            {
                ConnectResponse = null
            };

            Onx100Options options = new Onx100Options
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(100),
                CommandTimeout = TimeSpan.FromSeconds(1),
                PowerTransitionTimeout = TimeSpan.FromSeconds(1)
            };

            await using Onx100Device device = new Onx100Device(options, transport);

            TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() => device.ConnectAsync());

            Assert.Contains("connection handshake did not complete", exception.Message);
            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }


        /***************** PRIVATE METHODS ********************/
        private static Onx100Options CreateOptions()
        {
            return new Onx100Options
            {
                ConnectionTimeout = TimeSpan.FromSeconds(1),
                CommandTimeout = TimeSpan.FromSeconds(1),
                PowerTransitionTimeout = TimeSpan.FromSeconds(1)
            };
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException("The expected condition was not reached.");
                }

                await Task.Delay(10);
            }
        }
    }
}

