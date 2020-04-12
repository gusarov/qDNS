using System;
using System.Threading;

namespace qDNS
{
	public class ConsoleUtil
	{
		private static readonly ThreadLocal<ConsoleColor> _color =
			new ThreadLocal<ConsoleColor>(() => ConsoleColor.DarkGray);

		public static ConsoleColor Color => _color.Value;

		public static IDisposable UseColor(ConsoleColor color)
		{
			var orig = _color.Value;
			_color.Value = color;
			return new Scope(delegate
			{
				_color.Value = orig;
			});
		}
	}
}