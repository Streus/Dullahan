using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Dullahan.Logging;

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

		/// <summary>
		/// Indicates the state of the listening thread
		/// </summary>
		private volatile bool running;

		/// <summary>
		/// Data recieved from the client
		/// </summary>
		private string clientData;

		/// <summary>
		/// Indicates if there is data to be read from clientData
		/// </summary>
		private volatile bool unreadData;

		/// <summary>
		/// Used to lock clientData for the main Unity thread
		/// </summary>
		private object dataLock = new object ();

		[SerializeField]
		private int port = DEFAULT_PORT;

		private TcpListener server;
		private TcpClient client;
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

			if (instance == null)
				instance = this;
			else
			{
				Debug.LogError ("More than one Dullahan Server active! Destroying...");
				Destroy (gameObject);
			}

			server = null;
			client = null;

			Run();
		}

		public void OnDestroy()
		{
			// Clean up connections
			lock (dataLock)
				running = false;
		}

		/// <summary>
		/// Check the running state of the server. Thread safe.
		/// </summary>
		/// <returns></returns>
		public bool IsRunning()
		{
			lock (dataLock)
			{
				return running;
			}
		}

		public void Update()
		{
			//check on the listening thread
			if (IsRunning ())
			{
				lock (dataLock)
				{
					if (unreadData)
					{
						int success = Log.InvokeCommand (clientData);
					}
				}
			}
		}

		/// <summary>
		/// Entrypoint for running the server with its current configuration.
		/// </summary>
		public void Run()
		{
			IPAddress ip = IPAddress.Parse(DEFAULT_IP);
			server = new TcpListener (ip, port);

			server.Start ();
			running = true;

			Thread listeningThread = new Thread (Listen);
		}

		private void Listen()
		{
			byte[] bytes = new byte[Packet.BUFFER_SIZE];
			lock (dataLock)
			{
				clientData = "";
				unreadData = false;
			}

			//wait for client connection
			NetworkStream stream;
			lock (dataLock)
			{
				client = server.AcceptTcpClient ();
				Debug.Log ("[DUL] Connection from client.");
				stream = client.GetStream ();
			}

			//read incoming traffic from client
			while (IsRunning())
			{
				int i;
				while ((i = stream.Read (bytes, 0, bytes.Length)) != 0)
				{
					lock (dataLock)
						clientData = Encoding.ASCII.GetString (bytes, 0, i);
				}
				lock (dataLock)
					unreadData = true;
			}

			//close connection
			lock (dataLock)
			{
				client.Close ();
				client = null;
			}
		}

		/// <summary>
		/// Send data to the currently connected client
		/// </summary>
		/// <param name="data"></param>
		public void Send(string data)
		{
			if (client != null)
			{
				Thread pushThread = new Thread (Push);
			}
		}

		private void Push(object data)
		{
			string sdata = (string)data;
			byte[] bytes = Encoding.ASCII.GetBytes (sdata);
			lock (dataLock)
			{
				NetworkStream stream = client.GetStream ();
				stream.Write (bytes, 0, bytes.Length);
			}
		}
		#endregion

		#region INTERNAL_TYPES

		
		#endregion
	}
}
