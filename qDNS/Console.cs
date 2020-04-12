using System;
using System.Collections.Generic;
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

		public static void WriteLine(object msg)
		{
			if (_enable)
			{
				WriteLine(ConsoleUtil.Color, msg);
			}
		}
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
