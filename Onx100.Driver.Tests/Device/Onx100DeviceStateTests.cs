using Onx100.Driver.Configuration;
using Onx100.Driver.Enums;
using Onx100.Driver.Events;
using Onx100.Driver.Tests.TestDoubles;

namespace Onx100.Driver.Tests.Device
{
    public sealed class Onx100DeviceStateTests
    {
        /***************** PUBLIC TEST METHODS ********************/
        [Fact]
        public async Task ReceiveLoop_PowerEvent_UpdatesPowerState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            await device.ConnectAsync();
            transport.QueueIncoming("EVT PWR ON\r\n");

            await WaitUntilAsync(() => device.DeviceState.PowerState == Onx100PowerState.On, TimeSpan.FromSeconds(1));

            Assert.Equal(Onx100PowerState.On, device.DeviceState.PowerState);
        }

        [Fact]
        public async Task ReceiveLoop_SignalEvent_UpdatesSignalState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            await device.ConnectAsync();
            transport.QueueIncoming("EVT SIGNAL 3 LOST\r\n");

            await WaitUntilAsync(() => device.DeviceState.SignalStates[3] == Onx100SignalState.Lost, TimeSpan.FromSeconds(1));

            Assert.Equal(Onx100SignalState.Lost, device.DeviceState.SignalStates[3]);
        }

        [Fact]
        public async Task ReceiveLoop_PowerEvent_RaisesDeviceStateChangedEvent()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);
            Onx100DeviceStateChangedEventArgs? observedEvent = null;

            device.Onx100DeviceStateChanged += (_, eventArgs) => observedEvent = eventArgs;

            await device.ConnectAsync();
            transport.QueueIncoming("EVT PWR ON\r\n");

            await WaitUntilAsync(() => observedEvent is not null, TimeSpan.FromSeconds(1));

            Assert.Equal(Onx100PowerState.Unknown, observedEvent!.PreviousState.PowerState);
            Assert.Equal(Onx100PowerState.On, observedEvent.CurrentState.PowerState);
        }

        [Fact]
        public async Task ReceiveLoop_WhenDeviceStateChangedSubscriberThrows_ContinuesProcessingMessages()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);
            int invocationCount = 0;

            device.Onx100DeviceStateChanged += (_, _) =>
            {
                if (Interlocked.Increment(ref invocationCount) == 1)
                {
                    throw new InvalidOperationException("Subscriber failure.");
                }
            };

            await device.ConnectAsync();

            transport.QueueIncoming("EVT PWR ON\r\n");

            await WaitUntilAsync(() => device.DeviceState.PowerState == Onx100PowerState.On, TimeSpan.FromSeconds(1));

            transport.QueueIncoming("EVT PWR OFF\r\n");

            await WaitUntilAsync(() => device.DeviceState.PowerState == Onx100PowerState.Off, TimeSpan.FromSeconds(1));

            Assert.Equal(Onx100ConnectionState.Connected, device.ConnectionState);
            Assert.Equal(2, invocationCount);
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