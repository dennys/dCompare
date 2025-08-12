using System;

namespace dCompare.Tools
{
    public class ToolCreationException : Exception
    {
        public ToolCreationException(string message) : base(message)
        {
        }

        public ToolCreationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
