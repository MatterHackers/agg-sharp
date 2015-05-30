using System;
using System.Threading;

namespace MatterHackers.Agg
{
#if DEBUG

	public class TimedLock : IDisposable
#else
public struct TimedLock : IDisposable
#endif
	{
		private static readonly TimeSpan timeToLock = TimeSpan.FromSeconds(30);
		private object target;
		private string hint;
		private bool gotLock;

		public static TimedLock Lock(object o, string hint)
		{
			return Lock(o, timeToLock, hint);
		}

		public static TimedLock Lock(object o, TimeSpan timeout, string hint)
		{
			TimedLock tl = new TimedLock(o, hint);
			if (!Monitor.TryEnter(o, timeout))
			{
#if DEBUG
				System.GC.SuppressFinalize(tl);
#endif
				string exceptionString = string.Format("Timeout waiting for lock: object type '{0}' content '{1}'",
					o.GetType().ToString(),
					o.ToString());
				throw new LockTimeoutException(exceptionString);
			}

			return tl;
		}

		public static TimedLock TryLock(object o, out bool gotLock, string hint)
		{
			TimedLock timedLock = new TimedLock(o, hint);
			if (!Monitor.TryEnter(o, 1))
			{
				timedLock.gotLock = false;
			}

			gotLock = timedLock.gotLock;
			return timedLock;
		}

		private TimedLock(object o, string hint)
		{
			gotLock = true;
			this.hint = hint;
			target = o;
		}

		public void Dispose()
		{
			if (gotLock)
			{
				Monitor.Exit(target);
			}

			// It's a bad error if someone forgets to call Dispose,
			// so in Debug builds, we put a finalizer in to detect
			// the error. If Dispose is called, we suppress the
			// finalizer.
#if DEBUG
			GC.SuppressFinalize(this);
#endif
		}

#if DEBUG

		~TimedLock()
		{
			// If this finalizer runs, someone somewhere failed to
			// call Dispose, which means we've failed to leave
			// a monitor!
			System.Diagnostics.Debug.Fail("Undisposed lock");
		}

#endif
	}

	public class LockTimeoutException : Exception
	{
		public LockTimeoutException(string message)
			: base(message)
		{
		}
	}
}