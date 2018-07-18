using System;
using System.Net;
using Dullahan.Net;

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

		#region MISC_CONSTANTS

		private const string DEBUG_TAG = "[PROG]";

		private static volatile bool blocking = false;
		#endregion

		#region STATIC_VARS

		private static Client client;
		#endregion

		public static void Main(string[] args)
		{
			//default values
			string ip = "127.0.0.1";
			int port = Client.DEFAULT_PORT;
			ExecutionMode mode = ExecutionMode.listen;

			if (args.Length > 0)
			{
				int currArg = 0;

				//misc special flags that must be first
				//help flag
				if (args[currArg] == helpFlag || args[currArg] == helpFlagLong)
				{
					//print help and exit
					Console.WriteLine("help is todo");
					System.Environment.Exit(0);
				}
				//version info flag
				else if (args[currArg] == versionFlag || args[currArg] == versionFlagLong)
				{
					//print help and exit
					Console.WriteLine("version is todo");
					System.Environment.Exit(0);
				}

				//read args
				while (currArg < args.Length)
				{
					//ip flag
					if (args[currArg] == ipFlag)
					{
						ip = TryGetArg(args, ++currArg, ipFlag);
					}
					//port flag
					else if (args[currArg] == portFlag || args[currArg] == portFlagLong)
					{
						string flag = args[currArg];
						string pStr = TryGetArg(args, ++currArg, flag);
						if (!int.TryParse(pStr, out port))
						{
							Console.ForegroundColor = ConsoleColor.Red;
							Console.Error.WriteLine("\"" + pStr + "\" is not a valid port");
							Console.ResetColor();
							System.Environment.Exit(1);
						}
					}
					//execution mode flag
					else if (args[currArg] == executionModeFlag || args[currArg] == executionModeFlagLong)
					{
						string flag = args[currArg];
						string mStr = TryGetArg(args, ++currArg, flag);
						if (!Enum.TryParse<ExecutionMode>(mStr, out mode))
						{
							Console.ForegroundColor = ConsoleColor.Red;
							Console.Error.WriteLine("\"" + mStr + "\" is not a valid mode");
							Console.ResetColor();
							System.Environment.Exit(1);
						}
					}
					//unknown argument
					else
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.Error.WriteLine("Unknown or unexpected argument \"" + args[currArg] + "\"");
						Console.ResetColor();
						System.Environment.Exit(1);
					}

					currArg++;
				}
			}

			Environment.Init();

			//start tcp client
			client = null;
			try
			{
				client = new Client (IPAddress.Parse (ip), port);
			}
			catch (FormatException)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine ("\"" + ip + "\" is not a valid ip address.");
				Console.ResetColor();
				System.Environment.Exit (1);
			}
			if (mode == ExecutionMode.command)
				client.dataRead += CommandReceiveResponse;
			else if (mode == ExecutionMode.listen)
				client.dataRead += ListenerReceiveResponse;
			client.Start ();

			//block for client to connect
			while (!client.Connected) { }
			Console.WriteLine ("\nConnected!");

			//verify connection
			client.SendAndWait(new Packet(Packet.DataType.management, "setup"));

			if (mode == ExecutionMode.command)
			{
				client.SendAndWait(new Packet(Packet.DataType.command, "mute add " + client.Name));

				while (client.Connected)
				{
					string input = Console.ReadLine();
					client.Send(new Packet(Packet.DataType.command, input));
				}
			}
			else if (mode == ExecutionMode.listen)
			{
				while (client.Connected) { }
			}
			
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
					Console.ForegroundColor = ConsoleColor.Red;
					Console.Error.WriteLine ("Provided \"" + argName + "\" flag, but no data");
					Console.ResetColor ();
					System.Environment.Exit (1);
				}
			}
			catch (IndexOutOfRangeException)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine ("Provided \"" + argName + "\" flag, but no data");
				Console.ResetColor ();
				System.Environment.Exit (1);
			}

			return res;
		}

		#region INTERNAL_TYPES

		private enum ExecutionMode { listen, command }
		#endregion

		#region DEFAULT_COMMANDS

		#endregion
	}
}
