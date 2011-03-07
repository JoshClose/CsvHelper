using System;
using System.Runtime.Serialization;

namespace CsvHelper
{
    [Serializable]
    public class CsvHelperException : Exception
    {                
        public CsvHelperException(string message) : base(message)
        {
        }

        public CsvHelperException(string message, Exception inner) : base(message, inner)
        {
        }

        protected CsvHelperException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }    
}
