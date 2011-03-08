#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CsvHelper.Example
{
	class Program
	{
		private const string columnSeparator = ":";

		static void Main( string[] args )
		{
			//ReadRawFieldsByIndex();
			//ReadRawFieldsByName();
			//ReadFieldsByIndex();
			//ReadRecordsNoAttributes();
			ReadRecordsWithAttributes();
			//ReadAllRecords();

			//WriteRawFields();
			//WriteFields();
			//WriteRecordsNoAttributes();
			//WriteRecordsWithAttributes();
			//WriteAllRecords();

			Console.ReadKey();
		}

		public static void ReadRawFieldsByIndex()
		{
			Console.WriteLine( "Raw fields by index:" );

			using( var reader = new CsvReader( new StreamReader( GetDataStream( true, true ) ) ) )
			{
				while( reader.Read() )
				{
					Console.Write( reader.GetField( 0 ) + columnSeparator );
					Console.Write( reader.GetField( 1 ) + columnSeparator );
					Console.Write( reader.GetField( 2 ) + columnSeparator );
					Console.WriteLine( reader.GetField( 3 ) );
				}
			}
			Console.WriteLine();
		}

		public static void ReadRawFieldsByName()
		{
			Console.WriteLine( "Raw fields by name:" );

			using( var reader = new CsvReader( new StreamReader( GetDataStream( true, true ) ) ) )
			{
				while( reader.Read() )
				{
					Console.Write( reader.GetField( "String Column" ) + columnSeparator );
					Console.Write( reader.GetField( "Int Column" ) + columnSeparator );
					Console.Write( reader.GetField( "Guid Column" ) + columnSeparator );
					Console.Write( reader.GetField( "Does Not Exist Column" ) + columnSeparator );
					Console.WriteLine( reader.GetField( "Custom Type Column" ) );
				}
			}
			Console.WriteLine();
		}

		public static void ReadFieldsByIndex()
		{
			Console.WriteLine( "Fields by index:" );

			var customTypeTypeConverter = new CustomTypeTypeConverter();

			using( var reader = new CsvReader( new StreamReader( GetDataStream( true, true ) ) ) )
			{
				while( reader.Read() )
				{
					Console.Write( reader.GetField<string>( 0 ) + columnSeparator );
					Console.Write( reader.GetField<int>( "Int Column" ) + columnSeparator );
					Console.Write( reader.GetField<Guid>( 2 ) + columnSeparator );
					Console.WriteLine( reader.GetField<CustomType>( 3, customTypeTypeConverter ) );
				}
			}
			Console.WriteLine();
		}

		public static void ReadRecordsNoAttributes()
		{
			Console.WriteLine( "Records no attributes:" );

			using( var reader = new CsvReader( new StreamReader( GetDataStream( true, false ) ) ) )
			{
				while( reader.Read() )
				{
					Console.WriteLine( reader.GetRecord<CustomObject>() );
				}
			}
			Console.WriteLine();
		}

		public static void ReadRecordsWithAttributes()
		{
			Console.WriteLine( "Records with attributes:" );

			using( var reader = new CsvReader( new StreamReader( GetDataStream( true, true ) ) ) )
			{
				while( reader.Read() )
				{
					Console.WriteLine( reader.GetRecord<CustomObjectWithAttributes>() );
				}
			}
			Console.WriteLine();
		}

		public static void ReadAllRecords()
		{
			Console.WriteLine( "All records:" );

			using( var reader = new CsvReader( new StreamReader( GetDataStream( true, false ) ) ) )
			{
				var records = reader.GetRecords<CustomObject>();
				foreach( var record in records )
				{
					Console.WriteLine( record );
				}
			}
			Console.WriteLine();
		}

		public static void WriteRawFields()
		{
			Console.WriteLine( "Write raw fields" );

			using( var memoryStream = new MemoryStream() )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var writer = new CsvWriter( streamWriter ) )
			{
				writer.WriteField( "String Column" );
				writer.WriteField( "Int Column" );
				writer.WriteField( "Guid Column" );
				writer.WriteField( "Custom Type Column" );
				writer.NextRecord();

				writer.WriteField( "one" );
				writer.WriteField( ( 1 ).ToString() );
				writer.WriteField( Guid.NewGuid().ToString() );
				writer.WriteField( ( new CustomType { First = 1, Second = 2, Third = 3 } ).ToString() );
				writer.NextRecord();

				memoryStream.Position = 0;

				Console.WriteLine( streamReader.ReadToEnd() );
			}
			Console.WriteLine();
		}

		public static void WriteFields()
		{
			Console.WriteLine( "Write fields" );

			using( var memoryStream = new MemoryStream() )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var writer = new CsvWriter( streamWriter ) )
			{
				writer.WriteField( "String Column" );
				writer.WriteField( "Int Column" );
				writer.WriteField( "Guid Column" );
				writer.WriteField( "Custom Type Column" );
				writer.NextRecord();

				writer.WriteField( "one" );
				writer.WriteField( 1 );
				writer.WriteField( Guid.NewGuid() );
				writer.WriteField( new CustomType { First = 1, Second = 2, Third = 3 } );
				writer.NextRecord();

				memoryStream.Position = 0;

				Console.WriteLine( streamReader.ReadToEnd() );
			}
			Console.WriteLine();
		}

		public static void WriteRecordsNoAttributes()
		{
			Console.WriteLine( "Write records no attributes:" );

			var records = new List<CustomObject>
			{
				new CustomObject
				{
					CustomTypeColumn = new CustomType
					{
					    First = 1,
					    Second = 2,
					    Third = 3,
					},
					GuidColumn = Guid.NewGuid(),
					IntColumn = 1,
					StringColumn = "one",
				},
				new CustomObject
				{
					CustomTypeColumn = new CustomType
					{
					    First = 4,
					    Second = 5,
					    Third = 6,
					},
					GuidColumn = Guid.NewGuid(),
					IntColumn = 2,
					StringColumn = "two",
				},
			};

			using( var memoryStream = new MemoryStream() )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var writer = new CsvWriter( streamWriter ) )
			{
				foreach( var record in records )
				{
					writer.WriteRecord( record );
				}

				memoryStream.Position = 0;

				Console.WriteLine( streamReader.ReadToEnd() );
			}
			Console.WriteLine();
		}

		public static void WriteRecordsWithAttributes()
		{
			Console.WriteLine( "Write records with attributes:" );

			var records = new List<CustomObjectWithAttributes>
			{
				new CustomObjectWithAttributes
				{
					CustomTypeColumn = new CustomType
					{
					    First = 1,
					    Second = 2,
					    Third = 3,
					},
					GuidColumn = Guid.NewGuid(),
					IntColumn = 1,
					StringColumn = "one",
				},
				new CustomObjectWithAttributes
				{
					CustomTypeColumn = new CustomType
					{
					    First = 4,
					    Second = 5,
					    Third = 6,
					},
					GuidColumn = Guid.NewGuid(),
					IntColumn = 2,
					StringColumn = "two",
				},
			};

			using( var memoryStream = new MemoryStream() )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var writer = new CsvWriter( streamWriter ) )
			{
				foreach( var record in records )
				{
					writer.WriteRecord( record );
				}

				memoryStream.Position = 0;

				Console.WriteLine( streamReader.ReadToEnd() );
			}
			Console.WriteLine();
		}

		public static void WriteAllRecords()
		{
			Console.WriteLine( "Write all records with attributes:" );

			var records = new List<CustomObjectWithAttributes>
			{
				new CustomObjectWithAttributes
				{
					CustomTypeColumn = new CustomType
					{
					    First = 1,
					    Second = 2,
					    Third = 3,
					},
					GuidColumn = Guid.NewGuid(),
					IntColumn = 1,
					StringColumn = "one",
				},
				new CustomObjectWithAttributes
				{
					CustomTypeColumn = new CustomType
					{
					    First = 4,
					    Second = 5,
					    Third = 6,
					},
					GuidColumn = Guid.NewGuid(),
					IntColumn = 2,
					StringColumn = "two",
				},
			};

			using( var memoryStream = new MemoryStream() )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var writer = new CsvWriter( streamWriter ) )
			{
				writer.WriteRecords( records );

				memoryStream.Position = 0;

				Console.WriteLine( streamReader.ReadToEnd() );
			}
			Console.WriteLine();
		}

		public static MemoryStream GetDataStream( bool hasHeader, bool hasSpacesInHeaderNames )
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );

			if( hasHeader )
			{
				var header = hasSpacesInHeaderNames
				             	? "String Column,Int Column,Guid Column,Custom Type Column"
				             	: "StringColumn,IntColumn,GuidColumn,CustomTypeColumn";
				writer.WriteLine( header );
			}
			writer.WriteLine( "one,1,{0},1|2|3", Guid.NewGuid() );
			writer.WriteLine( "two,2,{0},4|5|6", Guid.NewGuid() );
			writer.WriteLine( "\"this, has a comma\",2,{0},7|8|9", Guid.NewGuid() );
			writer.WriteLine( "\"this has \"\"'s\",4,{0},10|11|12", Guid.NewGuid() );
			writer.Flush();
			stream.Position = 0;

			return stream;
		}

		public class CustomType
		{
			public int First { get; set; }
			public int Second { get; set; }
			public int Third { get; set; }

			public override string ToString()
			{
				var serializer = new JavaScriptSerializer();
				return serializer.Serialize( this );
			}
		}

		public class CustomTypeTypeConverter : TypeConverter
		{
			public override bool CanConvertFrom( ITypeDescriptorContext context, Type sourceType )
			{
				return sourceType == typeof( string );
			}

			public override bool CanConvertTo( ITypeDescriptorContext context, Type destinationType )
			{
				return destinationType == typeof( string );
			}

			public override object ConvertFrom( ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value )
			{
				var values = ( (string)value ).Split( '|' );

				var obj = new CustomType
				{
					First = int.Parse( values[0] ),
					Second = int.Parse( values[1] ),
					Third = int.Parse( values[2] ),
				};
				return obj;
			}

			public override object ConvertTo( ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType )
			{
				var obj = (CustomType)value;
				return string.Format( "{0}|{1}|{2}", obj.First, obj.Second, obj.Third );
			}
		}

		public class CustomObject
		{
			public CustomType CustomTypeColumn { get; set; }
			public Guid GuidColumn { get; set; }
			public int IntColumn { get; set; }
			public string StringColumn { get; set; }

			public override string ToString()
			{
				var serializer = new JavaScriptSerializer();
				return serializer.Serialize( this );
			}
		}

		public class CustomObjectWithAttributes
		{
			[TypeConverter( typeof( CustomTypeTypeConverter ) )]
			[CsvHelper.CsvField( FieldName = "Custom Type Column", FieldIndex = 3 )]
			public CustomType CustomTypeColumn { get; set; }

			[CsvHelper.CsvField( FieldName = "Guid Column", FieldIndex = 2 )]
			public Guid GuidColumn { get; set; }

			[CsvHelper.CsvField( FieldName = "Int Column", FieldIndex = 1 )]
			public int IntColumn { get; set; }

			[CsvHelper.CsvField( FieldName = "String Column", FieldIndex = 0 )]
			public string StringColumn { get; set; }

			[CsvHelper.CsvField( Ignore = true )]
			public string IgnoredColumn { get; set; }

			public override string ToString()
			{
				var serializer = new JavaScriptSerializer();
				return serializer.Serialize( this );
			}
		}
	}
}
