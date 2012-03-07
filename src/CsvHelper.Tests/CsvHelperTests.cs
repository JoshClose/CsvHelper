using System;
using System.IO;
using System.Text;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvHelperTests
	{
		[Fact]
		public void ReadUsingReadOnlyStreamTest()
		{
			var data = Encoding.Default.GetBytes( "one,two,three" );
			var memoryStream = new MemoryStream( data, false );

			var csvHelper = new CsvHelper( memoryStream );
			csvHelper.Reader.Read();

			Assert.Throws<CsvWriterException>(() => csvHelper.Writer.WriteField("test"));
		}

		[Fact]
		public void WriteUsingWriteOnlyStreamTest()
		{
			var writeOnlyStream = new WriteOnlyStream();

			var csvHelper = new CsvHelper( writeOnlyStream );
			csvHelper.Writer.WriteField( "test" );

			Assert.Throws<CsvReaderException>(() => csvHelper.Reader.Read());
		}

		[Fact]
		public void DisposeWhenUsingReadOnlyStream()
		{
			var data = Encoding.Default.GetBytes( "one,two,three" );
			var memoryStream = new MemoryStream( data, false );

			using( var csvHelper = new CsvHelper( memoryStream ) )
			{
				csvHelper.Reader.Read();
			}
		}

		[Fact]
		public void DisposeWhenUsingWriteOnlyStream()
		{
			var writeOnlyStream = new WriteOnlyStream();

			using( var csvHelper = new CsvHelper( writeOnlyStream ) )
			{
				csvHelper.Writer.WriteField( "test" );
			}
		}

		private class WriteOnlyStream : Stream
		{
			public override void Flush()
			{
			}

			public override long Seek( long offset, SeekOrigin origin )
			{
				throw new NotImplementedException();
			}

			public override void SetLength( long value )
			{
				throw new NotImplementedException();
			}

			public override int Read( byte[] buffer, int offset, int count )
			{
				throw new NotImplementedException();
			}

			public override void Write( byte[] buffer, int offset, int count )
			{
			}

			public override bool CanRead
			{
				get { return false; }
			}

			public override bool CanSeek
			{
				get { return false; }
			}

			public override bool CanWrite
			{
				get { return true; }
			}

			public override long Length
			{
				get { throw new NotImplementedException(); }
			}

			public override long Position
			{
				get { throw new NotImplementedException(); }
				set { throw new NotImplementedException(); }
			}

			protected override void Dispose( bool disposing )
			{
			}
		}
	}
}
