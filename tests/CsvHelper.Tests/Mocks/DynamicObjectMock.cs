// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Dynamic;

namespace CsvHelper.Tests.Mocks
{
	public class DynamicObjectMock : DynamicObject
	{
		private Dictionary<string, object> dictionary = new Dictionary<string, object>();

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			return dictionary.TryGetValue(binder.Name, out result);
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			dictionary[binder.Name] = value;

			return true;
		}

		public override IEnumerable<string> GetDynamicMemberNames()
		{
			return dictionary.Keys;
		}
	}
}
