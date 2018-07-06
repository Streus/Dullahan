using System;
using System.Net;
using Dullahan.Comm;

namespace Dullahan
{
	public class Program
	{
		#region ARG_FLAGS

		private const string helpFlag = "-h";
		private const string helpFlagLong = "--help";

		private const string versionFlag = "-v";
		private const string versionFlagLong = "--version";

		private const string ipFlag = "--ip";
		private const string portFlag = "-p";
		private const string portFlagLong = "--port";

		private const string executionModeFlag = "-m";
		private const string executionModeFlagLong = "--mode";
		#endregion

		public static void Main(string[] args)
		{
			//default values
			string ip = "127.0.0.1";
			int port = Protocol.DEFAULT_PORT;
			ExecutionMode mode = ExecutionMode.listen;

			//misc special flags that must be first
			int currArg = 0;

			//help flag
			if (args[currArg] == helpFlag || args[currArg] == helpFlagLong)
			{
				//print help and exit
				Console.WriteLine ("help is todo");
				Environment.Exit (0);
			}
			//version info flag
			else if (args[currArg] == versionFlag || args[currArg] == versionFlagLong)
			{
				//print help and exit
				Console.WriteLine ("version is todo");
				Environment.Exit (0);
			}

			//read args
			while (currArg < args.Length)
			{
				//ip flag
				if (args[currArg] == ipFlag)
				{
					ip = TryGetArg (args, ++currArg, ipFlag);
				}
				//port flag
				else if (args[currArg] == portFlag || args[currArg] == portFlagLong)
				{
					string flag = args[currArg];
					string pStr = TryGetArg (args, ++currArg, flag);
					if (!int.TryParse (pStr, out port))
					{
						Console.Error.WriteLine ("\"" + pStr + "\" is not a valid port");
						Environment.Exit (1);
					}
				}
				//execution mode flag
				else if (args[currArg] == executionModeFlag || args[currArg] == executionModeFlagLong)
				{
					string flag = args[currArg];
					string mStr = TryGetArg (args, ++currArg, flag);
					if (!Enum.TryParse<ExecutionMode> (mStr, out mode))
					{
						Console.Error.WriteLine ("\"" + mStr + "\" is not a valid mode");
						Environment.Exit (1);
					}
				}
				//unknown argument
				else
				{
					Console.Error.WriteLine ("Unknown or unexpected argument \"" + args[currArg] + "\"");
					Environment.Exit (1);
				}

				currArg++;
			}

			//start tcp client
			Client c = null;
			try
			{
				c = new Client (IPAddress.Parse (ip), port);
			}
			catch (FormatException)
			{
				Console.Error.WriteLine ("\"" + ip + "\" is not a valid ip address.");
				Environment.Exit (1);
			}

			c.Start ();

			//block for client to connect
			while (!c.Connnected)
			{
				Console.Write ("\rConnecting...");
			}
			Console.WriteLine ("\nConnected!");
		}

		/// <summary>
		/// Attempt to retreive an arguement from the given array, failing execution
		/// if the argument does not exist, or fails to meet expectations.
		/// </summary>
		/// <param name="args"></param>
		/// <param name="index"></param>
		/// <param name="argName"></param>
		/// <returns></returns>
		private static string TryGetArg(string[] args, int index, string argName)
		{
			string res = "";
			try
			{
				res = args[index];

				if (res.StartsWith ("-"))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.Error.WriteLine ("Provided \"" + argName + "\" flag, but no data");
					Console.ResetColor ();
					Environment.Exit (1);
				}
			}
			catch (IndexOutOfRangeException)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine ("Provided \"" + argName + "\" flag, but no data");
				Console.ResetColor ();
				Environment.Exit (1);
			}

			return res;
		}

		#region INTERNAL_TYPES

		private enum ExecutionMode { listen, command }
		#endregion
	}
}
