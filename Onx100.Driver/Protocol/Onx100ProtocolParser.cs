using Onx100.Driver.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onx100.Driver.Protocol
{
    internal sealed class Onx100ProtocolParser
    {
        private const string HelloPrefix = "*HELLO ONX-100 FW:";

        public Onx100ProtocolMessage Parse(string raw)
        {
            ArgumentNullException.ThrowIfNull(raw);

            if (raw == "OK")
            {
                return Create(raw, Onx100MessageKind.OkResponse);
            }

            if (raw == "*BUSY")
            {
                return Create(raw, Onx100MessageKind.Busy);
            }

            if (raw == "BYE")
            {
                return Create(raw, Onx100MessageKind.Bye);
            }

            if (raw.StartsWith(HelloPrefix, StringComparison.Ordinal))
            {
                String? firmwareVersion = raw[HelloPrefix.Length..];

                if (!string.IsNullOrWhiteSpace(firmwareVersion))
                {
                    return new Onx100ProtocolMessage
                    {
                        Kind = Onx100MessageKind.Hello,
                        Raw = raw,
                        FirmwareVersion = firmwareVersion
                    };
                }
            }

            string[] parts = raw.Split(' ');

            if (TryParseError(parts, raw, out var message) ||
                TryParsePowerResponse(parts, raw, out message) ||
                TryParseInputResponse(parts, raw, out message) ||
                TryParseVolumeResponse(parts, raw, out message) ||
                TryParseMuteResponse(parts, raw, out message) ||
                TryParsePowerEvent(parts, raw, out message) ||
                TryParseSignalEvent(parts, raw, out message))
            {
                return message;
            }

            return Create(raw, Onx100MessageKind.Unknown);
        }

        private static bool TryParseError(string[] parts, string raw, out Onx100ProtocolMessage message)
        {
            if (parts.Length == 2 && parts[0] == "ERR" && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var errorCode))
            {
                message = new Onx100ProtocolMessage
                {
                    Kind = Onx100MessageKind.ErrorResponse,
                    Raw = raw,
                    ErrorCode = errorCode
                };

                return true;
            }

            message = null!;
            return false;
        }

        private static bool TryParsePowerResponse(string[] parts, string raw, out Onx100ProtocolMessage message)
        {
            if (parts.Length == 2 && parts[0] == "PWR" && TryParsePowerState(parts[1], out var powerState))
            {
                message = new Onx100ProtocolMessage
                {
                    Kind = Onx100MessageKind.PowerResponse,
                    Raw = raw,
                    PowerState = powerState
                };

                return true;
            }

            message = null!;
            return false;
        }

        private static bool TryParseInputResponse(string[] parts, string raw, out Onx100ProtocolMessage message)
        {
            if (parts.Length == 2 &&parts[0] == "IN" &&
                int.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var input) &&
                input is >= 1 and <= 4)
            {
                message = new Onx100ProtocolMessage
                {
                    Kind = Onx100MessageKind.InputResponse,
                    Raw = raw,
                    Input = input
                };

                return true;
            }

            message = null!;
            return false;
        }
        
        private static bool TryParseVolumeResponse(string[] parts, string raw, out Onx100ProtocolMessage message)
        {
            if (parts.Length == 2 && parts[0] == "VOL" && int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var volume) 
                && volume is >= 0 and <= 100)
            {
                message = new Onx100ProtocolMessage
                {
                    Kind = Onx100MessageKind.VolumeResponse,
                    Raw = raw,
                    Volume = volume
                };

                return true;
            }

            message = null!;
            return false;
        }

        private static bool TryParseMuteResponse(string[] parts, string raw, out Onx100ProtocolMessage message)
        {
            if (parts.Length == 2 && parts[0] == "MUTE")
            {
                if (parts[1] == "ON")
                {
                    message = new Onx100ProtocolMessage
                    {
                        Kind = Onx100MessageKind.MuteResponse,
                        Raw = raw,
                        IsMuted = true
                    };

                    return true;
                }

                if (parts[1] == "OFF")
                {
                    message = new Onx100ProtocolMessage
                    {
                        Kind = Onx100MessageKind.MuteResponse,
                        Raw = raw,
                        IsMuted = false
                    };

                    return true;
                }
            }

            message = null!;
            return false;
        }

        private static bool TryParsePowerEvent(string[] parts, string raw, out Onx100ProtocolMessage message)
        {
            if (parts.Length == 3 && parts[0] == "EVT" && parts[1] == "PWR" && TryParsePowerState(parts[2], out var powerState))
            {
                message = new Onx100ProtocolMessage
                {
                    Kind = Onx100MessageKind.PowerEvent,
                    Raw = raw,
                    PowerState = powerState
                };

                return true;
            }

            message = null!;
            return false;
        }

        private static bool TryParseSignalEvent(string[] parts, string raw, out Onx100ProtocolMessage message)
        {
            if (parts.Length == 4 && parts[0] == "EVT" && parts[1] == "SIGNAL" &&
                int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var input) && input is >= 1 and <= 4 &&
                TryParseSignalState(parts[3], out var signalState))
            {
                message = new Onx100ProtocolMessage
                {
                    Kind = Onx100MessageKind.SignalEvent,
                    Raw = raw,
                    Input = input,
                    SignalState = signalState
                };

                return true;
            }

            message = null!;
            return false;
        }
        
        private static bool TryParsePowerState(string value, out Onx100PowerState state)
        {
            state = value switch
            {
                "OFF" => Onx100PowerState.Off,
                "WARM" => Onx100PowerState.Warming,
                "ON" => Onx100PowerState.On,
                "COOL" => Onx100PowerState.Cooling,
                _ => Onx100PowerState.Unknown
            };

            return state != Onx100PowerState.Unknown;
        }

        private static bool TryParseSignalState(string value, out Onx100SignalState state)
        {
            state = value switch
            {
                "OK" => Onx100SignalState.Ok,
                "LOST" => Onx100SignalState.Lost,
                _ => Onx100SignalState.Unknown
            };

            return state != Onx100SignalState.Unknown;
        }

        private static Onx100ProtocolMessage Create(string raw, Onx100MessageKind onx100MessageKind) 
        {
            return new Onx100ProtocolMessage { Kind = onx100MessageKind, Raw = raw };
        }
    }
}
