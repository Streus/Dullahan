
using System.Collections.Generic;
using System.Diagnostics;

namespace Dullahan.Logging
{
	public class Log
	{
		#region STATIC_VARS

		public const string TAG_TYPE_DEBUG = "DBG";
		public const string TAG_TYPE_WARNING = "WRN";
		public const string TAG_TYPE_ERROR = "ERR";

		private static Log instance;
		/// <summary>
		/// Sends a log messages to any broadcast listeners
		/// </summary>
		public static Log Broadcast //TODO make subclass
		{
			get
			{
				if (instance == null)
				{
					instance = new Log ();
				}
				return instance;
			}
		}

		/// <summary>
		/// Toggle on to add stackframe info to log messages
		/// </summary>
		public static bool IncludeContext { get; set; } = false;
		#endregion

		#region INSTANCE_VARS

		private List<ILogWriter> writers; //TODO default only one writer, Broadcast supports many
		private ILogReader reader;
		#endregion

		#region STATIC_METHODS


		#endregion

		#region INSTANCE_METHODS

		public Log()
		{
			writers = new List<ILogWriter> ();
			reader = null;
		}

		public void AddOutput(ILogWriter w)
		{
			writers.Add(w);
		}

		public void RemoveOutput(ILogWriter w)
		{
			writers.Remove (w);
		}

		public void SetInput(ILogReader r)
		{
			reader = r;
		}

		private void WriteMessage(Message msg, bool broadcast)
		{
			if (msg.Tags.Count <= 1)
				throw new System.ArgumentException("Provided an empty tag");

			for (int i = 0; i < writers.Count; i++)
			{
				writers[i]?.Write (msg);
			}
			if (broadcast)
			{
				for (int i = 0; i < writers.Count; i++)
				{
					Broadcast.writers[i]?.Write (msg);
				}
			}
		}

		private void TryCaptureContextInfo(Message msg)
		{
			//do not overwrite an existing context capture
			if (msg.Context == "")
			{
				StackFrame frame = new StackFrame (2, true);
				msg.Context = string.Format ("{0}.{1} ({2}, {3})",
					frame.GetFileName (),
					frame.GetMethod ().Name,
					frame.GetFileLineNumber (),
					frame.GetFileColumnNumber ());
			}
		}

		/// <summary>
		/// Write an error message to the logging system with a tag
		/// </summary>
		public void E(Message msg, bool broadcast = false)
		{
			msg.Tags.Add (TAG_TYPE_ERROR);
			if(IncludeContext)
				TryCaptureContextInfo (msg);
			WriteMessage(msg, broadcast);
		}

		public void E(string[] tags, string msg, bool broadcast = false)
		{
			Message m = new Message (tags, msg);
			if (IncludeContext)
				TryCaptureContextInfo (m);
			E (m, broadcast);
		}

		public void E(string tag, string msg, bool broadcast = false)
		{
			Message m = new Message (tag, msg);
			if (IncludeContext)
				TryCaptureContextInfo (m);
			E (m, broadcast);
		}

		/// <summary>
		/// Write a debug message to the logging system with a tag
		/// </summary>
		public void D(Message msg, bool broadcast = false)
		{
			msg.Tags.Add (TAG_TYPE_DEBUG);
			if (IncludeContext)
				TryCaptureContextInfo (msg);
			WriteMessage (msg, broadcast);
		}

		public void D(string[] tags, string msg, bool broadcast = false)
		{
			Message m = new Message (tags, msg);
			if (IncludeContext)
				TryCaptureContextInfo (m);
			D (m, broadcast);
		}

		public void D(string tag, string msg, bool broadcast = false)
		{
			Message m = new Message (tag, msg);
			if (IncludeContext)
				TryCaptureContextInfo (m);
			D (m, broadcast);
		}

		/// <summary>
		/// Write a warning message to the logging system with a tag
		/// </summary>
		public void W(Message msg, bool broadcast = false)
		{
			msg.Tags.Add (TAG_TYPE_WARNING);
			if (IncludeContext)
				TryCaptureContextInfo (msg);
			WriteMessage (msg, broadcast);
		}

		public void W(string[] tags, string msg, bool broadcast = false)
		{
			Message m = new Message (tags, msg);
			TryCaptureContextInfo (m);
			W (m, broadcast);
		}

		public void W(string tag, string msg, bool broadcast = false)
		{
			Message m = new Message (tag, msg);
			TryCaptureContextInfo (m);
			W (new Message (tag, msg), broadcast);
		}

		/// <summary>
		/// Read a line of text from the log reader
		/// </summary>
		/// <returns></returns>
		public string ReadLine()
		{
			return reader?.ReadLine ();
		}
		#endregion

		#region INTERNAL_TYPES

		#endregion
	}
}
