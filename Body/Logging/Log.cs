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
    public static class Log
    {
		#region STATIC_VARS

		/// <summary>
		/// Command result status code.
		/// </summary>
		public const int EXEC_SUCCESS = 0, EXEC_SKIP = 1, EXEC_FAILURE = 2;

		/// <summary>
		/// Collection of all commands in the project.
		/// </summary>
		private static Dictionary<string, Command> commands;

		/// <summary>
		/// Has the logging system been initialized?
		/// </summary>
		private static bool initalized = false;

        #endregion

        #region INSTANCE_VARS

        #endregion

        #region STATIC_METHODS

		public static void Init()
		{
			if (initalized)
				return;

			Debug.Log ("[DUL] Initializing Log...");

			//redirect Console.Write to Unity's logger
			//Console.SetOut (new Utility.ConsoleRedirector ());

			//instantiate commands collection
			commands = new Dictionary<string, Command> ();
			
			//assemble all methods marked as commands into a local collection
			foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies ())
			{
				foreach(Type t in a.GetTypes())
				{
					foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
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

						//create wrapper for command
						Command com = new Command();
						com.invocation = cAttrs[0].Invocation.ToLower();
						com.function = c;
						com.helpText = cAttrs[0].Help;

						//default Dullahan commands get precidence
						if (commands.ContainsKey (com.invocation))
						{
							//overwrite user command with Dullahan command
							if (t == typeof (Log) || t == typeof (Server))
								commands.Remove (com.invocation);
							//notify that command was not added
							else
							{
								Debug.LogError ("A command with the invokation " +
									com.invocation + " already exists.\nAssembly: " +
									a.FullName + "\nType: " + t.FullName + "\nMethod: " + m.Name);
								continue;
							}
						}

						//add valid command to collection
						commands.Add(cAttrs[0].Invocation, com);
						Debug.Log ("Added \"" + com.invocation + "\" to command list."); //DEBUG
					}
				}
			}

			//toggle initialization flag
			initalized = true;
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
			string invocation = args[0].ToLower ();
			Debug.Log ("Log received invoke request: " + invocation); //DEBUG
			if (commands.TryGetValue (invocation, out c))
			{
				//found command, try executing
				try
				{
					Debug.Log ("Executing " + invocation); //DEBUG
					status = c.function.Invoke (args);
				}
				catch (Exception e)
				{
					Println ("Failed executing " + args[0] + "\n" + e.ToString ());
				}
			}
			else
			{
				Println ("Could not find " + args[0] + ".");
			}

			return status;
		}

		public static void Print(string msg)
		{
			Server.GetInstance ().Send (msg);
		}

		public static void Println(string msg)
		{
			Print (msg + "\n");
		}
        #endregion

        #region INSTANCE_METHODS

        #endregion

        #region INTERNAL_TYPES

		#endregion

		#region DEFAULT_COMMANDS


		#endregion
	}
}
