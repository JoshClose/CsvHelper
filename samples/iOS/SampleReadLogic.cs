using System;
using System.IO;
using CsvHelper;
using CsvHelper.TypeConversion;

namespace CsvHelperSample
{
	public static class SampleReadLogic
	{
		public static void DoAutomaticRead(string path, Action<CustomObject> action)
		{
			// register any converters with the system
			var converter = new CustomTypeTypeConverter();
			TypeConverterFactory.AddConverter<CustomTypeTypeConverter>(converter);

			// start reading
			using (var stream = File.OpenRead(path))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader))
			{
				var records = csv.GetRecords<CustomObject>();
				// deffered loading
				foreach (var record in records)
				{
					action(record);
				}
			}

			// deregister any converters with the system
			TypeConverterFactory.RemoveConverter<CustomTypeTypeConverter>();
		}

		public static void DoFieldRead(string path, Action<CustomObject> action)
		{
			var converter = new CustomTypeTypeConverter();

			// start reading
			using (var stream = File.OpenRead(path))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader))
			{
				while (csv.Read())
				{
					var record = new CustomObject
					{
						StringColumn = csv.GetField<string>("StringColumn"),
						NumberColumn = csv.GetField<int>("NumberColumn"),
						CustomTypeColumn = csv.GetField<CustomType>("CustomTypeColumn", converter)
					};
					action(record);
				}
			}
		}
	}
}
