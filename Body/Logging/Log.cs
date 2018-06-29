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
		public const int EXEC_SUCCESS = 0, EXEC_FAILURE = 1;

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

		public static int InvokeCommand(string[] args)
		{
			//TODO InvokeCommand
			return EXEC_FAILURE;
		}
        #endregion

        #region INSTANCE_METHODS

        #endregion

        #region INTERNAL_TYPES

		/// <summary>
		/// A data stream for information sent thtough the logging system.
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
					s.Send(null, message);
				}
			}

			public void WriteLine(string message)
			{
				Write(message + "\n");
			}
		}

		
        #endregion
    }
}
