using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Helpers for profiling with dotnet-trace:
///   profile &lt;Class.Method&gt; [seconds]   runs one benchmark method in a loop
///   summarize &lt;file.speedscope.json&gt;  prints the hottest frames by self time
/// </summary>
public static class ProfileRunner
{
	public static void Profile(string[] args)
	{
		var parts = args[0].Split('.');
		var type = typeof(ProfileRunner).Assembly.GetTypes().Single(t => t.Name == parts[0]);
		var method = type.GetMethod(parts[1])!;
		var seconds = args.Length > 1 ? double.Parse(args[1]) : 10;
		var instance = Activator.CreateInstance(type)!;
		foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<GlobalSetupAttribute>() is not null))
			setup.Invoke(instance, null);
		var sw = Stopwatch.StartNew();
		var n = 0;
		while (sw.Elapsed.TotalSeconds < seconds)
		{
			var result = method.Invoke(instance, null);
			if (result is Task task) task.GetAwaiter().GetResult();
			n++;
		}

		Console.WriteLine($"{n} iterations, {sw.Elapsed.TotalMilliseconds / n:F3} ms each");
	}

	public static void Summarize(string path, int top = 40)
	{
		using var doc = JsonDocument.Parse(File.ReadAllText(path));
		var frames = doc.RootElement.GetProperty("shared").GetProperty("frames").EnumerateArray()
			.Select(f => f.GetProperty("name").GetString() ?? "?").ToArray();
		var self = new Dictionary<int, double>();
		var total = new Dictionary<int, double>();
		var all = 0.0;
		foreach (var profile in doc.RootElement.GetProperty("profiles").EnumerateArray())
		{
			var stack = new List<(int Frame, double At)>();
			var last = 0.0;
			foreach (var e in profile.GetProperty("events").EnumerateArray())
			{
				var at = e.GetProperty("at").GetDouble();
				var frame = e.GetProperty("frame").GetInt32();
				if (stack.Count > 0)
				{
					var dt = at - last;
					// Attribute the synthetic CPU_TIME leaf to the method that was running.
					var top0 = stack[^1].Frame;
					if (frames[top0] is "CPU_TIME" or "UNMANAGED_CODE_TIME" && stack.Count > 1) top0 = stack[^2].Frame;
					self[top0] = self.GetValueOrDefault(top0) + dt;
					all += dt;
				}

				last = at;
				if (e.GetProperty("type").GetString() == "O")
				{
					stack.Add((frame, at));
				}
				else
				{
					var i = stack.FindLastIndex(s => s.Frame == frame);
					if (i >= 0)
					{
						// Inclusive time, counted once per frame even when it recurses.
						if (!stack.Take(i).Any(s => s.Frame == frame))
							total[frame] = total.GetValueOrDefault(frame) + (at - stack[i].At);
						stack.RemoveRange(i, stack.Count - i);
					}
				}
			}
		}

		Console.WriteLine($"Total sampled time: {all:F0} ms");
		Console.WriteLine("Self time:");
		foreach (var (frame, t) in self.OrderByDescending(kv => kv.Value).Take(top).Select(kv => (kv.Key, kv.Value)))
			Console.WriteLine($"  {t / all * 100,6:F2}%  {Trim(frames[frame])}");
		Console.WriteLine("Inclusive time:");
		foreach (var (frame, t) in total.OrderByDescending(kv => kv.Value).Take(top).Select(kv => (kv.Key, kv.Value)))
			Console.WriteLine($"  {t / all * 100,6:F2}%  {Trim(frames[frame])}");
	}

	private static string Trim(string name) => name.Length > 160 ? name[..160] : name;
}
