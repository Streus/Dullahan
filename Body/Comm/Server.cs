using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Dullahan.Logging;

namespace Dullahan.Comm
{
	/// <summary>
	/// Handles communication between Dullhan Body (Unity side) and 
	/// Dullahan Head (CLI side).
	/// </summary>
	[AddComponentMenu("Dullahan/Server"), DisallowMultipleComponent]
	public sealed class Server : MonoBehaviour
	{
		#region STATIC_VARS

		// The default IP the server will run on
		public const string DEFAULT_IP = "127.0.0.1";

		// Marks the end of packets
		public const string EOF = "\0";

		private const string TAG = "[DUL]";

		private const int CDB_LENGTH = 1024;

		private static Server instance;
		#endregion

		#region INSTANCE_VARS

		/// <summary>
		/// Indicates the state of the listening thread
		/// </summary>
		private bool running;

		/// <summary>
		/// Data recieved from the client
		/// </summary>
		private string clientData;
		private byte[] clientDataBuffer;

		/// <summary>
		/// Indicates if there is data to be read from clientData
		/// </summary>
		private bool unreadData;

		[SerializeField]
		private int port = Protocol.DEFAULT_PORT;

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
			{
				instance = this;
				gameObject.name = "Dullahan Server";
			}
			else
			{
				Debug.LogError (TAG + " More than one Dullahan Server active! Destroying...");
				Destroy (gameObject);
			}

			server = null;
			client = null;

			Debug.Log (TAG + " Initializing Log...");
			Log.Init ();
			Debug.Log (TAG + " Starting Dullahan Server...");
			Run();
		}

		public void OnDestroy()
		{
			Disconnect (null);
		}

		/// <summary>
		/// Check the running state of the server. Thread safe.
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
			server = new TcpListener (ip, port);

			server.Start ();
			running = true;

			server.BeginAcceptTcpClient (ClientAcceptCallback, server);
			StartCoroutine(WaitForAccept ());
		}

		private void ClientAcceptCallback(IAsyncResult res)
		{
			TcpListener listener = (TcpListener)res.AsyncState;
			client = listener.EndAcceptTcpClient (res);
		}

		private IEnumerator WaitForAccept()
		{
			while (client == null)
			{
				Debug.Log (TAG + " Waiting..."); //DEBUG
				yield return new WaitForEndOfFrame();
			}
			Debug.Log (TAG + " Starting Listen");
			StartCoroutine (Listen ());
		}

		private IEnumerator Listen()
		{
			clientData = "";
			clientDataBuffer = new byte[CDB_LENGTH];
			unreadData = false;

			//wait for client connection
			NetworkStream stream;
			stream = client.GetStream ();

			Debug.Log (TAG + " Starting Read"); //DEBUG

			//read incoming traffic from client
			while (IsRunning())
			{
				Debug.Log (TAG + " Listening for data..."); //DEBUG
				yield return null;

				if (!unreadData)
					stream.BeginRead (clientDataBuffer, 0, clientDataBuffer.Length, ReadFinished, stream);
				else
				{
					Debug.Log (TAG + " Invoking \"" + clientData + "\""); //DEBUG
					int success = Log.InvokeCommand (clientData);
					clientData = "";
					clientDataBuffer = new byte[CDB_LENGTH];
					unreadData = false;
				}
			}

			Disconnect(null);

			Debug.Log (TAG + " Closed connection");
		}

		private void ReadFinished(IAsyncResult res)
		{
			NetworkStream stream = (NetworkStream)res.AsyncState;

			int byteC = stream.EndRead (res);
			Debug.Log (TAG + " Read " + byteC + "B"); //DEBUG

			clientData += Encoding.ASCII.GetString (clientDataBuffer, 0, byteC);
			Debug.Log ("clientData: \"" + clientData + "\""); //DEBUG

			while (stream.DataAvailable)
			{
				stream.BeginRead (clientDataBuffer, 0, clientDataBuffer.Length, ReadFinished, stream);
			}

			unreadData = true;
		}

		/// <summary>
		/// Send data to the currently connected client
		/// </summary>
		/// <param name="data"></param>
		public void Send(string data)
		{
			Debug.Log (TAG + " Sending " + data + " to client."); //DEBUG
			byte[] bytes = Encoding.ASCII.GetBytes (data);
			client.GetStream ().BeginWrite (bytes, 0, bytes.Length, SendFinished, client);
		}
		private void SendFinished(IAsyncResult res)
		{
			client.GetStream ().EndWrite (res);
		}
		#endregion

		#region INTERNAL_TYPES

		#endregion

		#region DEFAULT_COMMANDS

		/// <summary>
		/// Basic verification test for connection between Head and Body
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[Command (Invocation = "ping", HelpFile = "res:ping")]
		private static int Handshake(string[] args)
		{
			if (instance == null)
				return Log.EXEC_FAILURE;

			instance.Send ("Connection to '" + Application.productName + "' Established!");
			return Log.EXEC_SUCCESS;
		}

		/// <summary>
		/// Diconnect the remote client from the server.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[Command(Invocation = "logout", HelpFile = "res:logout")]
		private static int Disconnect(string[] args)
		{
			if (instance != null)
			{
				try
				{
					instance.running = false;
					instance.client.GetStream ().Close ();
					instance.client.Close ();
					instance.client = null;

					instance.server.Stop ();
					instance.server = null;
				}
				catch (Exception e)
				{
					//something went wrong
					Debug.LogException (e);
					return Log.EXEC_FAILURE;
				}

				//successfully disconnected
				return Log.EXEC_SUCCESS;
			}

			//nothing to disconnect from, strangely enough
			//wait, how did the server get this?
			//i'm gonna stop asking questions
			return Log.EXEC_SKIP;

		}
		#endregion
	}
}
