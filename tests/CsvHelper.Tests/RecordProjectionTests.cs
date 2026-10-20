// Every [CsvRecord] type of the tests gets projections, which also checks that their generated code compiles.
[assembly: CsvHelper.CsvRecordProjections]

namespace CsvHelper.Tests;

/// <summary>
/// Projections: generated mappers that resolve and read only some members, and the member descriptions they are
/// selected from (CsvRecordMapper.TryGetMembers and CreateProjection).
/// </summary>
public class RecordProjectionTests
{
	private const string Orders = "Id,Unit Price,Quantity,Note\nA1,1.50,3,first\nB2,2,,\n";

	[Fact]
	public void DescribesTheMembersOfAClass()
	{
		Assert.True(CsvRecordMapper.TryGetMembers<Order>(out var members));
		Assert.Equal(["Id", "Price", "Quantity", "Note"], members.Select(m => m.Name));
		Assert.All(members, m => Assert.Equal(CsvRecordMemberKind.Column, m.Kind));
		Assert.Equal(["Unit Price"], members[1].ColumnNames);
		Assert.Equal(typeof(decimal), members[1].Type);
		Assert.Equal(typeof(int?), members[2].Type);
		Assert.Equal([false, false, true, true], members.Select(m => m.Optional));
		Assert.All(members, m => Assert.False(m.IsConstructorParameter));
		Assert.All(members, m => Assert.Equal(-1, m.ColumnIndex));
	}

	[Fact]
	public void DescribesConstructorParametersFirst()
	{
		Assert.True(CsvRecordMapper.TryGetMembers<Line>(out var members));
		Assert.Equal(["Sku", "Weight", "Count"], members.Select(m => m.Name));
		Assert.All(members, m => Assert.True(m.IsConstructorParameter));
		Assert.Equal(2, members[1].ColumnIndex);
		Assert.True(members[2].Optional);
	}

	[Fact]
	public void DescribesNestedRecordsCollectionsAndConstants()
	{
		Assert.True(CsvRecordMapper.TryGetMembers<Shipment>(out var shipment));
		Assert.Equal(CsvRecordMemberKind.Nested, shipment[1].Kind);
		Assert.Empty(shipment[1].ColumnNames);
		Assert.True(CsvRecordMapper.TryGetMembers<Series>(out var series));
		Assert.Equal(CsvRecordMemberKind.Collection, series[1].Kind);
		Assert.Equal(["Value"], series[1].ColumnNames);
		Assert.True(CsvRecordMapper.TryGetMembers<Leveled>(out var leveled));
		Assert.Equal(CsvRecordMemberKind.Constant, leveled[1].Kind);
		Assert.Equal("Price (Column 'Unit Price')", Members<Order>()[1].ToString());
	}

	[Fact]
	public void DescribesMappedTypes()
	{
		Assert.True(CsvRecordMapper.TryGetMembers<Supplier>(out var members));
		Assert.Equal(["Code", "Name", "Rating", "Since"], members.Select(m => m.Name));
		Assert.Equal(["Full Name", "Name"], members[1].ColumnNames);
	}

	[Fact]
	public void HasNoMembersWithoutAGeneratedMapper()
	{
		Assert.False(CsvRecordMapper.TryGetMembers<Address>(out var members));
		Assert.Empty(members);
		Assert.False(CsvRecordMapper.TryGetMembers<CsvDynamicRecord>(out _));
		Assert.False(CsvRecordMapper.TryGetMembers<Uri>(out _));
	}

	[Fact]
	public void ReadsOnlyTheSelectedMembers()
	{
		// The prices cannot be parsed, but the projection does not read them.
		using var reader = CsvReader.FromText("Id,Unit Price,Quantity,Note\nA1,cheap,3,first\nB2,free,,\n");
		var mapper = CsvRecordMapper.CreateProjection<Order>(reader, ["Id", "Quantity"]);
		var orders = ReadAll(reader, mapper);
		Assert.Equal(["A1", "B2"], orders.Select(o => o.Id));
		Assert.Equal([3, null], orders.Select(o => o.Quantity));
		Assert.All(orders, o => Assert.Equal(0m, o.Price));
	}

	[Fact]
	public void UnselectedMembersKeepTheirInitialValues()
	{
		using var reader = CsvReader.FromText(Orders);
		var orders = ReadAll(reader, CsvRecordMapper.CreateProjection<Order>(reader, ["Price"]));
		Assert.Equal([1.50m, 2m], orders.Select(o => o.Price));
		Assert.All(orders, o => Assert.Equal("", o.Id));
		Assert.All(orders, o => Assert.Equal("none", o.Note));
	}

	[Fact]
	public void ColumnsOfUnselectedMembersMayBeMissing()
	{
		using var reader = CsvReader.FromText("Id\nA1\n");
		Assert.Throws<KeyNotFoundException>(() => CsvRecordMapper.Create<Order>(reader));
		var orders = ReadAll(reader, CsvRecordMapper.CreateProjection<Order>(reader, ["Id"]));
		Assert.Equal("A1", Assert.Single(orders).Id);
	}

	[Fact]
	public void ColumnsOfSelectedMembersAreStillRequired()
	{
		using var reader = CsvReader.FromText("Id,Quantity\nA1,3\n");
		var e = Assert.Throws<KeyNotFoundException>(() => CsvRecordMapper.CreateProjection<Order>(reader, ["Id", "Price"]));
		Assert.Contains("Order.Price ('Unit Price')", e.Message);
		Assert.DoesNotContain("Note", e.Message);
	}

	[Fact]
	public void UnselectedConstructorParametersGetTheirDefaults()
	{
		using var reader = CsvReader.FromText("Sku,Count,Weight\nX1,4,2.5\n");
		var line = Assert.Single(ReadAll(reader, CsvRecordMapper.CreateProjection<Line>(reader, ["Weight"])));
		Assert.Equal(2.5, line.Weight);
		// A string that is not nullable gets "", a parameter with a default value that value.
		Assert.Equal("", line.Sku);
		Assert.Equal(7, line.Count);
	}

	[Fact]
	public void UnselectedConstantsKeepTheirValue()
	{
		using var reader = CsvReader.FromText("Name\nAda\n");
		var leveled = Assert.Single(ReadAll(reader, CsvRecordMapper.CreateProjection<Leveled>(reader, ["Name"])));
		Assert.Equal(new Leveled("Ada", 5), leveled);
	}

	[Fact]
	public void UnselectedRequiredAndInitMembersGetTheirDefaults()
	{
		using var reader = CsvReader.FromText("Name,Age\nAda,36\n");
		var person = Assert.Single(ReadAll(reader, CsvRecordMapper.CreateProjection<Person>(reader, ["Age"])));
		Assert.Equal(36, person.Age);
		Assert.Null(person.Name);
	}

	[Fact]
	public void ReadsStructs()
	{
		using var reader = CsvReader.FromText("X,Y,Z\n1,2,3\n");
		var point = Assert.Single(ReadAll(reader, CsvRecordMapper.CreateProjection<Point>(reader, ["Y", "Z"])));
		Assert.Equal(0, point.X);
		Assert.Equal(2, point.Y);
		Assert.Equal(3L, point.Z);
	}

	[Fact]
	public void NestedRecordsAreReadOnlyWhenSelected()
	{
		// Only the From columns: the mapper of To is not created, so its columns may be missing.
		using var reader = CsvReader.FromText("Id,From City,From Geo Lat,From Geo Lon\nS1,Oslo,59.9,10.7\n");
		var shipment = Assert.Single(ReadAll(reader, CsvRecordMapper.CreateProjection<Shipment>(reader, ["From"])));
		Assert.Equal("Oslo", shipment.From.City);
		Assert.Equal(59.9, shipment.From.Point.Lat);
		Assert.Equal("", shipment.To.City);
		Assert.Equal("", shipment.Id);
	}

	[Fact]
	public void SharedNestedMappersAreCreatedForEitherMember()
	{
		const string text = "Id,From City,From Geo Lat,From Geo Lon,To City,To Geo Lat,To Geo Lon\nS1,Oslo,59.9,10.7,Rome,41.9,12.5\n";
		using var reader = CsvReader.FromText(text);
		var shipment = Assert.Single(ReadAll(reader, CsvRecordMapper.CreateProjection<Shipment>(reader, ["To"])));
		Assert.Equal("Rome", shipment.To.City);
		Assert.Equal("", shipment.From.City);
	}

	[Fact]
	public void CollectionsAreReadOnlyWhenSelected()
	{
		using var reader = CsvReader.FromText("Name,Value,Value\nA,x,y\n");
		var series = Assert.Single(ReadAll(reader, CsvRecordMapper.CreateProjection<Series>(reader, ["Name", "Notes"])));
		Assert.Equal("A", series.Name);
		Assert.Empty(series.Values);
		Assert.Empty(series.Notes);
	}

	[Fact]
	public void ChecksOnlyTheSelectedMembers()
	{
		using var reader = CsvReader.FromText("Name,Age,Email\nAda,200,not an email\n");
		var signup = Assert.Single(ReadAll(reader, CsvRecordMapper.CreateProjection<Signup>(reader, ["Name"])));
		Assert.Equal("Ada", signup.Name);
		Assert.Throws<CsvFormatException>(() => ReadAll(CsvReader.FromText("Name,Age\nAda,200\n"), reader => CsvRecordMapper.CreateProjection<Signup>(reader, ["Age"])));
	}

	[Fact]
	public void SelectingEveryMemberReadsAsTheFullMapper()
	{
		using var reader = CsvReader.FromText(Orders);
		var mapper = CsvRecordMapper.CreateProjection<Order>(reader, Members<Order>().Select(m => m.Name).Reverse());
		Assert.Equal(CsvRecordMapper.Create<Order>(reader).GetType(), mapper.GetType());
		var orders = ReadAll(reader, mapper);
		using var full = CsvReader.FromText(Orders);
		var expected = full.GetRecords<Order>().ToList();
		Assert.Equal(expected.Select(o => (o.Id, o.Price, o.Quantity, o.Note)), orders.Select(o => (o.Id, o.Price, o.Quantity, o.Note)));
	}

	[Fact]
	public void MembersCanBeNamedMoreThanOnce()
	{
		using var reader = CsvReader.FromText(Orders);
		var orders = ReadAll(reader, CsvRecordMapper.CreateProjection<Order>(reader, ["Id", "Id"]));
		Assert.Equal(["A1", "B2"], orders.Select(o => o.Id));
	}

	[Fact]
	public void UnknownMembersAreRejected()
	{
		using var reader = CsvReader.FromText(Orders);
		var e = Assert.Throws<ArgumentException>(() => CsvRecordMapper.CreateProjection<Order>(reader, ["Id", "Total"]));
		Assert.Contains("'Total'", e.Message);
		// Members are matched exactly.
		Assert.Throws<ArgumentException>(() => CsvRecordMapper.CreateProjection<Order>(reader, ["id"]));
	}

	[Fact]
	public void TypesWithoutProjectionsAreNotSupported()
	{
		using var reader = CsvReader.FromText("Street,City\nMain,Oslo\n");
		Assert.Throws<NotSupportedException>(() => CsvRecordMapper.CreateProjection<Address>(reader, ["City"]));
		Assert.Throws<NotSupportedException>(() => CsvRecordMapper.CreateProjection<CsvDynamicRecord>(reader, []));
		Assert.Throws<InvalidOperationException>(() => CsvRecordMapper.CreateProjection<Uri>(reader, []));
	}

	[Fact]
	public void FailuresNameTheSelectedMember()
	{
		using var reader = CsvReader.FromText("Id,Unit Price\nA1,cheap\n");
		var mapper = CsvRecordMapper.CreateProjection<Order>(reader, ["Price"]);
		Assert.True(reader.Read());
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecord(mapper));
		Assert.Equal("Order.Price", e.Member);
		Assert.Equal(1, e.FieldIndex);
		Assert.IsType<FormatException>(e.InnerException);
	}

	[Fact]
	public void TryGetRecordSkipsWhatTheHandlerSkips()
	{
		var errors = new List<CsvRecordError>();
		var options = new CsvReaderOptions { OnRecordError = e => { errors.Add(e); return true; } };
		using var reader = CsvReader.FromText("Id,Unit Price\nA1,cheap\nB2,2\n", options);
		var mapper = CsvRecordMapper.CreateProjection<Order>(reader, ["Price"]);
		Assert.True(reader.Read());
		Assert.False(reader.TryGetRecord(mapper, out var skipped));
		Assert.Null(skipped);
		Assert.True(reader.Read());
		Assert.True(reader.TryGetRecord(mapper, out var order));
		Assert.Equal(2m, order.Price);
		Assert.Single(errors);
	}

	[Fact]
	public void GetRecordRejectsNullMappers()
	{
		using var reader = CsvReader.FromText(Orders);
		Assert.True(reader.Read());
		Assert.Throws<ArgumentNullException>(() => reader.GetRecord((CsvRecordMapper<Order>)null!));
		Assert.Throws<ArgumentNullException>(() => reader.TryGetRecord((CsvRecordMapper<Order>)null!, out _));
	}

	[Fact]
	public void WorkersShareAProjection()
	{
		var text = new System.Text.StringBuilder("Id,Unit Price\n");
		for (var i = 0; i < 5000; i++)
		{
			text.Append("R").Append(i).Append(",bad\n");
		}

		using var reader = CsvReader.FromText(text.ToString());
		var mapper = CsvRecordMapper.CreateProjection<Order>(reader, ["Id"]);
		var ids = reader.ParallelEnumerate(r => r.GetRecord(mapper).Id, 4).ToList();
		Assert.Equal(Enumerable.Range(0, 5000).Select(i => "R" + i), ids);
	}

	private static IReadOnlyList<CsvRecordMember> Members<T>()
	{
		Assert.True(CsvRecordMapper.TryGetMembers<T>(out var members));

		return members;
	}

	private static List<T> ReadAll<T>(CsvReader reader, CsvRecordMapper<T> mapper)
	{
		var records = new List<T>();
		while (reader.Read())
		{
			records.Add(reader.GetRecord(mapper));
		}

		return records;
	}

	private static List<T> ReadAll<T>(CsvReader reader, Func<CsvReader, CsvRecordMapper<T>> mapper)
	{
		using (reader)
		{
			return ReadAll(reader, mapper(reader));
		}
	}
}
