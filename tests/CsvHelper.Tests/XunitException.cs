using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests
{
	public class XunitException : Exception
	{
		public XunitException() : base() { }

		public XunitException(string message) : base(message) { }
    }
}
