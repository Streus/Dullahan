using Dullahan.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Dullahan.Net
{
	/// <summary>
	/// Manages the connection to a Dullahan server/client
	/// </summary>
	public class Endpoint : ILogWriter, ILogReader
	{
		#region STATIC_VARS

		public const int DEFAULT_PORT = 8080;

		private const string DEBUG_TAG = "[DULCON]";

		//length of the data buffer
		private const int DB_LENGTH = 1024;
		#endregion

		#region INSTANCE_VARS

		public string Name { get; set; }

		/// <summary>
		/// Connected to the remote host.
		/// </summary>
		public bool Connected { get; private set; }

		/// <summary>
		/// Receiving data from the remote host.
		/// </summary>
		public bool Reading { get; private set; }

		/// <summary>
		/// Sending data to the remote host.
		/// </summary>
		public bool Sending { get; private set; }

		/// <summary>
		/// Was connected and lost connection, or attempted connection failed.
		/// </summary>
		public bool Disconnected { get; private set; }

		/// <summary>
		/// The availability state of the Client. 
		/// Returns true if connected and no operations are underway.
		/// </summary>
		public bool Idle
		{
			get
			{
				return !Disconnected && Connected && !Reading && !Sending;
			}
		}

		public FlowState Flow { get; set; } = FlowState.bidirectional;

		/// <summary>
		/// Triggers when a read operation has finished, and data is available for use
		/// </summary>
		public event DataReceivedCallback dataRead;

		private int port;

		private IPAddress address;

		private TcpClient client;
		private NetworkStream stream;

		/// <summary>
		/// Data received
		/// </summary>
		private List<byte> storedData;
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_METHODS

		/// <summary>
		/// Create a new Client object that needs to be connected to a remote endpoint
		/// </summary>
		/// <param name="address">The address to which connection wiil be attempted</param>
		/// <param name="port">The port, what else?</param>
		public Endpoint(IPAddress address, int port = DEFAULT_PORT) : this()
		{
			this.address = address;
			this.port = port;

			Connected = Reading = Sending = false;

			client = new TcpClient();
			stream = null;
		}

		/// <summary>
		/// Create a new Client objct with an existing and connected TcpClient
		/// </summary>
		/// <param name="existingClient"></param>
		public Endpoint(TcpClient existingClient) : this()
		{
			client = existingClient;
			stream = client.GetStream();

			Connected = true;
			Reading = Sending = false;

			address = null;
			port = -1;
		}

		private Endpoint()
		{
			storedData = new List<byte> ();
		}

		public void Start()
		{
			if (!Connected)
			{
				//establish connection
				client.Connect (address, port);
				try
				{
					stream = client.GetStream ();
				}
				catch (InvalidOperationException)
				{
					//failed connection for some reason
					Console.ForegroundColor = ConsoleColor.Red;
					Console.Error.WriteLine ("Could not connect to server at " + address.ToString () + ":" + port);
					Console.ResetColor ();
					Disconnected = true;
					return;
				}

				//connection established
				Connected = true;
			}
		}

		/// <summary>
		/// If the Client is not connected to an endpoint, try connecting.
		/// This function is async.
		/// </summary>
		public void StartAsync()
		{
			if (!Connected)
			{
				//establish connection
				client.BeginConnect(address, port, StartAsyncFinished, client);
			}
		}

		private void StartAsyncFinished(IAsyncResult res)
		{
			try
			{
				stream = client.GetStream ();
			}
			catch (InvalidOperationException)
			{
				//failed connection for some reason
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine ("Could not connect to server at " + address.ToString() + ":" + port);
				Console.ResetColor ();
				Disconnected = true;
				return;
			}

			//connection established
			Connected = true;
		}

		public bool HasPendingData()
		{
			return stream != null && stream.DataAvailable;
		}

		/// <summary>
		/// Blocking read from the currently open connection
		/// </summary>
		/// <returns></returns>
		public Packet[] Read()
		{
			if (HasPendingData() && (Flow & FlowState.incoming) == FlowState.incoming)
			{
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Beginning read");
#endif
				Reading = true;

				byte[] dataBuffer = new byte[DB_LENGTH];
				int byteC;
				while (stream.DataAvailable)
				{
					byteC = stream.Read (dataBuffer, 0, dataBuffer.Length);
					for (int i = 0; i < byteC; i++)
					{
						storedData.Add (dataBuffer[i]);
					}
				}

				Reading = false;

				Packet[] packets;
				int numRead = Packet.DeserializeAll (storedData.ToArray (), out packets);
				int leftOver = storedData.Count - numRead;
				storedData.Clear ();

#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Finished read (read: " + numRead + "B, leftover: " + leftOver + "B)");
#endif
				return packets;
			}
			return null;
		}

		/// <summary>
		/// Read from the currently open connection asynchronously
		/// </summary>
		public void ReadAsync()
		{
			if ((Flow & FlowState.incoming) == FlowState.incoming)
			{
				//TODO thread pooling?
				new Thread (() => {
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Started read thread");
#endif
					try
					{
						while (!HasPendingData ()) { }

						Packet[] packets = Read ();

						if (dataRead != null)
						{
#if DEBUG
							Console.WriteLine (DEBUG_TAG + " Notifying listeners of new data (" + packets.Length + " packets)");
#endif
							for (int i = 0; i < packets.Length; i++)
							{
								dataRead (this, packets[i]);
							}
						}
					}
					catch (Exception e)
					{
						Console.Error.WriteLine (e.ToString ());
					}
				}).Start ();
			}
		}

		/// <summary>
		/// Blocking send over the open connection
		/// </summary>
		/// <param name="packet"></param>
		public void Send(Packet packet)
		{
			if (stream != null && (Flow & FlowState.outgoing) == FlowState.outgoing)
			{
				Sending = true;
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Sending \"" + packet.Data + "\"");
#endif
				//convert packet into binary data
				byte[] sendBytes = packet.ToBytes ();

				//begin send operation
				stream.Write (sendBytes, 0, sendBytes.Length);

				Sending = false;
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Finished sending");
#endif
			}
		}

		/// <summary>
		/// Send data over the open connection asynchronously
		/// </summary>
		/// <param name="packet"></param>
		public void SendAsync(Packet packet)
		{
			if (stream != null && (Flow & FlowState.outgoing) == FlowState.outgoing)
			{
#if DEBUG
				Console.WriteLine(DEBUG_TAG + " Sending \"" + packet.Data + "\"");
#endif
				//convert packet into binary data
				byte[] sendBytes = packet.ToBytes ();

				//begin send operation
				stream.BeginWrite(sendBytes, 0, sendBytes.Length, SendAsyncFinished, stream);

				Sending = true;
			}
		}

		private void SendAsyncFinished(IAsyncResult res)
		{
			NetworkStream stream = (NetworkStream)res.AsyncState;
			stream.EndWrite (res);

			Sending = false;
#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Finished sending");
#endif
		}

		/// <summary>
		/// Perform a Send and block for the response
		/// </summary>
		/// <param name="outbound"></param>
		/// <returns></returns>
		public bool SendAndRead(Packet outbound, out Packet[] inbound)
		{
			Send(outbound);
			while (!stream.DataAvailable) { }
			inbound = Read ();

			return true;
		}
		public bool SendAndWait(Packet outbound)
		{
			Packet[] inbound;
			return SendAndRead(outbound, out inbound);
		}

		public void Disconnect()
		{
			Send (new Packet (Packet.DataType.command, "__disconnect")); //TODO recieve disconnect
			stream.Close();
			client.Close();
			Sending = Reading = Connected = false;
			Disconnected = true;
			Flow = FlowState.none;
#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Disconnected from remote host");
#endif
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return Name.Equals(((Endpoint)obj).Name);
		}

		#region INTERFACE_METHODS
		public void Write(Message msg)
		{
			SendAsync (new Packet (Packet.DataType.logentry, msg));
		}

		public string ReadLine()
		{
			//TODO signal to the remote host that input is expected

			//wait for data
			Flow = FlowState.incoming;
			Packet p = Read ()[0];

			return p.Data;
		}
		#endregion
		#endregion

		#region INTERNAL_TYPES

		public delegate void DataReceivedCallback(Endpoint endpoint, Packet data);

		/// <summary>
		/// Indicates the type of traffic this client will route
		/// </summary>
		[Flags]
		public enum FlowState
		{
			none = 0x0,
			outgoing = 0x1,
			incoming = 0x2,
			bidirectional = outgoing | incoming
		}
		#endregion
	}
}
