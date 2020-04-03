// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reflection
{
	[TestClass]
	public class ReflectionHelperTests
	{
		[TestMethod]
		public void CreateInstanceTests()
		{
			var test = ReflectionHelper.CreateInstance<Test>();

			Assert.IsNotNull(test);
			Assert.AreEqual("name", test.Name);

			test = (Test)ReflectionHelper.CreateInstance(typeof(Test));
			Assert.IsNotNull(test);
			Assert.AreEqual("name", test.Name);
		}

		[TestMethod]
		public void PrivateConstructorTest()
		{
			var c = ReflectionHelper.CreateInstance<PrivateConstructor>();

			Assert.IsNotNull(c);
		}

		[TestMethod]
		public void DifferentRunTimeTypesWithSameAssemblyQualifiedNameTest()
		{
			Type type1 = GenerateDynamicType();
			Type type2 = GenerateDynamicType();

			Debug.Assert(type1.AssemblyQualifiedName.Equals(type2.AssemblyQualifiedName, StringComparison.Ordinal), @"The two generated dynamic types should have same assembly qualified name.");
			Debug.Assert(type1.GetHashCode() != type2.GetHashCode(), @"The two generated dynamic types should have different hash codes.");

			var instance1 = ReflectionHelper.CreateInstance(type1);

			Assert.IsNotNull(instance1);
			Assert.IsInstanceOfType(instance1, type1);
			Assert.IsNotInstanceOfType(instance1, type2);

			var instance2 = ReflectionHelper.CreateInstance(type2);

			Assert.IsNotNull(instance2);
			Assert.IsInstanceOfType(instance2, type2);
			Assert.IsNotInstanceOfType(instance2, type1);

			instance1 = RunGenericCreateInstance(type1);

			Assert.IsNotNull(instance1);
			Assert.IsInstanceOfType(instance1, type1);
			Assert.IsNotInstanceOfType(instance1, type2);

			instance2 = RunGenericCreateInstance(type2);

			Assert.IsNotNull(instance2);
			Assert.IsInstanceOfType(instance2, type2);
			Assert.IsNotInstanceOfType(instance2, type1);
		}

		private static object RunGenericCreateInstance(Type type)
		{
			var methodInfo = typeof(ReflectionHelper)
				.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.FirstOrDefault(m => m.Name.Equals(@"CreateInstance", StringComparison.Ordinal) && m.IsGenericMethod)?
				.MakeGenericMethod(type);

			Debug.Assert(methodInfo != null, @"The generic method instance should not be null.");

			return methodInfo.Invoke(null, new[] { new object[0] });
		}

		private static Type GenerateDynamicType()
		{
			var assemblyName = new AssemblyName("DynamicAssemblyForCsvHelperTest");
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
			var typeBuilder = moduleBuilder.DefineType("DynamicTypeForCsvHelperTest", TypeAttributes.Public);

			return typeBuilder.CreateType();
		}

		private class Test
		{
			public string Name
			{
				get { return "name"; }
			}
		}

		private class PrivateConstructor
		{
			private PrivateConstructor() { }
		}
	}
}
