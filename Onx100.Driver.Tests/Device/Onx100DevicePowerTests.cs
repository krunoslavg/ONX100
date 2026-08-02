using System.Text;
using Onx100.Driver.Configuration;
using Onx100.Driver.Enums;
using Onx100.Driver.Exceptions;
using Onx100.Driver.Tests.TestDoubles;

namespace Onx100.Driver.Tests.Device
{

    public sealed class Onx100DevicePowerTests
    {
        /***************** PUBLIC TEST METHODS ********************/
        [Fact]
        public async Task PowerOnAsync_SendsCommandAndWaitsForPowerEvent()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (data, _) =>
            {
                string command = Encoding.ASCII.GetString(data.Span);

                if (command == "PWR ?\r")
                {
                    transport.QueueIncoming("PWR OFF\r\n");
                }
                else if (command == "PWR ON\r")
                {
                    transport.QueueIncoming("OK\r\n");
                }

                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            Task powerOnTask = device.PowerOnAsync();

            await Assert.ThrowsAsync<TimeoutException>(() => powerOnTask.WaitAsync(TimeSpan.FromMilliseconds(100)));

            Assert.Equal(Onx100PowerState.Warming, device.DeviceState.PowerState);

            transport.QueueIncoming("EVT PWR ON\r\n");
            await powerOnTask;

            Assert.Equal(Onx100PowerState.On, device.DeviceState.PowerState);
            Assert.Equal(["PWR ?\r", "PWR ON\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task PowerOnAsync_WhenAlreadyOn_DoesNotSendSetter()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("PWR ON\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            await device.PowerOnAsync();

            Assert.Equal(Onx100PowerState.On, device.DeviceState.PowerState);
            Assert.Equal(["PWR ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task PowerOnAsync_WhenAlreadyWarming_DoesNotSendSetter()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("PWR WARM\r\nEVT PWR ON\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            await device.PowerOnAsync();

            Assert.Equal(Onx100PowerState.On, device.DeviceState.PowerState);
            Assert.Equal(["PWR ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task PowerOffAsync_SendsCommandAndWaitsForPowerEvent()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (data, _) =>
            {
                string command = Encoding.ASCII.GetString(data.Span);

                if (command == "PWR ?\r")
                {
                    transport.QueueIncoming("PWR ON\r\n");
                }
                else if (command == "PWR OFF\r")
                {
                    transport.QueueIncoming("OK\r\nEVT PWR OFF\r\n");
                }

                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            await device.PowerOffAsync();

            Assert.Equal(Onx100PowerState.Off, device.DeviceState.PowerState);
            Assert.Equal(["PWR ?\r", "PWR OFF\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task PowerOnAsync_WhenPowerEventIsMissing_ThrowsTimeoutException()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(TimeSpan.FromMilliseconds(100)), transport);

            transport.OnSendAsync = (data, _) =>
            {
                string command = Encoding.ASCII.GetString(data.Span);
                transport.QueueIncoming(command == "PWR ?\r" ? "PWR OFF\r\n" : "OK\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            Onx100TimeoutException exception = await Assert.ThrowsAsync<Onx100TimeoutException>(() => device.PowerOnAsync());

            Assert.Equal("PWR ON", exception.Command);
            Assert.Equal(TimeSpan.FromMilliseconds(100), exception.Timeout);
        }


        /***************** PRIVATE METHODS ********************/
        private static Onx100Options CreateOptions(TimeSpan? powerTransitionTimeout = null)
        {
            return new Onx100Options
            {
                ConnectionTimeout = TimeSpan.FromSeconds(1),
                CommandTimeout = TimeSpan.FromSeconds(1),
                PowerTransitionTimeout = powerTransitionTimeout ?? TimeSpan.FromSeconds(1)
            };
        }
    }
}