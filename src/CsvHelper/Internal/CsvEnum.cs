using System;
using System.Collections.Generic;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CsvHelper;

/// <summary>
/// Parses UTF-8 fields as the enum <typeparamref name="T"/>, as the generated mappers of <see cref="CsvRecordAttribute"/>
/// types read enum members: the names of its members and their <see cref="CsvEnumNameAttribute"/> names are matched in
/// UTF-8, ignoring ASCII case unless two names of different members differ only in case, and anything else, such as a
/// number or a list of flags, goes to <c>Enum.TryParse</c>, ignoring case.
/// </summary>
/// <typeparam name="T">The enum type (checked by the callers on .NET Framework and .NET Standard).</typeparam>
#if NET
internal static class CsvEnum<[DynamicallyAccessedMembers(CsvEnumExtensions.EnumMembers)] T> where T : struct, Enum
#else
internal static class CsvEnum<T>
#endif
{
	// The UTF-8 names, the members they name, and whether they are matched with case.
	private static readonly byte[][] names;
	private static readonly T[] values;
	private static readonly bool exact;
#if !NET
	// Enum.TryParse<T>(string, bool, out T), whose struct constraint T does not have on these targets.
	private static readonly StringTryParser tryParse = (StringTryParser)Delegate.CreateDelegate(typeof(StringTryParser),
		Array.Find(typeof(Enum).GetMethods(BindingFlags.Public | BindingFlags.Static), IsStringTryParse)!.MakeGenericMethod(typeof(T)));
#endif

	/// <summary>Reads the names of the members, in the order they are declared, each followed by its other names.</summary>
	static CsvEnum()
	{
		var texts = new List<byte[]>();
		var members = new List<T>();
		var named = new List<string>();
		foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
		{
#if NET
			var value = Enum.Parse<T>(field.Name);
#else
			var value = (T)Enum.Parse(typeof(T), field.Name);
#endif
			texts.Add(Encoding.UTF8.GetBytes(field.Name));
			members.Add(value);
			named.Add(field.Name);
			if (field.GetCustomAttribute<CsvEnumNameAttribute>() is not { } attribute)
			{
				continue;
			}

			// A name that a member already has stays that member's.
			foreach (var alias in attribute.Names)
			{
				var bytes = Encoding.UTF8.GetBytes(alias);
				if (!texts.Exists(t => t.AsSpan().SequenceEqual(bytes)))
				{
					texts.Add(bytes);
					members.Add(value);
					named.Add(field.Name);
				}
			}
		}

		names = texts.ToArray();
		values = members.ToArray();
		for (var i = 0; i < names.Length && !exact; i++)
		{
			for (var j = i + 1; j < names.Length; j++)
			{
				if (named[i] != named[j] && Same(names[i], names[j], exact: false))
				{
					exact = true;

					break;
				}
			}
		}
	}

	/// <summary>Parses <paramref name="s"/> as a member of <typeparamref name="T"/>.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="hide">Whether the message of the exception leaves the value out.</param>
	/// <returns>The member.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a value of <typeparamref name="T"/>.</exception>
	public static T Parse(ReadOnlySpan<byte> s, bool hide) => TryParse(s, out var value) ? value : throw NotValue(s, hide);

	/// <summary>Tries to parse <paramref name="s"/> as a member of <typeparamref name="T"/>, as <see cref="Parse"/> parses it.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="value">The member, or the default of <typeparamref name="T"/> when <paramref name="s"/> is not a value of it.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryParse(ReadOnlySpan<byte> s, out T value)
	{
		for (var i = 0; i < names.Length; i++)
		{
			if (Same(s, names[i], exact))
			{
				value = values[i];

				return true;
			}
		}

		return TryParseText(s, out value);
	}

	/// <summary>Whether <paramref name="s"/> is <paramref name="name"/>, ignoring the case of ASCII letters unless <paramref name="exact"/>.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="name">A UTF-8 name.</param>
	/// <param name="exact">Whether case matters.</param>
	/// <returns><see langword="true"/> when they are the same.</returns>
	private static bool Same(ReadOnlySpan<byte> s, byte[] name, bool exact)
	{
		if (s.Length != name.Length)
		{
			return false;
		}

		if (exact)
		{
			return s.SequenceEqual(name);
		}

		for (var i = 0; i < s.Length; i++)
		{
			var a = s[i];
			var b = name[i];
			if (a != b && ((a | 0x20) != (b | 0x20) || (uint)((a | 0x20) - 'a') > 'z' - 'a'))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> with <c>Enum.TryParse</c>, ignoring case: numbers, lists of flags, and names
	/// with surrounding whitespace. On .NET a field of at most 128 bytes is decoded on the stack.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="value">The parsed value, or the default of <typeparamref name="T"/> when parsing fails.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool TryParseText(ReadOnlySpan<byte> s, out T value)
	{
#if NET
		if (s.Length <= 128)
		{
			Span<char> chars = stackalloc char[128];
			var n = Encoding.UTF8.GetChars(s, chars);

			return Enum.TryParse(chars.Slice(0, n), true, out value);
		}

		return Enum.TryParse(Encoding.UTF8.GetString(s), true, out value);
#else
		return tryParse(Encoding.UTF8.GetString(s), true, out value);
#endif
	}

	/// <summary>The exception for a field that is not a value of <typeparamref name="T"/>.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="hide">Whether the message leaves the value out.</param>
	/// <returns>The exception to throw.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static FormatException NotValue(ReadOnlySpan<byte> s, bool hide)
	{
		var type = typeof(T).FullName!.Replace('+', '.');

		return new FormatException(hide
			? $"The field is not a value of {type} (IncludeValuesInExceptions is off, so its value is not shown)."
			: $"'{Encoding.UTF8.GetString(s)}' is not a value of {type}.");
	}

#if !NET
	/// <summary>Whether <paramref name="method"/> is <c>Enum.TryParse&lt;TEnum&gt;(string, bool, out TEnum)</c>.</summary>
	/// <param name="method">A public static method of <see cref="Enum"/>.</param>
	/// <returns><see langword="true"/> when it is that method.</returns>
	private static bool IsStringTryParse(MethodInfo method)
	{
		var parameters = method.GetParameters();

		return method.Name == nameof(Enum.TryParse) && method.IsGenericMethodDefinition && parameters.Length == 3 &&
			   parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(bool);
	}

	/// <summary>The signature of <c>Enum.TryParse&lt;T&gt;(string, bool, out T)</c>.</summary>
	/// <param name="text">The text to parse.</param>
	/// <param name="ignoreCase">Whether to ignore case.</param>
	/// <param name="value">The parsed value.</param>
	/// <returns>Whether <paramref name="text"/> could be parsed.</returns>
	private delegate bool StringTryParser(string text, bool ignoreCase, out T value);
#endif
}
