using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TypeConverterOptionsFactoryTests
	{
		[TestMethod]
		public void AddGetRemoveTest()
		{
			var customOptions = new TypeConverterOptions
			{
				Format = "custom",
			};
			TypeConverterOptionsFactory.AddOptions<string>( customOptions );
			var options = TypeConverterOptionsFactory.GetOptions<string>();

			Assert.AreEqual( customOptions.Format, options.Format );

			TypeConverterOptionsFactory.RemoveOptions<string>();

			options = TypeConverterOptionsFactory.GetOptions<string>();

			Assert.AreNotEqual( customOptions.Format, options.Format );
		}
	}
}
