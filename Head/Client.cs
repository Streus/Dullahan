using System;
using System.Net;
using Dullahan.Comm;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Dullahan
{
	/// <summary>
	/// Connects to a Dullahan Server running in a Unity instance.
	/// </summary>
	public class Client
	{
		#region STATIC_VARS

		private const string TAG = "[Client]";
		#endregion

		#region INSTANCE_VARS

		private int port;

		private IPAddress serverAddress;

		private TcpClient client;
		private NetworkStream stream;
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_METHODS

		public Client(IPAddress serverAddress, int port = Server.DEFAULT_PORT)
		{
			this.serverAddress = serverAddress;
			this.port = port;
		}

		public void Start()
		{
			Console.WriteLine ("Attempting to connect to " + serverAddress.ToString ());

			//establish connection
			IPEndPoint ipep = new IPEndPoint (serverAddress, port);
			client = new TcpClient ("localhost", port); //TODO paramatize host address
			stream = client.GetStream ();

			Console.WriteLine ("Connection Established!\nVerifying...");

			//send a basic handshake request to the Body
			byte[] sendBytes = Encoding.ASCII.GetBytes ("ping");
			stream.BeginWrite (sendBytes, 0, sendBytes.Length, SendFinished, stream);

			byte[] readBytes = new byte[1024];
			stream.BeginRead (readBytes, 0, readBytes.Length, ReadFinished, stream);

			while (true)
			{

			}
		}

		/// <summary>
		/// Read from the server has finished
		/// </summary>
		/// <param name="res"></param>
		private void ReadFinished(IAsyncResult res)
		{
			NetworkStream stream = (NetworkStream)res.AsyncState;

			byte[] byteV = new byte[1024];
			string message = "";

			int byteC = stream.EndRead (res);
			
#if DEBUG
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine ("\n" + TAG + " Read " + byteC + "B");
			Console.ResetColor ();
#endif

			message += Encoding.ASCII.GetString (byteV, 0, byteC);

			while (stream.DataAvailable)
			{
				stream.BeginRead (byteV, 0, byteV.Length, ReadFinished, stream);
			}

			Console.WriteLine (message);
		}

		private void SendFinished(IAsyncResult res)
		{
			NetworkStream stream = (NetworkStream)res.AsyncState;
			stream.EndWrite (res);

#if DEBUG
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine (TAG + " Finished sending");
			Console.ResetColor ();
#endif
		}
		#endregion

		#region INTERNAL_TYPES

		#endregion
	}
}
