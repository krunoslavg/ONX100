using System.Net.Sockets;

namespace Onx100.Driver.Transport
{
    internal sealed class TcpOnx100Transport : IOnx100Transport
    {
        /********* PRIVATE MEMEBERS ****************/
        private TcpClient? tcpClient;
        private NetworkStream? clientStream;



        /********** PUBLIC INTERFACE FUNCTIONS ***************/
        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(host);

            if (tcpClient is not null)
                throw new InvalidOperationException("Transport is already connected!");

            TcpClient client = new TcpClient { NoDelay = true };

            try
            {
                await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
                tcpClient = client;
                clientStream = tcpClient.GetStream();
            }
            catch {
                client.Dispose();
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NetworkStream stream = Interlocked.Exchange(ref clientStream, null);
            TcpClient client = Interlocked.Exchange(ref tcpClient, null);  

            if (stream is not null)
                await stream.DisposeAsync().ConfigureAwait(false);

            client?.Dispose();
        }

        public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            NetworkStream stream = GetConnectedStream();
            
            await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            NetworkStream stream = GetConnectedStream();

            return await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
        }


        /********** PRIVATE METHODS ***************/
        private NetworkStream GetConnectedStream() {
            return clientStream ?? throw new InvalidOperationException("Transport is not connected");
        }
    }
}
