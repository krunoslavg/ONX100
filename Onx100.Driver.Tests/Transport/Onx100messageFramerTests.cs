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
            Onx100MessageFramer framer = new Onx100MessageFramer();
            IReadOnlyList<string> messages = framer.Append(Bytes("PWR ON\r\n"));

            Assert.Equal(["PWR ON"], messages);            
        }

        [Fact]
        public void Append_FragmentedMessage_ReturnsMessageAfterTerminatorArrives()
        {
            Onx100MessageFramer framer = new Onx100MessageFramer();

            IReadOnlyList<string> firstResult = framer.Append(Bytes("EVT PWR O"));
            IReadOnlyList<string> secondResult = framer.Append(Bytes("N\r\n"));

            Assert.Empty(firstResult);
            Assert.Equal(["EVT PWR ON"], secondResult);
        }

        [Fact]
        public void Append_MultipleMessages_ReturnsAllMessagesInOrder() 
        {
            Onx100MessageFramer framer = new Onx100MessageFramer();
            IReadOnlyList<string> messages = framer.Append(Bytes("OK\r\nVOL 28\r\nEVT SIGNAL 2 LOST\r\n"));

            Assert.Equal(["OK", "VOL 28", "EVT SIGNAL 2 LOST"], messages);
        }

        [Fact]
        public void Append_SplitTerminator_ReturnsCompleteMessage() 
        {
            Onx100MessageFramer framer = new Onx100MessageFramer();
            IReadOnlyList<string> firstResult = framer.Append(Bytes("MUTE ON\r"));
            IReadOnlyList<string> secondResult = framer.Append(Bytes("\n"));

            Assert.Empty(firstResult);
            Assert.Equal(["MUTE ON"], secondResult);
        }

        [Fact]
        public void Append_CompleteAndPartialMessages_PreservePartialMessage() 
        {
            Onx100MessageFramer framer = new Onx100MessageFramer();

            IReadOnlyList<string> firstResult = framer.Append(Bytes("OK\r\nPWR W"));
            IReadOnlyList<string> secondResult = framer.Append(Bytes("ARM\r\n"));

            Assert.Equal(["OK"], firstResult);
            Assert.Equal(["PWR WARM"], secondResult);
        }

        [Fact]
        public void Reset_RemovesBufferedPartialMessage()
        {
            Onx100MessageFramer framer = new Onx100MessageFramer();

            framer.Append(Bytes("OLD PARTIAL"));
            framer.Reset();

            IReadOnlyList<string> messages = framer.Append(Bytes("OK\r\n"));

            Assert.Equal(["OK"], messages);
        }


        /*********** PRIVATE METHODS ***********/
        private static byte[] Bytes(string value)
        { 
            return Encoding.UTF8.GetBytes(value);
        }
    }
}
