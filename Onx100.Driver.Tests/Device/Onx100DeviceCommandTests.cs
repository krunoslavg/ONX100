using Onx100.Driver.Configuration;
using Onx100.Driver.Enums;
using Onx100.Driver.Tests.TestDoubles;

namespace Onx100.Driver.Tests.Device
{
    public sealed class Onx100DeviceCommandTests
    {
        /***************** PUBLIC TEST METHODS ********************/
        [Fact]
        public async Task GetPowerStateAsync_ReturnsResponseAndUpdatesState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("PWR WARM\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            Onx100PowerState result = await device.GetPowerStateAsync();

            Assert.Equal(Onx100PowerState.Warming, result);
            Assert.Equal(Onx100PowerState.Warming, device.DeviceState.PowerState);
            Assert.Equal(["PWR ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task SelectInputAsync_SendsCommandAndUpdatesState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("OK\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            await device.SelectInputAsync(3);

            Assert.Equal(3, device.DeviceState.SelectedInput);
            Assert.Equal(["IN 3\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task GetSelectedInputAsync_ReturnsResponseAndUpdatesState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("IN 4\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            int result = await device.GetSelectedInputAsync();

            Assert.Equal(4, result);
            Assert.Equal(4, device.DeviceState.SelectedInput);
            Assert.Equal(["IN ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task SetVolumeAsync_SendsDecimalCommandAndUpdatesState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("OK\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            await device.SetVolumeAsync(60);

            Assert.Equal(60, device.DeviceState.Volume);
            Assert.Equal(["VOL 60\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task GetVolumeAsync_ParsesHexadecimalResponseAndUpdatesState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("VOL 3C\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            int result = await device.GetVolumeAsync();

            Assert.Equal(60, result);
            Assert.Equal(60, device.DeviceState.Volume);
            Assert.Equal(["VOL ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task SetMuteAsync_SendsCommandAndUpdatesState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("OK\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            await device.SetMuteAsync(true);

            Assert.True(device.DeviceState.IsMuted);
            Assert.Equal(["MUTE ON\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task GetMuteAsync_ReturnsResponseAndUpdatesState()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("MUTE OFF\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();
            bool result = await device.GetMuteAsync();

            Assert.False(result);
            Assert.False(device.DeviceState.IsMuted);
            Assert.Equal(["MUTE ?\r"], transport.GetSentCommands());
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
    }
}