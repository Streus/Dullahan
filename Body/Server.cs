using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Dullahan
{
	/// <summary>
	/// Handles communication between Dullhan Body (Unity side) and 
	/// Dullahan Head (CLI side).
	/// </summary>
	public class Server
	{
		#region STATIC_VARS

		// The default port that the server will run on
		public const int DEFAULT_PORT = 8080;

		// Marks the end of packets
		public const string EOF = "<EOF>";

		private static Server instance;
		#endregion

		#region INSTANCE_VARS

		private int Port { get; set; }

		private ManualResetEvent finished;
		#endregion

		#region STATIC_METHODS

		public static Server GetInstance()
		{
			if (instance == null)
				instance = new Server();
			return instance;
		}
		#endregion

		#region INSTANCE_METHODS

		private Server()
		{
			Port = DEFAULT_PORT;

			finished = new ManualResetEvent(false);
		}

		~Server()
		{

		}

		/// <summary>
		/// 
		/// </summary>
		public void Run()
		{
			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			IPAddress ip = ipHostInfo.AddressList[0];
			IPEndPoint localEndPoint = new IPEndPoint(ip, Port);
			Socket listener = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			try
			{
				listener.Bind(localEndPoint);
				listener.Listen(100);

				while(true)
				{
					finished.Reset();

					Debug.Log("Waiting for a connection...");
					listener.BeginAccept(new AsyncCallback(AcceptConnection), listener);

					finished.WaitOne(5000);
				}
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
		}

		/// <summary>
		/// Callback for accepting a socket connection
		/// </summary>
		/// <param name="res"></param>
		private void AcceptConnection(IAsyncResult res)
		{
			finished.Set();

			Socket listener = (Socket)res.AsyncState;
			Socket handler = listener.EndAccept(res);

			State s = new State();
			s.workSock = handler;
			handler.BeginReceive(s.buffer, 0, State.BUFFER_SIZE, 0, new AsyncCallback(ReadConnection), s);
		}

		/// <summary>
		/// Callback for reading a socket connection
		/// </summary>
		/// <param name="res"></param>
		private void ReadConnection(IAsyncResult res)
		{
			string content = "";
			State s = (State)res.AsyncState;
			Socket handler = s.workSock;

			int bytesRead = handler.EndReceive(res);

			if(bytesRead > 0)
			{
				s.data += Encoding.ASCII.GetString(s.buffer, 0, bytesRead);

				content = s.data;
				if(content.IndexOf(EOF) > -1)
				{
					Debug.Log("Read " + content.Length + " bytes from socket.\ncontent=" + content);
					Send(handler, content);
				}
				else
				{
					handler.BeginReceive(s.buffer, 0, State.BUFFER_SIZE, 0, new AsyncCallback(ReadConnection), s);
				}
			}
		}

		/// <summary>
		/// Send data over a socket
		/// </summary>
		/// <param name="handler">The socket to send with</param>
		/// <param name="data">the data to send</param>
		private void Send(Socket handler, string data)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(data);

			handler.BeginSend(bytes, 0, bytes.Length, 0, new AsyncCallback(SendConnection), handler);
		}

		/// <summary>
		/// Callback for sending over a socket
		/// </summary>
		/// <param name="res"></param>
		private void SendConnection(IAsyncResult res)
		{
			try
			{
				Socket handler = (Socket)res.AsyncState;

				int bytesSent = handler.EndSend(res);
				Debug.Log("Sent " + bytesSent + " to client.");

				handler.Shutdown(SocketShutdown.Both);
				handler.Close();
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
		}
		#endregion

		#region INTERNAL_TYPES

		/// <summary>
		/// For reading client data
		/// </summary>
		public class State
		{
			public const int BUFFER_SIZE = 1024;

			public Socket workSock = null;
			public byte[] buffer;
			public string data;
		}
		#endregion
	}
}
