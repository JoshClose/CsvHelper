using System;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.DocsGenerator
{
	public static class ConsoleHost
	{
		/// <summary>
		/// Block the calling thread until shutdown is triggered via Ctrl+C or SIGTERM.
		/// </summary>
		public static void WaitForShutdown()
		{
			WaitForShutdownAsync().GetAwaiter().GetResult();
		}

		/// <summary>
		/// Returns a Task that completes when shutdown is triggered via the given token, Ctrl+C or SIGTERM.
		/// </summary>
		/// <param name="token">The token to trigger shutdown.</param>
		public static async Task WaitForShutdownAsync(CancellationToken token = default(CancellationToken))
		{
			var done = new ManualResetEventSlim(false);
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
			{
				AttachCtrlcSigtermShutdown(cts, done, shutdownMessage: string.Empty);
				await WaitForTokenShutdownAsync(cts.Token);
				done.Set();
			}
		}

		/// <summary>
		/// Runs an application and block the calling thread until host shutdown.
		/// </summary>
		/// <param name="host">The <see cref="IWebHost"/> to run.</param>
		public static void Wait()
		{
			WaitAsync().GetAwaiter().GetResult();
		}

		/// <summary>
		/// Runs an application and returns a Task that only completes when the token is triggered or shutdown is triggered.
		/// </summary>
		/// <param name="host">The <see cref="IConsoleHost"/> to run.</param>
		/// <param name="token">The token to trigger shutdown.</param>
		public static async Task WaitAsync(CancellationToken token = default(CancellationToken))
		{
			//Wait for the token shutdown if it can be cancelled
			if (token.CanBeCanceled)
			{
				await WaitAsync(token, shutdownMessage: null);
				return;
			}

			//If token cannot be cancelled, attach Ctrl+C and SIGTERN shutdown
			var done = new ManualResetEventSlim(false);
			using (var cts = new CancellationTokenSource())
			{
				AttachCtrlcSigtermShutdown(cts, done, shutdownMessage: "Application is shutting down...");
				await WaitAsync(cts.Token, "Application running. Press Ctrl+C to shut down.");
				done.Set();
			}
		}

		private static async Task WaitAsync(CancellationToken token, string shutdownMessage)
		{
			if (!string.IsNullOrEmpty(shutdownMessage))
			{
				Console.WriteLine(shutdownMessage);
			}

			await WaitForTokenShutdownAsync(token);
		}

		private static void AttachCtrlcSigtermShutdown(CancellationTokenSource cts, ManualResetEventSlim resetEvent, string shutdownMessage)
		{
			Action ShutDown = () =>
			{
				if (!cts.IsCancellationRequested)
				{
					if (!string.IsNullOrWhiteSpace(shutdownMessage))
					{
						Console.WriteLine(shutdownMessage);
					}

					try
					{
						cts.Cancel();
					}
					catch (ObjectDisposedException) { }
				}

				// Wait on the given reset event
				resetEvent.Wait();
			};

			AppDomain.CurrentDomain.ProcessExit += delegate { ShutDown(); };
			Console.CancelKeyPress += (sender, eventArgs) =>
			{
				ShutDown();
				//Don't terminate the process immediately, wait for the Main thread to exit gracefully.
				eventArgs.Cancel = true;
			};
		}

		private static async Task WaitForTokenShutdownAsync(CancellationToken token)
		{
			var waitForStop = new TaskCompletionSource<object>();
			token.Register(obj =>
			{
				var tcs = (TaskCompletionSource<object>)obj;
				tcs.TrySetResult(null);
			}, waitForStop);
			await waitForStop.Task;
		}
	}
}
