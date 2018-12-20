using System;

namespace Dullahan.Net
{
	public class ConnectionRefusedException : Exception
	{
		public ConnectionRefusedException(string message) : base (message) { }
		public ConnectionRefusedException(string message, Exception innerException) : base (message, innerException) { }
	}
}
