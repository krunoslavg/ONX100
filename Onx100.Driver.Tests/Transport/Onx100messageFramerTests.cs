using System.Runtime.CompilerServices;
using System.Text;
using Onx100.Driver.Transport;

namespace Onx100.Driver.Tests.Transport
{
    public sealed class Onx100messageFramerTests
    {
        /*********** PUBLIC TEST METHODS ***********/
        [Fact]
        public void Append_CompleteMessage_ReturnsMessage() 
        {
            Onx100MessageAssembler framer = new Onx100MessageAssembler();
            IReadOnlyList<string> messages = framer.AppendBytes(Bytes("PWR ON\r\n"));

            Assert.Equal(["PWR ON"], messages);            
        }

        [Fact]
        public void Append_FragmentedMessage_ReturnsMessageAfterTerminatorArrives()
        {
            Onx100MessageAssembler framer = new Onx100MessageAssembler();

            IReadOnlyList<string> firstResult = framer.AppendBytes(Bytes("EVT PWR O"));
            IReadOnlyList<string> secondResult = framer.AppendBytes(Bytes("N\r\n"));

            Assert.Empty(firstResult);
            Assert.Equal(["EVT PWR ON"], secondResult);
        }

        [Fact]
        public void Append_MultipleMessages_ReturnsAllMessagesInOrder() 
        {
            Onx100MessageAssembler framer = new Onx100MessageAssembler();
            IReadOnlyList<string> messages = framer.AppendBytes(Bytes("OK\r\nVOL 28\r\nEVT SIGNAL 2 LOST\r\n"));

            Assert.Equal(["OK", "VOL 28", "EVT SIGNAL 2 LOST"], messages);
        }

        [Fact]
        public void Append_SplitTerminator_ReturnsCompleteMessage() 
        {
            Onx100MessageAssembler framer = new Onx100MessageAssembler();
            IReadOnlyList<string> firstResult = framer.AppendBytes(Bytes("MUTE ON\r"));
            IReadOnlyList<string> secondResult = framer.AppendBytes(Bytes("\n"));

            Assert.Empty(firstResult);
            Assert.Equal(["MUTE ON"], secondResult);
        }

        [Fact]
        public void Append_CompleteAndPartialMessages_PreservePartialMessage() 
        {
            Onx100MessageAssembler framer = new Onx100MessageAssembler();

            IReadOnlyList<string> firstResult = framer.AppendBytes(Bytes("OK\r\nPWR W"));
            IReadOnlyList<string> secondResult = framer.AppendBytes(Bytes("ARM\r\n"));

            Assert.Equal(["OK"], firstResult);
            Assert.Equal(["PWR WARM"], secondResult);
        }

        [Fact]
        public void Reset_RemovesBufferedPartialMessage()
        {
            Onx100MessageAssembler framer = new Onx100MessageAssembler();

            framer.AppendBytes(Bytes("OLD PARTIAL"));
            framer.Reset();

            IReadOnlyList<string> messages = framer.AppendBytes(Bytes("OK\r\n"));

            Assert.Equal(["OK"], messages);
        }


        /*********** PRIVATE METHODS ***********/
        private static byte[] Bytes(string value)
        { 
            return Encoding.UTF8.GetBytes(value);
        }
    }
}
