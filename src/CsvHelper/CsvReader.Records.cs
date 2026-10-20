using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CsvHelper;

// Records of [CsvRecord] types, read by the mappers the source generator writes (see CsvRecordMapper).
public sealed partial class CsvReader
{
	// The mapper GetRecord used last.
	private object? recordMapper;

	/// <summary>
	/// Reads the current record as a <typeparamref name="T"/>, a <see cref="CsvRecordAttribute"/> type. The mapper
	/// is created on the first call (reading the header if needed) and reused while <typeparamref name="T"/> stays
	/// the same.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type to read.</typeparam>
	/// <returns>A new <typeparamref name="T"/> with the values of the current record.</returns>
	/// <exception cref="System.InvalidOperationException"><typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type.</exception>
	/// <exception cref="KeyNotFoundException">The header lacks a column that <typeparamref name="T"/> requires.</exception>
	/// <exception cref="CsvFormatException">
	/// The record cannot be read, such as a field that cannot be parsed as the type of its member. The exception names
	/// the record, the field and the member, and the cause is its <see cref="Exception.InnerException"/>.
	/// </exception>
	public T GetRecord<T>() => Map(Mapper<T>());

	/// <summary>
	/// Reads the current record into <paramref name="record"/>, an existing instance of a <see cref="CsvRecordAttribute"/>
	/// class, instead of creating one. See <see cref="EnumerateRecords{T}(T)"/>.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> class to read.</typeparam>
	/// <param name="record">The instance whose members are set.</param>
	/// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
	/// <exception cref="System.InvalidOperationException"><typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type.</exception>
	/// <exception cref="NotSupportedException">
	/// <typeparamref name="T"/> has members that only its constructor or an object initializer can set.
	/// </exception>
	/// <exception cref="KeyNotFoundException">The header lacks a column that <typeparamref name="T"/> requires.</exception>
	/// <exception cref="CsvFormatException">
	/// The record cannot be read, such as a field that cannot be parsed as the type of its member. The exception names
	/// the record, the field and the member, and the cause is its <see cref="Exception.InnerException"/>.
	/// </exception>
	public void GetRecord<T>(T record) where T : class
	{
		ArgumentNullException.ThrowIfNull(record);
		var mapper = Mapper<T>();
		try
		{
			mapper.Read(this, record);
		}
		catch (Exception e) when (e is not NotSupportedException)
		{
			throw RecordException(mapper, e);
		}
	}

	private CsvRecordMapper<T> Mapper<T>()
	{
		if (recordMapper is not CsvRecordMapper<T> mapper)
		{
			recordMapper = mapper = CsvRecordMapper.Create<T>(this);
		}

		return mapper;
	}

	/// <summary>
	/// Reads the remaining records as <typeparamref name="T"/>, a <see cref="CsvRecordAttribute"/> type. The header
	/// is read and the columns are resolved by this call; the records are read as they are enumerated, and the
	/// exceptions of <see cref="Read"/> are thrown by the enumeration. A record that cannot be read as
	/// <typeparamref name="T"/> throws a <see cref="CsvFormatException"/> that names the record, the field and the member,
	/// with the exception of the field or member as its <see cref="Exception.InnerException"/>, unless
	/// <see cref="CsvReaderOptions.OnRecordError"/> skips it.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type to read.</typeparam>
	/// <returns>A sequence that reads the next record each time it moves, and can be enumerated once.</returns>
	/// <exception cref="System.InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type, or this reader is the one a
	/// <see cref="ParallelEnumerate{T}(System.Func{CsvReader, T}, int)"/> selector got.
	/// </exception>
	/// <exception cref="KeyNotFoundException">The header lacks a column that <typeparamref name="T"/> requires.</exception>
	/// <exception cref="System.ObjectDisposedException">The reader has been disposed.</exception>
	public IEnumerable<T> GetRecords<T>()
	{
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		var mapper = CsvRecordMapper.Create<T>(this);

		return onRecordError is null ? Records(mapper) : RecordsSkippingErrors(mapper);
	}

	private IEnumerable<T> Records<T>(CsvRecordMapper<T> mapper)
	{
		while (Read())
		{
			// Not a call to Map: the handler stays in this loop, so a record costs no extra call.
			T record;
			try
			{
				record = mapper.Read(this);
			}
			catch (Exception e) when (e is not NotSupportedException)
			{
				throw RecordException(mapper, e);
			}

			yield return record;
		}
	}

	private IEnumerable<T> RecordsSkippingErrors<T>(CsvRecordMapper<T> mapper)
	{
		while (Read())
		{
			if (TryMap(mapper, out var record))
			{
				yield return record;
			}
		}
	}

	/// <summary>
	/// Reads the remaining records into <paramref name="record"/>, one instance of a <see cref="CsvRecordAttribute"/>
	/// class that is refilled for every record, so that reading allocates no object per record (strings are still
	/// created, unless <see cref="CsvReaderOptions.PoolStrings"/> shares them). Each step of the enumeration returns the
	/// same instance: copy what you keep. The header is read and the columns are resolved by this call.
	/// </summary>
	/// <remarks>
	/// Every member that a record sets is set again: a member whose column is optional and missing from the header keeps
	/// the value it has. Members whose type is itself a record type get a new instance for each record.
	/// </remarks>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> class to read.</typeparam>
	/// <param name="record">The instance to refill.</param>
	/// <returns>A sequence that reads the next record into <paramref name="record"/> each time it moves, and can be enumerated once.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
	/// <exception cref="System.InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type, or this reader is the one a
	/// <see cref="ParallelEnumerate{T}(System.Func{CsvReader, T}, int)"/> selector got.
	/// </exception>
	/// <exception cref="NotSupportedException">
	/// <typeparamref name="T"/> has members that only its constructor or an object initializer can set (constructor
	/// parameters, <c>init</c> accessors); thrown by the enumeration.
	/// </exception>
	/// <exception cref="KeyNotFoundException">The header lacks a column that <typeparamref name="T"/> requires.</exception>
	/// <exception cref="System.ObjectDisposedException">The reader has been disposed.</exception>
	public IEnumerable<T> EnumerateRecords<T>(T record) where T : class
	{
		ArgumentNullException.ThrowIfNull(record);
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		var mapper = CsvRecordMapper.Create<T>(this);

		return Refill(mapper, record);
	}

	private IEnumerable<T> Refill<T>(CsvRecordMapper<T> mapper, T record) where T : class
	{
		while (Read())
		{
			if (TryRefill(mapper, record))
			{
				yield return record;
			}
		}
	}

	/// <summary>
	/// Asynchronously reads the remaining records as <typeparamref name="T"/>, a <see cref="CsvRecordAttribute"/>
	/// type. The header is read and the columns are resolved when the enumeration starts, so every exception,
	/// including those of <see cref="GetRecords{T}"/>, is thrown by the enumeration.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type to read.</typeparam>
	/// <param name="cancellationToken">
	/// Cancels the reads. A token passed to <c>WithCancellation</c> is combined with this one.
	/// </param>
	/// <returns>An asynchronous sequence that reads the next record each time it moves, and can be enumerated once.</returns>
	public async IAsyncEnumerable<T> GetRecordsAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		if (!headerDone)
		{
			await EnsureHeaderAsync(cancellationToken).ConfigureAwait(false);
		}

		var mapper = CsvRecordMapper.Create<T>(this);
		while (await ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			if (TryMap(mapper, out var record))
			{
				yield return record;
			}
		}
	}

	/// <summary>
	/// Asynchronously reads the remaining records into <paramref name="record"/>, one instance that is refilled for every
	/// record. See <see cref="EnumerateRecords{T}(T)"/>; as in <see cref="GetRecordsAsync{T}"/>, every exception is
	/// thrown by the enumeration.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> class to read.</typeparam>
	/// <param name="record">The instance to refill.</param>
	/// <param name="cancellationToken">
	/// Cancels the reads. A token passed to <c>WithCancellation</c> is combined with this one.
	/// </param>
	/// <returns>An asynchronous sequence that reads the next record into <paramref name="record"/> each time it moves, and can be enumerated once.</returns>
	public async IAsyncEnumerable<T> EnumerateRecordsAsync<T>(T record, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(record);
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		if (!headerDone)
		{
			await EnsureHeaderAsync(cancellationToken).ConfigureAwait(false);
		}

		var mapper = CsvRecordMapper.Create<T>(this);
		while (await ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			if (TryRefill(mapper, record))
			{
				yield return record;
			}
		}
	}

	/// <summary>
	/// Reads the remaining records as <typeparamref name="T"/>, a <see cref="CsvRecordAttribute"/> type, on several
	/// threads and returns them in input order. The header is read and the columns are resolved by this call. See
	/// <see cref="ParallelEnumerate{T}(System.Func{CsvReader, T}, int)"/>.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type to read.</typeparam>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <returns>The records in input order. The sequence can be enumerated once.</returns>
	/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="System.InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type, or this reader is the one a
	/// <see cref="ParallelEnumerate{T}(System.Func{CsvReader, T}, int)"/> selector got.
	/// </exception>
	/// <exception cref="KeyNotFoundException">The header lacks a column that <typeparamref name="T"/> requires.</exception>
	/// <exception cref="System.ObjectDisposedException">The reader has been disposed.</exception>
	public IEnumerable<T> ParallelEnumerate<T>(int maxDegreeOfParallelism = -1)
	{
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		var mapper = CsvRecordMapper.Create<T>(this);
		if (onRecordError is null)
		{
			return ParallelEnumerate(reader => reader.Map(mapper), maxDegreeOfParallelism);
		}

		// The workers mark the records they could not map; the enumeration leaves them out.
		var results = ParallelEnumerate(reader => reader.TryMap(mapper, out var record) ? (true, record) : (false, default(T)!), maxDegreeOfParallelism);

		return Mapped(results);

		static IEnumerable<T> Mapped(IEnumerable<(bool Ok, T Record)> results)
		{
			foreach (var (ok, record) in results)
			{
				if (ok)
				{
					yield return record;
				}
			}
		}
	}

	/// <summary>
	/// Checks, without reading a record, that the header has every column that <typeparamref name="T"/> requires: the
	/// check that <see cref="GetRecords{T}"/> makes. The header is read if it has not been.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <exception cref="KeyNotFoundException">
	/// The header lacks columns that <typeparamref name="T"/> requires, which the message names, or the input has no
	/// header and required members have no column index.
	/// </exception>
	/// <exception cref="System.InvalidOperationException"><typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type.</exception>
	/// <exception cref="System.ObjectDisposedException">The reader has been disposed.</exception>
	public void ValidateHeader<T>()
	{
		ThrowIfDisposed();
		_ = CsvRecordMapper.Create<T>(this);
	}

	/// <summary>
	/// Checks, without reading a record and without throwing, that the header has every column that
	/// <typeparamref name="T"/> requires; see <see cref="ValidateHeader{T}"/>.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="missingColumns">
	/// The missing columns, as the message of <see cref="ValidateHeader{T}"/> names them: the member and the header name it
	/// looks for, such as <c>Order.Price ('Unit Price')</c>. Empty when none is missing.
	/// </param>
	/// <returns>Whether the header has every required column.</returns>
	/// <exception cref="System.InvalidOperationException"><typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type.</exception>
	/// <exception cref="System.ObjectDisposedException">The reader has been disposed.</exception>
	public bool TryValidateHeader<T>(out IReadOnlyList<string> missingColumns)
	{
		ThrowIfDisposed();
		try
		{
			_ = CsvRecordMapper.Create<T>(this);
		}
		catch (KeyNotFoundException e) when (e.Data[CsvRecordMapper.MissingColumnsKey] is string[] columns)
		{
			missingColumns = columns;

			return false;
		}

		missingColumns = [];

		return true;
	}

	/// <summary>Maps the current record; a failure becomes the <see cref="CsvFormatException"/> of <see cref="RecordException"/>.</summary>
	private T Map<T>(CsvRecordMapper<T> mapper)
	{
		try
		{
			return mapper.Read(this);
		}
		catch (Exception e) when (e is not NotSupportedException)
		{
			throw RecordException(mapper, e);
		}
	}

	/// <summary>
	/// Maps the current record, or tells <see cref="CsvReaderOptions.OnRecordError"/> (when there is one) why it cannot be
	/// mapped.
	/// </summary>
	/// <returns>Whether the record was mapped; <see langword="false"/> when the handler skips it.</returns>
	private bool TryMap<T>(CsvRecordMapper<T> mapper, out T record)
	{
		try
		{
			record = mapper.Read(this);

			return true;
		}
		catch (Exception e) when (e is not NotSupportedException)
		{
			var error = RecordException(mapper, e);
			if (onRecordError is null || !onRecordError(RecordError(error)))
			{
				throw error;
			}

			record = default!;

			return false;
		}
	}

	private bool TryRefill<T>(CsvRecordMapper<T> mapper, T record)
	{
		try
		{
			mapper.Read(this, record);

			return true;
		}
		catch (Exception e) when (e is not NotSupportedException)
		{
			var error = RecordException(mapper, e);
			if (onRecordError is null || !onRecordError(RecordError(error)))
			{
				throw error;
			}

			return false;
		}
	}

	private CsvRecordError RecordError(Exception e) =>
		new(rowNumber, ByteOffset, e, System.Text.Encoding.UTF8.GetString(RawRecord));

	/// <summary>
	/// The exception for a record that <paramref name="mapper"/> could not read: a <see cref="CsvFormatException"/> that
	/// names the record, and the field and member that <see cref="CsvRecordMapper{T}.FindFailingMember"/> finds, with
	/// <paramref name="e"/> as its cause.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private CsvFormatException RecordException<T>(CsvRecordMapper<T> mapper, Exception e)
	{
		string? member;
		int field;
		try
		{
			member = mapper.FindFailingMember(this, out field);
		}
		catch (Exception)
		{
			// The search of a hand-written mapper failed itself: the record is all that is known.
			member = null;
			field = -1;
		}

		string message;
		if (member is null)
		{
			message = $"Cannot read record {recordNumber}{AtLine} as {typeof(T).Name}: {e.Message}";
		}
		else
		{
			var name = field >= 0 && field < headers.Length ? $" '{headers[field]}'" : "";
			var from = field >= 0 ? $" from field {field}{name}" : "";
			message = $"Cannot read {member}{from} of record {recordNumber}{AtLine}: {e.Message}";
		}

		return new CsvFormatException(message, recordNumber, ByteOffset, e) { LineNumber = LineOrZero, Member = member, FieldIndex = field };
	}
}
