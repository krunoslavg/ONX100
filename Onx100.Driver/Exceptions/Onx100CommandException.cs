namespace Onx100.Driver.Exceptions
{
    public sealed class Onx100CommandException : Onx100Exception
    {
        /******* PUBLIC PROPERTIES *******************/
        public string Command { get; }
        public int ErrorCode { get; }


        /******* CONSTRUCTORS *******************/
        public Onx100CommandException(string command, int errorCode) : base($"Command '{command}' failed with device error ERR {errorCode:00}.")
        {
            Command = command;
            ErrorCode = errorCode;
        }
    }
}
