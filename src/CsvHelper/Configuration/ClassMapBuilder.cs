// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com


#if !NET_2_0
using System;
using System.Linq.Expressions;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
    public class ClassMapBuilder<T>
    {
        private CsvPropertyMap currMap;

        private CsvClassMap<T> currClassMap;

        public ClassMapBuilder()
        {
            currClassMap = new BuilderClassMap<T>();
        }

        private sealed class BuilderClassMap<T> : CsvClassMap<T>{}

        public ClassMapBuilder<T> Map(Expression<Func<T, object>> expression)
        {
            currMap = currClassMap.Map(expression);
            return this;   
        }

        public ClassMapBuilder<T> TypeConvert(ITypeConverter t)
        {
            currMap.TypeConverter( t );
            return this;
        }

        public ClassMapBuilder<T> Index(int i)
        {
            currMap.NameIndex(i);
            return this;
        }

        public ClassMapBuilder<T> Name(string n)
        {
            currMap.Name( n );
            return this;
        }

        public ClassMapBuilder<T> NameIndex(int ni)
        {
            currMap.NameIndex(ni);
            return this;
        }

        public ClassMapBuilder<T> ConvertUsing(Func<ICsvReaderRow,T> t)
        {
            currMap.ConvertUsing( t );
            return this;
        }

        public ClassMapBuilder<T> Default(T d)
        {
            currMap.Default( d );
            return this;
        }

        public CsvClassMap<T> Build()
        {
            return currClassMap;
        }
    }
}
#endif