// Copyright 2009-2021 Josh Close
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.DataTableTests
{
	[TestClass]
	public class CsvDataReaderTests
	{
		[TestMethod]
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

				Assert.AreEqual(true, dataReader.GetBoolean(0));
				Assert.AreEqual(1, dataReader.GetByte(1));

				byte[] byteBuffer = new byte[2];
				dataReader.GetBytes(2, 0, byteBuffer, 0, byteBuffer.Length);
				Assert.AreEqual(0x1, byteBuffer[0]);
				Assert.AreEqual(0x2, byteBuffer[1]);

				Assert.AreEqual('a', dataReader.GetChar(3));

				char[] charBuffer = new char[2];
				dataReader.GetChars(4, 0, charBuffer, 0, charBuffer.Length);
				Assert.AreEqual('a', charBuffer[0]);
				Assert.AreEqual('b', charBuffer[1]);

				Assert.IsNull(dataReader.GetData(0));
				Assert.AreEqual(DateTime.Parse("1/1/2019"), dataReader.GetDateTime(5));
				Assert.AreEqual(typeof(string).Name, dataReader.GetDataTypeName(0));
				Assert.AreEqual(1.23m, dataReader.GetDecimal(6));
				Assert.AreEqual(4.56d, dataReader.GetDouble(7));
				Assert.AreEqual(typeof(string), dataReader.GetFieldType(0));
				Assert.AreEqual(7.89f, dataReader.GetFloat(8));
				Assert.AreEqual(Guid.Parse("eca0c8c6-9a2a-4e6c-8599-3561abda13f1"), dataReader.GetGuid(9));
				Assert.AreEqual(1, dataReader.GetInt16(10));
				Assert.AreEqual(2, dataReader.GetInt32(11));
				Assert.AreEqual(3, dataReader.GetInt64(12));
				Assert.AreEqual("Boolean", dataReader.GetName(0));
				Assert.AreEqual(0, dataReader.GetOrdinal("Boolean"));

				Assert.AreEqual("true", dataReader.GetString(0));
				Assert.AreEqual("true", dataReader.GetValue(0));

				var objectBuffer = new object[14];
				dataReader.GetValues(objectBuffer);
				Assert.AreEqual("true", objectBuffer[0]);
				Assert.AreEqual(DBNull.Value, objectBuffer[13]);
				Assert.IsTrue(dataReader.IsDBNull(13));
			}
		}

		[TestMethod]
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
				Assert.AreEqual(25, schemaTable.Columns.Count);
				Assert.AreEqual(2, schemaTable.Rows.Count);
			}
		}

		[TestMethod]
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

				Assert.AreEqual(2, dataTable.Rows.Count);
				Assert.AreEqual(1, dataTable.Rows[0]["Id"]);
				Assert.AreEqual("one", dataTable.Rows[0]["Name"]);
				Assert.AreEqual(2, dataTable.Rows[1]["Id"]);
				Assert.AreEqual("two", dataTable.Rows[1]["Name"]);
			}
		}

		[TestMethod]
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

				Assert.AreEqual(2, dataTable.Rows.Count);
				Assert.AreEqual(1, dataTable.Rows[0]["Id"]);
				Assert.AreEqual("one", dataTable.Rows[0]["Name"]);
				Assert.AreEqual(2, dataTable.Rows[1]["Id"]);
				Assert.AreEqual("two", dataTable.Rows[1]["Name"]);
			}
		}

		[TestMethod]
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

				Assert.AreEqual(0, dataTable.Rows.Count);
			}
		}

		[TestMethod]
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
				Assert.AreEqual(1, dataReader.GetInt32(0));
				Assert.AreEqual("one", dataReader.GetString(1));

				dataReader.Read();
				Assert.AreEqual(2, dataReader.GetInt32(0));
				Assert.AreEqual("two", dataReader.GetString(1));
			}
		}

		[TestMethod]
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
				Assert.IsFalse(dataReader.IsDBNull(0));
				Assert.IsTrue(dataReader.IsDBNull(1));
			}
		}

		[TestMethod]
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
				Assert.AreEqual(string.Empty, dataReader.GetValue(0));
				Assert.AreEqual(DBNull.Value, dataReader.GetValue(1));

				var values = new object[2];
				dataReader.GetValues(values);
				Assert.AreEqual(string.Empty, values[0]);
				Assert.AreEqual(DBNull.Value, values[1]);
			}
		}

		[TestMethod]
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

					Assert.AreEqual(1, ordinal);
				}
			}
		}

		[TestMethod]
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
					Assert.ThrowsException<IndexOutOfRangeException>(() =>
					{
						dr.GetOrdinal("Foo");
					});
				}
			}
		}

		[TestMethod]
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
				Assert.AreEqual(0, dataReader.FieldCount);
			}
		}

		[TestMethod]
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
	}
}
