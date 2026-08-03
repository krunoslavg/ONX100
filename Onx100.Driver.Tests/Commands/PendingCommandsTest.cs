using Onx100.Driver.Commands;
using Onx100.Driver.Configuration;
using Onx100.Driver.Enums;
using Onx100.Driver.Protocol;
using Onx100.Driver.Tests.TestDoubles;

namespace Onx100.Driver.Tests.Commands
{
    public sealed class PendingCommandTests
    {
        /*************** PUBLIC TEST METHODS **************/
        [Fact]
        public void Constructor_ValidArguments_StoresCommandAndExpectedKind()
        {
            var pending = new PendingCommand("VOL ?\r",Onx100MessageKind.VolumeResponse);

            Assert.Equal("VOL ?\r", pending.Command);
            Assert.Equal(Onx100MessageKind.VolumeResponse, pending.ExpectedResponseKind);
        }

        [Fact]
        public void Constructor_EmptyCommand_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new PendingCommand(" ", Onx100MessageKind.OkResponse));
        }

        [Fact]
        public void Constructor_NonResponseKind_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PendingCommand("PWR ON\r", Onx100MessageKind.PowerEvent));
        }

        [Fact]
        public void CanAccept_ExpectedResponse_ReturnsTrue()
        {
            PendingCommand pending = new PendingCommand("PWR ?\r", Onx100MessageKind.PowerResponse);
            Onx100ProtocolMessage? message = CreateMessage(Onx100MessageKind.PowerResponse);

            Assert.True(pending.CanAccept(message));
        }

        [Fact]
        public void CanAccept_ErrorResponse_ReturnsTrue()
        {
            PendingCommand pending = new PendingCommand("IN ?\r", Onx100MessageKind.InputResponse);
            Onx100ProtocolMessage? message = CreateMessage(Onx100MessageKind.ErrorResponse);

            Assert.True(pending.CanAccept(message));
        }

        [Fact]
        public void CanAccept_UnsolicitedEvent_ReturnsFalse()
        {
            PendingCommand pending = new PendingCommand("PWR ?\r", Onx100MessageKind.PowerResponse);
            Onx100ProtocolMessage? message = CreateMessage(Onx100MessageKind.SignalEvent);

            Assert.False(pending.CanAccept(message));
        }

        [Fact]
        public async Task TrySetResponse_AcceptedMessage_CompletesResponseTask()
        {
            PendingCommand pending = new PendingCommand("MUTE ?\r", Onx100MessageKind.MuteResponse);
            Onx100ProtocolMessage? message = CreateMessage(Onx100MessageKind.MuteResponse);

            Assert.True(pending.TrySetResponse(message));
            Assert.Same(message, await pending.ResponseTask);
        }

        [Fact]
        public async Task TrySetException_CompletesResponseTaskWithException()
        {
            PendingCommand pending = new PendingCommand("VOL ?\r", Onx100MessageKind.VolumeResponse);
            InvalidOperationException? expectedException = new InvalidOperationException("Test failure.");

            Assert.True(pending.TrySetException(expectedException));

            InvalidOperationException? actualException = await Assert.ThrowsAsync<InvalidOperationException>(() => pending.ResponseTask);

            Assert.Same(expectedException, actualException);
        }

        [Fact]
        public async Task TrySetCanceled_CancelsResponseTask()
        {
            PendingCommand pending = new PendingCommand("IN ?\r", Onx100MessageKind.InputResponse);

            using CancellationTokenSource cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            Assert.True(pending.TrySetCanceled(cancellationSource.Token));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.ResponseTask);
        }

     
        /*************** PRIVATE METHODS **************/
        private static Onx100ProtocolMessage CreateMessage(Onx100MessageKind kind)
        {
            return new Onx100ProtocolMessage {
                Kind = kind,
                Raw = "TEST"
            };
        }

    }
}
