using Onx100.Driver.Enums;

namespace Onx100.Driver.Protocol;

internal sealed record Onx100ProtocolMessage
{
    public required Onx100MessageKind Kind { get; init; }

    public required string Raw { get; init; }

    public int? ErrorCode { get; init; }

    public Onx100PowerState? PowerState { get; init; }

    public int? Input { get; init; }

    public int? Volume { get; init; }

    public bool? IsMuted { get; init; }

    public Onx100SignalState? SignalState { get; init; }

    public string? FirmwareVersion { get; init; }
}