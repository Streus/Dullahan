using Dullahan.Logging;
using Dullahan.Security;
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

		private IPAddress address;
		private int port;

		private TcpClient connection;
		private NetworkStream netStream;

		/// <summary>
		/// Connected to the remote host.
		/// </summary>
		public bool Connected { get { return connection != null && connection.Connected; } }

		private object stateMutex = new object ();

		private int readingCount;
		/// <summary>
		/// Receiving data from the remote host.
		/// </summary>
		public bool Reading
		{
			get { return readingCount > 0; }
			private set
			{
				lock (stateMutex)
				{
					readingCount += (value ? 1 : -1);
					if (readingCount < 0)
						readingCount = 0;
				}
			}
		}

		private int sendingCount;
		/// <summary>
		/// Sending data to the remote host.
		/// </summary>
		public bool Sending
		{
			get { return sendingCount > 0; }
			private set
			{
				lock (stateMutex)
				{
					sendingCount += (value ? 1 : -1);
					if (sendingCount < 0)
						sendingCount = 0;
				}
			}
		}

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

		/// <summary>
		/// Determines what "direction" data will flow to/from.
		/// </summary>
		public FlowState Flow { get; set; } = FlowState.bidirectional;

		private EncryptionFilter secureFilter;

		/// <summary>
		/// Data received
		/// </summary>
		private List<byte> storedData;

		/// <summary>
		/// Triggers when a read operation has finished, and data is available for use
		/// </summary>
		public event DataReceivedCallback dataRead;
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_METHODS

		/// <summary>
		/// Create an unconnected Endpoint
		/// </summary>
		/// <param name="address"></param>
		/// <param name="port"></param>
		public Endpoint(IPAddress address, int port = DEFAULT_PORT) : this()
		{
			this.address = address;
			this.port = port;

			connection = new TcpClient ();
		}

		/// <summary>
		/// Create a new Endpoint with an existing and connected TcpClient
		/// </summary>
		/// <param name="existingClient"></param>
		public Endpoint(TcpClient existingClient) : this()
		{
			address = null;
			port = -1;

			connection = existingClient;
			netStream = connection.GetStream ();
		}

		private Endpoint()
		{
			readingCount = 0;
			sendingCount = 0;
			storedData = new List<byte> ();
			secureFilter = null;
		}

		/// <summary>
		/// Begins a connection. Sends public key to remote endpoint on success, and accepts
		/// a symmetric key.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentNullException"/>
		/// <exception cref="InvalidOperationException"/>
		/// <exception cref="ObjectDisposedException"/>
		/// <exception cref="SocketException"/>
		public void Start()
		{
			if (!Connected)
			{
				//establish connection
				connection.Connect (address, port);
				netStream = connection.GetStream ();

				secureFilter = new EncryptionFilter ();

				//send public key
				Send (new Packet (Packet.DataType.command, Convert.ToBase64String (secureFilter.GetPublicKey ())), false);
				while (!HasPendingData ()) { }

				//take symmetric key (encrypted by self public key)
				Packet keyPacket = Read (encrypted: false)[0];
				secureFilter.SetSymmetricKey (Convert.FromBase64String (keyPacket.Data));
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Sucessfully connected to " + address + ":" + port);
#endif
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
				new Thread (() => {
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Starting async connect");
#endif
					Start ();
				}).Start ();
			}
		}

		/// <summary>
		/// Counterpart to Start(). Generates a symmetric key and passes it to the remote endpoint.
		/// </summary>
		public void Accept()
		{
			while (!HasPendingData ()) { }

			secureFilter = new EncryptionFilter ();

			//take public key
			Packet keyPacket = Read (encrypted: false)[0];
			secureFilter.SetOtherPublicKey (Convert.FromBase64String (keyPacket.Data));

			//send symmetric key (encrypted with recieved public key)
			Send (new Packet (Packet.DataType.response, Convert.ToBase64String (secureFilter.GetSymmetricKey ())), false);
		}

		public bool HasPendingData()
		{
			return netStream != null && netStream.DataAvailable;
		}

		/// <summary>
		/// Blocking read from the currently open connection
		/// </summary>
		/// <returns></returns>
		public Packet[] Read(bool encrypted = true)
		{
			if (HasPendingData() && (Flow & FlowState.incoming) == FlowState.incoming)
			{
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Beginning read");
#endif
				Reading = true;

				byte[] dataBuffer = new byte[DB_LENGTH];
				int byteC;
				while (netStream.DataAvailable)
				{
					byteC = netStream.Read (dataBuffer, 0, dataBuffer.Length);
					for (int i = 0; i < byteC; i++)
					{
						storedData.Add (dataBuffer[i]);
					}
				}

				Reading = false;

				Packet[] packets;
				byte[] finalData;
				if (encrypted && secureFilter.Ready)
					finalData = secureFilter.Decrypt (storedData.ToArray ());
				else
					finalData = storedData.ToArray ();
				int numRead = Packet.DeserializeAll (finalData, out packets);
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
		public void ReadAsync(bool encrypted = true)
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

						Packet[] packets = Read (encrypted);

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
		public void Send(Packet packet, bool encrypted = true)
		{
			if (netStream != null && (Flow & FlowState.outgoing) == FlowState.outgoing)
			{
				Sending = true;
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Sending \"" + packet.Data + "\"");
#endif
				//convert packet into binary data
				byte[] sendBytes = packet.ToBytes ();
				byte[] finalData;
				if (encrypted && secureFilter.Ready)
					finalData = secureFilter.Encrypt (sendBytes);
				else
					finalData = sendBytes;

				//begin send operation
				netStream.Write (finalData, 0, finalData.Length);

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
		public void SendAsync(Packet packet, bool encrypted = true)
		{
			if ((Flow & FlowState.outgoing) == FlowState.outgoing)
			{
				new Thread (() => {
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Started async send");
#endif
					Send (packet, encrypted);
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Finished sending");
#endif
				}).Start ();
			}
		}

		private void SendAsyncFinished(IAsyncResult res)
		{
			netStream.EndWrite (res);

			Sending = false;

		}

		public void Disconnect()
		{
			if(netStream != null)
				netStream.Close ();
			connection.Close();
			Sending = Reading = false;
			Disconnected = true;
			Flow = FlowState.none;
#if DEBUG
			Console.WriteLine (DEBUG_TAG + " Disconnected from " + address.ToString() + ":" + port);
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

		/// <summary>
		/// Indicates the type of traffic this endpoint will route.
		/// </summary>
		[Flags]
		public enum FlowState
		{
			/// <summary>
			/// All data will be ignored.
			/// </summary>
			none = 0x0,

			/// <summary>
			/// Only send data.
			/// </summary>
			outgoing = 0x1,

			/// <summary>
			/// Only recieve data.
			/// </summary>
			incoming = 0x2,

			/// <summary>
			/// Send and recieve data.
			/// </summary>
			bidirectional = outgoing | incoming
		}

		public delegate void DataReceivedCallback(Endpoint endpoint, Packet data);
		#endregion
	}
}
