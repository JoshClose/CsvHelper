// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.DataTableTests
{
	
	public class CsvDataReaderTests
	{
		[Fact]
		public void GetValuesTest()
		{
			var s = new StringBuilder();
			s.AppendLine("Boolean,Byte,Bytes,Char,Chars,DateTime,Decimal,Double,Float,Guid,Short,Int,Long,Null");
			s.AppendLine("true,1,0x0102,a,ab,1/1/2019,1.23,4.56,7.89,eca0c8c6-9a2a-4e6c-8599-3561abda13f1,1,2,3,null");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("null");
				var dataReader = new CsvDataReader(csv);
				dataReader.Read();

				Assert.True(dataReader.GetBoolean(0));
				Assert.Equal(1, dataReader.GetByte(1));

				byte[] byteBuffer = new byte[2];
				dataReader.GetBytes(2, 0, byteBuffer, 0, byteBuffer.Length);
				Assert.Equal(0x1, byteBuffer[0]);
				Assert.Equal(0x2, byteBuffer[1]);

				Assert.Equal('a', dataReader.GetChar(3));

				char[] charBuffer = new char[2];
				dataReader.GetChars(4, 0, charBuffer, 0, charBuffer.Length);
				Assert.Equal('a', charBuffer[0]);
				Assert.Equal('b', charBuffer[1]);

				Assert.Throws<NotSupportedException>(() => dataReader.GetData(0));
				Assert.Equal(DateTime.Parse("1/1/2019"), dataReader.GetDateTime(5));
				Assert.Equal(typeof(string).Name, dataReader.GetDataTypeName(0));
				Assert.Equal(1.23m, dataReader.GetDecimal(6));
				Assert.Equal(4.56d, dataReader.GetDouble(7));
				Assert.Equal(typeof(string), dataReader.GetFieldType(0));
				Assert.Equal(7.89f, dataReader.GetFloat(8));
				Assert.Equal(Guid.Parse("eca0c8c6-9a2a-4e6c-8599-3561abda13f1"), dataReader.GetGuid(9));
				Assert.Equal(1, dataReader.GetInt16(10));
				Assert.Equal(2, dataReader.GetInt32(11));
				Assert.Equal(3, dataReader.GetInt64(12));
				Assert.Equal("Boolean", dataReader.GetName(0));
				Assert.Equal(0, dataReader.GetOrdinal("Boolean"));

				Assert.Equal("true", dataReader.GetString(0));
				Assert.Equal("true", dataReader.GetValue(0));

				var objectBuffer = new object[14];
				dataReader.GetValues(objectBuffer);
				Assert.Equal("true", objectBuffer[0]);
				Assert.Equal(DBNull.Value, objectBuffer[13]);
				Assert.True(dataReader.IsDBNull(13));
			}
		}

		[Fact]
		public void GetSchemaTableTest()
		{
			var s = new StringBuilder();
			s.AppendLine("Id,Name");
			s.AppendLine("1,one");
			s.AppendLine("2,two");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var dataReader = new CsvDataReader(csv);

				var schemaTable = dataReader.GetSchemaTable();
				Assert.Equal(25, schemaTable.Columns.Count);
				Assert.Equal(2, schemaTable.Rows.Count);
			}
		}

		[Fact]
		public void DataTableLoadTest()
		{
			var s = new StringBuilder();
			s.AppendLine("Id,Name");
			s.AppendLine("1,one");
			s.AppendLine("2,two");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var dataReader = new CsvDataReader(csv);

				var dataTable = new DataTable();
				dataTable.Columns.Add("Id", typeof(int));
				dataTable.Columns.Add("Name", typeof(string));

				dataTable.Load(dataReader);

				Assert.Equal(2, dataTable.Rows.Count);
				Assert.Equal(1, dataTable.Rows[0]["Id"]);
				Assert.Equal("one", dataTable.Rows[0]["Name"]);
				Assert.Equal(2, dataTable.Rows[1]["Id"]);
				Assert.Equal("two", dataTable.Rows[1]["Name"]);
			}
		}

		[Fact]
		public void DataTableLoadHeaderAndRowsHaveDifferentLengthTest()
		{
			var s = new StringBuilder();
			s.AppendLine("Id,Name");
			s.AppendLine("1,one,a");
			s.AppendLine("2,two,b");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var dataReader = new CsvDataReader(csv);

				var dataTable = new DataTable();
				dataTable.Columns.Add("Id", typeof(int));
				dataTable.Columns.Add("Name", typeof(string));

				dataTable.Load(dataReader);

				Assert.Equal(2, dataTable.Rows.Count);
				Assert.Equal(1, dataTable.Rows[0]["Id"]);
				Assert.Equal("one", dataTable.Rows[0]["Name"]);
				Assert.Equal(2, dataTable.Rows[1]["Id"]);
				Assert.Equal("two", dataTable.Rows[1]["Name"]);
			}
		}

		[Fact]
		public void DataTableLoadNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var s = new StringBuilder();
			s.AppendLine("1,one");
			s.AppendLine("2,two");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				var dataReader = new CsvDataReader(csv);

				var dataTable = new DataTable();

				dataTable.Load(dataReader);

				Assert.Equal(0, dataTable.Rows.Count);
			}
		}

		[Fact]
		public void ReadWithNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var s = new StringBuilder();
			s.AppendLine("1,one");
			s.AppendLine("2,two");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				var dataReader = new CsvDataReader(csv);

				dataReader.Read();
				Assert.Equal(1, dataReader.GetInt32(0));
				Assert.Equal("one", dataReader.GetString(1));

				dataReader.Read();
				Assert.Equal(2, dataReader.GetInt32(0));
				Assert.Equal("two", dataReader.GetString(1));
			}
		}

		[Fact]
		public void IsNullTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var s = new StringBuilder();
			s.AppendLine(",null");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("null");

				var dataReader = new CsvDataReader(csv);
				Assert.False(dataReader.IsDBNull(0));
				Assert.True(dataReader.IsDBNull(1));
			}
		}

		[Fact]
		public void DbNullTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var s = new StringBuilder();
			s.AppendLine(",null");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("null");

				var dataReader = new CsvDataReader(csv);
				Assert.Equal(string.Empty, dataReader.GetValue(0));
				Assert.Equal(DBNull.Value, dataReader.GetValue(1));

				var values = new object[2];
				dataReader.GetValues(values);
				Assert.Equal(string.Empty, values[0]);
				Assert.Equal(DBNull.Value, values[1]);
			}
		}

		[Fact]
		public void GetOrdinalCaseInsensitiveTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				null,
			};

			using (var csv = new CsvReader(parser))
			{
				using (var dr = new CsvDataReader(csv))
				{
					var ordinal = dr.GetOrdinal("name");

					Assert.Equal(1, ordinal);
				}
			}
		}

		[Fact]
		public void GetOrdinalMissingTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				null,
			};

			using (var csv = new CsvReader(parser))
			{
				using (var dr = new CsvDataReader(csv))
				{
					Assert.Throws<IndexOutOfRangeException>(() =>
					{
						dr.GetOrdinal("Foo");
					});
				}
			}
		}

		[Fact]
		public void DataTableLoadEmptyTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var reader = new StringReader(string.Empty))
			using (var csv = new CsvReader(reader, config))
			{
				var dataReader = new CsvDataReader(csv);
				Assert.Equal(0, dataReader.FieldCount);
			}
		}

		[Fact]
		public void DataTableNullableValueTypeTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			var parser = new ParserMock(config)
			{
				{ "Id", "Name", "DateTime" },
				{ "1", "one", DateTime.Now.ToString() },
			};
			using (var csv = new CsvReader(parser))
			using (var dr = new CsvDataReader(csv))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("");

				var table = new DataTable();
				table.Columns.Add("Id", typeof(int));
				table.Columns.Add("Name", typeof(string));
				var column = table.Columns.Add("DateTime", typeof(DateTime));
				column.AllowDBNull = true;

				table.Load(dr);
			}
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void GetDataTypeNameTest(bool useDefaultSchema)
		{
			var s = new StringBuilder();
			s.AppendLine("Boolean,Byte,Int,Decimal,Double,DateTime,String,Guid");
			s.AppendLine("true,1,2,4.56,7.89,2/17/2022,foo,eca0c8c6-9a2a-4e6c-8599-3561abda13f1");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				if (useDefaultSchema)
				{
					var dataReader = new CsvDataReader(csv);
					dataReader.Read();
					for (var i = 0; i < dataReader.FieldCount; ++i)
					{
						Assert.Equal(typeof(string).Name, dataReader.GetDataTypeName(i));
					}
				}
				else
				{
					var expectedDataTypeNames = new[]
					{
						typeof(bool).Name,
						typeof(byte).Name,
						typeof(int).Name,
						typeof(decimal).Name,
						typeof(double).Name,
						typeof(DateTime).Name,
						typeof(string).Name,
						typeof(Guid).Name,
					};
					var dataReader = new CsvDataReader(csv, GetOverridingSchemaTable());
					for (var i = 0; i < expectedDataTypeNames.Length; ++i)
					{
						Assert.Equal(expectedDataTypeNames[i], dataReader.GetDataTypeName(i));
					}
				}
			}
		}

		[Fact]
		public void GetDataTypeNameThrowsArgumentOutOfRangeExceptionWhenGivenAnEmptySchema()
		{
			var s = new StringBuilder();
			s.AppendLine("Boolean,Byte,Int,Decimal,Double,DateTime,String,Guid");
			s.AppendLine("true,1,2,4.56,7.89,2/17/2022,foo,eca0c8c6-9a2a-4e6c-8599-3561abda13f1");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				var badSchema = new DataTable();
				var dataReader = new CsvDataReader(csv, badSchema);
				dataReader.Read();
				var exceptionMessage = Assert.Throws<IndexOutOfRangeException>(() => dataReader.GetDataTypeName(0)).Message;
				Assert.Equal(
					"The index of the field to find was not within the range of known fields in the schema",
					exceptionMessage
				);
			}
		}

		[Fact]
		public void GetDataTypeNameThrowsArgumentExceptionWhenGivenASchemaMissingDataType()
		{
			var s = new StringBuilder();
			s.AppendLine("Boolean,Byte,Int,Decimal,Double,DateTime,String,Guid");
			s.AppendLine("true,1,2,4.56,7.89,2/17/2022,foo,eca0c8c6-9a2a-4e6c-8599-3561abda13f1");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				var badSchema = new DataTable();
				badSchema.Rows.Add(badSchema.NewRow());
				var dataReader = new CsvDataReader(csv, badSchema);
				dataReader.Read();
				var exceptionMessage = Assert.Throws<ArgumentException>(() => dataReader.GetDataTypeName(0)).Message;
				Assert.Equal(
					"Expected the schema to define the 'DataType', but no such definition was found",
					exceptionMessage
				);
			}
		}

		[Fact]
		public void GetDataTypeNameThrowsInvalidCastExceptionWhenGivenASchemaWithAnUnexpectedDataTypeValue()
		{
			var s = new StringBuilder();
			s.AppendLine("Boolean,Byte,Int,Decimal,Double,DateTime,String,Guid");
			s.AppendLine("true,1,2,4.56,7.89,2/17/2022,foo,eca0c8c6-9a2a-4e6c-8599-3561abda13f1");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				var badSchema = new DataTable();
				badSchema.Columns.Add("DataType");
				var row = badSchema.NewRow();
				row["DataType"] = "foo";
				badSchema.Rows.Add(row);
				var dataReader = new CsvDataReader(csv, badSchema);
				dataReader.Read();
				var exceptionMessage = Assert.Throws<InvalidCastException>(() => dataReader.GetDataTypeName(0)).Message;
				Assert.Equal(
					"Expected the 'DataType' to describe the underlying type of the column",
					exceptionMessage
				);
			}
		}

		private DataTable GetOverridingSchemaTable()
		{
			var columnTypeMapping = new Tuple<string, Type, DbType>[]
			{
				new Tuple<string, Type, DbType>("Boolean", typeof(bool), DbType.Boolean),
				new Tuple<string, Type, DbType>("Byte", typeof(byte), DbType.Byte),
				new Tuple<string, Type, DbType>("Int", typeof(int), DbType.Int32),
				new Tuple<string, Type, DbType>("Decimal", typeof(decimal), DbType.Decimal),
				new Tuple<string, Type, DbType>("Double", typeof(double), DbType.Double),
				new Tuple<string, Type, DbType>("DateTime", typeof(DateTime), DbType.DateTime),
				new Tuple<string, Type, DbType>("String", typeof(string), DbType.String),
				new Tuple<string, Type, DbType>("Guid", typeof(Guid), DbType.Guid),
			};

			// https://docs.microsoft.com/en-us/dotnet/api/system.data.datatablereader.getschematable?view=netframework-4.7.2
			var dt = new DataTable("SchemaTable");
			dt.Columns.Add("AllowDBNull", typeof(bool));
			dt.Columns.Add("AutoIncrementSeed", typeof(long));
			dt.Columns.Add("AutoIncrementStep", typeof(long));
			dt.Columns.Add("BaseCatalogName");
			dt.Columns.Add("BaseColumnName");
			dt.Columns.Add("BaseColumnNamespace");
			dt.Columns.Add("BaseSchemaName");
			dt.Columns.Add("BaseTableName");
			dt.Columns.Add("BaseTableNamespace");
			dt.Columns.Add("ColumnName");
			dt.Columns.Add("ColumnMapping", typeof(MappingType));
			dt.Columns.Add("ColumnOrdinal", typeof(int));
			dt.Columns.Add("ColumnSize", typeof(int));
			dt.Columns.Add("DataType", typeof(Type));
			dt.Columns.Add("DefaultValue", typeof(object));
			dt.Columns.Add("Expression");
			dt.Columns.Add("IsAutoIncrement", typeof(bool));
			dt.Columns.Add("IsKey", typeof(bool));
			dt.Columns.Add("IsLong", typeof(bool));
			dt.Columns.Add("IsReadOnly", typeof(bool));
			dt.Columns.Add("IsRowVersion", typeof(bool));
			dt.Columns.Add("IsUnique", typeof(bool));
			dt.Columns.Add("NumericPrecision", typeof(short));
			dt.Columns.Add("NumericScale", typeof(short));
			dt.Columns.Add("ProviderType", typeof(int));

			for (var i = 0; i < columnTypeMapping.Length; i++)
			{
				var row = dt.NewRow();
				row["AllowDBNull"] = true;
				row["AutoIncrementSeed"] = DBNull.Value;
				row["AutoIncrementStep"] = DBNull.Value;
				row["BaseCatalogName"] = null;
				row["BaseColumnName"] = columnTypeMapping[i].Item1;
				row["BaseColumnNamespace"] = null;
				row["BaseSchemaName"] = null;
				row["BaseTableName"] = null;
				row["BaseTableNamespace"] = null;
				row["ColumnName"] = columnTypeMapping[i].Item1;
				row["ColumnMapping"] = MappingType.Element;
				row["ColumnOrdinal"] = i;
				row["ColumnSize"] = int.MaxValue;
				row["DataType"] = columnTypeMapping[i].Item2;
				row["DefaultValue"] = null;
				row["Expression"] = null;
				row["IsAutoIncrement"] = false;
				row["IsKey"] = false;
				row["IsLong"] = false;
				row["IsReadOnly"] = true;
				row["IsRowVersion"] = false;
				row["IsUnique"] = false;
				row["NumericPrecision"] = columnTypeMapping[i].Item3 == DbType.Decimal ? 10 : DBNull.Value;
				row["NumericScale"] = columnTypeMapping[i].Item3 == DbType.Decimal ? 2 : DBNull.Value;
				row["ProviderType"] = columnTypeMapping[i].Item3;

				dt.Rows.Add(row);
			}

			return dt;
		}
	}
}
