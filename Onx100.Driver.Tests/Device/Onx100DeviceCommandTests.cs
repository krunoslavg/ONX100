using Onx100.Driver.Configuration;
using Onx100.Driver.Enums;
using Onx100.Driver.Exceptions;
using Onx100.Driver.Tests.TestDoubles;
using System.Text;

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

        [Fact]
        public async Task GetPowerStateAsync_WhenResponseIsDropped_DisconnectsAndAllowsQueryAfterReconnect()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(TimeSpan.FromMilliseconds(100)), transport);
            int sendCount = 0;

            transport.OnSendAsync = (_, _) =>
            {
                int currentSend = Interlocked.Increment(ref sendCount);

                if (currentSend == 2)
                {
                    transport.QueueIncoming("PWR OFF\r\n");
                }

                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            Onx100TimeoutException exception = await Assert.ThrowsAsync<Onx100TimeoutException>(() => device.GetPowerStateAsync());

            Assert.Equal("PWR ?", exception.Command);
            Assert.Equal(TimeSpan.FromMilliseconds(100), exception.Timeout);
            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);

            await device.ConnectAsync();

            Onx100PowerState result = await device.GetPowerStateAsync();

            Assert.Equal(Onx100PowerState.Off, result);
            Assert.Equal(Onx100PowerState.Off, device.DeviceState.PowerState);
            Assert.Equal(["PWR ?\r", "PWR ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task GetPowerStateAsync_WhenLateResponseArrivesAfterTimeout_DoesNotCrossReconnectBoundary()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(TimeSpan.FromMilliseconds(100)), transport);
            int sendCount = 0;

            transport.OnSendAsync = (_, _) =>
            {
                int currentSend = Interlocked.Increment(ref sendCount);

                if (currentSend == 2)
                {
                    transport.QueueIncoming("PWR ON\r\n");
                }

                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            await Assert.ThrowsAsync<Onx100TimeoutException>(() => device.GetPowerStateAsync());

            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);

            transport.QueueIncoming("PWR OFF\r\n");

            await device.ConnectAsync();

            Onx100PowerState result = await device.GetPowerStateAsync();

            Assert.Equal(Onx100PowerState.On, result);
            Assert.Equal(Onx100PowerState.On, device.DeviceState.PowerState);
            Assert.Equal(["PWR ?\r", "PWR ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task SetVolumeAsync_WhenResponseIsDropped_DisconnectsAndAllowsStateConfirmationAfterReconnect()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(TimeSpan.FromMilliseconds(100)), transport);
            int sendCount = 0;

            transport.OnSendAsync = (_, _) =>
            {
                int currentSend = Interlocked.Increment(ref sendCount);

                if (currentSend == 2)
                {
                    transport.QueueIncoming("VOL 3C\r\n");
                }

                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            Onx100TimeoutException exception = await Assert.ThrowsAsync<Onx100TimeoutException>(() => device.SetVolumeAsync(60));

            Assert.Equal("VOL 60", exception.Command);
            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);

            await device.ConnectAsync();

            int actualVolume = await device.GetVolumeAsync();

            Assert.Equal(60, actualVolume);
            Assert.Equal(60, device.DeviceState.Volume);
            Assert.Equal(["VOL 60\r", "VOL ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task GetVolumeAsync_WhenMalformedMessageArrivesBeforeResponse_IgnoresItAndReturnsValidResponse()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("VOL FF\r\nVOL 3C\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            int result = await device.GetVolumeAsync();

            Assert.Equal(60, result);
            Assert.Equal(60, device.DeviceState.Volume);
            Assert.Equal(Onx100ConnectionState.Connected, device.ConnectionState);
            Assert.Equal(["VOL ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task GetMuteAsync_WhenMultipleEventsArriveBeforeResponse_ProcessesEventsAndReturnsResponse()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);

            transport.OnSendAsync = (_, _) =>
            {
                transport.QueueIncoming("EVT SIGNAL 1 LOST\r\nEVT SIGNAL 2 OK\r\nEVT PWR ON\r\nMUTE OFF\r\n");
                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            bool result = await device.GetMuteAsync();

            Assert.False(result);
            Assert.Equal(Onx100SignalState.Lost, device.DeviceState.SignalStates[1]);
            Assert.Equal(Onx100SignalState.Ok, device.DeviceState.SignalStates[2]);
            Assert.Equal(Onx100PowerState.On, device.DeviceState.PowerState);
            Assert.False(device.DeviceState.IsMuted);
            Assert.Equal(Onx100ConnectionState.Connected, device.ConnectionState);
            Assert.Equal(["MUTE ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task ConcurrentQueries_AreSerializedAndReceiveCorrectResponses()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);
            TaskCompletionSource<bool> volumeCommandSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> muteCommandSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            transport.OnSendAsync = (data, _) =>
            {
                string command = Encoding.ASCII.GetString(data.Span);

                if (command == "VOL ?\r")
                {
                    volumeCommandSent.TrySetResult(true);
                }
                else if (command == "MUTE ?\r")
                {
                    muteCommandSent.TrySetResult(true);
                    transport.QueueIncoming("MUTE ON\r\n");
                }

                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            Task<int> volumeTask = device.GetVolumeAsync();

            await volumeCommandSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Task<bool> muteTask = device.GetMuteAsync();

            await Assert.ThrowsAsync<TimeoutException>(() => muteCommandSent.Task.WaitAsync(TimeSpan.FromMilliseconds(100)));

            transport.QueueIncoming("VOL 32\r\n");

            int volume = await volumeTask;

            await muteCommandSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            bool muted = await muteTask;

            Assert.Equal(50, volume);
            Assert.True(muted);
            Assert.Equal(50, device.DeviceState.Volume);
            Assert.True(device.DeviceState.IsMuted);
            Assert.Equal(["VOL ?\r", "MUTE ?\r"], transport.GetSentCommands());
        }

        [Fact]
        public async Task GetVolumeAsync_WhenCanceledAfterCommandWasSent_DisconnectsSession()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);
            TaskCompletionSource<bool> commandSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource cancellationSource = new CancellationTokenSource();

            transport.OnSendAsync = (_, _) =>
            {
                commandSent.TrySetResult(true);
                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            Task<int> queryTask = device.GetVolumeAsync(cancellationSource.Token);

            await commandSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queryTask);

            Assert.False(transport.IsConnected);
            Assert.Equal(Onx100ConnectionState.Disconnected, device.ConnectionState);
        }


        [Fact]
        public async Task ConcurrentQueries_StressTest_CompletesAllCommandsWithoutDeadlock()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            await using Onx100Device device = new Onx100Device(CreateOptions(), transport);
            const int iterationCount = 100;
            Task<int>[] volumeTasks = new Task<int>[iterationCount];
            Task<bool>[] muteTasks = new Task<bool>[iterationCount];
            Task[] allTasks = new Task[iterationCount * 2];

            transport.OnSendAsync = (data, _) =>
            {
                string command = Encoding.ASCII.GetString(data.Span);

                if (command == "VOL ?\r")
                {
                    transport.QueueIncoming("VOL 32\r\n");
                }
                else if (command == "MUTE ?\r")
                {
                    transport.QueueIncoming("MUTE ON\r\n");
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected command: {command}");
                }

                return Task.CompletedTask;
            };

            await device.ConnectAsync();

            for (int index = 0; index < iterationCount; index++)
            {
                volumeTasks[index] = device.GetVolumeAsync();
                muteTasks[index] = device.GetMuteAsync();

                allTasks[index * 2] = volumeTasks[index];
                allTasks[index * 2 + 1] = muteTasks[index];
            }

            await Task.WhenAll(allTasks).WaitAsync(TimeSpan.FromSeconds(10));

            for (int index = 0; index < iterationCount; index++)
            {
                Assert.Equal(50, volumeTasks[index].Result);
                Assert.True(muteTasks[index].Result);
            }

            Assert.Equal(200, transport.GetSentCommands().Length);
            Assert.Equal(50, device.DeviceState.Volume);
            Assert.True(device.DeviceState.IsMuted);
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

        private static Onx100Options CreateOptions(TimeSpan? commandTimeout = null)
        {
            return new Onx100Options
            {
                ConnectionTimeout = TimeSpan.FromSeconds(1),
                CommandTimeout = commandTimeout ?? TimeSpan.FromSeconds(1),
                PowerTransitionTimeout = TimeSpan.FromSeconds(1)
            };
        }
    }
}