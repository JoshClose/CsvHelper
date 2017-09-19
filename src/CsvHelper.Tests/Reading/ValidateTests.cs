using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
    public class ValidateTests
    {
		[TestMethod]
        public void ValidateTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( ",one" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.MissingFieldFoundCallback = null;
				csv.Configuration.RegisterClassMap<ValidateMap>();
				Assert.ThrowsException<ValidationException>( () => csv.GetRecords<Test>().ToList() );
			}
		}

		[TestMethod]
		public void LogInsteadTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1," );
				writer.Flush();
				stream.Position = 0;

				var logger = new StringBuilder();
				csv.Configuration.MissingFieldFoundCallback = null;
				csv.Configuration.RegisterClassMap( new LogInsteadMap( logger ) );
				csv.GetRecords<Test>().ToList();

				var expected = new StringBuilder();
				expected.AppendLine( "Field '' is not valid!" );

				Assert.AreEqual( expected.ToString(), logger.ToString() );
			}
		}

		[TestMethod]
		public void CustomExceptionTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( ",one" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.MissingFieldFoundCallback = null;
				csv.Configuration.RegisterClassMap<CustomExceptionMap>();
				Assert.ThrowsException<CustomException>( () => csv.GetRecords<Test>().ToList() );
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class ValidateMap : ClassMap<Test>
		{
			public ValidateMap()
			{
				Map( m => m.Id ).Validate( field => !string.IsNullOrEmpty( field ) );
				Map( m => m.Name );
			}
		}

		private sealed class LogInsteadMap : ClassMap<Test>
		{
			public LogInsteadMap( StringBuilder logger )
			{
				Map( m => m.Id );
				Map( m => m.Name ).Validate( field =>
				{
					var isValid = !string.IsNullOrEmpty( field );
					if( !isValid )
					{
						logger.AppendLine( $"Field '{field}' is not valid!" );
					}

					return true;
				} );
			}
		}

		private sealed class CustomExceptionMap : ClassMap<Test>
		{
			public CustomExceptionMap()
			{
				Map( m => m.Id ).Validate( field => throw new CustomException() );
				Map( m => m.Name );
			}
		}

		private class CustomException : CsvHelperException
		{
		}
    }
}
