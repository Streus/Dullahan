using Dullahan.Env;
using Dullahan.Net;
using System;
using System.Net;
using System.Reflection;
using System.Threading;

namespace Dullahan
{
	internal class Program
	{
		#region ARG_FLAGS

		private const string FLAG_HELP = "-h";
		private const string FLAG_HELP_LONG = "--help";

		private const string FLAG_VERSION = "-v";
		private const string FLAG_VERSION_LONG = "--version";

		private const string FLAG_IP = "--ip";
		private const string FLAG_PORT = "-p";
		private const string FLAG_PORT_LONG = "--port";

		private const string FLAG_EXEC_MODE = "-m";
		private const string FLAG_EXEC_MODE_LONG = "--mode";
		#endregion

		#region MISC_CONSTANTS

#if DEBUG
		private const string DEBUG_TAG = "[PROG]";
#endif
		#endregion

		#region STATIC_VARS

		private static Executor env;
		private static Endpoint client;
		private static ConsoleRedirector redir;

		private static IPAddress addr;
		private static int port;
		private static ExecutionMode execMode;
		#endregion

		public static void Main(string[] args)
		{
			Initialize (args);

			Executor.Init();
			User.RegistryPath = Assembly.GetEntryAssembly ().CodeBase;
			env = Executor.Build ("user");
			redir = new ConsoleRedirector ();
			env.SetOutput (redir); //TODO executor redirect class
			env.SetInput (redir);

			Connect ();
			client.Flow = Endpoint.FlowState.outgoing;

			//verify connection
			while (client.Connected)
			{
				Console.Write(addr.ToString() + "> ");

				string input;
				if (execMode != ExecutionMode.listen)
					input = Console.ReadLine ();
				else
					input = "listen -t all";

				int commandResult = Executor.EXEC_FAILURE;

				//try to run the command locally first
				string[] parsedInput = env.ParseInput(input);
				commandResult = env.InvokeCommand(parsedInput);

				//didn't find command locally, send command to server
				if (commandResult == Executor.EXEC_NOTFOUND)
				{
					client.Send (new Packet (Packet.DataType.command, input));
					client.Flow = Endpoint.FlowState.incoming;

					HandleResponses (ref commandResult);

					client.Flow = Endpoint.FlowState.outgoing;
				}

				//some failure occured
				if(commandResult != 0)
				{
#if DEBUG
					Console.WriteLine(DEBUG_TAG + " Status: " + commandResult);
#endif
					switch (commandResult)
					{
						case Executor.EXEC_SKIP:
							Write("Command execution skipped", ConsoleColor.Yellow);
							break;

						case Executor.EXEC_FAILURE:
							Write("Failed executing \"" + parsedInput[0] + "\"", ConsoleColor.Red);
							break;

						case Executor.EXEC_NOTFOUND:
							Write("Command \"" + parsedInput[0] + "\" could not be found", ConsoleColor.Yellow);
							break;

						default:
							Write("Unknown status (" + commandResult + ")", ConsoleColor.Magenta);
							break;
					}
				}
			}
#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Exiting...");
#endif
		}

		private static void HandleResponses(ref int commandResult)
		{
			Packet[] responses = null;
			while (true)
			{
				while (!client.HasPendingData ())
				{
#if DEBUG
					Write (DEBUG_TAG + " waiting...", ConsoleColor.Gray);
					Thread.Sleep (250);
#endif
				}
				responses = client.Read ();
				for (int i = 0; i < responses.Length; i++)
				{
					if (responses[i].Type == Packet.DataType.response)
					{
#if DEBUG
						Write (DEBUG_TAG + " got response", ConsoleColor.Green);
#endif
						int.TryParse (responses[i].Data, out commandResult);
						return;
					}
					else if (responses[i].Type == Packet.DataType.logentry)
					{
						redir.Write (responses[i].ToMessage ());
					}
#if DEBUG
					else
					{
						Write (DEBUG_TAG + " Recieved an unexpected packet type: " + responses[i].Type.ToString (), ConsoleColor.Red);
					}
#endif
				}
			}
		}

		/// <summary>
		/// Parse command line options
		/// </summary>
		/// <param name="args"></param>
		private static void Initialize(string[] args)
		{
			//default values
			string ip = "127.0.0.1";
			IPAddress.TryParse (ip, out addr);
			port = Endpoint.DEFAULT_PORT;
			execMode = ExecutionMode.listen;

			if (args.Length <= 0)
			{
				//nothing to parse
				return;
			}

			int currArg = 0;

			//misc special flags that must be first
			//help flag
			if (args[currArg] == FLAG_HELP || args[currArg] == FLAG_HELP_LONG)
			{
				//print help and exit
				Console.WriteLine("help is todo");
				Environment.Exit(0);
			}
			//version info flag
			else if (args[currArg] == FLAG_VERSION || args[currArg] == FLAG_VERSION_LONG)
			{
				//print help and exit
				Console.WriteLine("version is todo");
				Environment.Exit(0);
			}

			//read args
			while (currArg < args.Length)
			{
				//ip flag
				if (args[currArg] == FLAG_IP)
				{
					ip = TryGetArg(args, ++currArg, FLAG_IP);
					try
					{
						addr = IPAddress.Parse (ip);
					}
					catch (FormatException)
					{
						Write("\"" + ip + "\" is not a valid ip address.", ConsoleColor.Red);
						Environment.Exit (1);
					}
				}
				//port flag
				else if (args[currArg] == FLAG_PORT || args[currArg] == FLAG_PORT_LONG)
				{
					string flag = args[currArg];
					string pStr = TryGetArg(args, ++currArg, flag);
					if (!int.TryParse(pStr, out port))
					{
						Write("\"" + pStr + "\" is not a valid port", ConsoleColor.Red);
						Environment.Exit(1);
					}
				}
				//execution mode flag
				else if (args[currArg] == FLAG_EXEC_MODE || args[currArg] == FLAG_EXEC_MODE_LONG)
				{
					string flag = args[currArg];
					string mStr = TryGetArg(args, ++currArg, flag);
					if (!Enum.TryParse<ExecutionMode>(mStr, out execMode))
					{
						Write("\"" + mStr + "\" is not a valid mode", ConsoleColor.Red);
						Environment.Exit(1);
					}
				}
				//unknown argument
				else
				{
					Write("Unknown or unexpected argument \"" + args[currArg] + "\"", ConsoleColor.Red);
					Environment.Exit(1);
				}

				currArg++;
			}
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
					Write ("Provided \"" + argName + "\" flag, but no data", ConsoleColor.Red);
					Environment.Exit (1);
				}
			}
			catch (IndexOutOfRangeException)
			{
				Write ("Provided \"" + argName + "\" flag, but no data", ConsoleColor.Red);
				Environment.Exit (1);
			}

			return res;
		}

		/// <summary>
		/// Attempt to connect to the remote host
		/// </summary>
		private static void Connect()
		{
			//start tcp client
			client = new Endpoint (addr, port);
			client.Start (isServer: false);

			if (client.Disconnected)
			{
				Write ("Exiting...", ConsoleColor.Red);
				Environment.Exit (1);
			}
			Console.WriteLine ("\nConnected!");
		}

		/// <summary>
		/// Writes to the console with the indicated color
		/// </summary>
		/// <param name="error"></param>
		private static void Write(string text, ConsoleColor color)
		{
			Console.ForegroundColor = color;
			Console.WriteLine (text);
			Console.ResetColor ();
		}

		#region INTERNAL_TYPES

		private enum ExecutionMode { listen, command }
		#endregion
	}
}
