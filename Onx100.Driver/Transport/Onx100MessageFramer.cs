using System.Text;

namespace Onx100.Driver.Transport
{
    internal sealed class Onx100MessageFramer
    {
        /******** PRIVATE MEMBERS ************/
        private const string MessageTerminator = "\r\n";
        private readonly StringBuilder buffer = new StringBuilder();

        
        /******** PUBLIC FUNCTIONS ************/
        public IReadOnlyList<string> Append(ReadOnlySpan<byte> data)
        { 
            if (data.IsEmpty)
                return Array.Empty<string>();

            buffer.Append(Encoding.ASCII.GetString(data));  

            List<string> messages = new List<string>();

            while (TryExtractMessage(out string message))
                messages.Add(message);

            return messages;
        }
        
        public void Reset()
        { 
            buffer.Clear();
        }


        /******** PRIVATE FUNCTIONS ************/
        private bool TryExtractMessage(out string message) { 
            int terminatorIndex = FindTerminatorIndex();

            if (terminatorIndex < 0)
            {
                message = string.Empty;
                return false;
            }

            message = buffer.ToString(0, terminatorIndex);

            buffer.Remove(0, terminatorIndex + MessageTerminator.Length);

            return true;
        }

        private int FindTerminatorIndex() {
            for (int i = 0; i < buffer.Length - 1; i++)
            {
                if (buffer[i] == '\r' && buffer[i + 1] == '\n')
                    return i;
            }

            return -1;
        }
    }
}
