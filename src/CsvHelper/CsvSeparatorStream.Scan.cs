using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif
using IFlag = CsvHelper.CsvReader.IFlag;
using Off = CsvHelper.CsvReader.Off;
using On = CsvHelper.CsvReader.On;

namespace CsvHelper;

// The stream replaces separators in place, 64 bytes at a time. For each block it builds bit masks: the separator starts
// (one compare per separator byte, the later ones on loads shifted by that byte's offset), the quote or escape
// characters, line breaks, comment characters, the replacement and, with trimming, whitespace.
//
// The fast path takes blocks whose quotes follow the rules: as in the reader's scanner, a prefix XOR of the quote mask
// marks the bytes inside quotes, and every opening quote must start a field or follow a closing quote. Other blocks go
// through an exact loop over the set bits of the masks, which follows the reader's state machine (CsvReader.ScalarBlock)
// one character of interest at a time: escape characters, comment lines, quotes that do not open at the start of a field
// (text), overlapping separators (||| for ||), trimmed whitespace before an opening quote and the replacement.
//
// Replacing keeps the first byte of each separator, overwritten with the replacement, and drops the others. The kept bytes
// of a block are packed at the output position, which never passes the block (the output is never longer than the
// input), so the stream works in the caller's buffer: one VPCOMPRESSB per block with AVX-512 VBMI2 (on .NET 10),
// otherwise a shuffle of each 8 bytes from a table of the 256 masks of kept bytes (shifts on the targets without SIMD).
// With a separator of two bytes the shuffle writes the replacement too: the byte before each dropped byte is the first
// of a separator.
public sealed partial class CsvSeparatorStream
{
	private const int BlockSize = 64;
	private const ulong EvenBits = 0x5555_5555_5555_5555UL;
	// Room for a block, the lookahead of the longest separator and the vector loads at its shifted positions.
	private const int TailBufferSize = 2 * BlockSize + MaxSeparatorBytes;
	// A position long before the data. Positions are relative to the start of the data of the current read.
	private const int NoPosition = int.MinValue / 2;

	private const int StatePlain = 0;           // outside quotes
	private const int StateQuoted = 1;          // in a quoted field
	private const int StateQuoteInQuoted = 2;   // after a quote in a quoted field: closed, unless a quote follows at quotePos + 1
	private const int StateComment = 3;         // in a comment line

	internal const int CompactScalar = 0;
	internal const int CompactShuffle = 1;
	internal const int CompactCompress = 2;
	internal const int CompactPairShuffle = 3;      // CompactShuffle, with PairShuffleCompactor for separators of two bytes

	private static readonly int defaultVectorWidth =
#if NET
		Vector512.IsHardwareAccelerated ? 512 :
		Vector256.IsHardwareAccelerated ? 256 : 128;
#else
		64;
#endif

	private static readonly int defaultCompaction =
#if NET10_0_OR_GREATER
		Avx512Vbmi2.IsSupported && Avx512Vbmi.IsSupported ? CompactCompress :
#endif
#if NET
		Ssse3.IsSupported || AdvSimd.Arm64.IsSupported ? CompactPairShuffle :
#endif
		CompactScalar;

	internal int vectorWidth = defaultVectorWidth;  // 512, 256, 128 (.NET only), 64 (64-bit integer masks) or 0 (exact loop only, for testing)
	internal int compaction = defaultCompaction;    // CompactCompress (AVX-512 VBMI2), CompactPairShuffle or CompactShuffle (SSSE3, AdvSimd), or CompactScalar

	// The state after the last byte decided; positions are relative to the start of the data of the current read.
	private int state = StatePlain;
	private int fieldStart;                         // where the current field starts (a quote there opens a quoted field)
	private int rowStart;                           // where the current record starts (a comment character there starts a comment)
	private int quotePos = NoPosition;              // the quote of StateQuoteInQuoted
	private int escapedPos = NoPosition;            // the byte after an escape character, which is text
	private int escapedCrPos = NoPosition;          // the byte after an escaped CR, which is text if it is an LF
	private int skipUntil;                          // the end of the last separator replaced: its other bytes are dropped
	private bool blankRun = true;                   // with trimming: whether the bytes from fieldStart to the current block are all whitespace

	/// <summary>Whether this machine can run a compaction (for the tests).</summary>
	/// <param name="compaction">One of the <c>Compact</c> constants.</param>
	/// <returns><see langword="true"/> when <see cref="compaction"/> can be set to <paramref name="compaction"/>.</returns>
	internal static bool CompactionSupported(int compaction) => compaction switch
	{
#if NET10_0_OR_GREATER
		CompactCompress => Avx512Vbmi2.IsSupported && Avx512Vbmi.IsSupported,
#endif
#if NET
		CompactShuffle or CompactPairShuffle => Ssse3.IsSupported || AdvSimd.Arm64.IsSupported,
#endif
		CompactScalar => true,
		_ => false,
	};

	/// <summary>Shifts the stored positions to the start of the data of the next read, which starts at <paramref name="next"/>.</summary>
	/// <param name="next">The position, in the data of the current read, where the data of the next read starts.</param>
	private void Rebase(int next)
	{
		consumed += next;
		fieldStart = Shift(fieldStart, next);
		rowStart = Shift(rowStart, next);
		quotePos = Shift(quotePos, next);
		escapedPos = Shift(escapedPos, next);
		escapedCrPos = Shift(escapedCrPos, next);
		skipUntil = Shift(skipUntil, next);
	}

	/// <summary>A position moved back by <paramref name="by"/> bytes, no further back than <see cref="NoPosition"/>.</summary>
	/// <param name="position">The position.</param>
	/// <param name="by">The number of bytes to move it back.</param>
	/// <returns>The moved position.</returns>
	private static int Shift(int position, int by) => (int)Math.Max((long)position - by, NoPosition);

	/// <summary>
	/// Replaces the separators that start before <paramref name="processEnd"/> in the first <paramref name="length"/> bytes
	/// at <paramref name="buffer"/>, in place.
	/// </summary>
	/// <param name="buffer">The data: the pending bytes of the last read, then the bytes read now.</param>
	/// <param name="length">The number of bytes of data.</param>
	/// <param name="processEnd">Where to stop: separators that start before it are replaced whole.</param>
	/// <returns>The number of bytes of output, from the start of <paramref name="buffer"/>.</returns>
	private int Replace(Span<byte> buffer, int length, int processEnd)
	{
		ref var buf = ref MemoryMarshal.GetReference(buffer);

		return vectorWidth switch
		{
#if NET
			512 => Replace<SeparatorMasks512>(ref buf, length, processEnd),
			256 => Replace<SeparatorMasks256>(ref buf, length, processEnd),
			128 => Replace<SeparatorMasks128>(ref buf, length, processEnd),
#endif
			_ => Replace<SeparatorMasks64>(ref buf, length, processEnd),
		};
	}

	private int Replace<TM>(ref byte buf, int length, int processEnd) where TM : struct, ISeparatorMasks<TM> =>
		compaction switch
		{
#if NET10_0_OR_GREATER
			CompactCompress => Replace<TM, CompressCompactor>(ref buf, length, processEnd),
#endif
#if NET
			CompactPairShuffle when separator.Length == 2 => Replace<TM, PairShuffleCompactor>(ref buf, length, processEnd),
			CompactShuffle or CompactPairShuffle => Replace<TM, ShuffleCompactor>(ref buf, length, processEnd),
#endif
			_ => Replace<TM, ScalarCompactor>(ref buf, length, processEnd),
		};

	private int Replace<TM, TK>(ref byte buf, int length, int processEnd)
		where TM : struct, ISeparatorMasks<TM> where TK : struct, ICompactor<TK>
	{
		var compactor = default(TK).Create(replacement);
		var written = 0;
		var pos = 0;
		// Whole blocks need the separator's length - 1 bytes after them for the shifted loads.
		var blocksEnd = Math.Min(processEnd, length - (separator.Length - 1));
		while (pos + BlockSize <= blocksEnd)
		{
			if (vectorWidth != 0)
			{
				pos = FastBlocks<TM, TK>(ref buf, pos, blocksEnd - BlockSize + 1, ref written);
				if (pos + BlockSize > blocksEnd) break;
			}

			ref var block = ref Unsafe.Add(ref buf, (nint)(uint)pos);
			var rep = ExactBlock<TM>(ref block, pos, BlockSize, BlockSize, out var drop);
			written += compactor.Compact(ref block, ref Unsafe.Add(ref buf, (nint)(uint)written), rep, ~(drop | (rep * dropMultiplier)));
			pos += BlockSize;
		}

		return pos < processEnd ? Tail<TM, TK>(ref buf, length, processEnd, pos, written) : written;
	}

	/// <summary>The blocks from <paramref name="pos"/> that lack a whole block or its lookahead, through a padded copy.</summary>
	/// <typeparam name="TM">The vector width of the masks.</typeparam>
	/// <typeparam name="TK">The compaction.</typeparam>
	/// <param name="buf">The data.</param>
	/// <param name="length">The number of bytes of data.</param>
	/// <param name="processEnd">Where to stop.</param>
	/// <param name="pos">The first byte to process.</param>
	/// <param name="written">The number of bytes of output so far.</param>
	/// <returns>The number of bytes of output.</returns>
	private int Tail<TM, TK>(ref byte buf, int length, int processEnd, int pos, int written)
		where TM : struct, ISeparatorMasks<TM> where TK : struct, ICompactor<TK>
	{
		Span<byte> copy = stackalloc byte[TailBufferSize];
		ref var t = ref MemoryMarshal.GetReference(copy);
		var compactor = default(TK).Create(replacement);
		var n = separator.Length;
		while (pos < processEnd)
		{
			var valid = Math.Min(BlockSize, processEnd - pos);
			copy.Clear();
			Unsafe.CopyBlockUnaligned(ref t, ref Unsafe.Add(ref buf, (nint)(uint)pos), (uint)Math.Min(length - pos, BlockSize + n - 1));
			// At the end of the input, a separator must fit before it; otherwise processEnd leaves room for one.
			var starts = eof ? length - n + 1 - pos : valid;
			var rep = ExactBlock<TM>(ref t, pos, valid, starts, out var drop);
			// The bytes after the valid ones are packed too, after them, but not copied: a compactor may tell the
			// separators from the bytes it drops (PairShuffleCompactor), so only separators may drop bytes.
			var keep = ~(drop | (rep * dropMultiplier));
			compactor.Compact(ref t, ref t, rep, keep);
			var count = BitOperations.PopCount(keep & LowBits(valid));
			Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref buf, (nint)(uint)written), ref t, (uint)count);
			written += count;
			pos += valid;
		}

		return written;
	}

	// The fast path is compiled for each combination of the dialect's features, which the JIT folds to constants.
	private int FastBlocks<TM, TK>(ref byte buf, int pos, int limit, ref int written)
		where TM : struct, ISeparatorMasks<TM> where TK : struct, ICompactor<TK> =>
		quote < 0 ? escape >= 0 ? FastBlocks<TM, TK, Off, On, Off>(ref buf, pos, limit, ref written)
			: FastBlocks<TM, TK, Off, Off, Off>(ref buf, pos, limit, ref written)
		: trim ? FastBlocks<TM, TK, On, On, On>(ref buf, pos, limit, ref written)
		: FastBlocks<TM, TK, On, On, Off>(ref buf, pos, limit, ref written);

	private int FastBlocks<TM, TK, TQ, TS, TT>(ref byte buf, int pos, int limit, ref int written)
		where TM : struct, ISeparatorMasks<TM> where TK : struct, ICompactor<TK>
		where TQ : struct, IFlag where TS : struct, IFlag where TT : struct, IFlag =>
		comment >= 0
			? FastBlocks<TM, TK, TQ, TS, TT, On>(ref buf, pos, limit, ref written)
			: FastBlocks<TM, TK, TQ, TS, TT, Off>(ref buf, pos, limit, ref written);

	private int FastBlocks<TM, TK, TQ, TS, TT, TC>(ref byte buf, int pos, int limit, ref int written)
		where TM : struct, ISeparatorMasks<TM> where TK : struct, ICompactor<TK>
		where TQ : struct, IFlag where TS : struct, IFlag where TT : struct, IFlag where TC : struct, IFlag =>
		separator.Length == 2 && separator[0] == separator[1]
			? FastBlocks<TM, TK, TQ, TS, TT, TC, On>(ref buf, pos, limit, ref written)
			: FastBlocks<TM, TK, TQ, TS, TT, TC, Off>(ref buf, pos, limit, ref written);

	/// <summary>
	/// Replaces whole blocks from <paramref name="pos"/> while they need nothing but the fast path, up to the block that starts
	/// at <paramref name="limit"/> or later. Returns the position of the first block it did not replace.
	/// </summary>
	/// <typeparam name="TM">The vector width of the masks.</typeparam>
	/// <typeparam name="TK">The compaction.</typeparam>
	/// <typeparam name="TQ">Quotes (Rfc4180 mode).</typeparam>
	/// <typeparam name="TS">Quotes or escape characters (Escape mode): the special character of the mode.</typeparam>
	/// <typeparam name="TT">Trimming, with quotes: an opening quote can follow whitespace.</typeparam>
	/// <typeparam name="TC">Comments.</typeparam>
	/// <typeparam name="TP">A separator of two equal bytes, such as <c>||</c>.</typeparam>
	/// <param name="buf">The data.</param>
	/// <param name="pos">The first block.</param>
	/// <param name="limit">The end of the block starts: every block before it is whole, with its lookahead.</param>
	/// <param name="written">The number of bytes of output, updated.</param>
	/// <returns>The position of the first block not replaced.</returns>
#if NET
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	private int FastBlocks<TM, TK, TQ, TS, TT, TC, TP>(ref byte buf, int pos, int limit, ref int written)
		where TM : struct, ISeparatorMasks<TM> where TK : struct, ICompactor<TK>
		where TQ : struct, IFlag where TS : struct, IFlag where TT : struct, IFlag where TC : struct, IFlag where TP : struct, IFlag
	{
		var masks = default(TM).Create(separator, quote >= 0 ? quote : escape, comment, replacement);
		var compactor = default(TK).Create(replacement);
		// With a separator of two equal bytes, its length and the multiplier of its other bytes are constants.
		var n = default(TP).Value ? 2 : separator.Length;
		var dropMultiplier = default(TP).Value ? 2 : this.dropMultiplier;
		var breaksInQuotes = lineBreaksInQuotes;
		var overlaps = this.overlaps;
		var escaped = Math.Max(escapedPos, escapedCrPos);
		var state = this.state;
		var fieldStart = this.fieldStart;
		var rowStart = this.rowStart;
		var skipUntil = this.skipUntil;
		var blankRun = this.blankRun;
		var blankBytes = default(TT).Value ? this.blankBytes : null;
		var w = written;

		for (; pos < limit; pos += BlockSize)
		{
			ref var block = ref Unsafe.Add(ref buf, (nint)(uint)pos);
			masks.Compute(ref block, n, default(TP).Value, default(TS).Value, default(TQ).Value | default(TC).Value, default(TC).Value, default(TT).Value,
				out var firstM, out var sepM, out var spM, out var nlM, out var cmM, out var rpM, out var wsM);
			if (default(TT).Value && blankBytes is not null) wsM = Blanks(ref block, blankBytes);
			var drop = 0UL;
			if (skipUntil > pos)
			{
				// The last bytes of a separator that started in the previous block.
				drop = (1UL << (skipUntil - pos)) - 1;
				sepM &= ~drop;
			}

			if (default(TC).Value && state == StateComment) break;
			var quoted = 0UL;
			var closeQ = 0UL;
			var prevCloseQ = 0UL;
			if (default(TQ).Value)
			{
				if ((spM | (uint)state) != 0)
				{
					if (state == StateQuoteInQuoted && quotePos == pos - 1) prevCloseQ = 1;
					// Bit i of quoted is set when byte i is inside quotes (an opening quote included, a closing one not).
					quoted = CsvReader.PrefixXor(spM) ^ (state == StateQuoted ? ulong.MaxValue : 0);
					if (!breaksInQuotes && (nlM & quoted) != 0) break;
					closeQ = spM & ~quoted;
				}
			}
			else if (default(TS).Value && (spM != 0 || escaped >= pos))
			{
				// Escape characters need the exact path, and so does the byte after one at the end of the last block.
				break;
			}

			var outside = ~quoted;
			var sepStarts = sepM & outside;
			if (default(TP).Value)
			{
				// A separator of two equal bytes (||): in each run of them, separators start at the even offsets from the
				// run start, as the exact loop takes them from the left. A run never continues one of the last block:
				// its separator took the byte before this block, or the first byte of this block (drop).
				var run = firstM & outside & ~drop;
				var starts = run & ~(run << 1);
				var evenRuns = run & ~(run + (starts & EvenBits));
				sepStarts &= (evenRuns & EvenBits) | (run & ~evenRuns & ~EvenBits);
			}
			else if (overlaps != 0 && Overlapping(sepStarts, overlaps))
			{
				break;
			}

			var lines = nlM & outside;
			if (default(TQ).Value && spM != 0)
			{
				// An opening quote must start a field or follow a closing quote ("" in a quoted field).
				var fieldStarts = ((sepStarts << (n - 1)) | lines) << 1 | Bit(fieldStart - pos);
				if ((spM & quoted & ~(fieldStarts | (closeQ << 1) | prevCloseQ)) != 0) break;
			}

			if (default(TC).Value && (cmM & ((lines << 1) | Bit(rowStart - pos))) != 0) break;
			if ((rpM & outside) != 0) break;

			w += compactor.Compact(ref block, ref Unsafe.Add(ref buf, (nint)(uint)w), sepStarts, ~(drop | (sepStarts * dropMultiplier)));
			if (default(TQ).Value)
			{
				state = (long)quoted < 0 ? StateQuoted : (long)closeQ < 0 ? StateQuoteInQuoted : StatePlain;
				if (state == StateQuoteInQuoted) quotePos = pos + BlockSize - 1;
			}

			// Field starts matter to quotes, record starts to comments.
			if (sepStarts != 0)
			{
				skipUntil = pos + BlockSize - 1 - BitOperations.LeadingZeroCount(sepStarts) + n;
				if (default(TQ).Value && skipUntil > fieldStart) fieldStart = skipUntil;
			}

			if ((default(TQ).Value | default(TC).Value) && lines != 0)
			{
				var after = pos + BlockSize - BitOperations.LeadingZeroCount(lines);
				if (default(TC).Value) rowStart = after;
				if (default(TQ).Value && after > fieldStart) fieldStart = after;
			}

			if (default(TT).Value) blankRun = BlankRunAfter(fieldStart - pos, BlockSize, wsM, blankRun);
		}

		this.state = state;
		this.fieldStart = fieldStart;
		this.rowStart = rowStart;
		this.skipUntil = skipUntil;
		this.blankRun = blankRun;
		written = w;

		return pos;
	}

	/// <summary>Finds the separators of one block with the exact loop, updating the state.</summary>
	/// <typeparam name="TM">The vector width of the masks.</typeparam>
	/// <param name="block">The bytes of the block, readable for 64 bytes plus the separator's length - 1.</param>
	/// <param name="pos">The position of the block in the data.</param>
	/// <param name="valid">The number of bytes of the block to process.</param>
	/// <param name="starts">The number of bytes of the block where a separator can start.</param>
	/// <param name="drop">The bytes at the start of the block that belong to a separator of the previous block.</param>
	/// <returns>The starts of the separators to replace.</returns>
	private ulong ExactBlock<TM>(ref byte block, int pos, int valid, int starts, out ulong drop) where TM : struct, ISeparatorMasks<TM>
	{
		var quotes = quote >= 0;
		var comments = comment >= 0;
		var trimQuotes = trim && quotes;
		default(TM).Create(separator, quotes ? quote : escape, comment, replacement).Compute(ref block, separator.Length,
			false, quotes || escape >= 0, quotes || comments, comments, trimQuotes,
			out _, out var sepM, out var spM, out var nlM, out var cmM, out var rpM, out var wsM);
		if (trimQuotes && blankBytes is not null) wsM = Blanks(ref block, blankBytes);
		drop = skipUntil > pos ? LowBits(skipUntil - pos) : 0;
		var events = ((sepM & LowBits(starts)) | spM | nlM | cmM | rpM) & LowBits(valid) & ~drop;
		var rep = events != 0 ? Events(ref block, pos, events, wsM) : 0;
		if (trimQuotes) blankRun = BlankRunAfter(fieldStart - pos, valid, wsM | ~LowBits(valid), blankRun);

		return rep;
	}

	/// <summary>The bytes of a block that are whitespace by <paramref name="table"/>: the custom WhitespaceCharacters of a reader.</summary>
	/// <param name="block">The bytes of the block, readable for 64 bytes.</param>
	/// <param name="table">Whether each byte value is whitespace.</param>
	/// <returns>Bit i is set when byte i is whitespace.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static ulong Blanks(ref byte block, bool[] table)
	{
		var mask = 0UL;
		for (var i = 0; i < BlockSize; i++)
		{
			if (table[Unsafe.Add(ref block, (nint)(uint)i)])
			{
				mask |= 1UL << i;
			}
		}

		return mask;
	}

	/// <summary>
	/// The exact state machine, over the bytes of <paramref name="events"/> (separator starts, quotes or escapes, line
	/// breaks, comment characters and the replacement) in order. It mirrors the reader's: CsvReader.ScalarBlock.
	/// </summary>
	/// <param name="block">The bytes of the block.</param>
	/// <param name="pos">The position of the block in the data.</param>
	/// <param name="events">The bytes to visit; at least one.</param>
	/// <param name="blanks">The space and tab bytes of the block (with trimming and quotes).</param>
	/// <returns>The starts of the separators to replace.</returns>
	/// <exception cref="CsvFormatException">The replacement is outside quoted fields.</exception>
	private ulong Events(ref byte block, int pos, ulong events, ulong blanks)
	{
		var rep = 0UL;
		var sep0 = separator[0];
		var n = separator.Length;
		do
		{
			var i = BitOperations.TrailingZeroCount(events);
			events &= events - 1;
			var p = pos + i;
			int c = Unsafe.Add(ref block, (nint)(uint)i);
			if (p < skipUntil) continue;
			if (p == escapedPos)
			{
				// An escaped byte is text, and so is an LF right after an escaped CR.
				if (c == '\r') escapedCrPos = p + 1;
				continue;
			}

			if (p == escapedCrPos && c == '\n') continue;
			switch (state)
			{
				case StateQuoted:
					if (c == quote)
					{
						state = StateQuoteInQuoted;
						quotePos = p;
					}
					else if (!lineBreaksInQuotes && (c == '\r' || c == '\n'))
					{
						state = StatePlain;
						fieldStart = rowStart = p + 1;
					}

					continue;
				case StateQuoteInQuoted:
					if (c == quote && p == quotePos + 1)
					{
						// "" in a quoted field.
						state = StateQuoted;
						continue;
					}

					state = StatePlain;
					break;
				case StateComment:
					if (c == '\r' || c == '\n')
					{
						state = StatePlain;
						fieldStart = rowStart = p + 1;
					}

					continue;
			}

			if (c == sep0)
			{
				rep |= 1UL << i;
				skipUntil = fieldStart = p + n;
			}
			else if (c == '\r' || c == '\n')
			{
				fieldStart = rowStart = p + 1;
			}
			else if (c == quote)
			{
				// A quote opens a quoted field at the start of a field, after whitespace when trimming; elsewhere it is text.
				if (p == fieldStart || (trim && BlankBefore(pos, i, blanks))) state = StateQuoted;
			}
			else if (c == escape)
			{
				escapedPos = p + 1;
			}
			else if (c == comment)
			{
				if (p == rowStart) state = StateComment;
			}
			else if (c == replacement)
			{
				ThrowReplacementInInput(p);
			}
		} while (events != 0);

		return rep;
	}

	/// <summary>Whether the bytes from <see cref="fieldStart"/> to byte <paramref name="i"/> of the block at <paramref name="pos"/> are all whitespace.</summary>
	/// <param name="pos">The position of the block in the data.</param>
	/// <param name="i">The byte of the block, not included.</param>
	/// <param name="blanks">The space and tab bytes of the block.</param>
	/// <returns><see langword="true"/> when every byte of the field before byte <paramref name="i"/> is a space or a tab.</returns>
	private bool BlankBefore(int pos, int i, ulong blanks)
	{
		var from = fieldStart - pos;
		if (from >= i) return true;
		var range = LowBits(i) & ~LowBits(from);

		return (blanks & range) == range && (from >= 0 || blankRun);
	}

	/// <summary>
	/// Whether the bytes from the field start, <paramref name="from"/> bytes into a block, to the end of its first
	/// <paramref name="length"/> bytes are all whitespace (<paramref name="blanks"/>), given <paramref name="before"/> for a
	/// field that started before the block.
	/// </summary>
	/// <param name="from">The field start, relative to the block (negative when before it).</param>
	/// <param name="length">The number of bytes of the block.</param>
	/// <param name="blanks">The space and tab bytes of the block.</param>
	/// <param name="before">Whether the bytes of the field before the block are all whitespace.</param>
	/// <returns>Whether the bytes of the field up to the end of the block are all whitespace.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool BlankRunAfter(int from, int length, ulong blanks, bool before)
	{
		if (from >= length) return true;
		var range = LowBits(length) & ~LowBits(from);

		return (blanks & range) == range && (from >= 0 || before);
	}

	/// <summary>Throws for the replacement found outside quoted fields.</summary>
	/// <param name="p">Its position in the data.</param>
	/// <exception cref="CsvFormatException">Always.</exception>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ThrowReplacementInInput(int p) =>
		throw new CsvFormatException(
			$"The input contains U+{replacement:X4}, which the CsvSeparatorStream puts in place of its separator (byte offset {consumed + p}). " +
			"Create the stream with another replacement.", 0, consumed + p);

	/// <summary>Whether two of the separator starts are closer than its length (only possible at the distances in <paramref name="distances"/>).</summary>
	/// <param name="starts">The separator starts.</param>
	/// <param name="distances">Bit d set when two separators d bytes apart can overlap; at least one.</param>
	/// <returns><see langword="true"/> when two of <paramref name="starts"/> overlap.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool Overlapping(ulong starts, ulong distances)
	{
		do
		{
			if ((starts & (starts >> BitOperations.TrailingZeroCount(distances))) != 0) return true;
			distances &= distances - 1;
		} while (distances != 0);

		return false;
	}

	/// <summary>The bit of a position in a block, or 0 when it is outside the block.</summary>
	/// <param name="offset">The position, relative to the block.</param>
	/// <returns>The bit, or 0.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Bit(int offset) => (uint)offset < BlockSize ? 1UL << offset : 0;

	/// <summary>The lowest <paramref name="count"/> bits (none when it is negative, all 64 from 64 on).</summary>
	/// <param name="count">The number of bits.</param>
	/// <returns>The bits.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong LowBits(int count) => count <= 0 ? 0 : count >= BlockSize ? ulong.MaxValue : (1UL << count) - 1;
}

/// <summary>Computes the 64 bit masks of one block for <see cref="CsvSeparatorStream"/> with a given vector width.</summary>
/// <typeparam name="TSelf">The implementing struct, so that calls through the constraint are not virtual.</typeparam>
internal interface ISeparatorMasks<TSelf> where TSelf : struct, ISeparatorMasks<TSelf>
{
	/// <summary>Creates the masks for the given bytes, broadcast to the vector width. Called on <c>default(TSelf)</c>.</summary>
	/// <param name="separator">The bytes of the separator, at least 1.</param>
	/// <param name="special">The quote byte, or the escape byte in Escape mode, or -1.</param>
	/// <param name="comment">The comment byte, or -1.</param>
	/// <param name="replacement">The replacement byte.</param>
	/// <returns>The masks, ready for <see cref="Compute"/>.</returns>
	public TSelf Create(byte[] separator, int special, int comment, byte replacement);

	/// <summary>Computes the masks of the 64 bytes at <paramref name="p"/>: bit <c>i</c> of each mask describes byte <c>i</c>.</summary>
	/// <param name="p">The first byte of the block; 64 bytes plus <paramref name="separatorLength"/> - 1 from it must be readable.</param>
	/// <param name="separatorLength">The length of the separator.</param>
	/// <param name="firsts">Whether to compute <paramref name="firstM"/>; it is 0 otherwise.</param>
	/// <param name="specials">Whether to compute <paramref name="spM"/>; it is 0 otherwise.</param>
	/// <param name="lineBreaks">Whether to compute <paramref name="nlM"/>; it is 0 otherwise.</param>
	/// <param name="comments">Whether to compute <paramref name="cmM"/>; it is 0 otherwise.</param>
	/// <param name="blanks">Whether to compute <paramref name="wsM"/>; it is 0 otherwise.</param>
	/// <param name="firstM">The bytes equal to the first byte of the separator.</param>
	/// <param name="sepM">The bytes where the whole separator starts.</param>
	/// <param name="spM">The quote (or escape) bytes.</param>
	/// <param name="nlM">The CR and LF bytes.</param>
	/// <param name="cmM">The comment bytes.</param>
	/// <param name="rpM">The replacement bytes.</param>
	/// <param name="wsM">The space and tab bytes.</param>
	public void Compute(ref byte p, int separatorLength, bool firsts, bool specials, bool lineBreaks, bool comments, bool blanks,
		out ulong firstM, out ulong sepM, out ulong spM, out ulong nlM, out ulong cmM, out ulong rpM, out ulong wsM);
}

/// <summary>The masks from 64-bit integer operations, 8 bytes at a time: the targets without SIMD, and the tests on .NET.</summary>
internal struct SeparatorMasks64 : ISeparatorMasks<SeparatorMasks64>
{
	private const ulong Lf = 0x0A0A_0A0A_0A0A_0A0AUL;
	private const ulong Cr = 0x0D0D_0D0D_0D0D_0D0DUL;
	private const ulong Space = 0x2020_2020_2020_2020UL;
	private const ulong Tab = 0x0909_0909_0909_0909UL;

	private byte[] separator;
	private ulong first, special, comment, replacement;

	/// <inheritdoc/>
	public readonly SeparatorMasks64 Create(byte[] separator, int special, int comment, byte replacement) => new()
	{
		separator = separator,
		first = Portable.Repeat(separator[0]),
		special = Portable.Repeat((byte)special),
		comment = Portable.Repeat((byte)comment),
		replacement = Portable.Repeat(replacement),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Compute(ref byte p, int separatorLength, bool firsts, bool specials, bool lineBreaks, bool comments, bool blanks,
		out ulong firstM, out ulong sepM, out ulong spM, out ulong nlM, out ulong cmM, out ulong rpM, out ulong wsM)
	{
		ulong f = 0, s = 0, q = 0, l = 0, m = 0, r = 0, b = 0;
		ref var rest = ref MemoryMarshal.GetArrayDataReference(separator);
		for (var i = 0; i < 64; i += 8)
		{
			var w = Portable.LoadLittleEndian(ref Unsafe.Add(ref p, i));
			var match = Portable.MatchBytes(w, first);
			if (firsts) f |= Portable.MoveMask(match) << i;
			for (var k = 1; k < separatorLength && match != 0; k++)
				match &= Portable.MatchBytes(Portable.LoadLittleEndian(ref Unsafe.Add(ref p, i + k)), Portable.Repeat(Unsafe.Add(ref rest, k)));
			s |= Portable.MoveMask(match) << i;
			if (specials) q |= Portable.MoveMask(Portable.MatchBytes(w, special)) << i;
			if (lineBreaks) l |= Portable.MoveMask(Portable.MatchBytes(w, Lf) | Portable.MatchBytes(w, Cr)) << i;
			if (comments) m |= Portable.MoveMask(Portable.MatchBytes(w, comment)) << i;
			r |= Portable.MoveMask(Portable.MatchBytes(w, replacement)) << i;
			if (blanks) b |= Portable.MoveMask(Portable.MatchBytes(w, Space) | Portable.MatchBytes(w, Tab)) << i;
		}

		firstM = f;
		sepM = s;
		spM = q;
		nlM = l;
		cmM = m;
		rpM = r;
		wsM = b;
	}
}

#if NET
/// <summary>The masks from one 512-bit vector compare per character class (and per separator byte).</summary>
internal struct SeparatorMasks512 : ISeparatorMasks<SeparatorMasks512>
{
	private byte[] separator;
	private Vector512<byte> first, second, special, comment, replacement;

	/// <inheritdoc/>
	public readonly SeparatorMasks512 Create(byte[] separator, int special, int comment, byte replacement) => new()
	{
		separator = separator,
		first = Vector512.Create(separator[0]),
		second = Vector512.Create(separator.Length > 1 ? separator[1] : (byte)0),
		special = Vector512.Create((byte)special),
		comment = Vector512.Create((byte)comment),
		replacement = Vector512.Create(replacement),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Compute(ref byte p, int separatorLength, bool firsts, bool specials, bool lineBreaks, bool comments, bool blanks,
		out ulong firstM, out ulong sepM, out ulong spM, out ulong nlM, out ulong cmM, out ulong rpM, out ulong wsM)
	{
		// AVX-512 compares give bit masks, so the separator's bytes are combined as bits.
		var v = Vector512.LoadUnsafe(ref p);
		var s = Vector512.Equals(v, first).ExtractMostSignificantBits();
		firstM = firsts ? s : 0;
		if (separatorLength > 1)
		{
			s &= Vector512.Equals(Vector512.LoadUnsafe(ref p, 1), second).ExtractMostSignificantBits();
			ref var rest = ref MemoryMarshal.GetArrayDataReference(separator);
			for (var k = 2; k < separatorLength; k++)
				s &= Vector512.Equals(Vector512.LoadUnsafe(ref p, (nuint)k), Vector512.Create(Unsafe.Add(ref rest, k))).ExtractMostSignificantBits();
		}

		sepM = s;
		spM = specials ? Vector512.Equals(v, special).ExtractMostSignificantBits() : 0;
		nlM = lineBreaks ? (Vector512.Equals(v, Vector512.Create((byte)'\n')) | Vector512.Equals(v, Vector512.Create((byte)'\r'))).ExtractMostSignificantBits() : 0;
		cmM = comments ? Vector512.Equals(v, comment).ExtractMostSignificantBits() : 0;
		rpM = Vector512.Equals(v, replacement).ExtractMostSignificantBits();
		wsM = blanks ? (Vector512.Equals(v, Vector512.Create((byte)' ')) | Vector512.Equals(v, Vector512.Create((byte)'\t'))).ExtractMostSignificantBits() : 0;
	}
}

/// <summary>The masks from two 256-bit vector compares per character class (and per separator byte).</summary>
internal struct SeparatorMasks256 : ISeparatorMasks<SeparatorMasks256>
{
	private byte[] separator;
	private Vector256<byte> first, second, special, comment, replacement;

	/// <inheritdoc/>
	public readonly SeparatorMasks256 Create(byte[] separator, int special, int comment, byte replacement) => new()
	{
		separator = separator,
		first = Vector256.Create(separator[0]),
		second = Vector256.Create(separator.Length > 1 ? separator[1] : (byte)0),
		special = Vector256.Create((byte)special),
		comment = Vector256.Create((byte)comment),
		replacement = Vector256.Create(replacement),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Compute(ref byte p, int separatorLength, bool firsts, bool specials, bool lineBreaks, bool comments, bool blanks,
		out ulong firstM, out ulong sepM, out ulong spM, out ulong nlM, out ulong cmM, out ulong rpM, out ulong wsM)
	{
		var lo = Vector256.LoadUnsafe(ref p);
		var hi = Vector256.LoadUnsafe(ref p, 32);
		var sLo = Vector256.Equals(lo, first);
		var sHi = Vector256.Equals(hi, first);
		firstM = firsts ? Bits(sLo, sHi) : 0;
		if (separatorLength > 1)
		{
			sLo &= Vector256.Equals(Vector256.LoadUnsafe(ref p, 1), second);
			sHi &= Vector256.Equals(Vector256.LoadUnsafe(ref p, 33), second);
			ref var rest = ref MemoryMarshal.GetArrayDataReference(separator);
			for (var k = 2; k < separatorLength; k++)
			{
				var b = Vector256.Create(Unsafe.Add(ref rest, k));
				sLo &= Vector256.Equals(Vector256.LoadUnsafe(ref p, (nuint)k), b);
				sHi &= Vector256.Equals(Vector256.LoadUnsafe(ref p, (nuint)(32 + k)), b);
			}
		}

		sepM = Bits(sLo, sHi);
		spM = specials ? Bits(Vector256.Equals(lo, special), Vector256.Equals(hi, special)) : 0;
		if (lineBreaks)
		{
			var lf = Vector256.Create((byte)'\n');
			var cr = Vector256.Create((byte)'\r');
			nlM = Bits(Vector256.Equals(lo, lf) | Vector256.Equals(lo, cr), Vector256.Equals(hi, lf) | Vector256.Equals(hi, cr));
		}
		else
		{
			nlM = 0;
		}

		cmM = comments ? Bits(Vector256.Equals(lo, comment), Vector256.Equals(hi, comment)) : 0;
		rpM = Bits(Vector256.Equals(lo, replacement), Vector256.Equals(hi, replacement));
		if (blanks)
		{
			var space = Vector256.Create((byte)' ');
			var tab = Vector256.Create((byte)'\t');
			wsM = Bits(Vector256.Equals(lo, space) | Vector256.Equals(lo, tab), Vector256.Equals(hi, space) | Vector256.Equals(hi, tab));
		}
		else
		{
			wsM = 0;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Bits(Vector256<byte> lo, Vector256<byte> hi) =>
		lo.ExtractMostSignificantBits() | ((ulong)hi.ExtractMostSignificantBits() << 32);
}

/// <summary>The masks from four 128-bit vector compares per character class (and per separator byte).</summary>
internal struct SeparatorMasks128 : ISeparatorMasks<SeparatorMasks128>
{
	private byte[] separator;
	private Vector128<byte> first, second, special, comment, replacement;

	/// <inheritdoc/>
	public readonly SeparatorMasks128 Create(byte[] separator, int special, int comment, byte replacement) => new()
	{
		separator = separator,
		first = Vector128.Create(separator[0]),
		second = Vector128.Create(separator.Length > 1 ? separator[1] : (byte)0),
		special = Vector128.Create((byte)special),
		comment = Vector128.Create((byte)comment),
		replacement = Vector128.Create(replacement),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Compute(ref byte p, int separatorLength, bool firsts, bool specials, bool lineBreaks, bool comments, bool blanks,
		out ulong firstM, out ulong sepM, out ulong spM, out ulong nlM, out ulong cmM, out ulong rpM, out ulong wsM)
	{
		var a = Vector128.LoadUnsafe(ref p);
		var b = Vector128.LoadUnsafe(ref p, 16);
		var c = Vector128.LoadUnsafe(ref p, 32);
		var d = Vector128.LoadUnsafe(ref p, 48);
		var sa = Vector128.Equals(a, first);
		var sb = Vector128.Equals(b, first);
		var sc = Vector128.Equals(c, first);
		var sd = Vector128.Equals(d, first);
		firstM = firsts ? Bits(sa, sb, sc, sd) : 0;
		if (separatorLength > 1)
		{
			ref var rest = ref MemoryMarshal.GetArrayDataReference(separator);
			for (var k = 1; k < separatorLength; k++)
			{
				var v = k == 1 ? second : Vector128.Create(Unsafe.Add(ref rest, k));
				sa &= Vector128.Equals(Vector128.LoadUnsafe(ref p, (nuint)k), v);
				sb &= Vector128.Equals(Vector128.LoadUnsafe(ref p, (nuint)(16 + k)), v);
				sc &= Vector128.Equals(Vector128.LoadUnsafe(ref p, (nuint)(32 + k)), v);
				sd &= Vector128.Equals(Vector128.LoadUnsafe(ref p, (nuint)(48 + k)), v);
			}
		}

		sepM = Bits(sa, sb, sc, sd);
		spM = specials ? Bits(Vector128.Equals(a, special), Vector128.Equals(b, special), Vector128.Equals(c, special), Vector128.Equals(d, special)) : 0;
		nlM = lineBreaks ? Either(a, b, c, d, Vector128.Create((byte)'\n'), Vector128.Create((byte)'\r')) : 0;
		cmM = comments ? Bits(Vector128.Equals(a, comment), Vector128.Equals(b, comment), Vector128.Equals(c, comment), Vector128.Equals(d, comment)) : 0;
		rpM = Bits(Vector128.Equals(a, replacement), Vector128.Equals(b, replacement), Vector128.Equals(c, replacement), Vector128.Equals(d, replacement));
		wsM = blanks ? Either(a, b, c, d, Vector128.Create((byte)' '), Vector128.Create((byte)'\t')) : 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Either(Vector128<byte> a, Vector128<byte> b, Vector128<byte> c, Vector128<byte> d, Vector128<byte> x, Vector128<byte> y) =>
		Bits(Vector128.Equals(a, x) | Vector128.Equals(a, y), Vector128.Equals(b, x) | Vector128.Equals(b, y),
			Vector128.Equals(c, x) | Vector128.Equals(c, y), Vector128.Equals(d, x) | Vector128.Equals(d, y));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Bits(Vector128<byte> a, Vector128<byte> b, Vector128<byte> c, Vector128<byte> d) =>
		a.ExtractMostSignificantBits()
		| ((ulong)b.ExtractMostSignificantBits() << 16)
		| ((ulong)c.ExtractMostSignificantBits() << 32)
		| ((ulong)d.ExtractMostSignificantBits() << 48);
}
#endif

/// <summary>Packs the kept bytes of a 64 byte block, for <see cref="CsvSeparatorStream"/>.</summary>
/// <typeparam name="TSelf">The implementing struct, so that calls through the constraint are not virtual.</typeparam>
internal interface ICompactor<TSelf> where TSelf : struct, ICompactor<TSelf>
{
	/// <summary>Creates the compactor for a replacement byte. Called on <c>default(TSelf)</c>.</summary>
	/// <param name="replacement">The byte to write in place of the first byte of each separator.</param>
	/// <returns>The compactor, ready for <see cref="Compact"/>.</returns>
	public TSelf Create(byte replacement);

	/// <summary>
	/// Writes the bytes of the 64 at <paramref name="source"/> whose bits are set in <paramref name="keep"/>, in order, to
	/// <paramref name="destination"/>, with the replacement in place of the bytes of <paramref name="replace"/>. It may write
	/// up to 64 bytes, so <paramref name="destination"/> may equal or precede <paramref name="source"/> but must have room
	/// for 64.
	/// </summary>
	/// <param name="source">The block.</param>
	/// <param name="destination">Where the kept bytes go.</param>
	/// <param name="replace">The bytes to overwrite with the replacement; they are kept.</param>
	/// <param name="keep">The bytes to keep.</param>
	/// <returns>The number of kept bytes.</returns>
	public int Compact(ref byte source, ref byte destination, ulong replace, ulong keep);
}

/// <summary>Packs each 8 bytes with shifts: for the targets without SIMD.</summary>
internal struct ScalarCompactor : ICompactor<ScalarCompactor>
{
	private ulong replacement;

	/// <inheritdoc/>
	public readonly ScalarCompactor Create(byte replacement) => new() { replacement = Portable.Repeat(replacement) };

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int Compact(ref byte source, ref byte destination, ulong replace, ulong keep)
	{
		ref var expand = ref MemoryMarshal.GetArrayDataReference(SeparatorTables.Expand);
		var written = 0;
		for (var g = 0; g < 64; g += 8)
		{
			var word = Portable.LoadLittleEndian(ref Unsafe.Add(ref source, g));
			var m = Unsafe.Add(ref expand, (nint)((replace >> g) & 0xFF));
			word = (word & ~m) | (replacement & m);
			var kept = (uint)(keep >> g) & 0xFF;
			var drop = ~kept & 0xFF;
			while (drop != 0)
			{
				// Drop the highest byte first, so that the positions of the lower ones stay.
				var j = 31 - BitOperations.LeadingZeroCount(drop);
				var low = (1UL << (8 * j)) - 1;
				word = (word & low) | ((word >> 8) & ~low);
				drop ^= 1u << j;
			}

			Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, written), BitConverter.IsLittleEndian ? word : BinaryPrimitives.ReverseEndianness(word));
			written += BitOperations.PopCount(kept);
		}

		return written;
	}
}

#if NET
/// <summary>Packs each 8 bytes with one byte shuffle from a table (SSSE3 PSHUFB, AdvSimd TBL).</summary>
internal struct ShuffleCompactor : ICompactor<ShuffleCompactor>
{
	private ulong replacement;

	/// <inheritdoc/>
	public readonly ShuffleCompactor Create(byte replacement) => new() { replacement = 0x0101_0101_0101_0101UL * replacement };

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int Compact(ref byte source, ref byte destination, ulong replace, ulong keep)
	{
		ref var expand = ref MemoryMarshal.GetArrayDataReference(SeparatorTables.Expand);
		ref var shuffle = ref MemoryMarshal.GetArrayDataReference(SeparatorTables.Shuffle);
		var written = 0;
		for (var g = 0; g < 64; g += 8)
		{
			var word = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, g));
			var m = Unsafe.Add(ref expand, (nint)((replace >> g) & 0xFF));
			word = (word & ~m) | (replacement & m);
			var k = (nint)((keep >> g) & 0xFF);
			var packed = Vector128.ShuffleNative(Vector128.CreateScalarUnsafe(word).AsByte(), Vector128.CreateScalarUnsafe(Unsafe.Add(ref shuffle, k)).AsByte());
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, written), packed.AsUInt64().ToScalar());
			written += BitOperations.PopCount((uint)k);
		}

		return written;
	}
}

/// <summary>
/// Packs each 8 bytes with one byte shuffle that also writes the replacement, for separators of two bytes (SSSE3 PSHUFB,
/// AdvSimd TBL). The second byte of a separator is the only byte dropped, so a kept byte followed by a dropped one is the
/// first byte of a separator: the 8 kept bits of a group and the one after them choose the shuffle from a table of 512.
/// </summary>
/// <remarks>
/// <see cref="ICompactor{TSelf}.Compact"/> needs the bits of <c>keep</c> after a block's valid bytes set, unless a
/// separator drops them.
/// </remarks>
internal struct PairShuffleCompactor : ICompactor<PairShuffleCompactor>
{
	// The replacement in bytes 8 to 15, where the shuffles pick it.
	private Vector128<byte> replacement;

	/// <inheritdoc/>
	public readonly PairShuffleCompactor Create(byte replacement) => new()
	{
		replacement = Vector128.Create(0, 0x0101_0101_0101_0101UL * replacement).AsByte(),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int Compact(ref byte source, ref byte destination, ulong replace, ulong keep)
	{
		ref var shuffle = ref MemoryMarshal.GetArrayDataReference(SeparatorTables.PairShuffle);
		var written = 0;
		for (var g = 0; g < 56; g += 8) written = Group(ref source, ref destination, ref shuffle, g, (keep >> g) & 0x1FF, written);

		// The byte after the block is dropped when a separator starts at its last byte.
		return Group(ref source, ref destination, ref shuffle, 56, (keep >> 56) | ((~replace >> 63) << 8), written);
	}

	/// <summary>Packs the 8 bytes from <paramref name="g"/>, writing them after the <paramref name="written"/> bytes of output.</summary>
	/// <param name="source">The block.</param>
	/// <param name="destination">Where the kept bytes go.</param>
	/// <param name="shuffle">The first entry of <see cref="SeparatorTables.PairShuffle"/>.</param>
	/// <param name="g">The first byte of the group in the block.</param>
	/// <param name="index">The 8 kept bits of the group, then whether the byte after it is kept.</param>
	/// <param name="written">The number of bytes of output so far.</param>
	/// <returns>The number of bytes of output after the group.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private readonly int Group(ref byte source, ref byte destination, ref ulong shuffle, int g, ulong index, int written)
	{
		var bytes = Vector128.CreateScalar(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, g))).AsByte() | replacement;
		var packed = Vector128.ShuffleNative(bytes, Vector128.CreateScalarUnsafe(Unsafe.Add(ref shuffle, (nint)index)).AsByte());
		Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, written), packed.AsUInt64().ToScalar());

		return written + BitOperations.PopCount((uint)index & 0xFF);
	}
}
#endif

#if NET10_0_OR_GREATER
/// <summary>Packs a block with one AVX-512 VBMI2 compress.</summary>
internal struct CompressCompactor : ICompactor<CompressCompactor>
{
	private Vector512<byte> replacement, spreadIndex, bitOfByte;

	/// <inheritdoc/>
	public readonly CompressCompactor Create(byte replacement) => new()
	{
		replacement = Vector512.Create(replacement),
		spreadIndex = VectorMask.SpreadIndex,
		bitOfByte = VectorMask.BitOfByte,
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int Compact(ref byte source, ref byte destination, ulong replace, ulong keep)
	{
		var v = Vector512.ConditionalSelect(VectorMask.FromBits(replace, spreadIndex, bitOfByte), replacement, Vector512.LoadUnsafe(ref source));
		Avx512Vbmi2.Compress(Vector512<byte>.Zero, VectorMask.FromBits(keep, spreadIndex, bitOfByte), v).StoreUnsafe(ref destination);

		return BitOperations.PopCount(keep);
	}
}
#endif

/// <summary>Tables of the compactors.</summary>
internal static class SeparatorTables
{
	/// <summary>For each 8-bit mask, a <see cref="ulong"/> with all bits set in the bytes whose bit is set.</summary>
	public static readonly ulong[] Expand = BuildExpand();

	/// <summary>
	/// For each 8-bit mask of kept bytes, the byte shuffle (as 8 indexes in a <see cref="ulong"/>) that moves the kept bytes
	/// of 8 to the front, in order.
	/// </summary>
	public static readonly ulong[] Shuffle = BuildShuffle();

#if NET
	/// <summary>
	/// For each 8-bit mask of kept bytes and bit 8 set when the byte after them is kept, the byte shuffle of
	/// <see cref="PairShuffleCompactor"/>: like <see cref="Shuffle"/>, but a kept byte followed by a dropped one comes from
	/// byte 8, the replacement.
	/// </summary>
	public static readonly ulong[] PairShuffle = BuildPairShuffle();

	private static ulong[] BuildPairShuffle()
	{
		var table = new ulong[512];
		for (var mask = 0; mask < 512; mask++)
		{
			var indexes = 0x8080_8080_8080_8080UL;
			var k = 0;
			for (var j = 0; j < 8; j++)
			{
				if ((mask & (1 << j)) == 0) continue;
				var from = (mask & (2 << j)) == 0 ? 8 : j;
				indexes = (indexes & ~(0xFFUL << (8 * k))) | ((ulong)from << (8 * k));
				k++;
			}

			table[mask] = indexes;
		}

		return table;
	}
#endif

	private static ulong[] BuildExpand()
	{
		var table = new ulong[256];
		for (var mask = 0; mask < 256; mask++)
		{
			for (var j = 0; j < 8; j++)
			{
				if ((mask & (1 << j)) != 0) table[mask] |= 0xFFUL << (8 * j);
			}
		}

		return table;
	}

	private static ulong[] BuildShuffle()
	{
		var table = new ulong[256];
		for (var mask = 0; mask < 256; mask++)
		{
			var indexes = 0x8080_8080_8080_8080UL;
			var k = 0;
			for (var j = 0; j < 8; j++)
			{
				if ((mask & (1 << j)) == 0) continue;
				indexes = (indexes & ~(0xFFUL << (8 * k))) | ((ulong)j << (8 * k));
				k++;
			}

			table[mask] = indexes;
		}

		return table;
	}
}
