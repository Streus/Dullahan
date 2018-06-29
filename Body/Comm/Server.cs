using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Dullahan.Comm
{
	/// <summary>
	/// Handles communication between Dullhan Body (Unity side) and 
	/// Dullahan Head (CLI side).
	/// </summary>
	[AddComponentMenu("Dullahan/Server"), DisallowMultipleComponent]
	public class Server : MonoBehaviour
	{
		#region STATIC_VARS

		// The default IP the server will run on
		public const string DEFAULT_IP = "127.0.0.1";

		// The default port the server will run on
		public const int DEFAULT_PORT = 8080;

		// Marks the end of packets
		public const string EOF = "<EOF>";

		private static Server instance;
		#endregion

		#region INSTANCE_VARS

		private bool running;

		[SerializeField]
		private int port = DEFAULT_PORT;
		#endregion

		#region STATIC_METHODS

		public static Server GetInstance()
		{
			if (instance == null)
			{
				GameObject go = new GameObject("Dullahan Server");
				instance = go.AddComponent<Server>();
			}
			return instance;
		}
		#endregion

		#region INSTANCE_METHODS

		public void Awake()
		{
			DontDestroyOnLoad(gameObject);
			Run();
		}

		public void OnDestroy()
		{
			// Clean up connections
			running = false;
		}

		/// <summary>
		/// Check the running state of the server
		/// </summary>
		/// <returns></returns>
		public bool IsRunning()
		{
			return running;
		}

		/// <summary>
		/// Entrypoint for running the server with its current configuration.
		/// </summary>
		public void Run()
		{
			IPAddress ip = IPAddress.Parse(DEFAULT_IP);
			IPEndPoint localEndPoint = new IPEndPoint(ip, port);
			Socket listener = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			try
			{
				listener.Bind(localEndPoint);
				listener.Listen(100);

				StartCoroutine(WaitForConnection(listener));
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
		}

		private IEnumerator WaitForConnection(Socket listener)
		{
			running = true;
			while (running)
			{
				Debug.Log("Waiting for a connection...");
				listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

				yield return null;
			}
		}

		/// <summary>
		/// Callback for accepting a socket connection
		/// </summary>
		/// <param name="res"></param>
		private void AcceptCallback(IAsyncResult res)
		{
			Socket listener = (Socket)res.AsyncState;
			Socket handler = listener.EndAccept(res);

			Packet pack = new Packet();
			pack.workSock = handler;
			handler.BeginReceive(pack.buffer, 0, Packet.BUFFER_SIZE, 0, new AsyncCallback(ReadCallback), pack);
		}

		/// <summary>
		/// Callback for reading a socket connection
		/// </summary>
		/// <param name="res"></param>
		private void ReadCallback(IAsyncResult res)
		{
			string content = "";
			Packet pack = (Packet)res.AsyncState;
			Socket handler = pack.workSock;

			int bytesRead = handler.EndReceive(res);

			if(bytesRead > 0)
			{
				pack.data += Encoding.ASCII.GetString(pack.buffer, 0, bytesRead);

				content = pack.data;
				if(content.IndexOf(EOF) > -1)
				{
					Debug.Log("Read " + content.Length + "B from socket.\ncontent=" + content);
					Send(handler, content);
				}
				else
				{
					handler.BeginReceive(pack.buffer, 0, Packet.BUFFER_SIZE, 0, new AsyncCallback(ReadCallback), pack);
				}
			}
		}

		/// <summary>
		/// Send data over a socket
		/// </summary>
		/// <param name="handler">The socket to send with</param>
		/// <param name="data">the data to send</param>
		public void Send(Socket handler, string data)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(data);

			handler.BeginSend(bytes, 0, bytes.Length, 0, new AsyncCallback(SendCallback), handler);
		}

		/// <summary>
		/// Callback for sending over a socket
		/// </summary>
		/// <param name="res"></param>
		private void SendCallback(IAsyncResult res)
		{
			try
			{
				Socket handler = (Socket)res.AsyncState;

				int bytesSent = handler.EndSend(res);
				Debug.Log("Sent " + bytesSent + "B to client.");

				handler.Shutdown(SocketShutdown.Receive);
				handler.Close();
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
		}
		#endregion

		#region INTERNAL_TYPES

		
		#endregion
	}
}
