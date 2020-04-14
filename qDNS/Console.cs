using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SConsole = System.Console;

namespace qDNS
{
	class Console
	{
		private static object _sync = new object();

		static bool _enable = true;
		public static bool Enable
		{
			get => _enable;
			set => _enable = value;
		}

		[Conditional("DEBUG")]
		public static void WriteLine(object msg)
		{
			if (_enable)
			{
				WriteLine(ConsoleUtil.Color, msg);
			}
		}
		[Conditional("DEBUG")]
		public static void WriteLine(ConsoleColor color, object msg)
		{
			if (_enable)
			{
				lock (_sync)
				{
					var old = SConsole.ForegroundColor;
					SConsole.ForegroundColor = color;
					SConsole.WriteLine(msg);
					SConsole.ForegroundColor = old;
				}
			}
		}

		[Conditional("DEBUG")]
		public static void WriteLine()
		{
			if (_enable)
			{
				SConsole.WriteLine();
			}
		}

		public static void ReadLine()
		{
			SConsole.ReadLine();
		}
	}
}
