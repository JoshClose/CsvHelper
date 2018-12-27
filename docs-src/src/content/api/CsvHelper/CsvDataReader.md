# CsvDataReader Class

Namespace: [CsvHelper](/api/CsvHelper)

Provides a means of reading a CSV file forward-only by using CsvReader.

```CS
public class CsvDataReader : IDataReader
```

## Constructors
&nbsp; | &nbsp;
- | -
CsvDataReader([CsvReader](/api/CsvHelper/CsvReader)) | Initializes a new instance of the CsvDataReader class.

## Properties
&nbsp; | &nbsp;
- | -
Depth | Gets a value indicating the depth of nesting for the current row.
FieldCount | Gets the number of columns in the current row.
IsClosed | Gets a value indicating whether the data reader is closed.
RecordsAffected | Gets the number of rows changed, inserted, or deleted by execution of the SQL statement.
this[int] | Gets the column with the specified index.
this[string] | Gets the column with the specified name.

## Methods
&nbsp; | &nbsp;
- | -
Close() | Closes the System.Data.IDataReader Object.
Dispose() | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
GetBoolean(int) | Gets the value of the specified column as a Boolean.
GetByte(int) | Gets the 8-bit unsigned integer value of the specified column.
GetBytes(int, long, byte[], int, int) | Reads a stream of bytes from the specified column offset into the buffer as an array, starting at the given buffer offset.
GetChar(int) | Gets the character value of the specified column.
GetChars(int, long, char[], int, int) | Reads a stream of characters from the specified column offset into the buffer as an array, starting at the given buffer offset.
GetData(int) | Returns an System.Data.IDataReader for the specified column ordinal.
GetDataTypeName(int) | Gets the data type information for the specified field.
GetDateTime(int) | Gets the date and time data value of the specified field.
GetDecimal(int) | Gets the fixed-position numeric value of the specified field.
GetDouble(int) | Gets the double-precision floating point number of the specified field.
GetFieldType(int) | Gets the System.Type information corresponding to the type of System.Object that would be returned from System.Data.IDataRecord.GetValue(System.Int32).
GetFloat(int) | Gets the single-precision floating point number of the specified field.
GetGuid(int) | Returns the GUID value of the specified field.
GetInt16(int) | Gets the 16-bit signed integer value of the specified field.
GetInt32(int) | Gets the 32-bit signed integer value of the specified field.
GetInt64(int) | Gets the 64-bit signed integer value of the specified field.
GetName(int) | Gets the name for the field to find.
GetOrdinal(string) | Return the index of the named field.
GetSchemaTable() | Returns a System.Data.DataTable that describes the column metadata of the System.Data.IDataReader.
GetString(int) | Gets the string value of the specified field.
GetValue(int) | Return the value of the specified field.
GetValues(object[]) | Populates an array of objects with the column values of the current record.
IsDBNull(int) | Return whether the specified field is set to null.
NextResult() | Advances the data reader to the next result, when reading the results of batch SQL statements.
Read() | Advances the System.Data.IDataReader to the next record.
