using System.IO;
using System.Text;
using UnityEngine;

namespace Dullahan.Utility
{
	public class ConsoleRedirector : TextWriter
	{
		private StringBuilder buffer;
		private LogType type;

		public ConsoleRedirector(LogType type)
		{
			buffer = new StringBuilder ();
			this.type = type;
		}

		public override void Flush()
		{
			Debug.unityLogger.LogFormat(type, buffer.ToString());
			buffer.Length = 0;
		}

		public override void Write(char value)
		{
			lock (buffer)
			{
				buffer.Append (value);
				if (value == '\n')
					Flush ();
			}
		}

		public override void Write(string value)
		{
			lock (buffer)
			{
				buffer.Append (value);
				if (value != null)
				{
					if (value.Length > 0 && value[value.Length - 1] == '\n')
						Flush ();
				}
			}
		}

		public override void Write(char[] buffer, int index, int count)
		{
			Write (new string(buffer, index, count));
		}

		public override Encoding Encoding
		{
			get
			{
				return Encoding.Default;
			}
		}
	}
}
