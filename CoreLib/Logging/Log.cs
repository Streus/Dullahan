
namespace Dullahan.Logging
{
	public class Log
	{
		#region STATIC_VARS

		public const string TAG_TYPE_DEBUG = "DEBUG";
		public const string TAG_TYPE_WARNING = "WARNING";
		public const string TAG_TYPE_ERROR = "ERROR";

		private static Log instance;
		/// <summary>
		/// Sends a log messages to any broadcast listeners
		/// </summary>
		public static Log Broadcast
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
		#endregion

		#region INSTANCE_VARS

		private ILogWriter writer;
		private ILogReader reader;
		#endregion

		#region STATIC_METHODS

		
		#endregion

		#region INSTANCE_METHODS

		public void SetOutput(ILogWriter w)
		{
			writer = w;
		}

		public void SetInput(ILogReader r)
		{
			reader = r;
		}

		private void WriteMessage(Message msg, bool broadcast)
		{
			if (msg.Tags.Count <= 1)
				throw new System.ArgumentException("Provided an empty tag");

			writer?.Write(msg);
			if (broadcast)
			{
				Broadcast.writer?.Write (msg);
			}
		}

		/// <summary>
		/// Write an error message to the logging system with a tag
		/// </summary>
		public void E(Message msg, bool broadcast = false)
		{
			msg.Tags.Add (TAG_TYPE_ERROR);
			WriteMessage(msg, broadcast);
		}

		public void E(string[] tags, string msg, bool broadcast = false)
		{
			E (new Message (tags, msg), broadcast);
		}

		/// <summary>
		/// Write a debug message to the logging system with a tag
		/// </summary>
		public void D(Message msg, bool broadcast = false)
		{
			msg.Tags.Add (TAG_TYPE_DEBUG);
			WriteMessage (msg, broadcast);
		}

		public void D(string[] tags, string msg, bool broadcast = false)
		{
			D (new Message (tags, msg), broadcast);
		}

		/// <summary>
		/// Write a warning message to the logging system with a tag
		/// </summary>
		public void W(Message msg, bool broadcast = false)
		{
			msg.Tags.Add (TAG_TYPE_WARNING);
			WriteMessage (msg, broadcast);
		}

		public void W(string[] tags, string msg, bool broadcast = false)
		{
			W (new Message (tags, msg), broadcast);
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
