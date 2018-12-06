using Dullahan.Env;
using Dullahan.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using UnityEngine;

[assembly: CommandProvider]

namespace Dullahan.Net
{
	/// <summary>
	/// Handles communication between Dullhan Body (Unity side) and 
	/// Dullahan Head (CLI side).
	/// </summary>
	[CommandProvider]
	[AddComponentMenu ("Dullahan/Server"), DisallowMultipleComponent]
	public sealed class Server : MonoBehaviour
	{
		#region STATIC_VARS

		private const string TAG = "[DULSRV]";

		private static Server instance;
		#endregion

		#region INSTANCE_VARS

		/// <summary>
		/// Indicates the state of the listening thread
		/// </summary>
		private bool running;
#if UNITY_EDITOR
		private bool editorPlaying = true;
#endif
		[Header ("General")]
		[SerializeField]
		private int port = Endpoint.DEFAULT_PORT;

		[Header("Security")]
		[SerializeField]
		private bool useEncryption = false;

		[SerializeField]
		private ConnectionPolicy connectionPolicy = ConnectionPolicy.AskFirst;

		private TcpListener server;
		private List<User> users;

		/// <summary>
		/// Recieved packets that have not been read on the main thread yet
		/// </summary>
		private Queue<SourcedPacket> pendingPackets;
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

#if UNITY_EDITOR

#endif

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
				Debug.LogError (TAG + " More than one Dullahan Server active! Destroying " + gameObject.name);
				Destroy (gameObject);
			}
#if DEBUG
			//redirect stdout to the Unity console
			Console.SetOut(new Utility.ConsoleRedirector(LogType.Log));

			//redirect stderr to the Unity console
			Console.SetError(new Utility.ConsoleRedirector(LogType.Error));
#endif

#if UNITY_EDITOR
			//Rx to EditorApplication events
			UnityEditor.EditorApplication.pauseStateChanged += (UnityEditor.PauseState state) => {
				switch (state)
				{
				case UnityEditor.PauseState.Paused:
					editorPlaying = false;
#if DEBUG
					Debug.LogWarning (TAG + " Now rejecting commands");
#endif
					break;
				case UnityEditor.PauseState.Unpaused:
					editorPlaying = true;
#if DEBUG
					Debug.LogWarning (TAG + " Now accepting commands");
#endif
					break;
				}
			};

			UnityEditor.EditorApplication.quitting += () => {
				Stop ();
			};
#endif
			server = null;
			users = new List<User>();
			pendingPackets = new Queue<SourcedPacket>();

			//setup environment
			Executor.Init ();

			//set user directory
			User.RegistryPath = Application.streamingAssetsPath;

			Debug.Log (TAG + " Starting Dullahan Server...");
			Run();
		}

		public void OnDestroy()
		{
			Stop ();
			server = null;
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
			server = new TcpListener (IPAddress.Any, port);

			server.Start();
			running = true;

			server.BeginAcceptTcpClient (EndpointAcceptCallback, server);
#if DEBUG
			Debug.Log (TAG + " Server started; waiting for connections");
#endif
		}

		/// <summary>
		/// Loops waiting for incoming connections, adding a new Endpoint when one is found
		/// </summary>
		/// <param name="res"></param>
		private void EndpointAcceptCallback(IAsyncResult res)
		{
			try
			{
				if (connectionPolicy == ConnectionPolicy.AskFirst)
				{
					//TODO ask if can go ahead with connecting
					return;
				}

				Endpoint c = new Endpoint (server.EndAcceptTcpClient (res));
				c.Accept (useEncryption, (out bool addTotrusted, X509Certificate2 cert, IPEndPoint client) => {
					addTotrusted = false; //TODO
					switch (connectionPolicy)
					{
					case ConnectionPolicy.AcceptAll:
						return true;
					case ConnectionPolicy.AskForNew:
						//TODO ask user on new connection
						break;
					case ConnectionPolicy.DenyAll:
						return false;
					}
					return false;
				});
				c.Name = Convert.ToBase64String (Guid.NewGuid ().ToByteArray ());
				c.dataRead += DataReceived;
				c.Flow = Endpoint.FlowState.bidirectional;

				string identity = c.ConnectionIdentity;

				User u = User.Load ("User");
				u.Host = c;
				u.Environment.SetOutput (c);
				User.Store (u);
				users.Add (u);
#if DEBUG
				Debug.Log (TAG + " Added new client.\nName: " + c.Name + "\nHost: " + c.ToString () + "\nEnv: " + u.Environment.ToString ());
#endif
				c.ReadAsync ();
			}
			catch (ThreadInterruptedException tie)
			{
				Debug.LogWarning (TAG + " Stopped accepting connections");
#if DEBUG
				Debug.LogException (tie);
#endif
			}
			catch (Exception e)
			{
				Debug.LogError (TAG + " Failed adding new client");
#if DEBUG
				Debug.LogException (e);
#endif
			}
			finally
			{
				server.BeginAcceptTcpClient (EndpointAcceptCallback, server);
			}
		}

		public void Update()
		{
			//check for pending received data
			while (pendingPackets.Count > 0)
			{
				SourcedPacket sp;
				lock (pendingPackets)
				{
					sp = pendingPackets.Dequeue();
				}

				switch (sp.packet.Type)
				{
					case Packet.DataType.command:

					//run command and pass back success code
					Message m = new Message (sp.user.Environment.InvokeCommand (sp.packet.Data).ToString ());
					Packet responsePacket = new Packet (Packet.DataType.response, m);
					//wait for all logging messages to get through before signaling exec end
					while (sp.user.Host.Sending) { }
					sp.user.Host.SendAsync(responsePacket);
					break;

					default:
					//server only takes commands
					break;
				}
			}
		}

		/// <summary>
		/// Received data from a client.
		/// </summary>
		/// <param name="packet"></param>
		private void DataReceived(Endpoint source, Packet packet)
		{
#if DEBUG
			Debug.Log(TAG + " Received packet.\n" + packet.ToString());
#endif
#if UNITY_EDITOR
			if (!editorPlaying)
			{
#if DEBUG
				Debug.LogWarning (TAG + " Received command req, but bounced back because Editor is paused.");
#endif
				source.SendAsync (new Packet (Packet.DataType.logentry, Log.TAG_TYPE_WARNING, "Editor is currently paused; cannot execute!"));
				source.SendAsync (new Packet (Packet.DataType.response, Executor.EXEC_SKIP + ""));
			}
			else
			{
#endif
				SourcedPacket sp = new SourcedPacket ();

				foreach (User u in users)
				{
					if (u.Host.Equals (source))
					{
						sp.user = u;
						break;
					}
				}

				sp.packet = packet;

				lock (pendingPackets)
				{
					pendingPackets.Enqueue (sp);
				}

#if UNITY_EDITOR
			}
#endif
				source.ReadAsync ();
		}

		public void Stop()
		{
			running = false;
			for (int i = 0; i < instance.users.Count; i++)
			{
				users[i].Host.Send (new Packet (Packet.DataType.command, "SERVER", "dulnet disconnect")); //TODO dulnet utility commands
				users[i].Host.Disconnect ();
			}

			server.Stop ();
		}
		#endregion

		#region INTERNAL_TYPES

		/// <summary>
		/// A packet and its source client
		/// </summary>
		private struct SourcedPacket
		{
			public Packet packet;
			public User user;
		}

		public enum ConnectionPolicy
		{
			/// <summary>
			/// All incoming connections will be accepted, trusted or not
			/// </summary>
			AcceptAll,

			/// <summary>
			/// All incoming connections require confirmation, trusted or not
			/// </summary>
			AskFirst,

			/// <summary>
			/// Incoming connections from untrusted sources will prompt user for confirmation before connecting
			/// </summary>
			AskForNew,

			/// <summary>
			/// All incoming connections will be rejected, regardless of trust
			/// </summary>
			DenyAll
		}
		#endregion

		#region DEFAULT_COMMANDS

		/// <summary>
		/// Basic verification test for connection between Head and Body
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[Command (Invocation = "echo", Help = "Echoes back the first argument provided")]
		private static int EchoTest(string[] args, Executor env)
		{
			if (args.Length < 2)
				return Executor.EXEC_FAILURE;

			for(int i = 0; i < 1000; i++)
				env.Out.D ("SERVER", args[1]);
			return Executor.EXEC_SUCCESS;
		}
		#endregion
	}
}
