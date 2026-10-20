
using CsvHelper;
using System.Text;

string[] newlines = ["\n", "\r\n", "\r"];
var rng = new Random(5);
var getNewLine = () => newlines[rng.Next(3)];

var sb = new StringBuilder();
sb.Append("Id,Name").Append(getNewLine());
sb.Append("1,one").Append(getNewLine());

using var writer = CsvWriter.Create(new MemoryStream(), new CsvWriterOptions
{    
});

using var reader = CsvReader.FromText(sb.ToString(), new CsvReaderOptions 
{
});

while (reader.Read())
{
}

class Record
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

class RecordMap : CsvRecordMapper<Record>
{
	RecordMap(CsvReader reader)
	{
		reader.Parse<int>(1);
	}

	public override Record Read(CsvReader reader)
	{
		throw new NotImplementedException();
	}
}