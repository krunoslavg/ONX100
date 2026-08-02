using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Onx100.Driver.Transport;


namespace Onx100.Driver.Tests.TestDoubles
{
    internal sealed class FakeOnx100Transport : IOnx100Transport
    {
        private readonly Channel<byte[]> incomingData = Channel.CreateUnbounded<byte[]>();

        public ConcurrentQueue<byte[]> SentData { get; } = new();
        public Func<ReadOnlyMemory<byte>, CancellationToken, Task>? OnSendAsync { get; set; }
        public bool IsConnected { get; private set; }


        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsConnected)
                throw new InvalidOperationException("Transport is already connected!");
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = false;
            return Task.CompletedTask;
        }

        public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            byte[] sentBytes = data.ToArray();
            SentData.Enqueue(sentBytes);

            if (OnSendAsync is not null)
            {
                await OnSendAsync(data, cancellationToken);
            }
        }

        public async Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            byte[] data = await incomingData.Reader.ReadAsync(cancellationToken);

            if (data.Length == 0)
            {
                return 0;
            }

            if (data.Length > buffer.Length)
            {
                throw new InvalidOperationException("Incoming test data exceeds the receive buffer.");
            }

            data.CopyTo(buffer);
            return data.Length;
        }

        public void QueueIncoming(string message)
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            incomingData.Writer.TryWrite(data);
        }

        public void QueueRemoteDisconnect()
        {
            incomingData.Writer.TryWrite([]);
        }

        public string[] GetSentCommands()
        {
            return SentData.Select(Encoding.ASCII.GetString).ToArray();
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
