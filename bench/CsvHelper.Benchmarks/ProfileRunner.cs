using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Helpers for profiling with dotnet-trace:
///   profile &lt;Class.Method&gt; [seconds] [Name=Value ...]  runs one benchmark method in a loop
///   summarize &lt;file.speedscope.json&gt; [top] [under]   prints the hottest frames by self and inclusive time, only
///                                                        under a frame whose name contains <c>under</c> when given
/// </summary>
public static class ProfileRunner
{
	/// <summary>
	/// Runs one benchmark method in a loop for a time, after its class's setup, for a profiler to sample. The process is
	/// pinned first (<see cref="BenchAffinity"/>: <c>BENCH_AFFINITY</c>, or logical processor 4), and the cached workbooks
	/// are written if missing (<see cref="BenchWorkbooks.Ensure"/>). The line <c>setup done at HH:mm:ss.fff</c> marks where
	/// the loop starts, for a trace started after a delay.
	/// </summary>
	/// <param name="args">
	/// The method (<c>Class.Method</c>), then optionally the seconds to run it (10 when left out), then any
	/// <c>Name=Value</c> arguments, which set the class's properties (<see cref="SetParameters"/>).
	/// </param>
	/// <exception cref="ArgumentException">The class, the method or a property isn't found, or a value doesn't fit its property.</exception>
	/// <exception cref="FormatException">The seconds or a value isn't a number.</exception>
	public static void Profile(string[] args)
	{
		BenchAffinity.Apply(highPriority: false);
		BenchWorkbooks.Ensure();

		var (type, method) = FindMethod(args[0]);
		var seconds = args.Length > 1 && !args[1].Contains('=') ? double.Parse(args[1], CultureInfo.InvariantCulture) : 10;
		var instance = Activator.CreateInstance(type)!;
		SetParameters(instance, args.Skip(1));
		foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<GlobalSetupAttribute>() is not null))
		{
			setup.Invoke(instance, null);
		}

		Console.WriteLine($"setup done at {DateTime.Now:HH:mm:ss.fff}");

		var sw = Stopwatch.StartNew();
		var n = 0;
		while (sw.Elapsed.TotalSeconds < seconds)
		{
			var result = method.Invoke(instance, null);
			if (result is Task task)
			{
				task.GetAwaiter().GetResult();
			}

			n++;
		}

		Console.WriteLine($"{n} iterations, {sw.Elapsed.TotalMilliseconds / n:F3} ms each");
	}

	/// <summary>
	/// Sets a benchmark's properties from <c>Name=Value</c> arguments, such as <c>Strings=Inline</c>, before its setup runs,
	/// and prints each. Arguments without <c>=</c> are skipped. A <see cref="ParamsAttribute"/> property left out keeps the
	/// value its class gives it, not the first of its parameter values, and is printed as such.
	/// </summary>
	/// <param name="instance">The benchmark class's instance.</param>
	/// <param name="arguments">The arguments.</param>
	/// <exception cref="ArgumentException">An argument names a property the class doesn't have, or an enum value it doesn't have.</exception>
	/// <exception cref="FormatException">A value isn't of its property's type.</exception>
	public static void SetParameters(object instance, IEnumerable<string> arguments)
	{
		var type = instance.GetType();
		var given = new HashSet<string>(StringComparer.Ordinal);
		foreach (var argument in arguments.Where(a => a.Contains('=')))
		{
			var equals = argument.IndexOf('=');
			var name = argument[..equals];
			var property = type.GetProperty(name) ?? throw new ArgumentException($"{type.Name} has no property {name}.", nameof(arguments));
			var text = argument[(equals + 1)..];
			var value = property.PropertyType.IsEnum
				? Enum.Parse(property.PropertyType, text, ignoreCase: true)
				: Convert.ChangeType(text, property.PropertyType, CultureInfo.InvariantCulture);
			property.SetValue(instance, value);
			given.Add(property.Name);
			Console.WriteLine($"{property.Name} = {value}");
		}

		foreach (var property in type.GetProperties().Where(p => p.GetCustomAttribute<ParamsAttribute>() is not null && !given.Contains(p.Name)))
		{
			Console.WriteLine($"{property.Name} = {property.GetValue(instance)} (not given)");
		}
	}

	/// <summary>Returns the benchmark class of a name, in this assembly.</summary>
	/// <param name="name">The class's name, without its namespace.</param>
	/// <returns>The class.</returns>
	/// <exception cref="ArgumentException">No class, or more than one, has the name.</exception>
	public static Type FindType(string name)
	{
		var types = typeof(ProfileRunner).Assembly.GetTypes().Where(t => t.Name == name).ToList();
		if (types.Count != 1)
		{
			throw new ArgumentException($"{types.Count} classes are named {name}.", nameof(name));
		}

		return types[0];
	}

	/// <summary>
	/// Prints the hottest frames of a speedscope profile by self time and by inclusive time, as shares of the time counted:
	/// all the sampled time, or, given <paramref name="under"/>, only the time when a frame whose name contains it is on a
	/// thread's stack (the benchmark method, say, and not its setup). A frame's inclusive time counts once when it recurses.
	/// </summary>
	/// <param name="path">The speedscope file (dotnet-trace's <c>--format Speedscope</c>).</param>
	/// <param name="top">How many frames each list prints.</param>
	/// <param name="under">Part of a frame's name (ordinal), or <see langword="null"/> to count all the time.</param>
	public static void Summarize(string path, int top = 40, string? under = null)
	{
		using var doc = JsonDocument.Parse(File.ReadAllText(path));
		var frames = doc.RootElement.GetProperty("shared").GetProperty("frames").EnumerateArray()
			.Select(f => f.GetProperty("name").GetString() ?? "?").ToArray();
		var matches = frames.Select(f => under is null || f.Contains(under, StringComparison.Ordinal)).ToArray();
		var self = new Dictionary<int, double>();
		var total = new Dictionary<int, double>();
		var all = 0.0;
		foreach (var profile in doc.RootElement.GetProperty("profiles").EnumerateArray())
		{
			// Each open frame, with the time counted when it opened: its inclusive time is what is counted until it closes.
			var stack = new List<(int Frame, double Counted)>();
			var matching = 0;
			var last = 0.0;
			foreach (var e in profile.GetProperty("events").EnumerateArray())
			{
				var at = e.GetProperty("at").GetDouble();
				var frame = e.GetProperty("frame").GetInt32();
				if (stack.Count > 0 && matching > 0)
				{
					var dt = at - last;

					// Attribute the synthetic CPU_TIME leaf to the method that was running.
					var leaf = stack[^1].Frame;
					if (frames[leaf] is "CPU_TIME" or "UNMANAGED_CODE_TIME" && stack.Count > 1)
					{
						leaf = stack[^2].Frame;
					}

					self[leaf] = self.GetValueOrDefault(leaf) + dt;
					all += dt;
				}

				last = at;
				if (e.GetProperty("type").GetString() == "O")
				{
					stack.Add((frame, all));
					if (matches[frame])
					{
						matching++;
					}
				}
				else
				{
					var i = stack.FindLastIndex(s => s.Frame == frame);
					if (i >= 0)
					{
						// Inclusive time, counted once per frame even when it recurses.
						if (!stack.Take(i).Any(s => s.Frame == frame))
						{
							total[frame] = total.GetValueOrDefault(frame) + (all - stack[i].Counted);
						}

						matching -= stack.Skip(i).Count(s => matches[s.Frame]);
						stack.RemoveRange(i, stack.Count - i);
					}
				}
			}
		}

		Console.WriteLine(under is null ? $"Total sampled time: {all:F0} ms" : $"Time under '{under}': {all:F0} ms");
		Console.WriteLine("Self time:");
		foreach (var (frame, t) in self.OrderByDescending(kv => kv.Value).Take(top).Select(kv => (kv.Key, kv.Value)))
		{
			Console.WriteLine($"  {t / all * 100,6:F2}%  {Trim(frames[frame])}");
		}

		Console.WriteLine("Inclusive time:");
		// Under a frame, frames that only ran outside it have no time: they're left out.
		foreach (var (frame, t) in total.Where(kv => under is null || kv.Value > 0).OrderByDescending(kv => kv.Value).Take(top).Select(kv => (kv.Key, kv.Value)))
		{
			Console.WriteLine($"  {t / all * 100,6:F2}%  {Trim(frames[frame])}");
		}
	}

	/// <summary>Returns a benchmark method by its <c>Class.Method</c> name.</summary>
	/// <param name="name">The class's name and the method's, joined by a dot.</param>
	/// <returns>The class and the method.</returns>
	/// <exception cref="ArgumentException">The name isn't <c>Class.Method</c>, or the class or the method isn't found.</exception>
	private static (Type Type, MethodInfo Method) FindMethod(string name)
	{
		var parts = name.Split('.');
		if (parts.Length != 2)
		{
			throw new ArgumentException($"'{name}' isn't Class.Method.", nameof(name));
		}

		var type = FindType(parts[0]);
		var method = type.GetMethod(parts[1]) ?? throw new ArgumentException($"{type.Name} has no method {parts[1]}.", nameof(name));

		return (type, method);
	}

	/// <summary>Shortens a frame's name to 160 characters.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The name, or its first 160 characters.</returns>
	private static string Trim(string name) => name.Length > 160 ? name[..160] : name;
}
