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

namespace CsvHelper
{
	/// <summary>
	/// Efficiently creates instances of object types.
	/// </summary>
	public class ObjectCreator
	{
		private readonly Dictionary<int, Func<object[], object>> cache = new Dictionary<int, Func<object[], object>>();

		/// <summary>
		/// Creates an instance of type T using the given arguments.
		/// </summary>
		/// <typeparam name="T">The type to create an instance of.</typeparam>
		/// <param name="args">The constrcutor arguments.</param>
		public T CreateInstance<T>(params object[] args)
		{
			return (T)CreateInstance(typeof(T), args);
		}

		/// <summary>
		/// Creates an instance of the given type using the given arguments.
		/// </summary>
		/// <param name="type">The type to create an instance of.</param>
		/// <param name="args">The constructor arguments.</param>
		public object CreateInstance(Type type, params object[] args)
		{
			var func = GetFunc(type, args);

			return func(args);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Func<object[], object> GetFunc(Type type, object[] args)
		{
			var argTypes = GetArgTypes(args);
			var key = GetConstructorCacheKey(type, argTypes);
			if (!cache.TryGetValue(key, out var func))
			{
				cache[key] = func = CreateInstanceFunc(type, argTypes);
			}

			return func;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Type[] GetArgTypes(object[] args)
		{
			var argTypes = new Type[args.Length];
			for (var i = 0; i < args.Length; i++)
			{
				argTypes[i] = args[i]?.GetType() ?? typeof(object);
			}

			return argTypes;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetConstructorCacheKey(Type type, Type[] args)
		{
			var hashCode = new HashCode();
			hashCode.Add(type.GetHashCode());
			for (var i = 0; i < args.Length; i++)
			{
				hashCode.Add(args[i].GetHashCode());
			}

			return hashCode.ToHashCode();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Func<object[], object> CreateInstanceFunc(Type type, Type[] argTypes)
		{
			var parameterExpression = Expression.Parameter(typeof(object[]), "args");

			Expression body;
			if (type.IsValueType)
			{
				if (argTypes.Length > 0)
				{
					throw GetConstructorNotFoundException(type, argTypes);
				}

				body = Expression.Convert(Expression.Default(type), typeof(object));
			}
			else
			{
				var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				var constructor = GetConstructor(constructors, type, argTypes);

				var parameters = constructor.GetParameters();
				var parameterTypes = new Type[parameters.Length];
				for (var i = 0; i < parameters.Length; i++)
				{
					parameterTypes[i] = parameters[i].ParameterType;
				}

				var arguments = new List<Expression>();
				for (var i = 0; i < parameterTypes.Length; i++)
				{
					var parameterType = parameterTypes[i];
					var arrayIndexExpression = Expression.ArrayIndex(parameterExpression, Expression.Constant(i));
					var convertExpression = Expression.Convert(arrayIndexExpression, parameterType);
					arguments.Add(convertExpression);
				}

				body = Expression.New(constructor, arguments);
			}

			var lambda = Expression.Lambda<Func<object[], object>>(body, new[] { parameterExpression });
			var func = lambda.Compile();

			return func;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static ConstructorInfo GetConstructor(ConstructorInfo[] constructors, Type type, Type[] argTypes)
		{
			var matchType = MatchType.Exact;
			var fuzzyMatches = new List<ConstructorInfo>();
			for (var i = 0; i < constructors.Length; i++)
			{
				var constructor = constructors[i];
				var parameters = constructors[i].GetParameters();

				if (parameters.Length != argTypes.Length)
				{
					continue;
				}

				for (var j = 0; j < parameters.Length && j < argTypes.Length; j++)
				{
					var parameterType = parameters[j].ParameterType;
					var argType = argTypes[j];

					if (argType == parameterType)
					{
						matchType = MatchType.Exact;
						continue;
					}

					if (!parameterType.IsValueType && (parameterType.IsAssignableFrom(argType) || argType == typeof(object)))
					{
						matchType = MatchType.Fuzzy;
						continue;
					}

					matchType = MatchType.None;
					break;
				}

				if (matchType == MatchType.Exact)
				{
					// Only possible to have one exact match.
					return constructor;
				}

				if (matchType == MatchType.Fuzzy)
				{
					fuzzyMatches.Add(constructor);
				}
			}

			if (fuzzyMatches.Count == 1)
			{
				return fuzzyMatches[0];
			}

			if (fuzzyMatches.Count > 1)
			{
				throw new AmbiguousMatchException();
			}

			throw GetConstructorNotFoundException(type, argTypes);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static MissingMethodException GetConstructorNotFoundException(Type type, Type[] argTypes)
		{
			var signature = $"{type.FullName}({string.Join(", ", argTypes.Select(a => a.FullName))})";

			throw new MissingMethodException($"Constructor '{signature}' was not found.");
		}

		private enum MatchType
		{
			None = 0,
			Exact = 1,
			Fuzzy = 2
		}
	}
}
