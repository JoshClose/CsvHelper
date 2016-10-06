using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class EmptyTextReaderTests
	{
		[TestMethod]
		public void EmptyStreamDoesntFailTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				Assert.IsFalse( csv.Read() );
			}
		}
	}
}
