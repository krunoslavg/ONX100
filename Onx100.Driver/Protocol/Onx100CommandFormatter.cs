using System.Globalization;

namespace Onx100.Driver.Protocol
{
    internal static class Onx100CommandFormatter
    {
        /**************** PRIVATE MEMBERS ******************/
        private const string Terminator = "\r"; 
        

        /**************** PUBLIC METHODS **************/
        public static string PowerOn()
        {
            return $"PWR ON{Terminator}";
        }

        public static string PowerOff()
        {
            return $"PWR OFF{Terminator}";
        }

        public static string GetPower()
        {
            return $"PWR ?{Terminator}";
        }

        public static string SelectInput(int input)
        {
            if (input is < 1 or > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(input), input, "Input must be between 1 and 4.");
            }

            return $"IN {input.ToString(CultureInfo.InvariantCulture)}{Terminator}";
        }

        public static string GetInput()
        {
            return $"IN ?{Terminator}";
        }

        public static string SetVolume(int volume)
        {
            if (volume is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume must be between 0 and 100.");
            }

            return $"VOL {volume.ToString(CultureInfo.InvariantCulture)}{Terminator}";
        }

        public static string GetVolume()
        {
            return $"VOL ?{Terminator}";
        }

        public static string SetMute(bool muted)
        {
            return muted ? $"MUTE ON{Terminator}" : $"MUTE OFF{Terminator}";
        }

        public static string GetMute()
        {
            return $"MUTE ?{Terminator}";
        }
    }
}
