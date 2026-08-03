using System.Text;
using Onx100.Driver.Commands;
using Onx100.Driver.Exceptions;
using Onx100.Driver.Protocol;
using Onx100.Driver.Transport;

namespace Onx100.Driver.Tests.Commands
{
    public sealed class Onx100CommandDispatcherTests
    {
        /************* PUBLIC TEST METHODS ***********/
        [Fact]
        public async Task ExecuteAsync_ExpectedResponse_SendsCommandAndReturnsResponse()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100CommandDispatcher dispatcher = new Onx100CommandDispatcher(transport, TimeSpan.FromSeconds(1));
            Onx100ProtocolMessage? expectedResponse = CreateMessage(Onx100MessageKind.VolumeResponse);

            transport.OnSendAsync = (_, _) =>
            {
                Assert.True(dispatcher.TryHandleMessage(expectedResponse));
                return Task.CompletedTask;
            };

            Onx100ProtocolMessage? response = await dispatcher.ExecuteAsync("VOL ?\r", Onx100MessageKind.VolumeResponse);

            Assert.Same(expectedResponse, response);
            Assert.Single(transport.SentData);
            Assert.Equal("VOL ?\r", Encoding.ASCII.GetString(transport.SentData[0]));
        }

        [Fact]
        public async Task ExecuteAsync_UnsolicitedEvent_DoesNotConsumePendingResponse()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100CommandDispatcher dispatcher = new Onx100CommandDispatcher(transport, TimeSpan.FromSeconds(1));
            Onx100ProtocolMessage? signalEvent = CreateMessage(Onx100MessageKind.SignalEvent);
            Onx100ProtocolMessage? expectedResponse = CreateMessage(Onx100MessageKind.PowerResponse);

            transport.OnSendAsync = (_, _) =>
            {
                Assert.False(dispatcher.TryHandleMessage(signalEvent));
                Assert.True(dispatcher.TryHandleMessage(expectedResponse));

                return Task.CompletedTask;
            };

            Onx100ProtocolMessage? response = await dispatcher.ExecuteAsync("PWR ?\r", Onx100MessageKind.PowerResponse);

            Assert.Same(expectedResponse, response);
        }

        [Fact]
        public async Task ExecuteAsync_ErrorResponse_ThrowsCommandException()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100CommandDispatcher dispatcher = new Onx100CommandDispatcher(transport, TimeSpan.FromSeconds(1));

            transport.OnSendAsync = (_, _) =>
            {
                dispatcher.TryHandleMessage(new Onx100ProtocolMessage
                {
                    Kind = Onx100MessageKind.ErrorResponse,
                    Raw = "ERR 03",
                    ErrorCode = 3
                });

                return Task.CompletedTask;
            };

            Onx100CommandException? exception = await Assert.ThrowsAsync<Onx100CommandException>(() => dispatcher.ExecuteAsync("IN ?\r", Onx100MessageKind.InputResponse));

            Assert.Equal("IN ?", exception.Command);
            Assert.Equal(3, exception.ErrorCode);
        }

        [Fact]
        public async Task ExecuteAsync_Timeout_InvalidatesSessionUntilReset()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100CommandDispatcher dispatcher = new Onx100CommandDispatcher(transport, TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsAsync<Onx100TimeoutException>(() => dispatcher.ExecuteAsync("PWR ?\r", Onx100MessageKind.PowerResponse));

            await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.ExecuteAsync("MUTE ON\r", Onx100MessageKind.OkResponse));

            await dispatcher.ResetSessionAsync();

            transport.OnSendAsync = (_, _) =>
            {
                dispatcher.TryHandleMessage(CreateMessage(Onx100MessageKind.OkResponse));
                return Task.CompletedTask;
            };

            Onx100ProtocolMessage response = await dispatcher.ExecuteAsync("MUTE ON\r", Onx100MessageKind.OkResponse);

            Assert.Equal(Onx100MessageKind.OkResponse, response.Kind);
        }

        [Fact]
        public async Task TryFailPendingCommand_PropagatesExceptionToCaller()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100CommandDispatcher dispatcher = new Onx100CommandDispatcher(transport, TimeSpan.FromSeconds(1));
            IOException expectedException = new IOException("Connection was closed.");

            transport.OnSendAsync = (_, _) =>
            {
                Assert.True(dispatcher.TryFailPendingCommand(expectedException));

                return Task.CompletedTask;
            };

            IOException actualException = await Assert.ThrowsAsync<IOException>(() => dispatcher.ExecuteAsync("VOL ?\r", Onx100MessageKind.VolumeResponse));

            Assert.Same(expectedException, actualException);
        }

        [Fact]
        public async Task ExecuteAsync_ConcurrentCalls_SerializesCommands()
        {
            FakeOnx100Transport transport = new FakeOnx100Transport();
            Onx100CommandDispatcher dispatcher = new Onx100CommandDispatcher(transport, TimeSpan.FromSeconds(1));
            TaskCompletionSource<bool> firstSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> secondSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int sendCount = 0;

            transport.OnSendAsync = (_, _) =>
            {
                int currentSend = Interlocked.Increment(ref sendCount);

                if (currentSend == 1)
                    firstSent.TrySetResult(true);
                else
                {
                    secondSent.TrySetResult(true);
                    dispatcher.TryHandleMessage(CreateMessage(Onx100MessageKind.OkResponse));
                }

                return Task.CompletedTask;
            };

            Task<Onx100ProtocolMessage> firstTask = dispatcher.ExecuteAsync("PWR ON\r", Onx100MessageKind.OkResponse);

            await firstSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Task<Onx100ProtocolMessage> secondTask = dispatcher.ExecuteAsync("MUTE ON\r", Onx100MessageKind.OkResponse);

            await Assert.ThrowsAsync<TimeoutException>(() => secondSent.Task.WaitAsync(TimeSpan.FromMilliseconds(100)));

            Assert.True(dispatcher.TryHandleMessage(CreateMessage(Onx100MessageKind.OkResponse)));

            await firstTask;
            await secondSent.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await secondTask;

            Assert.Equal(2, transport.SentData.Count);
        }


        /************* PRIVATE METHODS ***********/
        private static Onx100ProtocolMessage CreateMessage(Onx100MessageKind kind)
        {
            return new Onx100ProtocolMessage
            {
                Kind = kind,
                Raw = "TEST"
            };
        }


        /************* FAKE TRANSPORT TEST CLASS ***********/
        private sealed class FakeOnx100Transport : IOnx100Transport
        {
            public List<byte[]> SentData { get; } = [];

            public Func<ReadOnlyMemory<byte>, CancellationToken, Task>? OnSendAsync { get; set; }

            public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DisconnectAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
            {
                SentData.Add(data.ToArray());

                if (OnSendAsync is not null)
                {
                    await OnSendAsync(data, cancellationToken);
                }
            }

            public Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return Task.FromException<int>(new NotSupportedException());
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}