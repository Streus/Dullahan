using System;
using System.Net;
using Dullahan.Comm;
using System.Net.Sockets;
using System.Text;

namespace Dullahan
{
	/// <summary>
	/// Connects to a Dullahan Server running in a Unity instance.
	/// </summary>
	public class Client
	{
		#region STATIC_VARS

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
			client = new TcpClient (ipep);
			stream = client.GetStream ();

			Console.WriteLine ("Connection Established!\n Verifying...");

			//send a basic handshake request to the Body
			byte[] bytes = Encoding.ASCII.GetBytes ("ping");
			stream.Write (bytes, 0, bytes.Length);

			bytes = new byte[256];
			int i;
			string response = "";
			while ((i = stream.Read (bytes, 0, bytes.Length)) != 0)
			{
				response = Encoding.ASCII.GetString (bytes, 0, i);
			}

			if (response == "")
			{
				Console.WriteLine ("Connection Failed. Aborting...");
				return;
			}

			Console.WriteLine (response + "\nWelcome to Dullahan! Start hacking!");
			Run ();
		}

		private void Run()
		{
			while (true)
			{
				Console.WriteLine ("AAAAAAAAAAAAAAAAAAAAAAAA");
			}
		}
		#endregion

		#region INTERNAL_TYPES

		#endregion
	}
}
