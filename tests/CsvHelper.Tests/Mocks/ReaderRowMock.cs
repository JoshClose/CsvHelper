// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mocks
{
	public class ReaderRowMock : IReaderRow
	{
		public string this[int index] => throw new NotImplementedException();

		public string this[string name] => throw new NotImplementedException();

		public string this[string name, int index] => throw new NotImplementedException();

		public int ColumnCount => throw new NotImplementedException();

		public int CurrentIndex => throw new NotImplementedException();

		public string[] HeaderRecord => throw new NotImplementedException();

		public IParser Parser => throw new NotImplementedException();

		public CsvContext Context => throw new NotImplementedException();

		public IReaderConfiguration Configuration { get; private set; }

		public ReaderRowMock()
		{
			Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
		}

		public ReaderRowMock(CsvConfiguration configuration)
		{
			Configuration = configuration;
		}

		public string GetField(int index)
		{
			throw new NotImplementedException();
		}

		public string GetField(string name)
		{
			throw new NotImplementedException();
		}

		public string GetField(string name, int index)
		{
			throw new NotImplementedException();
		}

		public object GetField(Type type, int index)
		{
			throw new NotImplementedException();
		}

		public object GetField(Type type, string name)
		{
			throw new NotImplementedException();
		}

		public object GetField(Type type, string name, int index)
		{
			throw new NotImplementedException();
		}

		public object GetField(Type type, int index, ITypeConverter converter)
		{
			throw new NotImplementedException();
		}

		public object GetField(Type type, string name, ITypeConverter converter)
		{
			throw new NotImplementedException();
		}

		public object GetField(Type type, string name, int index, ITypeConverter converter)
		{
			throw new NotImplementedException();
		}

		public T GetField<T>(int index)
		{
			throw new NotImplementedException();
		}

		public T GetField<T>(string name)
		{
			throw new NotImplementedException();
		}

		public T GetField<T>(string name, int index)
		{
			throw new NotImplementedException();
		}

		public T GetField<T>(int index, ITypeConverter converter)
		{
			throw new NotImplementedException();
		}

		public T GetField<T>(string name, ITypeConverter converter)
		{
			throw new NotImplementedException();
		}

		public T GetField<T>(string name, int index, ITypeConverter converter)
		{
			throw new NotImplementedException();
		}

		public T GetField<T, TConverter>(int index) where TConverter : ITypeConverter
		{
			throw new NotImplementedException();
		}

		public T GetField<T, TConverter>(string name) where TConverter : ITypeConverter
		{
			throw new NotImplementedException();
		}

		public T GetField<T, TConverter>(string name, int index) where TConverter : ITypeConverter
		{
			throw new NotImplementedException();
		}

		public T GetRecord<T>()
		{
			throw new NotImplementedException();
		}

		public T GetRecord<T>(T anonymousTypeDefinition)
		{
			throw new NotImplementedException();
		}

		public object GetRecord(Type type)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField(Type type, int index, out object field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField(Type type, string name, out object field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField(Type type, string name, int index, out object field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField(Type type, int index, ITypeConverter converter, out object field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField(Type type, string name, ITypeConverter converter, out object field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField(Type type, string name, int index, ITypeConverter converter, out object field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T>(int index, out T field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T>(string name, out T field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T>(string name, int index, out T field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T>(int index, ITypeConverter converter, out T field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T>(string name, ITypeConverter converter, out T field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T>(string name, int index, ITypeConverter converter, out T field)
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T, TConverter>(int index, out T field) where TConverter : ITypeConverter
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T, TConverter>(string name, out T field) where TConverter : ITypeConverter
		{
			throw new NotImplementedException();
		}

		public bool TryGetField<T, TConverter>(string name, int index, out T field) where TConverter : ITypeConverter
		{
			throw new NotImplementedException();
		}
	}
}
