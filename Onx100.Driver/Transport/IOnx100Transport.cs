namespace Onx100.Driver.Transport
{
    internal interface IOnx100Transport : IAsyncDisposable
    {
        /********** PUBLIC FUNCTIONS ***************/
        Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
        Task DisconnectAsync(CancellationToken cancellationToken = default);
        Task SendAsync(ReadOnlyMemory<byte> data,  CancellationToken cancellationToken = default);
        Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
    }
}
