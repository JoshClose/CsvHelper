using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.DocsGenerator
{
    public enum EncodingType
    {
		// Generic&ltParameter&gt;
		Html = 0,

		// Generic<Parameter>
		Code = 1,

		// Generic{Parameter}
		Xml = 2
    }
}
