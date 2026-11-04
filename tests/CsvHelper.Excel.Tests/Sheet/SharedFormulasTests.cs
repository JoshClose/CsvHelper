using System.Text;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary><see cref="SharedFormulas"/>: a sheet's groups, the room their moved formulas are written in, and the limit.</summary>
public class SharedFormulasTests
{
	private const string PartName = "xl/worksheets/sheet1.xml";

	[Fact]
	public void MovesTheDefiningCellsFormulaToEachCell()
	{
		var shared = new SharedFormulas(PartName, 1 << 20);
		Assert.False(shared.TryFind(0, out var hasFormula));
		Assert.False(shared.TryGet(0, 2, 1, out _));

		shared.Define(0, "A1*2"u8, 1, 1);
		shared.Define(5, default, 1, 3);
		Assert.True(shared.TryFind(0, out hasFormula));
		Assert.True(hasFormula);
		Assert.True(shared.TryFind(5, out hasFormula));
		Assert.False(hasFormula);
		Assert.Equal("=B3*2", Get(shared, 0, 3, 2));
		Assert.True(shared.TryGet(5, 2, 3, out var none));
		Assert.True(none.IsEmpty);

		// A later definition replaces the group.
		shared.Define(0, "C1"u8, 4, 0);
		Assert.Equal("=C2", Get(shared, 0, 5, 0));
	}

	[Fact]
	public void GrowsTheRoomForMovedFormulas()
	{
		var shared = new SharedFormulas(PartName, 1 << 20);
		var many = string.Join(",", Enumerable.Repeat("A1", 1000));
		shared.Define(0, "A1"u8, 1, 0);
		shared.Define(1, Encoding.UTF8.GetBytes(many), 1, 0);

		Assert.Equal("=A2", Get(shared, 0, 2, 0));
		Assert.Equal("=" + string.Join(",", Enumerable.Repeat("XFD1048576", 1000)), Get(shared, 1, CellReference.MaxRow, CellReference.MaxColumns - 1));
		Assert.Equal("=" + many.Replace("A1", "A3"), Get(shared, 1, 3, 0));
		Assert.Equal("=A4", Get(shared, 0, 4, 0));
	}

	[Fact]
	public void StopsAtThePartSizeLimit()
	{
		// Each group counts its formula and 64 bytes.
		var shared = new SharedFormulas(PartName, 1000);
		shared.Define(0, new byte[900], 1, 0);
		var e = Assert.Throws<InvalidDataException>(() => shared.Define(1, new byte[1], 1, 0));
		Assert.Equal($"The shared formulas of {PartName} take more than ExcelReaderOptions.MaxPartSize (1000 bytes).", e.Message);
		shared.Define(0, new byte[1000 - 64], 1, 0);
		Assert.Throws<InvalidDataException>(() => shared.Define(0, new byte[1000 - 63], 1, 0));

		// The room counts too: moved to the grid's far corner, 2,999 bytes take 10,999, past the limit, and a short move
		// still fits.
		var room = new SharedFormulas(PartName, 10_000);
		room.Define(0, Encoding.UTF8.GetBytes(string.Join(",", Enumerable.Repeat("A1", 1000))), 1, 0);
		Assert.Throws<InvalidDataException>(() => room.TryGet(0, CellReference.MaxRow, CellReference.MaxColumns - 1, out _));
		Assert.Equal("=" + string.Join(",", Enumerable.Repeat("A2", 1000)), Get(room, 0, 2, 0));
	}

	/// <summary>The formula of a cell of a group, which must have one.</summary>
	/// <param name="shared">The groups.</param>
	/// <param name="index">The group's index.</param>
	/// <param name="row">The cell's row.</param>
	/// <param name="column">The cell's column, from 0.</param>
	/// <returns>The formula, with its <c>=</c>.</returns>
	private static string Get(SharedFormulas shared, int index, long row, int column)
	{
		Assert.True(shared.TryGet(index, row, column, out var formula));

		return Encoding.UTF8.GetString(formula.ToArray());
	}
}
