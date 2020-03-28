// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Common reflection tasks.
	/// </summary>
	internal static class ReflectionHelper
	{
		private static readonly Dictionary<int, Dictionary<int, Delegate>> funcArgCache = new Dictionary<int, Dictionary<int, Delegate>>();
		private static object locker = new object();

		/// <summary>
		/// Creates an instance of type T using the current <see cref="IObjectResolver"/>.
		/// </summary>
		/// <typeparam name="T">The type of instance to create.</typeparam>
		/// <param name="args">The constructor arguments.</param>
		/// <returns>A new instance of type T.</returns>
		public static T CreateInstance<T>(params object[] args)
		{
			return (T)CreateInstance(typeof(T), args);
		}

		/// <summary>
		/// Creates an instance of the specified type using the current <see cref="IObjectResolver"/>.
		/// </summary>
		/// <param name="type">The type of instance to create.</param>
		/// <param name="args">The constructor arguments.</param>
		/// <returns>A new instance of the specified type.</returns>
		public static object CreateInstance(Type type, params object[] args)
		{
			return ObjectResolver.Current.Resolve(type, args);
		}

		/// <summary>
		/// Creates an instance of the specified type without using the
		/// current <see cref="IObjectResolver"/>.
		/// </summary>
		/// <param name="type">The type of instance to create.</param>
		/// <param name="args">The constructor arguments.</param>
		/// <returns>A new instance of the specified type.</returns>
		public static object CreateInstanceWithoutContractResolver(Type type, params object[] args)
		{
			Dictionary<int, Delegate> funcCache;
			lock (locker)
			{
				if (!funcArgCache.TryGetValue(args.Length, out funcCache))
				{
					funcArgCache[args.Length] = funcCache = new Dictionary<int, Delegate>();
				}
			}

			var typeHashCodes =
				new List<Type> { type }
				.Union(args.Select(a => a.GetType()))
				.Select(t => t.UnderlyingSystemType.GetHashCode());
			var key = string.Join("|", typeHashCodes).GetHashCode();

			Delegate func;
			lock (locker)
			{
				if (!funcCache.TryGetValue(key, out func))
				{
					funcCache[key] = func = CreateInstanceDelegate(type, args);
				}
			}

			try
			{
				return func.DynamicInvoke(args);
			}
			catch (TargetInvocationException ex)
			{
				throw ex.InnerException;
			}
		}

		/// <summary>
		/// Gets the <see cref="PropertyInfo"/> from the type where the property was declared.
		/// </summary>
		/// <param name="type">The type the property belongs to.</param>
		/// <param name="property">The property to search.</param>
		/// <param name="flags">Flags for how the property is retrieved.</param>
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
		/// Walk up the inheritance tree collecting properties. This will get a unique set or properties in the
		/// case where parents have the same property names as children.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> to get properties for.</param>
		/// <param name="flags">The flags for getting the properties.</param>
		/// <param name="overwrite">If true, parent class properties that are hidden by `new` child properties will be overwritten.</param>
		public static List<PropertyInfo> GetUniqueProperties(Type type, BindingFlags flags, bool overwrite = false)
		{
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

				currentType = currentType.BaseType;
			}

			return properties.Values.ToList();
		}

		/// <summary>
		/// Gets the property from the expression.
		/// </summary>
		/// <typeparam name="TModel">The type of the model.</typeparam>
		/// <typeparam name="TProperty">The type of the property.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns>The <see cref="PropertyInfo"/> for the expression.</returns>
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

		private static MemberExpression GetMemberExpression(Expression expression)
		{
			MemberExpression memberExpression = null;
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

		private static T Default<T>()
		{
			return default(T);
		}

		private static Delegate CreateInstanceDelegate(Type type, params object[] args)
		{
			Delegate compiled;
			if (type.GetTypeInfo().IsValueType)
			{
				var method = typeof(ReflectionHelper).GetMethod("Default", BindingFlags.Static | BindingFlags.NonPublic);
				method = method.MakeGenericMethod(type);
				compiled = Expression.Lambda(Expression.Call(method)).Compile();
			}
			else
			{
				var argumentTypes = args.Select(a => a.GetType()).ToArray();
				var argumentExpressions = argumentTypes.Select((t, i) => Expression.Parameter(t, "var" + i)).ToArray();
				var constructorInfo = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, argumentTypes, null);
				if (constructorInfo == null)
				{
					throw new InvalidOperationException("No public parameterless constructor found.");
				}

				var constructor = Expression.New(constructorInfo, argumentExpressions);
				compiled = Expression.Lambda(constructor, argumentExpressions).Compile();
			}

			return compiled;
		}
	}
}
