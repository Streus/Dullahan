using Dullahan.Env;
using Dullahan.Logging;
using Dullahan.Net;
using System;
using System.Net;

namespace Dullahan
{
	internal class Program : ILogWriter, ILogReader
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

		private const string DEBUG_TAG = "[PROG]";
		#endregion

		#region STATIC_VARS

		private static volatile bool blocking = false;

		private static Executor env;
		private static Client client;

		private static IPAddress addr;
		private static int port;
		private static ExecutionMode execMode;
		#endregion

		public static void Main(string[] args)
		{
			Initialize (args);

			Executor.Init();
			env = Executor.Build ();
			env.SetOutput (this); //TODO executor redirect class
			env.SetInput (this);

			Connect ();

			//verify connection
			client.SendAndWait(new Packet(Packet.DataType.management, "setup"));

			if (execMode == ExecutionMode.command)
			{
				client.SendAndWait(new Packet(Packet.DataType.command, "mute add " + client.Name));

				while (client.Connected)
				{
					Console.Write(client.Name + "> ");
					string input = Console.ReadLine();
					int commandResult = Executor.EXEC_FAILURE;
					string exceptionText = "";

					//try to run the command locally first
					string[] parsedInput = env.ParseInput(input);
					Exception error;
					commandResult = env.InvokeCommand(parsedInput, out error);
					if (error != null)
						exceptionText = error.ToString();

					//didn't find command locally, send command to server
					if (commandResult == Executor.EXEC_NOTFOUND)
					{
						client.SendAndRead(new Packet(Packet.DataType.command, input), delegate (Client c, Packet p) {
							if (p.type == Packet.DataType.response)
							{
								commandResult = p.logResult;
								exceptionText = p.data;
							}
#if DEBUG
							else
								Console.WriteLine(DEBUG_TAG + " Received a non-response packet while waiting " +
									"for a response from \"" + input + "\"");
#endif
						});
					}

					//some failure occured
					if(commandResult != 0)
					{
						Console.WriteLine("Status: " + commandResult);
						switch (commandResult)
						{
							case Executor.EXEC_SKIP:
								Write("Command does not fulfill requirements; execution skipped", ConsoleColor.Yellow);
								break;

							case Executor.EXEC_FAILURE:
								Write(exceptionText, ConsoleColor.DarkRed);
								break;

							case Executor.EXEC_NOTFOUND:
								Write("Command \"" + parsedInput[0] + "\" could not be found", ConsoleColor.Yellow);
								break;

							default:
								Write("Unknown status", ConsoleColor.Red);
								break;
						}
					}
				}
			}
			else if (execMode == ExecutionMode.listen)
			{
				while (client.Connected) { }
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
			port = Client.DEFAULT_PORT;
			execMode = ExecutionMode.listen;

			if (args.Length <= 0)
			{
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
		/// Attempt to connect to the remote host
		/// </summary>
		private static void Connect()
		{
			//start tcp client
			client = new Client (addr, port); ;

			if (execMode == ExecutionMode.command)
				client.dataRead += CommandReceiveResponse;
			else if (execMode == ExecutionMode.listen)
				client.dataRead += ListenerReceiveResponse;
			client.Start ();

			//block for client to connect
			while (!client.Connected)
			{
				if (client.Disconnected)
				{
					Write ("Exiting...", ConsoleColor.Red);
					Environment.Exit (1);
				}
			}
			Console.WriteLine ("\nConnected!");
		}

		private static void CommandReceiveResponse(Client endpoint, Packet packet)
		{
			if(packet.logResult != -1)
			{
				lock(client)
				{
					blocking = false;
				}
			}

			Console.WriteLine(packet.data);
		}

		private static void ListenerReceiveResponse(Client endpoint, Packet packet)
		{
			Console.WriteLine(packet.data);
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
		/// Writes to the console with the indicated color
		/// </summary>
		/// <param name="error"></param>
		private static void Write(string error, ConsoleColor color = ConsoleColor.White)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine (error);
			Console.ResetColor ();
		}

		public void Write(Message msg)
		{
			//TODO write to console
			throw new NotImplementedException ();
		}

		public string ReadLine()
		{
			//TODO read from console
			throw new NotImplementedException ();
		}

		#region INTERNAL_TYPES

		private enum ExecutionMode { listen, command }
		#endregion

		#region DEFAULT_COMMANDS

		#endregion
	}
}
