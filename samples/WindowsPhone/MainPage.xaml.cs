using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using CsvHelper;
using CsvHelper.TypeConversion;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using CsvHelperSample.Resources;

namespace CsvHelperSample
{
	public partial class MainPage : PhoneApplicationPage
	{
		// Constructor
		public MainPage()
		{
			InitializeComponent();

			// Sample code to localize the ApplicationBar
			//BuildLocalizedApplicationBar();
		}

		// Sample code for building a localized ApplicationBar
		//private void BuildLocalizedApplicationBar()
		//{
		//    // Set the page's ApplicationBar to a new instance of ApplicationBar.
		//    ApplicationBar = new ApplicationBar();

		//    // Create a new button and set the text value to the localized string from AppResources.
		//    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
		//    appBarButton.Text = AppResources.AppBarButtonText;
		//    ApplicationBar.Buttons.Add(appBarButton);

		//    // Create a new menu item with the localized string from AppResources.
		//    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
		//    ApplicationBar.MenuItems.Add(appBarMenuItem);
		//}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			var converter = new CustomTypeTypeConverter();

			// start reading
			var resource = Application.GetResourceStream(new Uri("Assets/Sample.csv", UriKind.Relative));
			using (var stream = resource.Stream)
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
			resource = Application.GetResourceStream(new Uri("Assets/Sample.csv", UriKind.Relative));
			using (var stream = resource.Stream)
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