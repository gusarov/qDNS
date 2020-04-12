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

		public static void WriteLine(object msg)
		{
			WriteLine(ConsoleUtil.Color, msg);
		}
		public static void WriteLine(ConsoleColor color, object msg)
		{
			lock (_sync)
			{
				var old = SConsole.ForegroundColor;
				SConsole.ForegroundColor = color;
				SConsole.WriteLine(msg);
				SConsole.ForegroundColor = old;
			}
		}

		public static void WriteLine()
		{
			SConsole.WriteLine();
		}

		public static void ReadLine()
		{
			SConsole.ReadLine();
		}
	}
}
