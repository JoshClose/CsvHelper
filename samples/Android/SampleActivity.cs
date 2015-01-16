using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using CsvHelper;
using CsvHelper.TypeConversion;

namespace CsvHelperSample
{
	[Activity(Label = "CsvHelperSample", MainLauncher = true, Icon = "@drawable/icon")]
	public class SampleActivity : Activity
	{
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);
			var textView = FindViewById<TextView>(Resource.Id.textView);
			
			var converter = new CustomTypeTypeConverter();

			// start reading
			using (var stream = Assets.Open("Sample.csv"))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader))
			{
				while (csv.Read())
				{
					var str = csv.GetField<string>("StringColumn");
					var num = csv.GetField<int>("NumberColumn");
					var cust = csv.GetField<CustomType>("CustomTypeColumn", converter);

					textView.Text += string.Format("{0} ({1}) = {2}\n", str, num, cust);
				}
			}

			// register any converters with the system
			TypeConverterFactory.AddConverter<CustomTypeTypeConverter>(converter);

			// start reading
			using (var stream = Assets.Open("Sample.csv"))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader))
			{
				// deffered loading
				var records = csv.GetRecords<CustomObject>();
				foreach (var record in records)
				{
					textView.Text += string.Format("{0} ({1}) = {2}\n", 
						record.StringColumn, 
						record.NumberColumn,
						record.CustomTypeColumn);
				}
			}

			// deregister any converters with the system
			TypeConverterFactory.RemoveConverter<CustomTypeTypeConverter>();
		}
	}
}

