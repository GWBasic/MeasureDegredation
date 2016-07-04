using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MeasureDegredation
{
	public static class Extensions
	{
		public static bool In<T>(this T o, params T[] vals)
		{
			foreach (var val in vals)
				if (val.Equals(o))
					return true;

			return false;
		}

		public static IEnumerable<List<T>> GroupsOf<T>(this IEnumerable<T> source, int chunkSize)
		{
			var enumerator = source.GetEnumerator();

			if (enumerator.MoveNext())
			{
				bool cont;
				do
					yield return Extensions.Next<T>(enumerator, chunkSize, out cont);
				while (cont);
			}
		}

		private static List<T> Next<T>(IEnumerator<T> enumerator, int chunkSize, out bool cont)
		{
			var items = new List<T>(chunkSize);

			do
			{
				items.Add(enumerator.Current);
				cont = enumerator.MoveNext();
			} while (cont && items.Count < chunkSize);

			return items;
		}

		public static IEnumerable<T> ProcessOnSubThread<T>(this IEnumerable<T> enumerable, string threadName)
		{
			object sync = new object();
			var hasNext = true;
			var t = default(T);

			var thread = new Thread(() =>
			{
				foreach (var tItr in enumerable)
				{
					lock (sync)
					{
						t = tItr;
						Monitor.Pulse(sync);
					}
				}

				lock (sync)
				{
					hasNext = false;
					Monitor.Pulse(sync);
				}
			})
			{
				Name = threadName
			};

			thread.Start();

			lock (sync)
			{
				Monitor.Wait(sync);

				while (hasNext)
				{
					yield return t;
					Monitor.Wait(sync);
				}
			}

			thread.Join();
		}
	}
}

