namespace Onx100.Driver.Protocol;

internal enum Onx100MessageKind
{
    Unknown = 0,
    OkResponse,
    ErrorResponse,
    PowerResponse,
    InputResponse,
    VolumeResponse,
    MuteResponse,
    PowerEvent,
    SignalEvent,
    Hello,
    Busy,
    Bye
}
