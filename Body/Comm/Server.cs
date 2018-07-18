using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Collections.Generic;

namespace Dullahan.Net
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

		[SerializeField]
		private int port = Client.DEFAULT_PORT;

		private TcpListener server;
		private List<Client> clients;

		/// <summary>
		/// Clients in this set are not accepting sent data
		/// </summary>
		private HashSet<Client> sendBlacklist;

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

			//redirect stdout to the Unity console
			Console.SetOut(new Utility.ConsoleRedirector(LogType.Log));

			//redirect stderr to the Unity console
			Console.SetError(new Utility.ConsoleRedirector(LogType.Error));

			server = null;
			clients = new List<Client>();
			pendingPackets = new Queue<SourcedPacket>();
			sendBlacklist = new HashSet<Client>();

			Environment.Init ();
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

			server.Start();
			running = true;

			server.BeginAcceptTcpClient (ClientAcceptCallback, null);
		}

		/// <summary>
		/// Loops waiting for incoming connections, adding a new Client when one is found
		/// </summary>
		/// <param name="res"></param>
		private void ClientAcceptCallback(IAsyncResult res)
		{
			Client c = new Client(server.EndAcceptTcpClient(res));
			c.Name = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
			c.dataRead += DataReceived;
			c.Read();
			lock (clients)
			{
				clients.Add(c);
			}
#if DEBUG
			Debug.Log("Added new client.\nName: " + c.Name);
#endif
			server.BeginAcceptTcpClient(ClientAcceptCallback, null);
		}

		public void Update()
		{
			//find idle clients and start reading from them
			for(int i = 0; i < clients.Count; i++)
			{
				if (clients[i].Idle)
				{
#if DEBUG
					Debug.Log("Client " + clients[i].Name + " is idle. Starting read...");
#endif
					lock (clients)
					{
						clients[i].Read();
					}
				}
			}

			//check for pending received data
			while(pendingPackets.Count > 0)
			{
				lock (pendingPackets)
				{
					SourcedPacket sp = pendingPackets.Dequeue();
					switch (sp.packet.type)
					{
						case Packet.DataType.command:
							//run command and pass back success code
							Packet responsePacket = new Packet(Packet.DataType.response);
							if (sp.packet.data == "ping")
								sp.packet.data += " " + sp.client.Name;
							responsePacket.logResult = Environment.InvokeCommand(sp.packet.data);
							sp.client.Send(responsePacket);
							break;

						default:
							//server only takes commands
							break;
					}
				}
			}
		}

		/// <summary>
		/// Received data from a client.
		/// </summary>
		/// <param name="packet"></param>
		private void DataReceived(Client source, Packet packet)
		{
#if DEBUG
			Debug.Log("Received packet.\n" + packet.ToString());
#endif
			SourcedPacket sp = new SourcedPacket();
			sp.client = source;
			sp.packet = packet;
			lock (pendingPackets)
			{
				pendingPackets.Enqueue(sp);
			}
		}

		/// <summary>
		/// Send data to all connected clients
		/// </summary>
		/// <param name="packet"></param>
		public void Send(Packet packet)
		{
#if DEBUG
			Debug.Log("Sending packet.\n" + packet.ToString());
#endif
			for(int i = 0; i < clients.Count; i++)
			{
				lock (clients)
				{
					if (packet.type != Packet.DataType.management && !sendBlacklist.Contains(clients[i]))
						clients[i].Send(packet);
				}
			}
		}
		public void Send(Packet.DataType type, string data)
		{
			Send(new Packet(type, data));
		}

		#endregion

		#region INTERNAL_TYPES

		/// <summary>
		/// A packet and its source client
		/// </summary>
		private struct SourcedPacket
		{
			public Packet packet;
			public Client client;
		}
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
			if (args.Length < 2)
				return Environment.EXEC_FAILURE;

			instance.Send (Packet.DataType.response, args[1]);
			return Environment.EXEC_SUCCESS;
		}

		/// <summary>
		/// Diconnect all remote clients from the server.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[Command(Invocation = "logout-all", HelpFile = "res:logout")]
		private static int Disconnect(string[] args)
		{
			if (instance != null)
			{
				try
				{
					instance.running = false;
					for(int i = 0; i < instance.clients.Count; i++)
					{
						instance.clients[i].Disconnect();
					}

					instance.server.Stop ();
					instance.server = null;
				}
				catch (Exception e)
				{
					//something went wrong
					Debug.LogException (e);
					return Environment.EXEC_FAILURE;
				}

				//successfully disconnected
				return Environment.EXEC_SUCCESS;
			}

			//nothing to disconnect from, strangely enough
			//wait, how did the server get this?
			//i'm gonna stop asking questions
			return Environment.EXEC_SKIP;
		}

		[Command(Invocation = "mute", Help = "Tells the server to not send any packets to the given client")]
		private static int BlacklistClient(string[] args)
		{
			if (args.Length < 3)
				return Environment.EXEC_FAILURE;

			string operation = args[1];
			string clientName = args[2];
			for (int i = 0; i < instance.clients.Count; i++)
			{
				lock(instance.clients)
				{
					if(instance.clients[i].Name == clientName)
					{
						if (operation == "add")
						{
							instance.sendBlacklist.Add(instance.clients[i]);
#if DEBUG
							Debug.Log("No longer sending data to " + instance.clients[i].Name);
#endif
						}
						else if (operation == "rem")
						{
							instance.sendBlacklist.Remove(instance.clients[i]);
#if DEBUG
							Debug.Log("Resuming sending data to " + instance.clients[i].Name);
#endif
						}
						else
							return Environment.EXEC_FAILURE;
						return Environment.EXEC_SUCCESS;
					}
				}
			}

			return Environment.EXEC_FAILURE;
		}
		#endregion
	}
}
