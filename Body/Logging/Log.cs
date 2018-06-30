using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Dullahan.Comm;

namespace Dullahan.Logging
{
    /// <summary>
    /// Entrypoint for sending logging information to clients.
    /// </summary>
    public class Log
    {
		#region STATIC_VARS

		/// <summary>
		/// Unprinted channel. Nothing that goes here is displayed.
		/// </summary>
		public const int CH_MUTE = 0x0;

		/// <summary>
		/// Default output channel. Command results direct here by default.
		/// </summary>
		public const int CH_DEFAULT = 0x1;

		/// <summary>
		/// All channels.
		/// </summary>
		public const int CH_BROADCAST = ~0x0;

		/// <summary>
		/// Command result status code.
		/// </summary>
		public const int EXEC_SUCCESS = 0, EXEC_SKIP = 1, EXEC_FAILURE = 2;

		/// <summary>
		/// Collection of all commands in the project.
		/// </summary>
		private static Dictionary<string, Command> commands;

		/// <summary>
		/// Bit-vector of channel ids whose data will be sent to clients
		/// </summary>
		private static int channelFilter;

		/// <summary>
		/// User defined names for channel ids
		/// </summary>
		private static Channel[] channels;

        #endregion

        #region INSTANCE_VARS

        #endregion

        #region STATIC_METHODS

		static Log()
		{
			//assemble all methods marked as commands into a local collection
			foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach(Type t in a.GetTypes())
				{
					foreach(MethodInfo m in t.GetMethods(BindingFlags.Static))
					{
						CommandAttribute[] cAttrs = (CommandAttribute[])m.GetCustomAttributes(typeof(CommandAttribute), false);

						//if the method doesn't have a Command Attribute, skip it
						if (cAttrs.Length < 1)
							continue;

						//create and validate command
						CommandDelegate c = (CommandDelegate)Delegate.CreateDelegate(typeof(CommandDelegate), m, false);
						if (c == null)
						{
							Debug.LogError(m.Name + " is marked as a Dullahan Command, but it does not match "
								+ "the required method signature: int name(string[]) .");
							continue;
						}

						//validate command invocation
						if (cAttrs[0].Invocation == null || cAttrs[0].Invocation == "")
						{
							Debug.LogError(m.Name + " does not have a valid Invocation.");
							continue;
						}

						//add valid command to collection
						Command com = new Command();
						com.invocation = cAttrs[0].Invocation.ToLower();
						com.function = c;
						com.helpText = cAttrs[0].Help;
						commands.Add(cAttrs[0].Invocation, com);
					}
				}
			}

			//set up channels
			channels = new Channel[32];
			channelFilter = CH_DEFAULT;
			channels[0] = new Channel("MUT");
			channels[1] = new Channel("DEF");
		}

		/// <summary>
		/// Get a channel by its name
		/// </summary>
		/// <param name="name">The name of the channel</param>
		/// <returns></returns>
		public static Channel Ch(string name)
		{
			if (name == "")
				throw new InvalidOperationException("Cannot resolve empty string to channel.");

			for (int i = 0; i < channels.Length; i++)
			{
				if (channels[i].Name == name)
					return channels[i];
			}

			//send to MUT channel
			return channels[0];
		}

		/// <summary>
		/// Give a channel ID a string name. Overwrites any existing name.
		/// </summary>
		/// <param name="channel">The channel ID (2-31)</param>
		/// <param name="name">The name to give the channel</param>
		public static void SetChannelName(int channel, string name)
		{
			if(channel < 2)
				throw new InvalidOperationException("Channels 0 and 1 are reserved.");

			try { channels[channel].Name = name; }
			catch(IndexOutOfRangeException)
			{
				Debug.LogError("Invalid channel ID: " + channel);
			}
		}

		/// <summary>
		/// Convert a channel name into a bit-vector. If the channel does not exist,
		/// then CH_MUTE is returned.
		/// </summary>
		/// <param name="name">The name of the channel to find</param>
		/// <returns></returns>
		public static int NameToChannel(string name)
		{
			if (name == "")
				throw new InvalidOperationException("Cannot resolve empty string to channel.");

			for(int i = 0; i < channels.Length; i++)
			{
				if (channels[i].Name == name)
					return 1 << i;
			}

			return CH_MUTE;
		}

		/// <summary>
		/// Parse a raw string into an argument array
		/// </summary>
		/// <param name="raw">The raw input</param>
		/// <returns></returns>
		public static string[] ParseInput(string raw)
		{
			List<string> argsList = new List<string> ();
			string mergeString = "";
			bool merging = false;

			for (int i = 0; i < raw.Length; i++)
			{
				if (raw[i] == '\"') //start or end space-ignoring group
				{
					merging = !merging;
					if (!merging)
					{
						argsList.Add (mergeString);
						mergeString = "";
					}
				}
				else if (raw[i] == '%' || raw[i] == '!') //try to resolve a variable
				{
					char delim = raw[i] == '%' ? '%' : '!';
					int start = i + 1;
					int end = raw.IndexOf (delim, start);
					if (end != -1)
					{
						mergeString += ""; //TODO resolve environment variable
						i = end;
					}
				}
				else if (raw[i] == ' ' && !merging) //end of a regular term
				{
					if (mergeString != "")
						argsList.Add (mergeString);
					mergeString = "";
				}
				else //add any other character to the mergeString
					mergeString += raw[i];
			}

			//if the merge string is not empty, add it the the args list
			if (mergeString != "")
				argsList.Add (mergeString);

			//return the parsed result
			return argsList.ToArray ();
		}

		public static int InvokeCommand(string input)
		{
			return InvokeCommand (ParseInput (input));
		}

		public static int InvokeCommand(string[] args)
		{
			int status = EXEC_FAILURE;

			//skip execution of no command was provided
			if (args.Length < 1)
				return EXEC_SKIP;

			Command c;
			if (commands.TryGetValue (args[0].ToLower (), out c))
			{
				//found command, try executing
				try
				{
					status = c.function.Invoke (args);
				}
				catch (Exception e)
				{
					Ch ("DEF").WriteLine ("Failed executing " + args[0] + "\n" + e.ToString ());
				}
			}
			else
			{
				Ch ("DEF").WriteLine ("Could not find " + args[0] + ".");
			}

			return status;
		}
        #endregion

        #region INSTANCE_METHODS

        #endregion

        #region INTERNAL_TYPES

		/// <summary>
		/// A data stream for information sent through the logging system.
		/// </summary>
		public class Channel
		{
			public string Name { get; set; }

			public Channel(string name)
			{
				Name = name;
			}

			//TODO Channel.Write
			public void Write(string message)
			{
				Server s = Server.GetInstance();
				if(s.IsRunning())
				{
					s.Send(message);
				}
			}

			public void WriteLine(string message)
			{
				Write(message + "\n");
			}
		}

		#endregion

		#region DEFAULT_COMMANDS

		/// <summary>
		/// Basic verification test for connection between Head and Body
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[Command(Invocation = "ping", Help = "Verify connection to Unity instance.")]
		private static int Handshake(string[] args)
		{
			Ch ("DEF").WriteLine ("Connection to '" + Application.productName + "' Established!");
			return EXEC_SUCCESS;
		}
		#endregion
	}
}
