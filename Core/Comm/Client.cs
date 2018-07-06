using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Dullahan.Comm
{
	/// <summary>
	/// Connects to a Dullahan Server running in a Unity instance.
	/// </summary>
	public class Client
	{
		#region STATIC_VARS

		private const string DEBUG_TAG = "[DEBUG]";

		private const int SDB_LENGTH = 1024;
		#endregion

		#region INSTANCE_VARS

		public bool Connnected { get; private set; }

		private int port;

		private IPAddress serverAddress;

		private TcpClient client;
		private NetworkStream stream;

		private string serverData;
		private byte[] serverDataBuffer;
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_METHODS

		public Client(IPAddress serverAddress, int port = Protocol.DEFAULT_PORT)
		{
			this.serverAddress = serverAddress;
			this.port = port;

			Connnected = false;

			serverData = "";
			serverDataBuffer = new byte[SDB_LENGTH];
		}

		public void Start()
		{
			//establish connection
			IPEndPoint ipep = new IPEndPoint (serverAddress, port);
			client = new TcpClient ("localhost", port); //TODO paramatize host address
			stream = client.GetStream ();

			//send a basic handshake request to the Body
			byte[] sendBytes = Encoding.ASCII.GetBytes ("ping");
			stream.BeginWrite (sendBytes, 0, sendBytes.Length, SendFinished, stream);

			stream.BeginRead (serverDataBuffer, 0, serverDataBuffer.Length, ReadFinished, stream);
		}

		/// <summary>
		/// Read from the server has finished
		/// </summary>
		/// <param name="res"></param>
		private void ReadFinished(IAsyncResult res)
		{
			Connnected = true;
			NetworkStream stream = (NetworkStream)res.AsyncState;

			int byteC = stream.EndRead (res);
			
#if DEBUG
			Console.WriteLine ("\n" + DEBUG_TAG + " Read " + byteC + "B");
#endif

			serverData += Encoding.ASCII.GetString (serverDataBuffer, 0, byteC);

			while (stream.DataAvailable)
			{
				stream.BeginRead (serverDataBuffer, 0, serverDataBuffer.Length, ReadFinished, stream);
			}

			Console.WriteLine (serverData);
		}

		private void SendFinished(IAsyncResult res)
		{
			NetworkStream stream = (NetworkStream)res.AsyncState;
			stream.EndWrite (res);

#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Finished sending");
#endif
		}
		#endregion

		#region INTERNAL_TYPES

		#endregion
	}
}
