using System;
using System.Collections.Generic;
using System.Reflection;
using Dullahan.Env;

namespace Dullahan
{
	/// <summary>
	/// Contains environment information and operations for Dullahan
	/// </summary>
	[CommandProvider]
    public class Environment
    {
		#region TOKEN_CONSTANTS

		public const char TERM_SEP = ' ';
		public const char VAR_MARKER = '%';
		public const char MERGE_MARKER = '\"';
		#endregion

		#region STATIC_VARS

#if DEBUG
		private const string DEBUG_TAG = "[DULENV]";
#endif

		/// <summary>
		/// Command result status code.
		/// </summary>
		public const int EXEC_SUCCESS = 0, EXEC_SKIP = 1, EXEC_FAILURE = 2, EXEC_NOTFOUND = 3;

		private static Environment instance;

		#endregion

		#region INSTANCE_VARS

		/// <summary>
		/// Collection of all commands in the project.
		/// </summary>
		private Dictionary<string, Command> commands;

		/// <summary>
		/// Collection of all variables defined during runtime
		/// </summary>
		private Dictionary<string, IVariable> variables;

		#endregion

		#region STATIC_METHODS

		public static Environment GetInstance()
		{
			if (instance == null)
				instance = new Environment();
			return instance;
		}

		public static void Init()
		{
			GetInstance();

#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Initializing");
#endif
			
			//assemble all methods marked as commands into a local collection
			foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies ())
			{
				//skip assemblies that are not command providers
				if (a.GetCustomAttributes (typeof (CommandProviderAttribute), false).Length <= 0)
					continue;

				foreach(Type t in a.GetTypes())
				{
					//skip types that are not command providers
					if (t.GetCustomAttributes (typeof (CommandProviderAttribute), false).Length <= 0)
						continue;

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
							Console.Error.WriteLine(m.Name + " is marked as a Dullahan Command, but it does not match "
								+ "the required method signature: int name(string[]) .");
							continue;
						}

						//validate command invocation
						if (cAttrs[0].Invocation == null || cAttrs[0].Invocation == "")
						{
							Console.Error.WriteLine(m.Name + " does not have a valid Invocation.");
							continue;
						}

						//create wrapper for command
						Command com = new Command();
						com.invocation = cAttrs[0].Invocation.ToLower();
						com.function = c;
						com.helpText = cAttrs[0].Help;

						//default Dullahan commands get precidence
						if (instance.commands.ContainsKey (com.invocation))
						{
							//overwrite user command with Dullahan command
							if (t.Assembly == typeof(Environment).Assembly)
								instance.commands.Remove (com.invocation);
							//notify that command was not added
							else
							{
								Console.Error.WriteLine("A command with the invokation " +
									com.invocation + " already exists.\nAssembly: " +
									a.FullName + "\nType: " + t.FullName + "\nMethod: " + m.Name);
								continue;
							}
						}

						//add valid command to collection
						instance.commands.Add(cAttrs[0].Invocation, com);
#if DEBUG
						Console.WriteLine(DEBUG_TAG + " Added \"" + com.invocation + "\" to command list.");
#endif
					}
				}
			}

			//set up default variables
			//TODO default vars?
		}

		/// <summary>
		/// Uninitializes Environment, removing all commands and variables from memory
		/// </summary>
		public static void Clear()
		{
			if (instance == null)
				return;

			instance.commands.Clear();
			instance.variables.Clear();

			instance = null;
		}

		public static bool HasCommand(string invocation)
		{
			return GetInstance().commands.ContainsKey(invocation);
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
				if (raw[i] == MERGE_MARKER) //start or end space-ignoring group
				{
					merging = !merging;
					if (!merging)
					{
						argsList.Add (mergeString);
						mergeString = "";
					}
				}
				else if (raw[i] == VAR_MARKER) //try to resolve a variable
				{
					int start = i + 1;
					int end = raw.IndexOf (VAR_MARKER, start);
					if (end != -1)
					{
						object var = GetVariable<object>(raw.Substring(start, end - start));
						if(var != null)
							mergeString += var.ToString();
						i = end;
					}
				}
				else if (raw[i] == TERM_SEP && !merging) //end of a regular term
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

		public static int InvokeCommand(string input, out Exception error)
		{
			return InvokeCommand (ParseInput (input), out error);
		}

		public static int InvokeCommand(string[] args, out Exception error)
		{
			int status = EXEC_FAILURE;
			error = null;

			//skip execution of no command was provided
			if (args.Length < 1)
				return EXEC_SKIP;

			Command c;
			string invocation = args[0].ToLower ();
#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Received invoke request: " + invocation);
#endif
			if (GetInstance().commands.TryGetValue (invocation, out c))
			{
				//found command, try executing
				try
				{
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Executing " + invocation);
#endif
					status = c.function.Invoke (args);
				}
				catch (Exception e)
				{
#if DEBUG
					Console.WriteLine(DEBUG_TAG + " Execution error: " + e.Message);
#endif
					status = EXEC_FAILURE;
					error = e;
				}
			}
			else
			{
#if DEBUG
				Console.WriteLine(DEBUG_TAG + " Cound not find \"" + args[0] + "\"");
#endif
				status = EXEC_NOTFOUND;
			}

			return status;
		}

		/// <summary>
		/// Retrieve a variable via its name
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <returns></returns>
		public static T GetVariable<T>(string name)
		{
			IVariable var;
			if(GetInstance().variables.TryGetValue(name, out var))
			{
				return var.GetValue<T>();
			}
			return default(T);
		}

		/// <summary>
		/// Sets the indicated variable to the given value.
		/// If a variable with the given name does not exist, then one
		/// is created
		/// </summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public static void SetVariable(string name, object value)
		{
			IVariable var;
			if(GetInstance().variables.TryGetValue(name, out var))
			{
				var.SetValue(value);
#if DEBUG
				Console.WriteLine(DEBUG_TAG + " Set \"" + name + "\" to " + value.ToString());
#endif
			}
			else
			{
				GetInstance().variables.Add(name, new LiteralVariable(value));
#if DEBUG
				Console.WriteLine(DEBUG_TAG + " Created new variable named \"" + name + "\" with value \"" + value.ToString() + "\"");
#endif
			}
		}

		/// <summary>
		/// Make a new variable.
		/// Throws an ArgumentException if a variable with the given name already exists
		/// </summary>
		/// <param name="name"></param>
		/// <param name="var"></param>
		public static void CreateVariable(string name, IVariable var)
		{
			if (GetInstance().variables.ContainsKey(name))
				throw new ArgumentException("Variable with the name " + name + " already exists");

			GetInstance().variables.Add(name, var);
#if DEBUG
			Console.WriteLine(DEBUG_TAG + " Created new variable named \"" + name + "\" with value \"" + var.GetValue<object>().ToString() + "\"");
#endif
		}
        #endregion

        #region INSTANCE_METHODS

		private Environment()
		{
			commands = new Dictionary<string, Command>();
			variables = new Dictionary<string, IVariable>();
		}

		~Environment()
		{
#if DEBUG
			Console.WriteLine(DEBUG_TAG + " Environment completely cleared");
#endif
		}
        #endregion

        #region INTERNAL_TYPES

		#endregion

		#region DEFAULT_COMMANDS


		#endregion
	}
}
