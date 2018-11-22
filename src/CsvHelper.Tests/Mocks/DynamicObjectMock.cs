using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
