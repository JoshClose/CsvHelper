// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class ReflectionHelperTests
	{
		[TestMethod]
		public void CreateInstanceTests()
		{
			var test = ReflectionHelper.CreateInstance<Test>();

			Assert.IsNotNull( test );
			Assert.AreEqual( "name", test.Name );

			test = (Test)ReflectionHelper.CreateInstance( typeof( Test ) );
			Assert.IsNotNull( test );
			Assert.AreEqual( "name", test.Name );
		}

		[TestMethod]
		public void PropertyInfoAreEqualTests()
		{
			var implType = typeof( Implementer );
			var ifaceType = typeof( IFace );

			var implProp1 = implType.GetProperty( "Prop1" );
			var ifaceProp1 = ifaceType.GetProperty( "Prop1" );
			Assert.IsTrue( ReflectionHelper.PropertyInfoAreEqual( implProp1, implProp1 ) );
			Assert.IsTrue( ReflectionHelper.PropertyInfoAreEqual( implProp1, ifaceProp1 ) );
			Assert.IsTrue( ReflectionHelper.PropertyInfoAreEqual( ifaceProp1, implProp1 ) );

			var implProp2 = implType.GetProperty( "Prop2" );
			var ifaceProp2 = ifaceType.GetProperty( "Prop2" );
			Assert.IsTrue( ReflectionHelper.PropertyInfoAreEqual( implProp2, ifaceProp2 ) );
			Assert.IsTrue( ReflectionHelper.PropertyInfoAreEqual( ifaceProp2, implProp2 ) );
			Assert.IsFalse( ReflectionHelper.PropertyInfoAreEqual( implProp1, ifaceProp2 ) );
			Assert.IsFalse( ReflectionHelper.PropertyInfoAreEqual( implProp2, ifaceProp1 ) );

			var implProp3 = implType.GetProperty( "Prop3" );
			var ifaceProp3 = ifaceType.GetProperty( "Prop3" );
			Assert.IsTrue( ReflectionHelper.PropertyInfoAreEqual( implProp3, ifaceProp3 ) );

			var implProp4 = implType.GetProperty( "Prop4" );
			Assert.IsFalse( ReflectionHelper.PropertyInfoAreEqual( implProp4, ifaceProp1 ) );
			Assert.IsFalse( ReflectionHelper.PropertyInfoAreEqual( ifaceProp1, implProp4 ) );
		}

		private class Test
		{
			public string Name
			{
				get { return "name"; }
			}
		}
	}

	interface IFace
	{
		int Prop1 { get; set; }
		int Prop2 { get; }
		int Prop3 { set; }
		int Method1();
		int Method2( int arg );
	}

	class Implementer : IFace
	{
		public int Prop1 { get; set; }
		public int Prop2 { get; set; }
		public int Prop3 { get; set; }
		public int Prop4 { get; set; }
		public int Method1() { return 0; }
		public int Method2( int arg ) { return arg; }
		public int Method3( int arg ) { return arg; }
	}
}
