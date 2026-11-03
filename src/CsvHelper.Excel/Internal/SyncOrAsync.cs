using System;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Gets the results of code written once for both reading modes, with an <c>async</c> flag: called with
/// <see langword="false"/>, it reads only synchronously, so its task has completed when it returns.
/// </summary>
internal static class SyncOrAsync
{
	/// <summary>The result of a task that ran synchronously.</summary>
	/// <typeparam name="T">The type of the result.</typeparam>
	/// <param name="task">The task, which has completed.</param>
	/// <returns>Its result; its exception is rethrown.</returns>
	/// <exception cref="InvalidOperationException">The task has not completed: synchronous code awaited something asynchronous.</exception>
	public static T Result<T>(ValueTask<T> task)
	{
		if (!task.IsCompleted)
		{
			throw new InvalidOperationException("A synchronous read awaited an asynchronous one.");
		}

		return task.GetAwaiter().GetResult();
	}

	/// <summary>Completes a task that ran synchronously.</summary>
	/// <param name="task">The task, which has completed.</param>
	/// <exception cref="InvalidOperationException">The task has not completed: synchronous code awaited something asynchronous.</exception>
	public static void Wait(ValueTask task)
	{
		if (!task.IsCompleted)
		{
			throw new InvalidOperationException("A synchronous read awaited an asynchronous one.");
		}

		task.GetAwaiter().GetResult();
	}
}
