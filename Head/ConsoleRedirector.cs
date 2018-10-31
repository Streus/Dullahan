using System;
using Dullahan.Logging;

namespace Dullahan
{
	internal class ConsoleRedirector : ILogWriter, ILogReader
	{
		public string ReadLine()
		{
			return Console.ReadLine ();
		}

		public void Write(Message msg)
		{
			if (msg.Tags.Contains (Log.TAG_TYPE_ERROR))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				
			}
			else if (msg.Tags.Contains (Log.TAG_TYPE_WARNING))
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
			}
			else if (msg.Tags.Contains (Log.TAG_TYPE_DEBUG))
			{
				Console.ForegroundColor = ConsoleColor.White;
				Console.BackgroundColor = ConsoleColor.Black;
			}

			Console.WriteLine (msg.ToString(true, true));
			Console.ResetColor ();
		}
	}
}
