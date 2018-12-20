using Dullahan.Env;
using Dullahan.Logging;
using System;

namespace Dullahan
{
	[CommandProvider]
	public class LocalCommands
	{
		[Command (Invocation = "clear", Help = "Clears the CLI output")]
		public int Clear(string[] args, Executor env)
		{
			Console.Clear ();
			return Executor.EXEC_SUCCESS;
		}
	}
}
