using Dullahan.Logging;
using Dullahan.Security;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
		#endregion

		#region INSTANCE_VARS

		public string Name { get; set; }

		private IPAddress address;
		private int port;

		private TcpClient connection;
		private NetworkStream netStream;

		public bool Encrypted
		{
			get { return secureFilter != null && secureFilter.Enabled; }
			//set { if (secureFilter != null) secureFilter.Enabled = value; }
		}

		/// <summary>
		/// Public key of the other side connected to this endpoint
		/// </summary>
		public string ConnectionIdentity
		{
			get { return Convert.ToBase64String(secureFilter?.GetOtherPublicKey ()); }
		}

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
				Send (new Packet (Packet.DataType.command, Convert.ToBase64String (secureFilter.GetPublicKey ())));

				//take symmetric key (encrypted by self public key)
				while (!HasPendingData ()) { }
				Packet keyPacket = Read ()[0];
				secureFilter.SetSymmetricKey (Convert.FromBase64String (keyPacket.Data));
				secureFilter.Enabled = true;

				//recieve encryption pref
				while (!HasPendingData ()) { }
				Packet encryptPrefPacket = Read ()[0];
				bool useEncryption;
				if (bool.TryParse (encryptPrefPacket.Data, out useEncryption))
				{
					secureFilter.Enabled = useEncryption;
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Set encryption pref to " + useEncryption.ToString());
#endif
				}
				else
				{
#if DEBUG
					Console.Error.WriteLine (DEBUG_TAG + " Failed parse of encryption pref: \"" + encryptPrefPacket.Data + "\"");
#endif
					secureFilter.Enabled = false;
				}
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
		public void Accept(bool useEncryption)
		{
			while (!HasPendingData ()) { }

			secureFilter = new EncryptionFilter ();

			//take public key
			Packet keyPacket = Read ()[0];
			secureFilter.SetOtherPublicKey (Convert.FromBase64String (keyPacket.Data));

			//send symmetric key (encrypted with recieved public key)
			Send (new Packet (Packet.DataType.response, Convert.ToBase64String (secureFilter.GetSymmetricKey ())));

			//set encryption prefs on both sides
			Send (new Packet (Packet.DataType.command, useEncryption.ToString ()));
			secureFilter.Enabled = true;
		}

		public bool HasPendingData()
		{
			return netStream != null && netStream.DataAvailable;
		}

		/// <summary>
		/// Blocking read from the currently open connection
		/// </summary>
		/// <returns></returns>
		public Packet[] Read()
		{
			if (HasPendingData () && (Flow & FlowState.incoming) == FlowState.incoming)
			{
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Beginning read");
#endif
				Reading = true;

				//read from the network
				byte[] data;
				lock (netStream)
				{
					using (MemoryStream decryptStream = new MemoryStream ())
					using (MemoryStream readingStream = new MemoryStream ())
					using (BinaryReader reader = new BinaryReader (netStream, Encoding.UTF8, true))
					{
						byte[] buffer = new byte[sizeof(long)];
						int byteC;
						while (netStream.DataAvailable
							&& (byteC = reader.Read (buffer, 0, buffer.Length)) != 0)
						{
							//hit the end of a packet
							if (buffer.Length == sizeof (long)
								&& BitConverter.ToInt64 (buffer, 0) == Packet.FOOTER)
							{
								//attempt to decrypt contents of readingStream
								byte[] decryptedBytes = readingStream.ToArray ();
								lock (secureFilter)
								{
									decryptedBytes = secureFilter.Decrypt (decryptedBytes);
								}
								//write decrypted to another stream, reset reading stream
								decryptStream.Write (decryptedBytes, 0, decryptedBytes.Length);
								readingStream.Seek (0L, SeekOrigin.Begin);
							}
							//still reading an encrypted(?) packet
							else
							{
								readingStream.Write (buffer, 0, byteC);
							}
						}
						data = decryptStream.ToArray ();
					}
				}

				//deserialize into packets
				Packet[] packets;
				int bytesRead = Packet.DeserializeAll (data, out packets);

				Reading = false;
#if DEBUG
				int leftOver = data.Length - bytesRead;
				Console.WriteLine (DEBUG_TAG + " Finished read (read: " + bytesRead + "B, leftover: " + leftOver + "B)");
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
			if (netStream != null && (Flow & FlowState.outgoing) == FlowState.outgoing)
			{
				Sending = true;
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Sending \"" + packet.Data + "\"");
#endif
				//convert packet into binary data
				byte[] sendBytes;
				lock (secureFilter)
				{
					sendBytes = secureFilter.Encrypt (packet.ToBytes ());
				}

				using (MemoryStream sendStream = new MemoryStream ())
				{
					sendStream.Write (sendBytes, 0, sendBytes.Length);
					sendStream.Write (BitConverter.GetBytes (Packet.FOOTER), 0, sizeof (long));

					//begin send operation
					lock (netStream)
					{
						sendBytes = sendStream.ToArray ();
						netStream.Write (sendBytes, 0, sendBytes.Length);
					}
				}

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
			if ((Flow & FlowState.outgoing) == FlowState.outgoing)
			{
				new Thread (() => {
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Started async send");
#endif
					Send (packet);
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Finished sending");
#endif
				}).Start ();
			}
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

		public override string ToString()
		{
			string str = nameof (Endpoint) + "(";
			IPEndPoint endpointInfo = (IPEndPoint)connection.Client.RemoteEndPoint;
			str += endpointInfo.Address + ":" + endpointInfo.Port + ")";

			return str;
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
