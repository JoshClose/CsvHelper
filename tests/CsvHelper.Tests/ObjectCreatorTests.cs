// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
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
	
	public class CreateInstance_ValueType
	{
		[Fact]
		public void CreatesInstance()
		{
			var creator = new ObjectCreator();
			var value = creator.CreateInstance<int>();

			Assert.Equal(default(int), value);
		}

		[Fact]
		public void ParameterSupplied_ThrowsMissingMethodExcepetion()
		{
			var creator = new ObjectCreator();

			Assert.Throws<MissingMethodException>(() => creator.CreateInstance<int>(1));
		}
	}

	
	public class CreateInstance_DefaultConstructor
	{
		[Fact]
		public void CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>();
			creator.CreateInstance<Foo>();

			Assert.IsType<Foo>(foo);
			Assert.Equal(default(int), foo.Id);
		}

		[Fact]
		public void ParameterSupplied_ThrowsMissingMethodExcepetion()
		{
			var creator = new ObjectCreator();

			Assert.Throws<MissingMethodException>(() => creator.CreateInstance<Foo>(1));
		}

		private class Foo
		{
			public int Id { get; private set; }

			public Foo() { }
		}
	}

	
	public class CreateInstance_OneParameterConstructor
	{
		[Fact]
		public void OneParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(1);

			Assert.IsType<Foo>(foo);
			Assert.Equal(1, foo.Id);
		}

		[Fact]
		public void NoParameter_ThrowsMissingMethodException()
		{
			var creator = new ObjectCreator();

			Assert.Throws<MissingMethodException>(() => creator.CreateInstance<Foo>());
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

	
	public class CreateInstance_DefaultConstructorAndOneParameterConstructor
	{
		[Fact]
		public void NoParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>();

			Assert.IsType<Foo>(foo);
			Assert.Equal(default(int), foo.Id);
		}

		[Fact]
		public void OneParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(1);

			Assert.IsType<Foo>(foo);
			Assert.Equal(1, foo.Id);
		}

		[Fact]
		public void OneParameterWrongType_ThrowsMissingMethodException()
		{
			var creator = new ObjectCreator();

			Assert.Throws<MissingMethodException>(() => creator.CreateInstance<Foo>(string.Empty));
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

	
	public class CreateInstance_ValueTypeAndReferenceTypeParameters
	{
		[Fact]
		public void FirstSignature_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(1, "one");

			Assert.IsType<Foo>(foo);
			Assert.Equal(1, foo.Id);
			Assert.Equal("one", foo.Name);
		}

		[Fact]
		public void SecondSignature_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>("one", 1);

			Assert.IsType<Foo>(foo);
			Assert.Equal(1, foo.Id);
			Assert.Equal("one", foo.Name);
		}

		[Fact]
		public void FirstSignature_NullReferenceType_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(1, null);

			Assert.IsType<Foo>(foo);
			Assert.Equal(1, foo.Id);
			Assert.Null(foo.Name);
		}

		[Fact]
		public void SecondSignature_NullReferenceType_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(null, 1);

			Assert.IsType<Foo>(foo);
			Assert.Equal(1, foo.Id);
			Assert.Null(foo.Name);
		}

		[Fact]
		public void FirstSignature_NullValueType_ThrowsMissingMethodException()
		{
			var creator = new ObjectCreator();

			Assert.Throws<MissingMethodException>(() => creator.CreateInstance<Foo>(null, "one"));
		}

		[Fact]
		public void SecondSignature_NullValueType_ThrowsMissingMethodException()
		{
			var creator = new ObjectCreator();

			Assert.Throws<MissingMethodException>(() => creator.CreateInstance<Foo>("one", null));
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

	
	public class CreateInstance_TwoReferenceTypeParameters
	{
		[Fact]
		public void FirstSignature_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var bar = new Bar();
			var foo = creator.CreateInstance<Foo>("one", bar);

			Assert.IsType<Foo>(foo);
			Assert.Equal("one", foo.Name);
			Assert.Equal(bar, foo.Bar);
		}

		[Fact]
		public void SecondSignature_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var bar = new Bar();
			var foo = creator.CreateInstance<Foo>(bar, "one");

			Assert.IsType<Foo>(foo);
			Assert.Equal("one", foo.Name);
			Assert.Equal(bar, foo.Bar);
		}

		[Fact]
		public void FirstSignature_NullFirstParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var bar = new Bar();
			var foo = creator.CreateInstance<Foo>(null, bar);

			Assert.IsType<Foo>(foo);
			Assert.Null(foo.Name);
			Assert.Equal(bar, foo.Bar);
		}

		[Fact]
		public void FirstSignature_NullSecondParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>("one", null);

			Assert.IsType<Foo>(foo);
			Assert.Equal("one", foo.Name);
			Assert.Null(foo.Bar);
		}

		[Fact]
		public void SecondSignature_NullFirstParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var foo = creator.CreateInstance<Foo>(null, "one");

			Assert.IsType<Foo>(foo);
			Assert.Null(foo.Bar);
			Assert.Equal("one", foo.Name);
		}

		[Fact]
		public void SecondSignature_NullSecondParameter_CreatesInstance()
		{
			var creator = new ObjectCreator();
			var bar = new Bar();
			var foo = creator.CreateInstance<Foo>(bar, null);

			Assert.IsType<Foo>(foo);
			Assert.Equal(bar, foo.Bar);
			Assert.Null(foo.Name);
		}

		[Fact]
		public void FirstSignature_BothNullParameters_ThrowsAmbiguousMatchException()
		{
			var creator = new ObjectCreator();

			Assert.Throws<AmbiguousMatchException>(() => creator.CreateInstance<Foo>(null, null));
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

	
	public class CreateInstance_PrivateConstructor
	{
		[Fact]
		public void CreatesInstance()
		{
			var creator = new ObjectCreator();

			var foo = creator.CreateInstance<Foo>();

			Assert.IsType<Foo>(foo);
		}

		private class Foo
		{
			private Foo() { }
		}
	}
}
