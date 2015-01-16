using System;
using System.Drawing;

using CoreFoundation;
using UIKit;
using Foundation;
using System.IO;

namespace CsvHelperSample
{
	[Register("SampleViewController")]
	public class SampleViewController : UIViewController
	{
		public SampleViewController()
		{
		}

		public override void DidReceiveMemoryWarning()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning();

			// Release any cached data, images, etc that aren't in use.
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();

			// Perform any additional setup after loading the view

			var path = Path.Combine(NSBundle.MainBundle.ResourcePath, "Sample.csv");
			try { 
			Console.WriteLine("Reading the whole object for each record:");
			SampleReadLogic.DoAutomaticRead(path, obj => Console.WriteLine("{0} ({1}) = {2}", obj.StringColumn, obj.NumberColumn, obj.CustomTypeColumn));

			Console.WriteLine("Reading one field at a time for each record:");
			SampleReadLogic.DoFieldRead(path, obj => Console.WriteLine("{0} ({1}) = {2}", obj.StringColumn, obj.NumberColumn, obj.CustomTypeColumn));
}catch(Exception ex)
			{ Console.WriteLine(ex); Console.WriteLine(ex.StackTrace); }
		}
	}
}