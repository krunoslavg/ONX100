using Onx100.Driver.Protocol;

namespace Onx100.Driver.Tests.Protocol
{
    public sealed class Onx100CommandFormatterTests
    {
        [Fact]
        public void PowerOn_ReturnsExpectedCommand()
        {
            Assert.Equal("PWR ON\r", Onx100CommandFormatter.PowerOn());
        }

        [Fact]
        public void PowerOff_ReturnsExpectedCommand()
        {
            Assert.Equal("PWR OFF\r", Onx100CommandFormatter.PowerOff());
        }

        [Fact]
        public void GetPower_ReturnsExpectedCommand()
        {
            Assert.Equal("PWR ?\r", Onx100CommandFormatter.GetPower());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public void SelectInput_ValidInput_ReturnsExpectedCommand(int input)
        {
            Assert.Equal($"IN {input}\r", Onx100CommandFormatter.SelectInput(input));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public void SelectInput_InvalidInput_ThrowsArgumentOutOfRangeException(int input)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Onx100CommandFormatter.SelectInput(input));
        }

        [Fact]
        public void GetInput_ReturnsExpectedCommand()
        {
            Assert.Equal("IN ?\r", Onx100CommandFormatter.GetInput());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(40)]
        [InlineData(100)]
        public void SetVolume_ValidVolume_ReturnsDecimalCommand(int volume)
        {
            Assert.Equal($"VOL {volume}\r", Onx100CommandFormatter.SetVolume(volume));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public void SetVolume_InvalidVolume_ThrowsArgumentOutOfRangeException(int volume)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Onx100CommandFormatter.SetVolume(volume));
        }

        [Fact]
        public void GetVolume_ReturnsExpectedCommand()
        {
            Assert.Equal("VOL ?\r", Onx100CommandFormatter.GetVolume());
        }

        [Theory]
        [InlineData(true, "MUTE ON\r")]
        [InlineData(false, "MUTE OFF\r")]
        public void SetMute_ReturnsExpectedCommand(bool muted, string expected)
        {
            Assert.Equal(expected, Onx100CommandFormatter.SetMute(muted));
        }

        [Fact]
        public void GetMute_ReturnsExpectedCommand()
        {
            Assert.Equal("MUTE ?\r", Onx100CommandFormatter.GetMute());
        }
    }
}
