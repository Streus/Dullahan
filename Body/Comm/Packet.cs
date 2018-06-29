using System.Net.Sockets;

namespace Dullahan.Comm
{
	/// <summary>
	/// For reading client data
	/// </summary>
	public class Packet
	{
		public const int BUFFER_SIZE = 1024;

		public Socket workSock = null;
		public byte[] buffer = new byte[BUFFER_SIZE];
		public string data = "";
	}
}
