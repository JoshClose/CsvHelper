// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace CsvHelper
{
	/// <summary>
	/// Common reflection tasks.
	/// </summary>
	internal static class ReflectionHelper
	{
		/// <summary>
		/// Gets the <see cref="PropertyInfo"/> from the type where the property was declared.
		/// </summary>
		/// <param name="type">The type the property belongs to.</param>
		/// <param name="property">The property to search.</param>
		/// <param name="flags">Flags for how the property is retrieved.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PropertyInfo GetDeclaringProperty(Type type, PropertyInfo property, BindingFlags flags)
		{
			if (property.DeclaringType != type)
			{
				var declaringProperty = property.DeclaringType.GetProperty(property.Name, flags);
				return GetDeclaringProperty(property.DeclaringType, declaringProperty, flags);
			}

			return property;
		}

		/// <summary>
		/// Gets the <see cref="FieldInfo"/> from the type where the field was declared.
		/// </summary>
		/// <param name="type">The type the field belongs to.</param>
		/// <param name="field">The field to search.</param>
		/// <param name="flags">Flags for how the field is retrieved.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FieldInfo GetDeclaringField(Type type, FieldInfo field, BindingFlags flags)
		{
			if (field.DeclaringType != type)
			{
				var declaringField = field.DeclaringType.GetField(field.Name, flags);
				return GetDeclaringField(field.DeclaringType, declaringField, flags);
			}

			return field;
		}

		/// <summary>
		/// Walk up the inheritance tree collecting properties. This will get a unique set of properties in the
		/// case where parents have the same property names as children.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> to get properties for.</param>
		/// <param name="flags">The flags for getting the properties.</param>
		/// <param name="overwrite">If true, parent class properties that are hidden by `new` child properties will be overwritten.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<PropertyInfo> GetUniqueProperties(Type type, BindingFlags flags, bool overwrite = false)
		{
			var ignoreBase = type.GetCustomAttribute(typeof(IgnoreBaseAttribute)) != null;

			var properties = new Dictionary<string, PropertyInfo>();

			flags |= BindingFlags.DeclaredOnly;
			var currentType = type;
			while (currentType != null)
			{
				var currentProperties = currentType.GetProperties(flags);
				foreach (var property in currentProperties)
				{
					if (!properties.ContainsKey(property.Name) || overwrite)
					{
						properties[property.Name] = property;
					}
				}

				if (ignoreBase)
				{
					break;
				}

				currentType = currentType.BaseType;
			}

			return properties.Values.ToList();
		}

		/// <summary>
		/// Walk up the inheritance tree collecting fields. This will get a unique set of fields in the
		/// case where parents have the same field names as children.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> to get fields for.</param>
		/// <param name="flags">The flags for getting the fields.</param>
		/// <param name="overwrite">If true, parent class fields that are hidden by `new` child fields will be overwritten.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<FieldInfo> GetUniqueFields(Type type, BindingFlags flags, bool overwrite = false)
		{
			var ignoreBase = type.GetCustomAttribute(typeof(IgnoreBaseAttribute)) != null;

			var fields = new Dictionary<string, FieldInfo>();

			flags |= BindingFlags.DeclaredOnly;
			var currentType = type;
			while (currentType != null)
			{
				var currentFields = currentType.GetFields(flags);
				foreach (var field in currentFields)
				{
					if (!fields.ContainsKey(field.Name) || overwrite)
					{
						fields[field.Name] = field;
					}
				}

				if (ignoreBase)
				{
					break;
				}

				currentType = currentType.BaseType;
			}

			return fields.Values.ToList();
		}

		/// <summary>
		/// Gets the property from the expression.
		/// </summary>
		/// <typeparam name="TModel">The type of the model.</typeparam>
		/// <typeparam name="TProperty">The type of the property.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns>The <see cref="PropertyInfo"/> for the expression.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MemberInfo GetMember<TModel, TProperty>(Expression<Func<TModel, TProperty>> expression)
		{
			var member = GetMemberExpression(expression.Body).Member;
			var property = member as PropertyInfo;
			if (property != null)
			{
				return property;
			}

			var field = member as FieldInfo;
			if (field != null)
			{
				return field;
			}

			throw new ConfigurationException($"'{member.Name}' is not a member.");
		}

		/// <summary>
		/// Gets the member inheritance chain as a stack.
		/// </summary>
		/// <typeparam name="TModel">The type of the model.</typeparam>
		/// <typeparam name="TProperty">The type of the property.</typeparam>
		/// <param name="expression">The member expression.</param>
		/// <returns>The inheritance chain for the given member expression as a stack.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Stack<MemberInfo> GetMembers<TModel, TProperty>(Expression<Func<TModel, TProperty>> expression)
		{
			var stack = new Stack<MemberInfo>();

			var currentExpression = expression.Body;
			while (true)
			{
				var memberExpression = GetMemberExpression(currentExpression);
				if (memberExpression == null)
				{
					break;
				}

				stack.Push(memberExpression.Member);
				currentExpression = memberExpression.Expression;
			}

			return stack;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static MemberExpression? GetMemberExpression(Expression expression)
		{
			MemberExpression? memberExpression = null;
			if (expression.NodeType == ExpressionType.Convert)
			{
				var body = (UnaryExpression)expression;
				memberExpression = body.Operand as MemberExpression;
			}
			else if (expression.NodeType == ExpressionType.MemberAccess)
			{
				memberExpression = expression as MemberExpression;
			}

			return memberExpression;
		}
	}
}
