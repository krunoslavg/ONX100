namespace Onx100.Driver.Configuration
{
    public sealed class Onx100Options
    {
        /*************** PUBLIC PROPERTIES ********************/
        public string Host { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 4999;
        public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(5);
        public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(3);
        public TimeSpan PowerTransitionTimeout { get; init; } = TimeSpan.FromSeconds(20);
    }
}
