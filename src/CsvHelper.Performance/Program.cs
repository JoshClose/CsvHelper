using System;
using System.Diagnostics;
using System.IO;

namespace CsvHelper.Performance
{
	class Program
	{
		static void Main( string[] args )
		{
			Parse();
			//ReadGetField();
			//ReadGetRecords();
			//Console.ReadKey();
		}

		static string GetFilePath()
		{
			var homePath = Environment.ExpandEnvironmentVariables( "%HOMEDRIVE%%HOMEPATH%" );
			var filePath = Path.Combine( homePath, "Documents", "large.csv" );
			return filePath;
		}

		static void Generate()
		{
			using( var stream = File.Create( GetFilePath() ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				for( var column = 1; column <= 50; column++ )
				{
					csv.WriteField( $"Column{column}" );
				}
				csv.NextRecord();

				for( var row = 1; row <= 2000000; row++ )
				{
					for( var column = 1; column <= 50; column++ )
					{
						csv.WriteField( column );
					}
					csv.NextRecord();
				}
			}
		}

		static void Parse()
		{
			Console.WriteLine( "Parsing" );
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			using( var stream = File.OpenRead( GetFilePath() ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				string[] row;
				while( ( row = parser.Read() ) != null )
				{
				}
			}
			stopwatch.Stop();
			Console.WriteLine( stopwatch.Elapsed );
		}

		static void ReadGetField()
		{
			Console.WriteLine( "Reading using GetField" );
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			using( var stream = File.OpenRead( GetFilePath() ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				// Read header.
				csv.Read();

				while( csv.Read() )
				{
					for( var i = 0; i < 50; i++ )
					{
						csv.GetField<int>( i );
					}
				}
			}
			stopwatch.Stop();
			Console.WriteLine( stopwatch.Elapsed );
		}

		static void ReadGetRecords()
		{
			Console.WriteLine( "Reading using GetRecords" );
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			using( var stream = File.OpenRead( GetFilePath() ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<Columns50>();
				foreach( var record in records )
				{
				}
			}
			stopwatch.Stop();
			Console.WriteLine( stopwatch.Elapsed );
		}

		private class Columns50
		{
			public int Column1 { get; set; }
			public int Column2 { get; set; }
			public int Column3 { get; set; }
			public int Column4 { get; set; }
			public int Column5 { get; set; }
			public int Column6 { get; set; }
			public int Column7 { get; set; }
			public int Column8 { get; set; }
			public int Column9 { get; set; }
			public int Column10 { get; set; }
			public int Column11 { get; set; }
			public int Column12 { get; set; }
			public int Column13 { get; set; }
			public int Column14 { get; set; }
			public int Column15 { get; set; }
			public int Column16 { get; set; }
			public int Column17 { get; set; }
			public int Column18 { get; set; }
			public int Column19 { get; set; }
			public int Column20 { get; set; }
			public int Column21 { get; set; }
			public int Column22 { get; set; }
			public int Column23 { get; set; }
			public int Column24 { get; set; }
			public int Column25 { get; set; }
			public int Column26 { get; set; }
			public int Column27 { get; set; }
			public int Column28 { get; set; }
			public int Column29 { get; set; }
			public int Column30 { get; set; }
			public int Column31 { get; set; }
			public int Column32 { get; set; }
			public int Column33 { get; set; }
			public int Column34 { get; set; }
			public int Column35 { get; set; }
			public int Column36 { get; set; }
			public int Column37 { get; set; }
			public int Column38 { get; set; }
			public int Column39 { get; set; }
			public int Column40 { get; set; }
			public int Column41 { get; set; }
			public int Column42 { get; set; }
			public int Column43 { get; set; }
			public int Column44 { get; set; }
			public int Column45 { get; set; }
			public int Column46 { get; set; }
			public int Column47 { get; set; }
			public int Column48 { get; set; }
			public int Column49 { get; set; }
			public int Column50 { get; set; }
		}
	}
}