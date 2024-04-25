using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper;

internal class FastDynamicObject : IDictionary<string, object>, IDynamicMetaObjectProvider
{
	private readonly Dictionary<string, object> dict;

	public FastDynamicObject()
	{
		dict = new Dictionary<string, object>();
	}

	object IDictionary<string, object>.this[string key]
	{
		get
		{
			if (!dict.ContainsKey(key))
			{
				throw new CsvHelperException($"{nameof(FastDynamicObject)} does not contain a definition for '{key}'.");
			}

			return dict[key];
		}

		set
		{
			dict[key] = value;
		}
	}

	object SetValue(string name, object value)
	{
		dict[name] = value;

		return value;
	}

	ICollection<string> IDictionary<string, object>.Keys => throw new NotSupportedException();

	ICollection<object> IDictionary<string, object>.Values => throw new NotSupportedException();

	int ICollection<KeyValuePair<string, object>>.Count => throw new NotSupportedException();

	bool ICollection<KeyValuePair<string, object>>.IsReadOnly => throw new NotSupportedException();

	DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
	{
		return new FastDynamicMetaObject(parameter, BindingRestrictions.Empty, this);
	}

	void IDictionary<string, object>.Add(string key, object value)
	{
		throw new NotSupportedException();
	}

	void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
	{
		throw new NotSupportedException();
	}

	void ICollection<KeyValuePair<string, object>>.Clear()
	{
		throw new NotSupportedException();
	}

	bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
	{
		throw new NotSupportedException();
	}

	bool IDictionary<string, object>.ContainsKey(string key)
	{
		throw new NotSupportedException();
	}

	void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
	{
		throw new NotSupportedException();
	}

	IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
	{
		throw new NotSupportedException();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		throw new NotSupportedException();
	}

	bool IDictionary<string, object>.Remove(string key)
	{
		throw new NotSupportedException();
	}

	bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
	{
		throw new NotSupportedException();
	}

	bool IDictionary<string, object>.TryGetValue(string key, out object value)
	{
		throw new NotSupportedException();
	}

	private class FastDynamicMetaObject : DynamicMetaObject
	{
		private static readonly MethodInfo getValueMethod = typeof(IDictionary<string, object>).GetProperty("Item")!.GetGetMethod()!;
		private static readonly MethodInfo setValueMethod = typeof(FastDynamicObject).GetMethod("SetValue", BindingFlags.NonPublic | BindingFlags.Instance)!;

		public FastDynamicMetaObject(Expression expression, BindingRestrictions restrictions) : base(expression, restrictions) { }

		public FastDynamicMetaObject(Expression expression, BindingRestrictions restrictions, object value) : base(expression, restrictions, value) { }

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
		{
			var parameters = new Expression[] { Expression.Constant(binder.Name) };

			var callMethod = CallMethod(getValueMethod, parameters);

			return callMethod;
		}

		public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
		{
			var parameters = new Expression[] { Expression.Constant(binder.Name), Expression.Convert(value.Expression, typeof(object)) };

			var callMethod = CallMethod(setValueMethod, parameters);

			return callMethod;
		}

		public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
		{
			var parameters = new Expression[] { Expression.Constant(binder.Name) };

			var callMethod = CallMethod(getValueMethod, parameters);

			return callMethod;
		}

		public override IEnumerable<string> GetDynamicMemberNames()
		{
			if (HasValue && Value is IDictionary<string, object> lookup)
			{
				return lookup.Keys;
			}

			return Array.Empty<string>();
		}

		private DynamicMetaObject CallMethod(MethodInfo method, Expression[] parameters)
		{
			var callMethod = new DynamicMetaObject(Expression.Call(Expression.Convert(Expression, LimitType), method, parameters), BindingRestrictions.GetTypeRestriction(Expression, LimitType));

			return callMethod;
		}
	}
}
