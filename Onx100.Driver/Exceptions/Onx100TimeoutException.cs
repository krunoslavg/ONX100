namespace Onx100.Driver.Exceptions
{
    public sealed class Onx100TimeoutException : Onx100Exception
    {

        /******* PUBLIC PROPERTIES *******************/
        public string Command { get; }
        public TimeSpan Timeout { get; }


        /******* PUBLIC PROPERTIES *******************/
        public Onx100TimeoutException(string command, TimeSpan timeout)  : base($"Command '{command}' did not receive a response within {timeout}.")
        {
            Command = command;
            Timeout = timeout;
        }
    }
}
