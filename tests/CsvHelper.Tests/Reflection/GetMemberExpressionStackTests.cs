﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Xunit;

namespace CsvHelper.Tests.Reflection
{

	public class GetPropertiesTests
	{
		[Fact]
		public void FirstLevelTest()
		{
			var stack = ReflectionHelper.GetMembers<A, string>(a => a.P1);

			Assert.Single(stack);
			Assert.Equal("P1", stack.Pop().Name);
		}

		[Fact]
		public void LastLevelTest()
		{
			var stack = ReflectionHelper.GetMembers<A, string>(a => a.B.C.D.P4);

			Assert.Equal(4, stack.Count);
			Assert.Equal("B", stack.Pop().Name);
			Assert.Equal("C", stack.Pop().Name);
			Assert.Equal("D", stack.Pop().Name);
			Assert.Equal("P4", stack.Pop().Name);
		}

		private class A
		{
			public string? P1 { get; set; }
			public B B { get; set; } = new B();
		}

		private class B
		{
			public string? P2 { get; set; }
			public C C { get; set; } = new C();
		}

		private class C
		{
			public string? P3 { get; set; }
			public D D { get; set; } = new D();
		}

		private class D
		{
			public string? P4 { get; set; }
		}
	}
}
