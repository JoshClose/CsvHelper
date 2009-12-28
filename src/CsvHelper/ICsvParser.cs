using System.Collections.Generic;

namespace CsvHelper
{
	public interface ICsvParser
	{
		IList<string> Read();
	}
}
