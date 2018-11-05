using Dullahan.Logging;

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Dullahan.Env
{
	/// <summary>
	/// Contains environment information and operations for Dullahan
	/// </summary>
	[CommandProvider]
    public sealed class Executor
    {
		#region TOKEN_CONSTANTS

		public const char TERM_SEP = ' ';
		public const char VAR_MARKER = '%';
		public const char MERGE_MARKER = '\"';
		#endregion

		#region STATIC_VARS

#if DEBUG
		private const string DEBUG_TAG = "[DULEXE]";
#endif

		/// <summary>
		/// Command result status code.
		/// </summary>
		public const int EXEC_SUCCESS = 0, EXEC_SKIP = 1, EXEC_FAILURE = 2, EXEC_NOTFOUND = 3;

		private static Executor globalEnv;

		#endregion

		#region INSTANCE_VARS

		public string Name { get; private set; }

		/// <summary>
		/// Collection of all commands available to this executor
		/// </summary>
		private Dictionary<string, Command> commands;

		/// <summary>
		/// Collection of all variables defined during runtime
		/// </summary>
		private Dictionary<string, IVariable> variables;

		public Log Out { get; private set; }

		#endregion

		#region STATIC_METHODS

		public static Executor GetGlobal()
		{
			if (globalEnv == null)
				globalEnv = new Executor();
			return globalEnv;
		}

		/// <summary>
		/// Initializes the global execution environmment
		/// </summary>
		public static void Init()
		{
			globalEnv = Build ("global");
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
							Console.Error.WriteLine(m.ReflectedType + "." + m.Name + " is marked as a Dullahan Command, but it does not match "
								+ "the required method signature: int name(string[], Executor)");
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
						if (globalEnv.commands.ContainsKey (com.invocation))
						{
							//overwrite user command with Dullahan command
							if (t.Assembly == typeof(Executor).Assembly)
								globalEnv.commands.Remove (com.invocation);
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
						globalEnv.commands.Add(cAttrs[0].Invocation, com);
#if DEBUG
						Console.WriteLine(DEBUG_TAG + " Added \"" + com.invocation + "\" to command list.");
#endif
					}
				}
			}
		}

		public static Executor Build(string name)
		{
			return new Executor () { Name = name };
		}
        #endregion

        #region INSTANCE_METHODS

		private Executor()
		{
			commands = new Dictionary<string, Command>();
			variables = new Dictionary<string, IVariable>();

			Out = new Log ();
		}

		~Executor()
		{
#if DEBUG
			Console.WriteLine(DEBUG_TAG + " Environment \"" + Name +  "\" dropped");
#endif
		}

		/// <summary>
		/// Uninitializes Environment, removing all commands and variables from memory
		/// </summary>
		public void Clear()
		{
			commands.Clear ();
			variables.Clear ();
		}

		/// <summary>
		/// Returns true if either this executor has the indicated command, or if the
		/// global executor does.
		/// </summary>
		/// <param name="invocation"></param>
		/// <returns></returns>
		public bool HasCommand(string invocation)
		{
			return commands.ContainsKey (invocation) 
				|| GetGlobal ().commands.ContainsKey (invocation); ;
		}

		/// <summary>
		/// Parse a raw string into an argument array
		/// </summary>
		/// <param name="raw">The raw input</param>
		/// <returns></returns>
		public string[] ParseInput(string raw)
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
						string name = raw.Substring (start, end - start);

						//check local and global executors
						object var = GetVariable<object> (name);
						if (var == null)
							var = GetGlobal ().GetVariable<object> (name);

						if (var != null)
							mergeString += var.ToString ();
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

		public int InvokeCommand(string rawInput)
		{
			return InvokeCommand (ParseInput (rawInput));
		}

		public int InvokeCommand(string[] args)
		{
			int status = EXEC_FAILURE;

			//skip execution of no command was provided
			if (args.Length < 1)
				return EXEC_SKIP;

			Command c;
			string invocation = args[0].ToLower ();
#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Received invoke request: " + invocation);
#endif
			if (commands.TryGetValue (invocation, out c) 
				|| GetGlobal ().commands.TryGetValue (invocation, out c))
			{
				//found command, try executing
				try
				{
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Executing " + invocation);
#endif
					status = c.function.Invoke (args, this);
				}
				catch (Exception e)
				{
#if DEBUG
					Console.Error.WriteLine (DEBUG_TAG + " Execution error: " + e.Message);
#endif
					status = EXEC_FAILURE;
					Out.E (new Message (e.ToString ()));
				}
			}
			else
			{
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Cound not find \"" + args[0] + "\"");
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
		public T GetVariable<T>(string name)
		{
			IVariable var;
			if (variables.TryGetValue (name, out var))
			{
				return var.GetValue<T> ();
			}
			return default (T);
		}

		/// <summary>
		/// Sets the indicated variable to the given value.
		/// If a variable with the given name does not exist, then one
		/// is created
		/// </summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public void SetVariable(string name, object value)
		{
			IVariable var;
			if (variables.TryGetValue (name, out var))
			{
				var.SetValue (value);
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Set \"" + name + "\" to " + value.ToString ());
#endif
			}
			else
			{
				CreateVariable (name, new LiteralVariable (value));
			}
		}

		/// <summary>
		/// Make a new variable.
		/// Throws an ArgumentException if a variable with the given name already exists
		/// </summary>
		/// <param name="name"></param>
		/// <param name="var"></param>
		public void CreateVariable(string name, IVariable var)
		{
			variables.Add (name, var);
#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Created new variable named \"" + name + "\" with value \"" + var.GetValue<object> ().ToString () + "\"");
#endif
		}

		/// <summary>
		/// Checks if a variable with the given name exists in this executor
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public bool HasVariable(string name)
		{
			return variables.ContainsKey (name);
		}

		/// <summary>
		/// Commands executed by this executor will write to here
		/// </summary>
		/// <param name="writer"></param>
		public void SetOutput(ILogWriter writer)
		{
			Out.AddOutput (writer);
		}

		/// <summary>
		/// A source for input for commands executed by this executor
		/// </summary>
		/// <param name="reader"></param>
		public void SetInput(ILogReader reader)
		{
			Out.SetInput (reader);
		}
		#endregion

		#region INTERNAL_TYPES

		#endregion

		#region DEFAULT_COMMANDS


		#endregion
	}
}
