#if NET
using System.Text;
using CsvHelper.Excel.Tests.Xml;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// What loading the shared strings allocates (Phase 9's step 4): the chunks grow without copying the text, and are sized
/// from the text the part's declared size says is still to come, so a part that declares its size and its strings
/// honestly allocates about what the table holds; a declared size that is wrong costs at most a chunk of 4 MiB more.
/// </summary>
/// <param name="output">Shows what the tests measured.</param>
public class SharedStringsAllocationTests(ITestOutputHelper output)
{
	/// <summary>The distinct strings of the budget's tests.</summary>
	private const int Strings = 200_000;

	/// <summary>The bytes of each of them: a GUID's 36 characters.</summary>
	private const int GuidLength = 36;

	/// <summary>The largest chunk of several strings, 4 MiB: what a wrong estimate, and trimming the last chunk, cost at most.</summary>
	private const int MaxChunk = 4 << 20;

	/// <summary>
	/// What a load allocates beyond its chunks and starts, at most: the arrays that find a chunk, the room moved strings
	/// leave, the part's markup that the estimate doesn't take off, and the runtime's own.
	/// </summary>
	private const int Slack = 64 << 10;

	/// <summary>The starts' capacity without <c>uniqueCount</c>: room for 16 strings and the end of the last.</summary>
	private const int MinStarts = 17;

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllocatesLittleMoreThanTheTableHolds(bool declared)
	{
		// 200,000 distinct GUIDs. With uniqueCount, the part's size less the markup is the text (and the root's tag), so the
		// chunks are filled: at most 1.05 times the text and the starts, and the slack. Without it, the estimate is the whole
		// part (the markup too, 44% more than the text), so the last chunk of 4 MiB is part spare and trimmed, and the starts
		// grow by doubling from 17 entries, as they always have, before their trim. Before the chunks, one array first sized
		// from the part's size less its markup took 8,000,336 bytes with uniqueCount, where that size was exact, and
		// 20,628,840 without (net10.0, 2026-11-21).
		var xml = Guids(declared, out var expected);
		var text = (long)GuidLength * Strings;
		var budget = declared
			? (long)(1.05 * (text + (4L * Strings))) + Slack
			: (long)(1.15 * (text + (4L * Strings))) + MaxChunk + StartsGrowth();

		// The smaller of two loads: the runtime allocates once, at a moment tiering sets.
		var allocated = Math.Min(Allocated(xml, xml.Length, out _), Allocated(xml, xml.Length, out var table));
		output.WriteLine(
			$"{Strings:N0} distinct strings of {text:N0} bytes, {(declared ? "with" : "without")} uniqueCount: the table holds {table.MemorySize:N0} bytes, " +
			$"and the load allocated {allocated:N0} ({allocated / (double)(text + (4L * Strings)):F3} times the text and starts; at most {budget:N0}).");

		AssertTable(expected, table);
		Assert.True(allocated <= budget, $"{allocated:N0} bytes allocated, against at most {budget:N0}.");
	}

	[Fact]
	public void CostsAtMostAChunkForADeclaredSizeThatIsWrong()
	{
		// The same strings with uniqueCount, and a declared size of a terabyte, whose chunks are each 4 MiB and whose last is
		// trimmed, or of nothing, whose estimate is used up at once, so the chunks double from 256 bytes: either way at most
		// a chunk of 4 MiB, and the slack, more than the honest part.
		var xml = Guids(true, out var expected);
		var allocated = new List<long>();
		foreach (var size in new long[] { xml.Length, 1L << 40, 0 })
		{
			// The smaller of two loads, as above.
			var first = Allocated(xml, size, out _);
			var second = Allocated(xml, size, out var table);
			AssertTable(expected, table);
			allocated.Add(Math.Min(first, second));
		}

		output.WriteLine($"{Strings:N0} distinct strings with uniqueCount: the load allocated {allocated[0]:N0} bytes for the part's size, {allocated[1]:N0} for a terabyte and {allocated[2]:N0} for nothing.");

		Assert.True(allocated[1] <= allocated[0] + MaxChunk + Slack, $"{allocated[1]:N0} bytes for a terabyte, against {allocated[0]:N0}.");
		Assert.True(allocated[2] <= allocated[0] + MaxChunk + Slack, $"{allocated[2]:N0} bytes for nothing, against {allocated[0]:N0}.");
	}

	[Fact]
	public void SizesAChunkFromWhatThePartDeclares()
	{
		// Two strings: a part that declares its own size has a chunk of the fewest bytes, 256, and one that declares a
		// terabyte has one of 4 MiB; each is trimmed after. A part of empty strings has no chunk, whatever it declares.
		var xml = Encoding.UTF8.GetBytes($"<sst xmlns=\"{TestPackage.MainNamespace}\"><si><t>a</t></si><si><t>b</t></si></sst>");
		var empty = Encoding.UTF8.GetBytes($"<sst xmlns=\"{TestPackage.MainNamespace}\"><si/><si><t></t></si><si><t/></si></sst>");

		// Each once first, as the runtime allocates once, at a moment tiering sets.
		Allocated(xml, xml.Length, out _);
		Allocated(xml, 1L << 40, out _);
		Allocated(empty, 1L << 40, out _);
		var small = Allocated(xml, xml.Length, out var table);
		var large = Allocated(xml, 1L << 40, out var declared);
		var none = Allocated(empty, 1L << 40, out var blank);
		output.WriteLine($"Two strings allocated {small:N0} bytes for their part's size and {large:N0} for a terabyte; three empty strings allocated {none:N0}.");

		Assert.Equal(["a", "b"], Packages.Strings(table));
		Assert.Equal(["a", "b"], Packages.Strings(declared));
		Assert.Equal(["", "", ""], Packages.Strings(blank));
		Assert.Equal(MaxChunk - 256, large - small);
		Assert.True(small < 2048, $"{small:N0} bytes allocated");
		Assert.True(none < small - 256, $"{none:N0} bytes allocated for empty strings, against {small:N0} for two of a byte");
		Assert.Equal(4 * 4, blank.MemorySize);
	}

	/// <summary>A part of 200,000 distinct GUIDs, the same each time.</summary>
	/// <param name="uniqueCount">Whether the root declares <c>count</c> and <c>uniqueCount</c>.</param>
	/// <param name="expected">The strings.</param>
	/// <returns>The part's bytes.</returns>
	private static byte[] Guids(bool uniqueCount, out List<string> expected)
	{
		var random = new Random(200);
		var guid = new byte[16];
		var content = new StringBuilder();
		expected = [];
		for (var i = 0; i < Strings; i++)
		{
			random.NextBytes(guid);
			expected.Add(new Guid(guid).ToString());
			content.Append("<si><t>").Append(expected[i]).Append("</t></si>");
		}

		var attributes = uniqueCount ? $" count=\"{Strings}\" uniqueCount=\"{Strings}\"" : "";

		return Encoding.UTF8.GetBytes($"<sst xmlns=\"{TestPackage.MainNamespace}\"{attributes}>{content}</sst>");
	}

	/// <summary>Checks a table of the GUIDs: its count, every 9,973rd string, and that it holds at most an eighth more than its text and starts.</summary>
	/// <param name="expected">The strings.</param>
	/// <param name="table">The table.</param>
	private static void AssertTable(List<string> expected, SharedStrings table)
	{
		Assert.Equal(Strings, table.Count);
		for (var i = 0; i < Strings; i += 9_973)
		{
			Assert.True(table.TryGet(i, out var value));
			Assert.Equal(expected[i], Encoding.UTF8.GetString(value.ToArray()));
		}

		var needed = ((long)GuidLength * Strings) + (4L * (Strings + 1));
		Assert.InRange(table.MemorySize, needed, needed * 9 / 8);
	}

	/// <summary>What the starts allocate as they grow without <c>uniqueCount</c>: each capacity, from 17 entries, doubling to hold the strings.</summary>
	/// <returns>The bytes of the capacities.</returns>
	private static long StartsGrowth()
	{
		var bytes = 0L;
		for (var capacity = (long)MinStarts; ; capacity *= 2)
		{
			bytes += 4 * capacity;
			if (capacity >= Strings + 1)
			{
				return bytes;
			}
		}
	}

	/// <summary>Loads shared strings from XML, whole and synchronously, and measures what the load allocates.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="declaredSize">The size the zip declares.</param>
	/// <param name="table">The shared strings.</param>
	/// <returns>The bytes the load allocated on this thread, the scanner's window aside.</returns>
	private static long Allocated(byte[] xml, long declaredSize, out SharedStrings table)
	{
		using var scanner = XmlScan.Open(xml, ScanMode.All[0]);
		var before = GC.GetAllocatedBytesForCurrentThread();
		table = SyncOrAsync.Result(SharedStrings.ReadAsync(scanner, declaredSize, false, CancellationToken.None));

		return GC.GetAllocatedBytesForCurrentThread() - before;
	}
}
#endif
