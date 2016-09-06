using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace Profiling
{
	class Program
	{
		static void Main( string[] args )
		{
			using( var stream = File.OpenRead( @"C:\Users\Josh\Documents\test.csv" ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				while( parser.Read() != null ) { }
			}
		}
	}
}
