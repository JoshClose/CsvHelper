using System;
using System.Collections.Generic;

namespace CsvHelper.Docs;

/// <summary>Console output. Warnings are collected: in strict mode any warning fails the build.</summary>
internal static class Log
{
	private static readonly List<string> warnings = [];

	public static IReadOnlyList<string> Warnings => warnings;

	public static void Info(string message) => Console.WriteLine(message);

	public static void Warn(string message)
	{
		lock (warnings) warnings.Add(message);
		var color = Console.ForegroundColor;
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.WriteLine($"warning: {message}");
		Console.ForegroundColor = color;
	}
}
