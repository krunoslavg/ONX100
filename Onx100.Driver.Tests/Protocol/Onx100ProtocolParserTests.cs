using Onx100.Driver.Enums;
using Onx100.Driver.Protocol;

namespace Onx100.Driver.Tests.Protocol
{
    public class Onx100ProtocolParserTests
    {
        private readonly Onx100ProtocolParser parser = new();
       
        [Fact]
        public void Parse_OkResponse_ReturnsOkMessage()
        {
            Onx100ProtocolMessage? message = parser.Parse("OK");

            Assert.Equal(Onx100MessageKind.OkResponse, message.Kind);
            Assert.Equal("OK", message.Raw);
        }

        [Fact]
        public void Parse_ErrorResponse_ReturnsErrorCode()
        {
            Onx100ProtocolMessage? message = parser.Parse("ERR 03");

            Assert.Equal(Onx100MessageKind.ErrorResponse, message.Kind);
            Assert.Equal(3, message.ErrorCode);
        }

        [Theory]
        [InlineData("PWR OFF", Onx100PowerState.Off)]
        [InlineData("PWR WARM", Onx100PowerState.Warming)]
        [InlineData("PWR ON", Onx100PowerState.On)]
        [InlineData("PWR COOL", Onx100PowerState.Cooling)]
        public void Parse_PowerResponse_ReturnsPowerState(string raw, Onx100PowerState expectedState)
        {
            Onx100ProtocolMessage? message = parser.Parse(raw);

            Assert.Equal(Onx100MessageKind.PowerResponse, message.Kind);
            Assert.Equal(expectedState, message.PowerState);
        }

        [Fact]
        public void Parse_InputResponse_ReturnsSelectedInput()
        {
            Onx100ProtocolMessage? message = parser.Parse("IN 3");

            Assert.Equal(Onx100MessageKind.InputResponse, message.Kind);
            Assert.Equal(3, message.Input);
        }
        
        [Fact]
        public void Parse_HexadecimalVolumeResponse_ReturnsDecimalVolume()
        {
            Onx100ProtocolMessage? message = parser.Parse("VOL 3C");

            Assert.Equal(Onx100MessageKind.VolumeResponse, message.Kind);
            Assert.Equal(60, message.Volume);
        }

        [Theory]
        [InlineData("MUTE ON", true)]
        [InlineData("MUTE OFF", false)]
        public void Parse_MuteResponse_ReturnsMuteState(string raw, bool expectedMuted)
        {
            Onx100ProtocolMessage? message = parser.Parse(raw);

            Assert.Equal(Onx100MessageKind.MuteResponse, message.Kind);
            Assert.Equal(expectedMuted, message.IsMuted);
        }

        [Fact]
        public void Parse_PowerEvent_ReturnsPowerState()
        {
            Onx100ProtocolMessage? message = parser.Parse("EVT PWR ON");

            Assert.Equal(Onx100MessageKind.PowerEvent, message.Kind);
            Assert.Equal(Onx100PowerState.On, message.PowerState);
        }

        [Theory]
        [InlineData("EVT SIGNAL 1 OK", 1, Onx100SignalState.Ok)]
        [InlineData("EVT SIGNAL 4 LOST", 4, Onx100SignalState.Lost)]
        public void Parse_SignalEvent_ReturnsInputAndSignalState(string raw, int expectedInput, Onx100SignalState expectedState)
        {
            Onx100ProtocolMessage? message = parser.Parse(raw);

            Assert.Equal(Onx100MessageKind.SignalEvent, message.Kind);
            Assert.Equal(expectedInput, message.Input);
            Assert.Equal(expectedState, message.SignalState);
        }

        [Fact]
        public void Parse_HelloMessage_ReturnsFirmwareVersion()
        {
            Onx100ProtocolMessage? message = parser.Parse("*HELLO ONX-100 FW:2.13");

            Assert.Equal(Onx100MessageKind.Hello, message.Kind);
            Assert.Equal("2.13", message.FirmwareVersion);
        }

        [Fact]
        public void Parse_BusyMessage_ReturnsBusyKind()
        {
            Onx100ProtocolMessage? message = parser.Parse("*BUSY");

            Assert.Equal(Onx100MessageKind.Busy, message.Kind);
        }

        [Fact]
        public void Parse_ByeMessage_ReturnsByeKind()
        {
            Onx100ProtocolMessage? message = parser.Parse("BYE");

            Assert.Equal(Onx100MessageKind.Bye, message.Kind);
        }

        [Theory]
        [InlineData("")]
        [InlineData("INVALID")]
        [InlineData("IN 5")]
        [InlineData("VOL FF")]
        [InlineData("MUTE MAYBE")]
        [InlineData("EVT SIGNAL 8 OK")]
        [InlineData("*HELLO ONX-100 FW:")]
        public void Parse_MalformedOrUnknownMessage_ReturnsUnknown(string raw)
        {
            Onx100ProtocolMessage? message = parser.Parse(raw);

            Assert.Equal(Onx100MessageKind.Unknown, message.Kind);
            Assert.Equal(raw, message.Raw);
        }

        [Fact]
        public void Parse_NullMessage_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => parser.Parse(null!));
        }
    }
}
