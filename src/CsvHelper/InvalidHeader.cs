using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Invalid header information.
	/// </summary>
    public class InvalidHeader
    {
		/// <summary>
		/// Header names mapped to a CSV field that couldn't be found.
		/// </summary>
		public List<string> Names { get; set; } = new List<string>();

		/// <summary>
		/// Header name index maped to a CSV field that couldn't be found.
		/// </summary>
		public int Index { get; set; }
    }
}
