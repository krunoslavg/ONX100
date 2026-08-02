namespace Onx100.Driver.Exceptions
{
    public class Onx100Exception : Exception
    {
        /******* CONSTRUCTORS *******************/
        public Onx100Exception(string message) : base(message)
        {
        }

        public Onx100Exception(string message, Exception e) : base(message, e) 
        {
        }
    }
}
