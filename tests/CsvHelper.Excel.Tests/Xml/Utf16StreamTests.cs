using System.Text;

namespace CsvHelper.Excel.Tests.Xml;

/// <summary>Reading UTF-16 as UTF-8 through <see cref="Utf16Stream"/>, with code units and surrogate pairs split between reads.</summary>
public class Utf16StreamTests
{
	private static readonly string Text =
		"<a>é日本" + char.ConvertFromUtf32(0x1F600) + "x" + char.ConvertFromUtf32(0x10FFFF) + new string('z', 5000) + char.ConvertFromUtf32(0x10000) + "</a>";

	[Theory]
	[InlineData(false, 1)]
	[InlineData(false, 3)]
	[InlineData(false, 4096)]
	[InlineData(true, 1)]
	[InlineData(true, 7)]
	[InlineData(true, 100_000)]
	public async Task TranscodesToUtf8(bool bigEndian, int chunk)
	{
		var encoding = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
		var expected = Encoding.UTF8.GetBytes(Text);

		Assert.Equal(expected, ReadAll(new Utf16Stream(new TrickleStream(encoding.GetBytes(Text), chunk), bigEndian), 5));
		Assert.Equal(expected, await ReadAllAsync(new Utf16Stream(new TrickleStream(encoding.GetBytes(Text), chunk), bigEndian), 5, false));
#if NET
		Assert.Equal(expected, await ReadAllAsync(new Utf16Stream(new TrickleStream(encoding.GetBytes(Text), chunk), bigEndian), 5, true));
#endif
	}

	[Fact]
	public void ReplacesLoneSurrogatesAndADanglingByte()
	{
		// A lone low surrogate, a lone high surrogate before 'a', and half a code unit at the end.
		var utf16 = new byte[] { 0x00, 0xDC, 0x3D, 0xD8, 0x61, 0x00, 0x62 };
		var replacement = ((char)0xFFFD).ToString();

		var text = Encoding.UTF8.GetString(ReadAll(new Utf16Stream(new TrickleStream(utf16), false), 3));

		Assert.Equal(replacement + replacement + "a" + replacement, text);
	}

	[Fact]
	public void IsReadOnlyAndForwardOnly()
	{
		var inner = new TrickleStream([]);
		var stream = new Utf16Stream(inner, false);

		Assert.True(stream.CanRead);
		Assert.False(stream.CanSeek);
		Assert.False(stream.CanWrite);
		Assert.Throws<NotSupportedException>(() => stream.Length);
		Assert.Throws<NotSupportedException>(() => stream.Position);
		Assert.Throws<NotSupportedException>(() => stream.Position = 0);
		Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
		Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
		Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
		stream.Flush();
		Assert.Equal(0, stream.Read(new byte[4], 0, 4));
		stream.Dispose();
		Assert.True(inner.Disposed);
	}

	/// <summary>Reads a stream to its end, a few bytes at a time.</summary>
	/// <param name="stream">The stream, which this disposes.</param>
	/// <param name="size">The most bytes to read at once.</param>
	/// <returns>The bytes.</returns>
	private static byte[] ReadAll(Stream stream, int size)
	{
		using (stream)
		{
			var output = new MemoryStream();
			var buffer = new byte[size];
			int count;
			while ((count = stream.Read(buffer, 0, size)) > 0)
			{
				output.Write(buffer, 0, count);
			}

			return output.ToArray();
		}
	}

	/// <summary>Asynchronously reads a stream to its end, a few bytes at a time.</summary>
	/// <param name="stream">The stream, which this disposes.</param>
	/// <param name="size">The most bytes to read at once.</param>
	/// <param name="memory">Whether to read with the <see cref="Memory{T}"/> overload (on .NET) rather than the array one.</param>
	/// <returns>The bytes.</returns>
	private static async Task<byte[]> ReadAllAsync(Stream stream, int size, bool memory)
	{
		using (stream)
		{
			var output = new MemoryStream();
			var buffer = new byte[size];
			while (true)
			{
#if NET
				var count = memory ? await stream.ReadAsync(buffer.AsMemory()) : await stream.ReadAsync(buffer, 0, size);
#else
				var count = await stream.ReadAsync(buffer, 0, size);
#endif
				if (count == 0)
				{
					return output.ToArray();
				}

				output.Write(buffer, 0, count);
			}
		}
	}
}
