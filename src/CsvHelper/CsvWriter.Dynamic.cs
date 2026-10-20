using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

// Records without a record type: CsvDynamicRecords, ExpandoObjects and other dictionaries keyed by column name.
public sealed partial class CsvWriter
{
	/// <summary>
	/// Writes dynamic records: the names of the first record are the header (when one is written) and the columns, and
	/// every record writes its values in them, an empty field where it has none.
	/// </summary>
	private void WriteDynamicRecords<T>(IEnumerable<T> records)
	{
		string[]? names = null;
		CsvDynamicSchema? schema = null;
		try
		{
			foreach (var record in records)
			{
				if (record is null)
				{
					throw NullRecord("records");
				}

				if (names is null)
				{
					names = DynamicNames(record);
					schema = (record as CsvDynamicRecord)?.Schema;
					if (NeedsHeader())
					{
						WriteHeader(names);
					}
				}

				WriteDynamicFields(record, names, schema);
				FinishRecord();
				if (pos >= flushThreshold)
				{
					FlushBuffer();
				}
			}
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	/// <summary>Writes dynamic records as <see cref="WriteDynamicRecords{T}"/> does, flushing asynchronously.</summary>
	private async ValueTask WriteDynamicRecordsAsync<T>(IEnumerable<T> records, CancellationToken cancellationToken)
	{
		string[]? names = null;
		CsvDynamicSchema? schema = null;
		try
		{
			foreach (var record in records)
			{
				if (record is null)
				{
					throw NullRecord("records");
				}

				if (names is null)
				{
					(names, schema) = await StartDynamicAsync(record, cancellationToken).ConfigureAwait(false);
				}

				WriteDynamicFields(record, names, schema);
				FinishRecord();
				if (pos >= flushThreshold)
				{
					await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	/// <summary>Writes an asynchronous sequence of dynamic records as <see cref="WriteDynamicRecords{T}"/> does.</summary>
	private async ValueTask WriteDynamicRecordsAsync<T>(IAsyncEnumerable<T> records, CancellationToken cancellationToken)
	{
		string[]? names = null;
		CsvDynamicSchema? schema = null;
		try
		{
			await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				if (record is null)
				{
					throw NullRecord("records");
				}

				if (names is null)
				{
					(names, schema) = await StartDynamicAsync(record, cancellationToken).ConfigureAwait(false);
				}

				WriteDynamicFields(record, names, schema);
				FinishRecord();
				if (pos >= flushThreshold)
				{
					await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	/// <summary>Takes the columns from the first dynamic record and writes the header, when one is written.</summary>
	private async ValueTask<(string[] Names, CsvDynamicSchema? Schema)> StartDynamicAsync(object record, CancellationToken cancellationToken)
	{
		var names = DynamicNames(record);
		if (NeedsHeader())
		{
			foreach (var name in names)
			{
				WriteField(name.AsSpan());
			}

			await EndRecordAsync(cancellationToken).ConfigureAwait(false);
		}

		return (names, (record as CsvDynamicRecord)?.Schema);
	}

	/// <summary>Writes one dynamic record in the order of its own names, as a complete record.</summary>
	private void WriteDynamicRecord(object record)
	{
		try
		{
			WriteDynamicFields(record, DynamicNames(record), (record as CsvDynamicRecord)?.Schema);
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	/// <summary>The names of a dynamic record: its columns, or the keys of a dictionary.</summary>
	private static string[] DynamicNames(object record)
	{
		switch (record)
		{
			case CsvDynamicRecord dynamicRecord:
				var names = dynamicRecord.Names;
				var copy = new string[names.Count];
				for (var i = 0; i < copy.Length; i++)
				{
					copy[i] = names[i];
				}

				return copy;
			case IDictionary<string, object?> dictionary:
				return [.. dictionary.Keys];
			case IReadOnlyDictionary<string, string?> dictionary:
				return [.. dictionary.Keys];
			case IDictionary<string, string?> dictionary:
				return [.. dictionary.Keys];
			default:
				throw NotDynamic(record.GetType());
		}
	}

	/// <summary>
	/// Writes the values of a dynamic record in the columns <paramref name="names"/>. A <see cref="CsvDynamicRecord"/>
	/// of the same schema as the first record writes its fields by index, as the bytes it read.
	/// </summary>
	private void WriteDynamicFields(object record, string[] names, CsvDynamicSchema? schema)
	{
		switch (record)
		{
			case CsvDynamicRecord dynamicRecord when dynamicRecord.Schema == schema:
				for (var i = 0; i < names.Length; i++)
				{
					WriteField(i < dynamicRecord.FieldCount ? dynamicRecord.GetSpan(i) : default);
				}

				break;
			case CsvDynamicRecord dynamicRecord:
				foreach (var name in names)
				{
					WriteField(dynamicRecord.TryGetSpan(name, out var field) ? field : default);
				}

				break;
			case IDictionary<string, object?> dictionary:
				foreach (var name in names)
				{
					if (dictionary.TryGetValue(name, out var value))
					{
						WriteField(value);
					}
					else
					{
						WriteField(ReadOnlySpan<char>.Empty);
					}
				}

				break;
			case IReadOnlyDictionary<string, string?> dictionary:
				foreach (var name in names)
				{
					WriteField(dictionary.TryGetValue(name, out var value) ? value : "");
				}

				break;
			case IDictionary<string, string?> dictionary:
				foreach (var name in names)
				{
					WriteField(dictionary.TryGetValue(name, out var value) ? value : "");
				}

				break;
			default:
				throw NotDynamic(record.GetType());
		}
	}

	private static InvalidOperationException NotDynamic(Type type) => new(
		$"{type} has no CSV record writer and is not a dynamic record: write CsvDynamicRecords, ExpandoObjects or dictionaries " +
		"keyed by column name, or mark the type [CsvRecord].");

	/// <summary>
	/// Whether records of <typeparamref name="T"/> are written as dynamic records: <see cref="object"/> (<c>dynamic</c>),
	/// <see cref="CsvDynamicRecord"/>, and dictionaries keyed by column name (<c>ExpandoObject</c> among them) that have
	/// no record writer of their own.
	/// </summary>
	private static class DynamicRecords<T>
	{
		public static readonly bool Apply = Compute();

		private static bool Compute()
		{
			var type = typeof(T);
			if (type == typeof(object) || type == typeof(CsvDynamicRecord))
			{
				return true;
			}

			if (type.IsValueType)
			{
				return false;
			}

			var dictionary = typeof(IDictionary<string, object?>).IsAssignableFrom(type) ||
							 typeof(IReadOnlyDictionary<string, string?>).IsAssignableFrom(type) ||
							 typeof(IDictionary<string, string?>).IsAssignableFrom(type);

			return dictionary && !CsvRecordWriter.IsRegistered<T>();
		}
	}
}
