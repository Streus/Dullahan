using System.Collections.Generic;

namespace Dullahan.Logging
{
	public class Log
	{
		#region STATIC_VARS

		private const string TAG_TYPE_DEBUG = "DEBUG";
		private const string TAG_TYPE_WARNING = "WARNING";
		private const string TAG_TYPE_ERROR = "ERROR";
		#endregion

		#region INSTANCE_VARS

		private ILogWriter writer;
		private ILogReader reader;
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

		private void WriteMessage(Message msg)
		{
			if (msg.Tags.Count <= 1)
				throw new System.ArgumentException("Provided an empty tag");

			writer.Write(msg);
		}

		/// <summary>
		/// Write an error message to the logging system with a tag
		/// </summary>
		public void E(Message msg)
		{
			msg.Tags.Add (TAG_TYPE_ERROR);
			WriteMessage(msg);
		}

		/// <summary>
		/// Write a debug message to the logging system with a tag
		/// </summary>
		public void D(Message msg)
		{
			msg.Tags.Add (TAG_TYPE_DEBUG);
			WriteMessage (msg);
		}

		/// <summary>
		/// Write a warning message to the logging system with a tag
		/// </summary>
		public void W(Message msg)
		{
			msg.Tags.Add (TAG_TYPE_WARNING);
			WriteMessage (msg);
		}
		#endregion

		#region INTERNAL_TYPES

		#endregion
	}
}
