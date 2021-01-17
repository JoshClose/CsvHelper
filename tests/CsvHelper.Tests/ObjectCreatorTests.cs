// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.ObjectCreatorTests
{
	[TestClass]
	public class CreateInstance_ValueType
	{
		[TestMethod]
		public void CreatesInstance()
		{
			var creator = new ObjectCreator();
			var value = creator.CreateInstance<int>();

			Assert.AreEqual(default(int), value);
		}

		[TestMethod]
		public void ParameterSupplied_ThrowsMissingMethodExcepetion()
		{
			var creator = new ObjectCreator();

			Assert.ThrowsException<MissingMethodException>(() => creator.CreateInstance<int>(1));
		}
	}

	[TestClass]
	public class CreateInstance_DefaultConstructor
	{
		[TestMethod]
		public void CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>();
			creator.CreateInstance<Foo>();

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(default(int), foo.Id);
		}

		[TestMethod]
		public void ParameterSupplied_ThrowsMissingMethodExcepetion()
		{
			var creator = new ObjectCreator();

			Assert.ThrowsException<MissingMethodException>(() => creator.CreateInstance<Foo>(1));
		}

		private class Foo
		{
			public int Id { get; private set; }

			public Foo() { }
		}
	}

	[TestClass]
	public class CreateInstance_OneParameterConstructor
	{
		[TestMethod]
		public void OneParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(1);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(1, foo.Id);
		}

		[TestMethod]
		public void NoParameter_ThrowsMissingMethodException()
		{
			var creator = new ObjectCreator();

			Assert.ThrowsException<MissingMethodException>(() => creator.CreateInstance<Foo>());
		}

		private class Foo
		{
			public int Id { get; private set; }

			public Foo(int id)
			{
				Id = id;
			}
		}
	}

	[TestClass]
	public class CreateInstance_DefaultConstructorAndOneParameterConstructor
	{
		[TestMethod]
		public void NoParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>();

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(default(int), foo.Id);
		}

		[TestMethod]
		public void OneParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(1);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(1, foo.Id);
		}

		[TestMethod]
		public void OneParameterWrongType_ThrowsMissingMethodException()
		{
			var creator = new ObjectCreator();

			Assert.ThrowsException<MissingMethodException>(() => creator.CreateInstance<Foo>(string.Empty));
		}

		private class Foo
		{
			public int Id { get; private set; }

			public Foo() { }

			public Foo(int id)
			{
				Id = id;
			}
		}
	}

	[TestClass]
	public class CreateInstance_ValueTypeAndReferenceTypeParameters
	{
		[TestMethod]
		public void FirstSignature_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(1, "one");

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(1, foo.Id);
			Assert.AreEqual("one", foo.Name);
		}

		[TestMethod]
		public void SecondSignature_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>("one", 1);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(1, foo.Id);
			Assert.AreEqual("one", foo.Name);
		}

		[TestMethod]
		public void FirstSignature_NullReferenceType_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(1, null);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(1, foo.Id);
			Assert.IsNull(foo.Name);
		}

		[TestMethod]
		public void SecondSignature_NullReferenceType_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(null, 1);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(1, foo.Id);
			Assert.IsNull(foo.Name);
		}

		[TestMethod]
		public void FirstSignature_NullValueType_ThrowsMissingMethodException()
		{
			var creator = new ObjectCreator();

			Assert.ThrowsException<MissingMethodException>(() => creator.CreateInstance<Foo>(null, "one"));
		}

		[TestMethod]
		public void SecondSignature_NullValueType_ThrowsMissingMethodException()
		{
			var creator = new ObjectCreator();

			Assert.ThrowsException<MissingMethodException>(() => creator.CreateInstance<Foo>("one", null));
		}

		private class Foo
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public Foo(int id, string name)
			{
				Id = id;
				Name = name;
			}

			public Foo(string name, int id)
			{
				Name = name;
				Id = id;
			}
		}
	}

	[TestClass]
	public class CreateInstance_TwoReferenceTypeParameters
	{
		[TestMethod]
		public void FirstSignature_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var bar = new Bar();
			var foo = creator.CreateInstance<Foo>("one", bar);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual("one", foo.Name);
			Assert.AreEqual(bar, foo.Bar);
		}

		[TestMethod]
		public void SecondSignature_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var bar = new Bar();
			var foo = creator.CreateInstance<Foo>(bar, "one");

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual("one", foo.Name);
			Assert.AreEqual(bar, foo.Bar);
		}

		[TestMethod]
		public void FirstSignature_NullFirstParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var bar = new Bar();
			var foo = creator.CreateInstance<Foo>(null, bar);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.IsNull(foo.Name);
			Assert.AreEqual(bar, foo.Bar);
		}

		[TestMethod]
		public void FirstSignature_NullSecondParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>("one", null);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual("one", foo.Name);
			Assert.IsNull(foo.Bar);
		}

		[TestMethod]
		public void SecondSignature_NullFirstParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(null, "one");

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.IsNull(foo.Bar);
			Assert.AreEqual("one", foo.Name);
		}

		[TestMethod]
		public void SecondSignature_NullSecondParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var bar = new Bar();
			var foo = creator.CreateInstance<Foo>(bar, null);

			Assert.IsInstanceOfType(foo, typeof(Foo));
			Assert.AreEqual(bar, foo.Bar);
			Assert.IsNull(foo.Name);
		}

		[TestMethod]
		public void FirstSignature_BothNullParameters_ThrowsAmbiguousMatchException()
		{
			var creator = new ObjectCreator();

			Assert.ThrowsException<AmbiguousMatchException>(() => creator.CreateInstance<Foo>(null, null));
		}

		private class Foo
		{
			public string Name { get; set; }

			public Bar Bar { get; set; }

			public Foo(string name, Bar bar)
			{
				Name = name;
				Bar = bar;
			}

			public Foo(Bar bar, string name)
			{
				Bar = bar;
				Name = name;
			}
		}

		private class Bar { }
	}

	[TestClass]
	public class CreateInstance_PrivateConstructor
	{
		[TestMethod]
		public void CreatesInstance()
		{
			var creator = new ObjectCreator();

			var foo = creator.CreateInstance<Foo>();

			Assert.IsInstanceOfType(foo, typeof(Foo));
		}

		private class Foo
		{
			private Foo() { }
		}
	}
}
