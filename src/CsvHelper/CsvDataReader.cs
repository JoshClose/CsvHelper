﻿// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
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
		private readonly DataTable schemaTable;
		private bool skipNextRead;

		/// <inheritdoc />
		public object this[int i]
		{
			get
			{
				return csv[i] ?? string.Empty;
			}
		}

		/// <inheritdoc />
		public object this[string name]
		{
			get
			{
				return csv[name] ?? string.Empty;
			}
		}

		/// <inheritdoc />
		public int Depth
		{
			get
			{
				return 0;
			}
		}

		/// <inheritdoc />
		public bool IsClosed { get; private set; }

		/// <inheritdoc />
		public int RecordsAffected
		{
			get
			{
				return 0;
			}
		}

		/// <inheritdoc />
		public int FieldCount
		{
			get
			{
				return csv.Parser.Count;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvDataReader"/> class.
		/// </summary>
		/// <param name="csv">The CSV.</param>
		/// <param name="schemaTable">The DataTable representing the file schema.</param>
		public CsvDataReader(CsvReader csv, DataTable? schemaTable = null)
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

			this.schemaTable = schemaTable ?? GetSchemaTable();
		}

		/// <inheritdoc />
		public void Close()
		{
			Dispose();
		}

		/// <inheritdoc />
		public void Dispose()
		{
			csv.Dispose();
			IsClosed = true;
		}

		/// <inheritdoc />
		public bool GetBoolean(int i)
		{
			return csv.GetField<bool>(i);
		}

		/// <inheritdoc />
		public byte GetByte(int i)
		{
			return csv.GetField<byte>(i);
		}

		/// <inheritdoc />
		public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
		{
			var bytes = csv.GetField<byte[]>(i);

			if (bytes == null)
			{
				return 0;
			}

			if (buffer != null)
			{
				Array.Copy(bytes, fieldOffset, buffer, bufferoffset, length);
			}

			return bytes.Length;
		}

		/// <inheritdoc />
		public char GetChar(int i)
		{
			return csv.GetField<char>(i);
		}

		/// <inheritdoc />
		public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
		{
			var s = csv.GetField(i);

			if (string.IsNullOrEmpty(s))
			{
				return 0;
			}

			if (buffer != null)
			{
				s.CopyTo((int)fieldoffset, buffer, bufferoffset, length);
			}

			return s.Length;
		}

		/// <inheritdoc />
		public IDataReader GetData(int i)
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public string GetDataTypeName(int i)
		{
			if (i >= schemaTable.Rows.Count)
			{
				throw new IndexOutOfRangeException($"SchemaTable does not contain a definition for field '{i}'.");
			}

			var row = schemaTable.Rows[i];
			var field = row["DataType"] as Type;

			if (field == null)
			{
				throw new InvalidOperationException($"SchemaTable does not contain a 'DataType' of type 'Type' for field '{i}'.");
			}

			return field.Name;
		}

		/// <inheritdoc />
		public DateTime GetDateTime(int i)
		{
			return csv.GetField<DateTime>(i);
		}

		/// <inheritdoc />
		public decimal GetDecimal(int i)
		{
			return csv.GetField<decimal>(i);
		}

		/// <inheritdoc />
		public double GetDouble(int i)
		{
			return csv.GetField<double>(i);
		}

		/// <inheritdoc />
		public Type GetFieldType(int i)
		{
			return typeof(string);
		}

		/// <inheritdoc />
		public float GetFloat(int i)
		{
			return csv.GetField<float>(i);
		}

		/// <inheritdoc />
		public Guid GetGuid(int i)
		{
			return csv.GetField<Guid>(i);
		}

		/// <inheritdoc />
		public short GetInt16(int i)
		{
			return csv.GetField<short>(i);
		}

		/// <inheritdoc />
		public int GetInt32(int i)
		{
			return csv.GetField<int>(i);
		}

		/// <inheritdoc />
		public long GetInt64(int i)
		{
			return csv.GetField<long>(i);
		}

		/// <inheritdoc />
		public string GetName(int i)
		{
			Debug.Assert(!csv.Configuration.HasHeaderRecord || csv.HeaderRecord != null,
				$"Expected {nameof(csv.ReadHeader)} to have already been called (i.e in the constructor)");

			return csv.Configuration.HasHeaderRecord
				? csv.HeaderRecord![i]
				: string.Empty;
		}

		/// <inheritdoc />
		public int GetOrdinal(string name)
		{
			var index = csv.GetFieldIndex(name, isTryGet: true);
			if (index >= 0)
			{
				return index;
			}

			var args = new PrepareHeaderForMatchArgs(name, 0);
			var namePrepared = csv.Configuration.PrepareHeaderForMatch(args);

			var headerRecord = csv.HeaderRecord;

			Debug.Assert(headerRecord != null, $"Expected {nameof(csv.GetFieldIndex)} to throw if header not read");

			for (var i = 0; i < headerRecord.Length; i++)
			{
				args = new PrepareHeaderForMatchArgs(headerRecord[i], i);
				var headerPrepared = csv.Configuration.PrepareHeaderForMatch(args);
				if (csv.Configuration.CultureInfo.CompareInfo.Compare(namePrepared, headerPrepared, CompareOptions.IgnoreCase) == 0)
				{
					return i;
				}
			}

			throw new IndexOutOfRangeException($"Field with name '{name}' and prepared name '{namePrepared}' was not found.");
		}

		/// <inheritdoc />
		public DataTable GetSchemaTable()
		{
			if (schemaTable != null)
			{
				return schemaTable;
			}

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
				var header = csv.HeaderRecord;

				Debug.Assert(header != null, $"Expected {nameof(csv.ReadHeader)} to have already been called (i.e in the constructor)");

				for (var i = 0; i < header.Length; i++)
				{
					var row = dt.NewRow();
					row["AllowDBNull"] = true;
					row["AutoIncrementSeed"] = DBNull.Value;
					row["AutoIncrementStep"] = DBNull.Value;
					row["BaseCatalogName"] = null;
					row["BaseColumnName"] = header[i];
					row["BaseColumnNamespace"] = null;
					row["BaseSchemaName"] = null;
					row["BaseTableName"] = null;
					row["BaseTableNamespace"] = null;
					row["ColumnName"] = header[i];
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

		/// <inheritdoc />
		public string GetString(int i)
		{
			return csv.GetField(i) ?? string.Empty;
		}

		/// <inheritdoc />
		public object GetValue(int i)
		{
			return IsDBNull(i) ? DBNull.Value : (csv.GetField(i) ?? string.Empty);
		}

		/// <inheritdoc />
		public int GetValues(object[] values)
		{
			for (var i = 0; i < values.Length; i++)
			{
				values[i] = IsDBNull(i) ? DBNull.Value : (csv.GetField(i) ?? string.Empty);
			}

			return csv.Parser.Count;
		}

		/// <inheritdoc />
		public bool IsDBNull(int i)
		{
			var field = csv.GetField(i);
			var nullValues = csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues;

			return field == null || nullValues.Contains(field);
		}

		/// <inheritdoc />
		public bool NextResult()
		{
			return false;
		}

		/// <inheritdoc />
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
