using Xunit;

namespace CsvHelper.Tests.Dynamic;

public class FastDynamicObjectTests
{
	[Fact]
	public void Dynamic_SetAndGet_Works()
	{
		dynamic obj = new FastDynamicObject();
		obj.Id = 1;
		obj.Name = "one";
		obj.Null = null;

		var id = obj.Id;
		var name = obj.Name;
		var @null = obj.Null;

		Assert.Equal(1, id);
		Assert.Equal("one", name);
		Assert.Null(@null);
	}

	[Fact]
	public void CopyTo_NegativeIndex_Throws()
	{
		IDictionary<string, object?> d = new FastDynamicObject();
		var a = new KeyValuePair<string, object?>[1];

		Assert.Throws<ArgumentOutOfRangeException>(() => d.CopyTo(a, -1));
	}

	[Fact]
	public void CopyTo_IndexLargerThanArrayLength_Throws()
	{
		IDictionary<string, object?> d = new FastDynamicObject();
		var a = new KeyValuePair<string, object?>[1];

		Assert.Throws<ArgumentOutOfRangeException>(() => d.CopyTo(a, a.Length));
	}

	[Fact]
	public void CopyTo_SourceIsLargerThanDestination_Throws()
	{
		IDictionary<string, object?> d = new FastDynamicObject();
		d["a"] = 1;
		d["b"] = 2;
		var a = new KeyValuePair<string, object?>[1];

		Assert.Throws<ArgumentException>(() => d.CopyTo(a, 0));
	}

	[Fact]
	public void CopyTo_IndexGreaterThanZeroAndSourceIsLargerThanDestination_Throws()
	{
		IDictionary<string, object?> d = new FastDynamicObject();
		d["a"] = 1;
		d["b"] = 2;
		var a = new KeyValuePair<string, object?>[2];

		Assert.Throws<ArgumentException>(() => d.CopyTo(a, 1));
	}

	[Fact]
	public void CopyTo_StartAtZero_Copies()
	{
		IDictionary<string, object?> d = new FastDynamicObject();
		d["a"] = 1;
		d["b"] = 2;
		var a = new KeyValuePair<string, object?>[2];

		d.CopyTo(a, 0);

		Assert.Equal("a", a[0].Key);
		Assert.Equal(1, a[0].Value);
		Assert.Equal("b", a[1].Key);
		Assert.Equal(2, a[1].Value);
	}

	[Fact]
	public void CopyTo_StartGreaterThanZero_Copies()
	{
		IDictionary<string, object?> d = new FastDynamicObject();
		d["a"] = 1;
		d["b"] = 2;
		var a = new KeyValuePair<string, object?>[4];

		d.CopyTo(a, 1);

		Assert.Null(a[0].Key);
		Assert.Null(a[0].Value);
		Assert.Equal("a", a[1].Key);
		Assert.Equal(1, a[1].Value);
		Assert.Equal("b", a[2].Key);
		Assert.Equal(2, a[2].Value);
		Assert.Null(a[3].Key);
		Assert.Null(a[3].Value);
	}
}
