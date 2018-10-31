using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Collections.Generic;
using Dullahan.Env;
using Dullahan.Logging;

[assembly: CommandProvider]

namespace Dullahan.Net
{
	/// <summary>
	/// Handles communication between Dullhan Body (Unity side) and 
	/// Dullahan Head (CLI side).
	/// </summary>
	[CommandProvider]
	[AddComponentMenu("Dullahan/Server"), DisallowMultipleComponent]
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

		[SerializeField]
		private int port = Endpoint.DEFAULT_PORT;

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

			server = null;
			users = new List<User>();
			pendingPackets = new Queue<SourcedPacket>();

			//setup environment
			Executor.Init ();

			//set user directory
			User.RegistryPath = Application.streamingAssetsPath;

			Log.IncludeContext = true; //TODO remove

			Debug.Log (TAG + " Starting Dullahan Server...");
			Run();
		}

		public void OnDestroy()
		{
			running = false;
			for (int i = 0; i < instance.users.Count; i++)
			{
				users[i].Host.Disconnect ();
			}

			server.Stop ();
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
				Endpoint c = new Endpoint (server.EndAcceptTcpClient (res));
				c.Name = Convert.ToBase64String (Guid.NewGuid ().ToByteArray ());
				c.dataRead += DataReceived;
				c.Flow = Endpoint.FlowState.bidirectional;
				c.ReadAsync ();

				User u = User.Load ("User");
				u.Host = c;
				u.Environment.SetOutput (c);
				User.Store (u);
				users.Add (u);
#if DEBUG
				Debug.Log (TAG + " Added new client.\nName: " + c.Name + "\nHost: " + c.ToString () + "\nEnv: " + u.Environment.ToString ());
#endif
			}
			catch (Exception e)
			{
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
			while(pendingPackets.Count > 0)
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
					sp.user.Host.Send(responsePacket); //TODO async brok?
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
			SourcedPacket sp = new SourcedPacket();

			foreach (User u in users)
			{
				if (u.Host.Equals(source))
				{
					sp.user = u;
					break;
				}
			}

			sp.packet = packet;

			lock (pendingPackets)
			{
				pendingPackets.Enqueue(sp);
			}

			source.ReadAsync ();
		}

		/// <summary>
		/// Send data to all connected clients
		/// </summary>
		/// <param name="packet"></param>
		public void Send(Packet packet)
		{
#if DEBUG
			Debug.Log(TAG + " Sending packet.\n" + packet.ToString());
#endif
			for(int i = 0; i < users.Count; i++)
			{
				users[i].Host.SendAsync(packet);
			}
		}
		public void Send(Packet.DataType type, string data)
		{
			Send(type, Message.TAG_DEFAULT, data);
		}
		public void Send(Packet.DataType type, string tag, string data)
		{
			Send(new Packet(type, tag, data));
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
		#endregion

		#region DEFAULT_COMMANDS

		/// <summary>
		/// Basic verification test for connection between Head and Body
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[Command (Invocation = "echo")]
		private static int Handshake(string[] args, Executor env)
		{
			if (args.Length < 2)
				return Executor.EXEC_FAILURE;

			env.Out.D ("SERVER", "Message was \"" + args[1] + "\"");
			return Executor.EXEC_SUCCESS;
		}

		/// <summary>
		/// Diconnect all remote clients from the server.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[Command(Invocation = "logout-all")]
		private static int Disconnect(string[] args, Executor env)
		{
			if (instance != null)
			{
				try
				{
					instance.running = false;
					for(int i = 0; i < instance.users.Count; i++)
					{
						instance.users[i].Host.Disconnect();
					}

					instance.server.Stop ();
					instance.server = null;
				}
				catch (Exception e)
				{
					//something went wrong
					Debug.LogException (e);
					return Executor.EXEC_FAILURE;
				}

				//successfully disconnected
				return Executor.EXEC_SUCCESS;
			}

			//nothing to disconnect from, strangely enough
			//wait, how did the server get this?
			//i'm gonna stop asking questions
			return Executor.EXEC_SKIP;
		}
		#endregion
	}
}
