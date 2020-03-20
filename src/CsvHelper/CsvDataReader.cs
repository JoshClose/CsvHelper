// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Data;
using System.Linq;

namespace CsvHelper
{
	/// <summary>
	/// Provides a means of reading a CSV file forward-only by using CsvReader.
	/// </summary>
	/// <seealso cref="System.Data.IDataReader" />
	public class CsvDataReader : IDataReader
	{
		private readonly CsvReader csv;
		private bool skipNextRead;

		/// <summary>
		/// Gets the column with the specified index.
		/// </summary>
		/// <value>
		/// The <see cref="System.Object"/>.
		/// </value>
		/// <param name="i">The i.</param>
		/// <returns></returns>
		public object this[int i]
		{
			get
			{
				return csv[i];
			}
		}

		/// <summary>
		/// Gets the column with the specified name.
		/// </summary>
		/// <value>
		/// The <see cref="System.Object"/>.
		/// </value>
		/// <param name="name">The name.</param>
		/// <returns></returns>
		public object this[string name]
		{
			get
			{
				return csv[name];
			}
		}

		/// <summary>
		/// Gets a value indicating the depth of nesting for the current row.
		/// </summary>
		public int Depth
		{
			get
			{
				return 0;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the data reader is closed.
		/// </summary>
		public bool IsClosed { get; private set; }

		/// <summary>
		/// Gets the number of rows changed, inserted, or deleted by execution of the SQL statement.
		/// </summary>
		public int RecordsAffected
		{
			get
			{
				return 0;
			}
		}

		/// <summary>
		/// Gets the number of columns in the current row.
		/// </summary>
		public int FieldCount
		{
			get
			{
				return csv.Context.Record.Length;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvDataReader"/> class.
		/// </summary>
		/// <param name="csv">The CSV.</param>
		public CsvDataReader(CsvReader csv)
		{
			this.csv = csv;

			csv.Read();

			if (csv.Configuration.HasHeaderRecord)
			{
				csv.ReadHeader();
			}
			else
			{
				skipNextRead = true;
			}
		}

		/// <summary>
		/// Closes the <see cref="T:System.Data.IDataReader"></see> Object.
		/// </summary>
		public void Close()
		{
			Dispose();
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			csv.Dispose();
			IsClosed = true;
		}

		/// <summary>
		/// Gets the value of the specified column as a Boolean.
		/// </summary>
		/// <param name="i">The zero-based column ordinal.</param>
		/// <returns>
		/// The value of the column.
		/// </returns>
		public bool GetBoolean(int i)
		{
			return csv.GetField<bool>(i);
		}

		/// <summary>
		/// Gets the 8-bit unsigned integer value of the specified column.
		/// </summary>
		/// <param name="i">The zero-based column ordinal.</param>
		/// <returns>
		/// The 8-bit unsigned integer value of the specified column.
		/// </returns>
		public byte GetByte(int i)
		{
			return csv.GetField<byte>(i);
		}

		/// <summary>
		/// Reads a stream of bytes from the specified column offset into the buffer as an array, starting at the given buffer offset.
		/// </summary>
		/// <param name="i">The zero-based column ordinal.</param>
		/// <param name="fieldOffset">The index within the field from which to start the read operation.</param>
		/// <param name="buffer">The buffer into which to read the stream of bytes.</param>
		/// <param name="bufferoffset">The index for buffer to start the read operation.</param>
		/// <param name="length">The number of bytes to read.</param>
		/// <returns>
		/// The actual number of bytes read.
		/// </returns>
		public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
		{
			var bytes = csv.GetField<byte[]>(i);

			Array.Copy(bytes, fieldOffset, buffer, bufferoffset, length);

			return bytes.Length;
		}

		/// <summary>
		/// Gets the character value of the specified column.
		/// </summary>
		/// <param name="i">The zero-based column ordinal.</param>
		/// <returns>
		/// The character value of the specified column.
		/// </returns>
		public char GetChar(int i)
		{
			return csv.GetField<char>(i);
		}

		/// <summary>
		/// Reads a stream of characters from the specified column offset into the buffer as an array, starting at the given buffer offset.
		/// </summary>
		/// <param name="i">The zero-based column ordinal.</param>
		/// <param name="fieldoffset">The index within the row from which to start the read operation.</param>
		/// <param name="buffer">The buffer into which to read the stream of bytes.</param>
		/// <param name="bufferoffset">The index for buffer to start the read operation.</param>
		/// <param name="length">The number of bytes to read.</param>
		/// <returns>
		/// The actual number of characters read.
		/// </returns>
		public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
		{
			var chars = csv.GetField(i).ToArray();

			Array.Copy(chars, fieldoffset, buffer, bufferoffset, length);

			return chars.Length;
		}

		/// <summary>
		/// Returns an <see cref="T:System.Data.IDataReader"></see> for the specified column ordinal.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The <see cref="T:System.Data.IDataReader"></see> for the specified column ordinal.
		/// </returns>
		public IDataReader GetData(int i)
		{
			return null;
		}

		/// <summary>
		/// Gets the data type information for the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The data type information for the specified field.
		/// </returns>
		public string GetDataTypeName(int i)
		{
			return typeof(string).Name;
		}

		/// <summary>
		/// Gets the date and time data value of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The date and time data value of the specified field.
		/// </returns>
		public DateTime GetDateTime(int i)
		{
			return csv.GetField<DateTime>(i);
		}

		/// <summary>
		/// Gets the fixed-position numeric value of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The fixed-position numeric value of the specified field.
		/// </returns>
		public decimal GetDecimal(int i)
		{
			return csv.GetField<decimal>(i);
		}

		/// <summary>
		/// Gets the double-precision floating point number of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The double-precision floating point number of the specified field.
		/// </returns>
		public double GetDouble(int i)
		{
			return csv.GetField<double>(i);
		}

		/// <summary>
		/// Gets the <see cref="T:System.Type"></see> information corresponding to the type of <see cref="T:System.Object"></see> that would be returned from <see cref="M:System.Data.IDataRecord.GetValue(System.Int32)"></see>.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The <see cref="T:System.Type"></see> information corresponding to the type of <see cref="T:System.Object"></see> that would be returned from <see cref="M:System.Data.IDataRecord.GetValue(System.Int32)"></see>.
		/// </returns>
		public Type GetFieldType(int i)
		{
			return typeof(string);
		}

		/// <summary>
		/// Gets the single-precision floating point number of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The single-precision floating point number of the specified field.
		/// </returns>
		public float GetFloat(int i)
		{
			return csv.GetField<float>(i);
		}

		/// <summary>
		/// Returns the GUID value of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The GUID value of the specified field.
		/// </returns>
		public Guid GetGuid(int i)
		{
			return csv.GetField<Guid>(i);
		}

		/// <summary>
		/// Gets the 16-bit signed integer value of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The 16-bit signed integer value of the specified field.
		/// </returns>
		public short GetInt16(int i)
		{
			return csv.GetField<short>(i);
		}

		/// <summary>
		/// Gets the 32-bit signed integer value of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The 32-bit signed integer value of the specified field.
		/// </returns>
		public int GetInt32(int i)
		{
			return csv.GetField<int>(i);
		}

		/// <summary>
		/// Gets the 64-bit signed integer value of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The 64-bit signed integer value of the specified field.
		/// </returns>
		public long GetInt64(int i)
		{
			return csv.GetField<long>(i);
		}

		/// <summary>
		/// Gets the name for the field to find.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The name of the field or the empty string (""), if there is no value to return.
		/// </returns>
		public string GetName(int i)
		{
			return csv.Configuration.HasHeaderRecord
				? csv.Context.HeaderRecord[i]
				: string.Empty;
		}

		/// <summary>
		/// Return the index of the named field.
		/// </summary>
		/// <param name="name">The name of the field to find.</param>
		/// <returns>
		/// The index of the named field.
		/// </returns>
		public int GetOrdinal(string name)
		{
			return Array.IndexOf(csv.Context.HeaderRecord, name);
		}

		/// <summary>
		/// Returns a <see cref="T:System.Data.DataTable"></see> that describes the column metadata of the <see cref="T:System.Data.IDataReader"></see>.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Data.DataTable"></see> that describes the column metadata.
		/// </returns>
		public DataTable GetSchemaTable()
		{
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

			if (csv.Configuration.HasHeaderRecord)
			{
				for (var i = 0; i < csv.Context.HeaderRecord.Length; i++)
				{
					var header = csv.Context.HeaderRecord[i];
					var row = dt.NewRow();
					row["AllowDBNull"] = true;
					row["AutoIncrementSeed"] = DBNull.Value;
					row["AutoIncrementStep"] = DBNull.Value;
					row["BaseCatalogName"] = null;
					row["BaseColumnName"] = csv.Context.HeaderRecord[i];
					row["BaseColumnNamespace"] = null;
					row["BaseSchemaName"] = null;
					row["BaseTableName"] = null;
					row["BaseTableNamespace"] = null;
					row["ColumnName"] = csv.Context.HeaderRecord[i];
					row["ColumnMapping"] = MappingType.Element;
					row["ColumnOrdinal"] = i;
					row["ColumnSize"] = int.MaxValue;
					row["DataType"] = typeof(string);
					row["DefaultValue"] = null;
					row["Expression"] = null;
					row["IsAutoIncrement"] = false;
					row["IsKey"] = false;
					row["IsLong"] = false;
					row["IsReadOnly"] = true;
					row["IsRowVersion"] = false;
					row["IsUnique"] = false;
					row["NumericPrecision"] = DBNull.Value;
					row["NumericScale"] = DBNull.Value;
					row["ProviderType"] = DbType.String;

					dt.Rows.Add(row);
				}
			}

			return dt;
		}

		/// <summary>
		/// Gets the string value of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The string value of the specified field.
		/// </returns>
		public string GetString(int i)
		{
			return csv.GetField(i);
		}

		/// <summary>
		/// Return the value of the specified field.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// The <see cref="T:System.Object"></see> which will contain the field value upon return.
		/// </returns>
		public object GetValue(int i)
		{
			return IsDBNull(i) ? DBNull.Value : (object)csv.GetField(i);
		}

		/// <summary>
		/// Populates an array of objects with the column values of the current record.
		/// </summary>
		/// <param name="values">An array of <see cref="T:System.Object"></see> to copy the attribute fields into.</param>
		/// <returns>
		/// The number of instances of <see cref="T:System.Object"></see> in the array.
		/// </returns>
		public int GetValues(object[] values)
		{
			for (var i = 0; i < values.Length; i++)
			{
				values[i] = IsDBNull(i) ? DBNull.Value : (object)csv.GetField(i);
			}

			return csv.Context.Record.Length;
		}

		/// <summary>
		/// Return whether the specified field is set to null.
		/// </summary>
		/// <param name="i">The index of the field to find.</param>
		/// <returns>
		/// true if the specified field is set to null; otherwise, false.
		/// </returns>
		public bool IsDBNull(int i)
		{
			var field = csv.GetField(i);
			var nullValues = csv.Configuration.TypeConverterOptionsCache.GetOptions<string>().NullValues;

			return nullValues.Contains(field);
		}

		/// <summary>
		/// Advances the data reader to the next result, when reading the results of batch SQL statements.
		/// </summary>
		/// <returns>
		/// true if there are more rows; otherwise, false.
		/// </returns>
		public bool NextResult()
		{
			return false;
		}

		/// <summary>
		/// Advances the <see cref="T:System.Data.IDataReader"></see> to the next record.
		/// </summary>
		/// <returns>
		/// true if there are more rows; otherwise, false.
		/// </returns>
		public bool Read()
		{
			if (skipNextRead)
			{
				skipNextRead = false;
				return true;
			}

			return csv.Read();
		}
	}
}
