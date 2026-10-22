using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using ArmAes = System.Runtime.Intrinsics.Arm.Aes;
#endif

namespace CsvHelper;

// The scanner turns the input into an index:
//
//   ends[k]    = (position of field terminator k) + 1, i.e. where the following field starts. Every
//                 separator, CR and LF outside quotes is a terminator. The high bit marks a field that
//                 needs decoding: it started with a quote (or trimming is on).
//   rowEnds[r] = index in ends of the LF or CR that ends record r. The high bit marks a CRLF record,
//                 whose last real field ends at the CR entry just before the LF entry.
//
// It works on 64 byte blocks. For each block it builds 64 bit masks of separators, quotes, CR and LF.
// Quoted regions come from a prefix XOR of the quote mask (one carry-less multiply), which removes
// separators and line breaks inside quotes without branching. That parity rule matches RFC 4180
// for well formed data. It stops matching only when a quote opens somewhere other than at the
// start of a field (for example 5'10" or ab"c). Such quotes are detected with a few mask operations,
// and the block is then handed to an exact scalar state machine that treats them as text. The scalar
// path also handles comments, strict validation failures and the final partial block, and it
// produces exactly the same index as the SIMD path.
//
// The terminators of a block become entries of ends from their trailing zero counts (BitFlattener), or, on the 512-bit
// scanner with AVX-512 VBMI2, from one compress of their positions per block (CompressFlattener), which is faster at any
// field density.
//
// In Escape mode the quote mask holds the escape characters instead. The bytes they escape (the byte after
// an odd run of escapes, and an LF after an escaped CR) are found branch-free and removed from the
// terminators, and every field that contains an escape is flagged for decoding. FixedWidth mode has its own
// line scanner (CsvReader.FixedWidth.cs).
public sealed partial class CsvReader
{
	private const int StateFieldStart = 0;
	private const int StateUnquoted = 1;
	private const int StateQuoted = 2;
	private const int StateQuoteInQuoted = 3;
	private const int StateAfterQuoted = 4;
	private const int StateComment = 5;
	private const int StateEscaped = 6;     // the next byte is escaped
	private const int StateEscapedCr = 7;   // after an escaped CR: an LF is escaped too

	/// <summary>
	/// A compile-time flag: the JIT compiles a copy of the scanner for each combination and folds
	/// <c>default(T).Value</c> to a constant (struct type arguments get their own code).
	/// </summary>
	internal interface IFlag
	{
		/// <summary>Whether the feature the flag stands for is on.</summary>
		public bool Value { get; }
	}

	/// <summary>A flag that is on.</summary>
	internal readonly struct On : IFlag
	{
		/// <summary>Always <see langword="true"/>.</summary>
		public bool Value => true;
	}

	/// <summary>A flag that is off.</summary>
	internal readonly struct Off : IFlag
	{
		/// <summary>Always <see langword="false"/>.</summary>
		public bool Value => false;
	}

	/// <summary>
	/// Scans whole blocks from <paramref name="pos"/> up to <paramref name="limit"/>. Returns the position
	/// of the first block that was not processed: either <paramref name="limit"/>, or a block that needs
	/// the scalar path.
	/// </summary>
	private int ScanSimd(int pos, int limit)
	{
		if (quotes)
			return strict ? ScanQ<On, On, Off>(pos, limit) : ScanQ<On, Off, Off>(pos, limit);

		return escapes ? ScanQ<Off, Off, On>(pos, limit) : ScanQ<Off, Off, Off>(pos, limit);
	}

	private int ScanQ<TQ, TS, TE>(int pos, int limit) where TQ : struct, IFlag where TS : struct, IFlag where TE : struct, IFlag
	{
		// Line breaks in quotes only matter with quotes: without them, one instantiation does.
		var breaks = lineBreaksInQuotes || !default(TQ).Value;
		if (comments)
			return breaks ? ScanW<TQ, TS, TE, On, On>(pos, limit) : ScanW<TQ, TS, TE, On, Off>(pos, limit);

		return breaks ? ScanW<TQ, TS, TE, Off, On>(pos, limit) : ScanW<TQ, TS, TE, Off, Off>(pos, limit);
	}

	private int ScanW<TQ, TS, TE, TC, TN>(int pos, int limit)
		where TQ : struct, IFlag where TS : struct, IFlag where TE : struct, IFlag where TC : struct, IFlag where TN : struct, IFlag
		=> vectorWidth switch
		{
#if NET
			256 => Scan<TQ, TS, TE, TC, TN, Masks256, BitFlattener>(pos, limit),
			512 => compressIndex
				? Scan<TQ, TS, TE, TC, TN, Masks512, CompressFlattener>(pos, limit)
				: Scan<TQ, TS, TE, TC, TN, Masks512, BitFlattener>(pos, limit),
			128 => Scan<TQ, TS, TE, TC, TN, Masks128, BitFlattener>(pos, limit),
#endif
			64 => Scan<TQ, TS, TE, TC, TN, Masks64, BitFlattener>(pos, limit),
			_ => pos,
		};

	/// <typeparam name="TQ">Quotes (Rfc4180 mode).</typeparam>
	/// <typeparam name="TS">Malformed quote detection.</typeparam>
	/// <typeparam name="TE">Escape characters (Escape mode).</typeparam>
	/// <typeparam name="TC">Comments.</typeparam>
	/// <typeparam name="TN">Line breaks allowed in quotes.</typeparam>
	/// <typeparam name="TM">The vector width.</typeparam>
	/// <typeparam name="TF">How the terminators of a block become entries of the index.</typeparam>
#if NET
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	private int Scan<TQ, TS, TE, TC, TN, TM, TF>(int pos, int limit)
		where TQ : struct, IFlag where TS : struct, IFlag where TE : struct, IFlag where TC : struct, IFlag where TN : struct, IFlag
		where TM : struct, IBlockMasks<TM> where TF : struct, IFlattener<TF>
	{
		ref var buf = ref MemoryMarshal.GetArrayDataReference(buffer);
		ref var ends = ref MemoryMarshal.GetArrayDataReference(this.ends);
		ref var rowEnds = ref MemoryMarshal.GetArrayDataReference(this.rowEnds);
		var endsCount = this.endsCount;
		var rowCount = this.rowCount;
		var inQuote = carry.InQuote;
		var prevDelim = carry.PrevDelim;
		var prevCloseQ = carry.PrevCloseQ;
		var prevCr = carry.PrevCr;
		var prevRowStart = carry.PrevRowStart;
		var fieldQuoted = carry.FieldQuoted;
		var prevEscape = default(TE).Value ? carry.PrevEscape : 0;
		var prevEscapedCr = default(TE).Value ? carry.PrevEscapedCr : 0;
		var flagAll = trim ? int.MinValue : 0;
		// In Escape mode quoteByte is the escape character, so the quote mask holds the escape characters.
		var masks = default(TM).Create(sepByte, quoteByte, commentByte);
		var flattener = default(TF).Create();

		for (; pos < limit; pos += BlockSize)
		{
			masks.Compute(ref Unsafe.Add(ref buf, (nint)(uint)pos), default(TQ).Value | default(TE).Value, default(TC).Value,
				out var sepM, out var lfM, out var crM, out var qM, out var cmM);

			var nextInQuote = 0UL;
			var nextCloseQ = 0UL;
			var quoteStarts = 0UL;
			if (default(TQ).Value)
			{
				if ((qM | inQuote) != 0)
				{
					// Bit i of quoteMask is set when byte i is inside quotes (opening quote included).
					var quoteMask = PrefixXor(qM) ^ inQuote;
					if (!default(TN).Value && ((lfM | crM) & quoteMask) != 0) break;
					var outside = ~quoteMask;
					sepM &= outside;
					lfM &= outside;
					crM &= outside;
					var delims = sepM | lfM | crM;
					var closeQ = qM & outside;
					var fieldStarts = (delims << 1) | prevDelim;
					// An opening quote must start a field or follow a closing quote ("" escapes).
					if ((qM & quoteMask & ~(fieldStarts | (closeQ << 1) | prevCloseQ)) != 0) break;
					if (default(TS).Value)
					{
						// A closing quote must be followed by a terminator or another quote.
						var next = delims | qM;
						if ((closeQ & ~((next >> 1) | 0x8000_0000_0000_0000UL)) != 0) break;
						if ((prevCloseQ & ~next) != 0) break;
					}

					quoteStarts = qM & fieldStarts;
					nextInQuote = (ulong)((long)quoteMask >> 63);
					nextCloseQ = closeQ >> 63;
				}
				else if (default(TS).Value && (prevCloseQ & ~(sepM | lfM | crM)) != 0)
				{
					break;
				}
			}

			var nextPrevEscape = 0UL;
			var nextPrevEscapedCr = 0UL;
			if (default(TE).Value && (qM | prevEscape | prevEscapedCr) != 0)
			{
				// Bit i of escaped is set when byte i follows an odd run of escape characters (simdjson's branch-free
				// escape scanner): subtracting the run starts from the odd bits carries through each run.
				const ulong OddBits = 0xAAAA_AAAA_AAAA_AAAAUL;
				var potential = qM & ~prevEscape;
				var escapeAndTerminal = (((potential << 1) | OddBits) - potential) ^ OddBits;
				var escaped = escapeAndTerminal ^ (qM | prevEscape);
				nextPrevEscape = (escapeAndTerminal & qM) >> 63;
				// An escaped CR takes the LF after it along: an escape before CR LF escapes the line break.
				var escapedCr = escaped & crM;
				escaped |= lfM & ((escapedCr << 1) | prevEscapedCr);
				nextPrevEscapedCr = escapedCr >> 63;
				var text = ~escaped;
				sepM &= text;
				lfM &= text;
				crM &= text;
				// Fields with an escape character need decoding.
				quoteStarts = qM;
			}

			// LF always ends a record. CR ends one unless an LF follows; a CR in bit 63 is decided by the
			// next block (pendingCr).
			var lfAfterCr = lfM & ((crM << 1) | prevCr);
			var loneCr = crM & ~(lfM >> 1) & 0x7FFF_FFFF_FFFF_FFFFUL;
			var pendingCr = prevCr & ~lfM;
			if (default(TC).Value)
			{
				var starts = ((lfM | loneCr) << 1) | prevRowStart | pendingCr;
				if ((cmM & starts) != 0) break;
				prevRowStart = lfM >> 63;
			}

			var termM = sepM | lfM | crM;

			// Mark the terminator of every field that starts with a quote: adding the start bits to the
			// non-terminator mask carries each one up to the next terminator (and across blocks). In Escape mode the
			// start bits are every escape character; several in one field still mark just its terminator, as the
			// sum is masked with the terminators.
			var decodeM = 0UL;
			var nextFieldQuoted = 0UL;
			if ((default(TQ).Value || default(TE).Value) && (quoteStarts | fieldQuoted) != 0)
			{
				var t = ~termM;
				var s1 = t + fieldQuoted;
				var sum = s1 + quoteStarts;
				decodeM = sum & termM;
				nextFieldQuoted = (s1 < t ? 1UL : 0) | (sum < s1 ? 1UL : 0);
			}

			inQuote = nextInQuote;
			prevCloseQ = nextCloseQ;
			fieldQuoted = nextFieldQuoted;
			prevDelim = termM >> 63;
			prevCr = crM >> 63;
			if (default(TE).Value)
			{
				prevEscape = nextPrevEscape;
				prevEscapedCr = nextPrevEscapedCr;
			}

			var ec = endsCount;
			if (pendingCr != 0)
			{
				// The CR that ended the previous block was not followed by LF, so it ended a record.
				Unsafe.Add(ref rowEnds, (nint)(uint)rowCount) = ec - 1;
				rowCount++;
			}

			if (termM == 0) continue;

			var rowM = lfM | loneCr;
			if (rowM != 0)
			{
				var rc = rowCount;
				do
				{
					var low = rowM & (0 - rowM);
					var index = ec + BitOperations.PopCount(termM & (low - 1));
					Unsafe.Add(ref rowEnds, (nint)(uint)rc) = index | (BitOperations.PopCount(lfAfterCr & low) << 31);
					rc++;
					rowM ^= low;
				} while (rowM != 0);
				rowCount = rc;
			}

			var cnt = BitOperations.PopCount(termM);
			flattener.Write(ref Unsafe.Add(ref ends, (nint)(uint)ec), termM, decodeM, cnt, pos + 1, flagAll);
			endsCount = ec + cnt;
		}

		this.endsCount = endsCount;
		this.rowCount = rowCount;
		carry.InQuote = inQuote;
		carry.PrevDelim = prevDelim;
		carry.PrevCloseQ = prevCloseQ;
		carry.PrevCr = prevCr;
		carry.PrevRowStart = prevRowStart;
		carry.FieldQuoted = fieldQuoted;
		if (default(TE).Value)
		{
			carry.PrevEscape = prevEscape;
			carry.PrevEscapedCr = prevEscapedCr;
		}

		return pos;
	}

	/// <summary>Prefix XOR, with one carry-less multiply where the hardware has it: bit i = XOR of bits 0..i.</summary>
	/// <param name="x">The bits.</param>
	/// <returns>Bit i set when an odd number of bits 0..i of <paramref name="x"/> are set.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ulong PrefixXor(ulong x)
	{
#if NET
		if (Pclmulqdq.IsSupported)
			return Pclmulqdq.CarrylessMultiply(Vector128.CreateScalarUnsafe(x), Vector128<ulong>.AllBitsSet, 0).ToScalar();
		if (ArmAes.IsSupported)
			return ArmAes.PolynomialMultiplyWideningLower(Vector64.CreateScalar(x), Vector64<ulong>.AllBitsSet).ToScalar();
#endif

		return PrefixXorPortable(x);
	}

	/// <summary>Prefix XOR without carry-less multiply: bit i = XOR of bits 0..i.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ulong PrefixXorPortable(ulong x)
	{
		x ^= x << 1;
		x ^= x << 2;
		x ^= x << 4;
		x ^= x << 8;
		x ^= x << 16;
		x ^= x << 32;

		return x;
	}

	internal static ulong PrefixXorForTest(ulong x) => PrefixXor(x);

	/// <summary>
	/// Exact byte-at-a-time state machine. It consumes and produces the same carried state as the SIMD
	/// path, so the two can be interleaved freely at block boundaries.
	/// </summary>
	private void ScalarBlock(int pos, int length)
	{
		ref var buf = ref MemoryMarshal.GetArrayDataReference(buffer);
		ref var ends = ref MemoryMarshal.GetArrayDataReference(this.ends);
		ref var rowEnds = ref MemoryMarshal.GetArrayDataReference(this.rowEnds);
		var endsCount = this.endsCount;
		var rowCount = this.rowCount;
		int sep = sepByte;
		var quote = quoteInt;
		var escape = escapeInt;
		var comment = commentInt;
		var trim = this.trim;
		var whitespace = this.whitespace;
		var flagAll = trim ? int.MinValue : 0;
		var lineBreaksInQuotes = this.lineBreaksInQuotes;
		var strict = this.strict;
		var carry = this.carry;
		var state = forceScalar ? StateComment
			: carry.InQuote != 0 ? StateQuoted
			: carry.PrevCloseQ != 0 ? StateQuoteInQuoted
			: carry.PrevEscape != 0 ? StateEscaped
			: carry.PrevEscapedCr != 0 ? StateEscapedCr
			: carry.PrevDelim != 0 ? StateFieldStart
			: StateUnquoted;
		// Without quotes, whitespace before a field changes nothing (trimming flags every field).
		if (state == StateUnquoted && trim && quote >= 0) state = ResolveTrimmedState(pos);
		// A CR at the end of the previous block (SIMD path) also makes the next byte a record start.
		var rowStart = carry.PrevRowStart != 0 || carry.PrevCr != 0;
		var prevCr = carry.PrevCr != 0;
		var fieldFlag = carry.FieldQuoted != 0 ? int.MinValue : 0;
		var end = pos + length;

		for (var i = pos; i < end; i++)
		{
			int c = Unsafe.Add(ref buf, (nint)(uint)i);
			var afterCr = prevCr;
			prevCr = false;
			if (afterCr && c != '\n')
			{
				// The previous CR was not followed by LF, so it ended a record.
				Unsafe.Add(ref rowEnds, (nint)(uint)rowCount) = endsCount - 1;
				rowCount++;
			}

			var atRowStart = rowStart;
			rowStart = false;

			if (c == '\n' || c == '\r')
			{
				if (state == StateQuoted)
				{
					if (lineBreaksInQuotes) continue;
					if (strict) RecordMalformed(i, CsvMalformedReason.LineBreakInQuotes);
				}
				else if (state == StateEscaped)
				{
					// An escaped line break is text, and so is an LF right after an escaped CR.
					state = c == '\r' ? StateEscapedCr : StateUnquoted;
					continue;
				}
				else if (state == StateEscapedCr && c == '\n')
				{
					state = StateUnquoted;
					continue;
				}

				Unsafe.Add(ref ends, (nint)(uint)endsCount) = (i + 1) | fieldFlag | flagAll;
				endsCount++;
				fieldFlag = 0;
				if (c == '\n')
				{
					Unsafe.Add(ref rowEnds, (nint)(uint)rowCount) = (endsCount - 1) | (afterCr ? int.MinValue : 0);
					rowCount++;
				}
				else
				{
					prevCr = true;
				}

				state = StateFieldStart;
				rowStart = true;
				continue;
			}

			switch (state)
			{
				case StateFieldStart:
					if (c == sep)
					{
						Unsafe.Add(ref ends, (nint)(uint)endsCount) = (i + 1) | flagAll;
						endsCount++;
					}
					else if (c == quote)
					{
						state = StateQuoted;
						fieldFlag = int.MinValue;
					}
					else if (atRowStart && c == comment) state = StateComment;
					else if (c == escape)
					{
						state = StateEscaped;
						fieldFlag = int.MinValue;
					}
					else if (!(trim && whitespace[c])) state = StateUnquoted;
					break;

				case StateEscapedCr:
					// Not an LF: the escaped CR was all.
					state = StateUnquoted;
					goto case StateUnquoted;

				case StateUnquoted:
					if (c == sep)
					{
						Unsafe.Add(ref ends, (nint)(uint)endsCount) = (i + 1) | fieldFlag | flagAll;
						endsCount++;
						fieldFlag = 0;
						state = StateFieldStart;
					}
					else if (c == quote && strict) RecordMalformed(i, CsvMalformedReason.StrayQuote);
					else if (c == escape)
					{
						state = StateEscaped;
						fieldFlag = int.MinValue;
					}

					break;

				case StateEscaped:
					state = StateUnquoted;
					break;

				case StateQuoted:
					if (c == quote) state = StateQuoteInQuoted;
					break;

				case StateQuoteInQuoted:
					if (c == quote) state = StateQuoted;
					else if (c == sep)
					{
						Unsafe.Add(ref ends, (nint)(uint)endsCount) = (i + 1) | fieldFlag | flagAll;
						endsCount++;
						fieldFlag = 0;
						state = StateFieldStart;
					}
					else if (trim && whitespace[c]) state = StateAfterQuoted;
					else
					{
						if (strict) RecordMalformed(i, CsvMalformedReason.TextAfterClosingQuote);
						state = StateUnquoted;
					}

					break;

				case StateAfterQuoted:
					if (c == sep)
					{
						Unsafe.Add(ref ends, (nint)(uint)endsCount) = (i + 1) | fieldFlag | flagAll;
						endsCount++;
						fieldFlag = 0;
						state = StateFieldStart;
					}
					else if (!whitespace[c])
					{
						if (strict) RecordMalformed(i, CsvMalformedReason.TextAfterClosingQuote);
						state = StateUnquoted;
					}

					break;

					// StateComment: everything up to the line break belongs to the comment.
			}
		}

		this.endsCount = endsCount;
		this.rowCount = rowCount;
		carry.InQuote = state == StateQuoted ? ulong.MaxValue : 0;
		carry.PrevCloseQ = state == StateQuoteInQuoted ? 1UL : 0;
		carry.PrevDelim = state == StateFieldStart ? 1UL : 0;
		carry.PrevEscape = state == StateEscaped ? 1UL : 0;
		carry.PrevEscapedCr = state == StateEscapedCr ? 1UL : 0;
		carry.PrevCr = prevCr ? 1UL : 0;
		carry.PrevRowStart = rowStart ? 1UL : 0;
		carry.FieldQuoted = fieldFlag != 0 ? 1UL : 0;
		this.carry = carry;
		forceScalar = state == StateComment;
		afterClosingQuote = strict && state == StateAfterQuoted;
	}

	/// <summary>
	/// With trimming, a field can start with whitespace before its opening quote. The SIMD carry only
	/// knows the previous byte, so look back over whitespace to find the real state.
	/// </summary>
	private int ResolveTrimmedState(int pos)
	{
		for (var i = pos - 1; i >= dataStart; i--)
		{
			int c = buffer[i];
			if (c == sepByte || c == '\r' || c == '\n') return StateFieldStart;
			if (whitespace[c]) continue;
			if (c == quoteInt) return StateAfterQuoted;

			return StateUnquoted;
		}

		return StateFieldStart;
	}
}

/// <summary>Scanner state carried from one 64 byte block to the next.</summary>
internal struct ScanCarry
{
	/// <summary>All ones when the next byte is inside a quoted field.</summary>
	public ulong InQuote;
	/// <summary>1 when the previous byte ended a field (or at the start of the input).</summary>
	public ulong PrevDelim;
	/// <summary>1 when the previous byte was a quote that closed a quoted field.</summary>
	public ulong PrevCloseQ;
	/// <summary>1 when the previous byte was a CR outside quotes (it ends a record unless an LF follows).</summary>
	public ulong PrevCr;
	/// <summary>1 when the next byte starts a record (only tracked when comments are enabled).</summary>
	public ulong PrevRowStart;
	/// <summary>1 when the field being scanned started with a quote, or contains an escape character.</summary>
	public ulong FieldQuoted;
	/// <summary>1 when the next byte is escaped (Escape mode).</summary>
	public ulong PrevEscape;
	/// <summary>1 when the previous byte was an escaped CR, so an LF next is escaped too (Escape mode).</summary>
	public ulong PrevEscapedCr;

	/// <summary>The state at the start of the input: a field and a record start at the first byte.</summary>
	public static ScanCarry Initial => new() { PrevDelim = 1, PrevRowStart = 1 };
}

/// <summary>Computes the 64 bit character class masks of one block with a given vector width.</summary>
/// <typeparam name="TSelf">The implementing struct, so that calls through the constraint are not virtual.</typeparam>
internal interface IBlockMasks<TSelf> where TSelf : struct, IBlockMasks<TSelf>
{
	/// <summary>
	/// Creates the masks for the given bytes, broadcast to the vector width once per scan. Called on
	/// <c>default(TSelf)</c>.
	/// </summary>
	/// <param name="separator">The separator byte.</param>
	/// <param name="quote">The quote byte, or the escape byte in Escape mode.</param>
	/// <param name="comment">The comment prefix byte.</param>
	/// <returns>The masks, ready for <see cref="Compute"/>.</returns>
	public TSelf Create(byte separator, byte quote, byte comment);

	/// <summary>
	/// Computes the masks of the 64 bytes at <paramref name="p"/>: bit <c>i</c> of each mask is set when byte
	/// <c>i</c> of the block is that character.
	/// </summary>
	/// <param name="p">The first byte of the block; 64 bytes from it must be readable.</param>
	/// <param name="quotes">Whether to compute <paramref name="qM"/>; it is 0 otherwise.</param>
	/// <param name="comments">Whether to compute <paramref name="cmM"/>; it is 0 otherwise.</param>
	/// <param name="sepM">The separator bytes.</param>
	/// <param name="lfM">The LF bytes.</param>
	/// <param name="crM">The CR bytes.</param>
	/// <param name="qM">The quote bytes (the escape bytes in Escape mode).</param>
	/// <param name="cmM">The comment prefix bytes.</param>
	public void Compute(ref byte p, bool quotes, bool comments,
		out ulong sepM, out ulong lfM, out ulong crM, out ulong qM, out ulong cmM);
}

/// <summary>
/// The masks from 64-bit integer operations, 8 bytes at a time (see <see cref="Portable.MatchBytes"/>): the scanner of
/// the targets without SIMD, and one more width for the tests on .NET.
/// </summary>
internal struct Masks64 : IBlockMasks<Masks64>
{
	private ulong sepPattern, quotePattern, commentPattern;

	/// <inheritdoc/>
	public readonly Masks64 Create(byte separator, byte quote, byte comment) => new()
	{
		sepPattern = Portable.Repeat(separator),
		quotePattern = Portable.Repeat(quote),
		commentPattern = Portable.Repeat(comment),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Compute(ref byte p, bool quotes, bool comments,
		out ulong sepM, out ulong lfM, out ulong crM, out ulong qM, out ulong cmM)
	{
		const ulong Lf = 0x0A0A_0A0A_0A0A_0A0AUL;
		const ulong Cr = 0x0D0D_0D0D_0D0D_0D0DUL;
		ulong s = 0, l = 0, c = 0, q = 0, m = 0;
		for (var i = 0; i < 64; i += 8)
		{
			var w = Portable.LoadLittleEndian(ref Unsafe.Add(ref p, i));
			s |= Portable.MoveMask(Portable.MatchBytes(w, sepPattern)) << i;
			l |= Portable.MoveMask(Portable.MatchBytes(w, Lf)) << i;
			c |= Portable.MoveMask(Portable.MatchBytes(w, Cr)) << i;
			if (quotes) q |= Portable.MoveMask(Portable.MatchBytes(w, quotePattern)) << i;
			if (comments) m |= Portable.MoveMask(Portable.MatchBytes(w, commentPattern)) << i;
		}

		sepM = s;
		lfM = l;
		crM = c;
		qM = q;
		cmM = m;
	}
}

#if NET
/// <summary>The masks from two 256-bit vector compares per character class.</summary>
internal struct Masks256 : IBlockMasks<Masks256>
{
	private Vector256<byte> sep, quote, comment;

	/// <inheritdoc/>
	public readonly Masks256 Create(byte separator, byte quote, byte comment) => new()
	{
		sep = Vector256.Create(separator),
		quote = Vector256.Create(quote),
		comment = Vector256.Create(comment),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Compute(ref byte p, bool quotes, bool comments,
		out ulong sepM, out ulong lfM, out ulong crM, out ulong qM, out ulong cmM)
	{
		var lo = Vector256.LoadUnsafe(ref p);
		var hi = Vector256.LoadUnsafe(ref p, 32);
		sepM = Bits(Vector256.Equals(lo, sep), Vector256.Equals(hi, sep));
		lfM = Bits(Vector256.Equals(lo, Vector256.Create((byte)'\n')), Vector256.Equals(hi, Vector256.Create((byte)'\n')));
		crM = Bits(Vector256.Equals(lo, Vector256.Create((byte)'\r')), Vector256.Equals(hi, Vector256.Create((byte)'\r')));
		qM = quotes ? Bits(Vector256.Equals(lo, quote), Vector256.Equals(hi, quote)) : 0;
		cmM = comments ? Bits(Vector256.Equals(lo, comment), Vector256.Equals(hi, comment)) : 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Bits(Vector256<byte> lo, Vector256<byte> hi) =>
		lo.ExtractMostSignificantBits() | ((ulong)hi.ExtractMostSignificantBits() << 32);
}

/// <summary>The masks from one 512-bit vector compare per character class.</summary>
internal struct Masks512 : IBlockMasks<Masks512>
{
	private Vector512<byte> sep, quote, comment;

	/// <inheritdoc/>
	public readonly Masks512 Create(byte separator, byte quote, byte comment) => new()
	{
		sep = Vector512.Create(separator),
		quote = Vector512.Create(quote),
		comment = Vector512.Create(comment),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Compute(ref byte p, bool quotes, bool comments,
		out ulong sepM, out ulong lfM, out ulong crM, out ulong qM, out ulong cmM)
	{
		var v = Vector512.LoadUnsafe(ref p);
		sepM = Vector512.Equals(v, sep).ExtractMostSignificantBits();
		lfM = Vector512.Equals(v, Vector512.Create((byte)'\n')).ExtractMostSignificantBits();
		crM = Vector512.Equals(v, Vector512.Create((byte)'\r')).ExtractMostSignificantBits();
		qM = quotes ? Vector512.Equals(v, quote).ExtractMostSignificantBits() : 0;
		cmM = comments ? Vector512.Equals(v, comment).ExtractMostSignificantBits() : 0;
	}
}

/// <summary>The masks from four 128-bit vector compares per character class.</summary>
internal struct Masks128 : IBlockMasks<Masks128>
{
	private Vector128<byte> sep, quote, comment;

	/// <inheritdoc/>
	public readonly Masks128 Create(byte separator, byte quote, byte comment) => new()
	{
		sep = Vector128.Create(separator),
		quote = Vector128.Create(quote),
		comment = Vector128.Create(comment),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Compute(ref byte p, bool quotes, bool comments,
		out ulong sepM, out ulong lfM, out ulong crM, out ulong qM, out ulong cmM)
	{
		var a = Vector128.LoadUnsafe(ref p);
		var b = Vector128.LoadUnsafe(ref p, 16);
		var c = Vector128.LoadUnsafe(ref p, 32);
		var d = Vector128.LoadUnsafe(ref p, 48);
		sepM = Bits(a, b, c, d, sep);
		lfM = Bits(a, b, c, d, Vector128.Create((byte)'\n'));
		crM = Bits(a, b, c, d, Vector128.Create((byte)'\r'));
		qM = quotes ? Bits(a, b, c, d, quote) : 0;
		cmM = comments ? Bits(a, b, c, d, comment) : 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Bits(Vector128<byte> a, Vector128<byte> b, Vector128<byte> c, Vector128<byte> d, Vector128<byte> v) =>
		Vector128.Equals(a, v).ExtractMostSignificantBits()
		| ((ulong)Vector128.Equals(b, v).ExtractMostSignificantBits() << 16)
		| ((ulong)Vector128.Equals(c, v).ExtractMostSignificantBits() << 32)
		| ((ulong)Vector128.Equals(d, v).ExtractMostSignificantBits() << 48);
}
#endif

/// <summary>Writes the entries of the index for the terminators of one block.</summary>
/// <typeparam name="TSelf">The implementing struct, so that calls through the constraint are not virtual.</typeparam>
internal interface IFlattener<TSelf> where TSelf : struct, IFlattener<TSelf>
{
	/// <summary>Creates the flattener, with its constants loaded once per scan. Called on <c>default(TSelf)</c>.</summary>
	/// <returns>The flattener, ready for <see cref="Write"/>.</returns>
	public TSelf Create();

	/// <summary>
	/// Writes entry <c>k</c> for the <c>k</c>th set bit of <paramref name="termM"/>: <paramref name="start"/> plus the position
	/// of the bit, with the high bit set when the field needs decoding. Extra writes may land in the slack after the entries.
	/// </summary>
	/// <param name="dst">The first entry.</param>
	/// <param name="termM">The terminators of the block; not 0.</param>
	/// <param name="decodeM">The terminators of the fields that need decoding, a subset of <paramref name="termM"/>.</param>
	/// <param name="count">The number of set bits of <paramref name="termM"/>.</param>
	/// <param name="start">The position after the first byte of the block.</param>
	/// <param name="flagAll"><see cref="int.MinValue"/> when every field needs decoding (trimming), otherwise 0.</param>
	public void Write(ref int dst, ulong termM, ulong decodeM, int count, int start, int flagAll);
}

/// <summary>Writes the entries from the trailing zero counts of the terminators, in groups without per-bit branches.</summary>
internal struct BitFlattener : IFlattener<BitFlattener>
{
	/// <inheritdoc/>
	public readonly BitFlattener Create() => default;

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Write(ref int dst, ulong termM, ulong decodeM, int count, int start, int flagAll)
	{
		var b = start | flagAll;
		var m = termM;
		dst = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
		Unsafe.Add(ref dst, 1) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
		Unsafe.Add(ref dst, 2) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
		Unsafe.Add(ref dst, 3) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
		Unsafe.Add(ref dst, 4) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
		Unsafe.Add(ref dst, 5) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
		Unsafe.Add(ref dst, 6) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
		Unsafe.Add(ref dst, 7) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
		if (count > 8)
		{
			Unsafe.Add(ref dst, 8) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
			Unsafe.Add(ref dst, 9) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
			Unsafe.Add(ref dst, 10) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
			Unsafe.Add(ref dst, 11) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
			Unsafe.Add(ref dst, 12) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
			Unsafe.Add(ref dst, 13) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
			Unsafe.Add(ref dst, 14) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
			Unsafe.Add(ref dst, 15) = b + BitOperations.TrailingZeroCount(m); m &= m - 1;
			if (count > 16)
			{
				nint k = 16;
				do
				{
					Unsafe.Add(ref dst, k) = b + BitOperations.TrailingZeroCount(m);
					m &= m - 1;
					k++;
				} while (m != 0);
			}
		}

		while (decodeM != 0)
		{
			var low = decodeM & (0 - decodeM);
			Unsafe.Add(ref dst, BitOperations.PopCount(termM & (low - 1))) |= int.MinValue;
			decodeM ^= low;
		}
	}
}

#if NET
/// <summary>
/// Writes the entries with AVX-512 VBMI2: one compress packs the positions of the terminators (0 to 63, with bit 7 set when
/// the field needs decoding) into the low bytes, and each 16 of them are sign extended to entries.
/// </summary>
internal struct CompressFlattener : IFlattener<CompressFlattener>
{
	private Vector512<byte> positions, flagged, spreadIndex, bitOfByte;
	private Vector512<int> keep;

	/// <inheritdoc/>
	public readonly CompressFlattener Create() => new()
	{
		positions = Vector512<byte>.Indices,
		flagged = Vector512<byte>.Indices | Vector512.Create((byte)0x80),
		spreadIndex = VectorMask.SpreadIndex,
		bitOfByte = VectorMask.BitOfByte,
		keep = Vector512.Create(unchecked((int)0x8000_007F)),
	};

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Write(ref int dst, ulong termM, ulong decodeM, int count, int start, int flagAll)
	{
		var p = positions;
		if (flagAll != 0)
		{
			p = flagged;
		}
		else if (decodeM != 0)
		{
			p = Vector512.ConditionalSelect(VectorMask.FromBits(decodeM, spreadIndex, bitOfByte), flagged, positions);
		}

		var packed = Avx512Vbmi2.Compress(Vector512<byte>.Zero, VectorMask.FromBits(termM, spreadIndex, bitOfByte), p);
		var s = Vector512.Create(start);
		(Entries(packed.GetLower().GetLower()) + s).StoreUnsafe(ref dst);
		if (count > 16)
		{
			(Entries(Avx512F.ExtractVector128(packed, 1)) + s).StoreUnsafe(ref dst, 16);
			if (count > 32)
			{
				(Entries(Avx512F.ExtractVector128(packed, 2)) + s).StoreUnsafe(ref dst, 32);
				if (count > 48)
				{
					(Entries(Avx512F.ExtractVector128(packed, 3)) + s).StoreUnsafe(ref dst, 48);
				}
			}
		}
	}

	/// <summary>
	/// Sign extends 16 packed positions to entries without the start: a flagged one, <c>0x80 | i</c>, becomes
	/// <c>0xFFFF_FF80 | i</c>, which the mask turns into <c>int.MinValue | i</c>.
	/// </summary>
	/// <param name="packed">The 16 positions.</param>
	/// <returns>The 16 entries, relative to the start of the block.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private readonly Vector512<int> Entries(Vector128<byte> packed) => Avx512F.ConvertToVector512Int32(packed.AsSByte()) & keep;
}
#endif
