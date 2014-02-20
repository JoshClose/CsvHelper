// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;

namespace CsvHelper.SpeedTests
{
	class Program
	{
		private const string FileName = "test.csv";

		static void Main( string[] args )
		{
			CreateCsvFile();
			SelectTest();

			Console.ReadKey();
		}
		
		private static string ReadUntilValid( string question, string errorMessage, Func<string,bool> answerValidator )
		{
			for( ; ; )
			{
				Console.Write( question );
				var answer = Console.ReadLine();
				if( answerValidator( answer ) )
				{
					return answer;
				}
				Console.WriteLine( errorMessage );
			}
		}

		private static void CreateCsvFile()
		{
			var answer = ReadUntilValid( "Create CSV? ", "Not a valid answer.", a => new[] { "y", "yes", "1", "true", "n", "no", "0", "false" }.Contains( a, StringComparer.Create( CultureInfo.CurrentCulture, true ) ) );
			if( !new[] { "y", "yes", "1", "true" }.Contains( answer, StringComparer.Create( CultureInfo.CurrentCulture, true ) ) )
			{
				return;
			}

			int rowCount = 0;
			ReadUntilValid( "How many rows? ", "You need to specify a valid integer.", s => int.TryParse( s, NumberStyles.Number, CultureInfo.CurrentCulture, out rowCount ) );

			var rowsWrittenText = "Rows written: ";
			Console.Write( rowsWrittenText );
			using( var stream = File.Open( FileName, FileMode.Create ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.RegisterClassMap<TestClassMap>();

				var stopwatch = new Stopwatch();
				stopwatch.Start();

				for( var i = 1; i <= rowCount; i++ )
				{
					var row = new TestClass
					{
						IntColumn = i,
						StringColumn = string.Format( "Row {0}", i ),
						DateColumn = DateTime.Now,
						BoolColumn = i % 2 == 0,
						GuidColumn = Guid.NewGuid()
					};
					Console.CursorLeft = rowsWrittenText.Length;
					csv.WriteRecord( row );
					Console.Write( "{0:N0}", i );
				}
				stopwatch.Stop();
				Console.WriteLine( "Time: {0}", stopwatch.Elapsed );
			}
			Console.WriteLine();
		}

		private static void SelectTest()
		{
			while( true )
			{
				var question = new StringBuilder();
				question.AppendLine( "1) Parse" );
				question.AppendLine( "2) Parse and count bytes" );
				question.AppendLine( "q) Quit" );
				question.Append( "Select test to run: " );
				var option = ReadUntilValid( question.ToString(), "Not a valid option.", s => new[] { "1", "2" }.Contains( s, StringComparer.Create( CultureInfo.CurrentCulture, true ) ) );

				switch( option )
				{
					case "1":
						ParseTest();
						break;
					case "2":
						ParseCountingBytesTest();
						break;
					case "q":
						return;
				}
			}
		}

		private static void ParseTest()
		{
			using( var stream = File.OpenRead( FileName ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				var stopwatch = new Stopwatch();
				stopwatch.Start();
				while( true )
				{
					var record = parser.Read();
					if( record == null )
					{
						break;
					}
				}
				stopwatch.Stop();
				Console.WriteLine( "Time: {0}", stopwatch.Elapsed );
			}
		}

		private static void ParseCountingBytesTest()
		{
			using( var stream = File.OpenRead( FileName ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				parser.Configuration.CountBytes = true;
				var stopwatch = new Stopwatch();
				stopwatch.Start();
				while( true )
				{
					var record = parser.Read();
					if( record == null )
					{
						break;
					}
				}
				stopwatch.Stop();
				Console.WriteLine( "Time: {0}", stopwatch.Elapsed );
			}
		}

		private class TestClass
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }

			public DateTime DateColumn { get; set; }

			public bool BoolColumn { get; set; }

			public Guid GuidColumn { get; set; }
		}

		private sealed class TestClassMap : CsvClassMap<TestClass>
		{
			public TestClassMap()
			{
				Map( m => m.IntColumn ).Name( "Int Column" );
				Map( m => m.StringColumn ).Name( "String Column" );
				Map( m => m.DateColumn ).Name( "Date Column" );
				Map( m => m.BoolColumn ).Name( "Bool Column" );
				Map( m => m.GuidColumn ).Name( "Guid Column" );
			}
		}
	}
}
