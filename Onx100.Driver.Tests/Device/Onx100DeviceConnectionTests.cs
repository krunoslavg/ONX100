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

