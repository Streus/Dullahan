using Dullahan.Logging;
using Dullahan.Security;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Dullahan.Net
{
	/// <summary>
	/// Manages the connection to a Dullahan server/client
	/// </summary>
	public class Connection : ILogWriter, ILogReader, IDisposable
	{
		#region STATIC_VARS

		public const int DEFAULT_PORT = 8080;

		private const string DEBUG_TAG = "[DULCON]";

		private const string DC_MSG = "dulnet dc";
		#endregion

		#region INSTANCE_VARS

		public string Name { get; set; }

		private IPAddress address;
		private int port;

		private TcpClient tcpClient;
		private NetworkStream netStream;

		private Identity identity;
		private Identity otherIdentity;

		/// <summary>
		/// Connected to the remote host.
		/// </summary>
		public bool Connected { get { return tcpClient != null && tcpClient.Connected; } }

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

		/// <summary>
		/// Triggers when a read operation has finished, and data is available for use
		/// </summary>
		public event DataReceivedCallback dataRead;

		/// <summary>
		/// Triggers when this connection is disposed, closing the network pipe
		/// </summary>
		public event DisconnectCallback disconnected;
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_METHODS

		/// <summary>
		/// Create an unconnected Endpoint
		/// </summary>
		/// <param name="address"></param>
		/// <param name="port"></param>
		public Connection(IPAddress address, int port = DEFAULT_PORT) : this()
		{
			this.address = address;
			this.port = port;

			tcpClient = new TcpClient ();
		}

		/// <summary>
		/// Create a new Endpoint with an existing and connected TcpClient
		/// </summary>
		/// <param name="existingClient"></param>
		public Connection(TcpClient existingClient) : this()
		{
			address = null;
			port = -1;

			tcpClient = existingClient;
			netStream = tcpClient.GetStream ();
		}

		private Connection()
		{
			readingCount = 0;
			sendingCount = 0;
			identity = new Identity ();
			otherIdentity = null;
		}

		~Connection()
		{
			Dispose ();
#if DEBUG
			Console.WriteLine (DEBUG_TAG + " " + Name + " was culled or closed.");
#endif
		}

		/// <summary>
		/// Begins a client connection.
		/// </summary>
		/// <exception cref="AuthenticationException"/>
		public void Start(VerifyTrustCallback callback)
		{
			if (!Connected)
			{
				//establish connection
				tcpClient.Connect (address, port);
				netStream = tcpClient.GetStream ();

				Handshake (callback);
			}
		}

		/// <summary>
		/// Begins a server connection. Counterpart to Start().
		/// </summary>
		public void Accept(VerifyTrustCallback callback)
		{
			Handshake (callback);
		}

		private void Handshake(VerifyTrustCallback callback)
		{
			Send (new Packet (Packet.DataType.settings, identity.ToString ()));
			if (WaitForData (TimeSpan.FromSeconds (10)))
			{
				Packet p = Read ()[0];
				otherIdentity = new Identity (p.Data);
			}
			else
			{
				throw new TimeoutException ("Did not receive identity packet");
			}

			if (!IdentityRepository.Loaded)
			{
				if (!IdentityRepository.Load ())
				{
					throw new IOException ("Failed to load identites from \"" + IdentityRepository.RepoDir + "\"");
				}
			}

			bool addToTrusted;
			if (!IdentityRepository.CheckTrusted (otherIdentity))
			{
				if (callback (out addToTrusted, otherIdentity, (IPEndPoint)tcpClient.Client.RemoteEndPoint))
				{
					if (!addToTrusted)
						otherIdentity.Drop ();
					else
					{
						IdentityRepository.AddTrusted (otherIdentity);
						IdentityRepository.Store ();
					}

					Send (new Packet (Packet.DataType.response, "0"));
				}
				else
				{
					Send (new Packet (Packet.DataType.response, "1"));
					throw new ConnectionRefusedException ("User refused connection");
				}
			}

			if (WaitForData (TimeSpan.FromMinutes(5)))
			{
				Packet p = Read ()[0];
				int response;
				if (int.TryParse (p.Data, out response))
				{
					if (response != 0)
						throw new ConnectionRefusedException ("Remote refused connection");
				}
			}
			else
			{
				string message = "Timed out waiting for connection acknowledge";
				Send (new Packet (Packet.DataType.response, Log.TAG_TYPE_ERROR, message));
				throw new TimeoutException (message);
			}
		}

		public bool HasPendingData()
		{
			return netStream != null && netStream.DataAvailable;
		}

		/// <summary>
		/// Waits for data to be pending read. Returns true if data is pending, false if
		/// timeout was reached before any data was recieved.
		/// </summary>
		/// <param name="timeout"></param>
		/// <returns></returns>
		public bool WaitForData(TimeSpan timeout = default(TimeSpan))
		{
			DateTime start = DateTime.Now;
			while (timeout == default (TimeSpan) || DateTime.Now - start < timeout)
			{
				if (HasPendingData ())
					return true;
			}
			return false;
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
					using (MemoryStream readingStream = new MemoryStream ())
					using (BinaryReader reader = new BinaryReader (netStream, Encoding.UTF8, true))
					{
						byte[] buffer = new byte[1024];
						int byteC;
						while (netStream.DataAvailable
							&& (byteC = reader.Read (buffer, 0, buffer.Length)) != 0)
						{
							readingStream.Write (buffer, 0, byteC);
						}
						data = readingStream.ToArray ();
					}
				}

				//deserialize into packets
				Packet[] packets;
				int bytesRead = Packet.DeserializeAll (out packets, data);

				Reading = false;
#if DEBUG
				int leftOver = data.Length - bytesRead;
				Console.WriteLine (DEBUG_TAG + " Finished read (read: " + bytesRead + "B, leftover: " + leftOver + "B)");
#endif
				//check for dc packet
				foreach (Packet p in packets)
				{
					if (p.Type == Packet.DataType.command && p.Data == DC_MSG)
					{
						Flow = FlowState.none;
						Dispose ();
						break;
					}
				}
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
				ThreadPool.QueueUserWorkItem ((object data) => {
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Started async read #" + (int)data);
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
					finally
					{
#if DEBUG
						Console.WriteLine (DEBUG_TAG + " Finished async read #" + (int)data);
#endif
					}
				}, readingCount + 1);
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
				sendBytes = packet.ToBytes ();

				lock (netStream)
				{
					netStream.Write (sendBytes, 0, sendBytes.Length);
				}

				Sending = false;
#if DEBUG
				Console.WriteLine (DEBUG_TAG + " Finished sending " + sendBytes.Length + "B");
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
				ThreadPool.QueueUserWorkItem ((object data) => {
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Started async send #" + (int)data);
#endif
					Send (packet);
#if DEBUG
					Console.WriteLine (DEBUG_TAG + " Finished async send #" + (int)data);
#endif
				}, sendingCount + 1);
			}
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return Name.Equals(((Connection)obj).Name);
		}

		public override string ToString()
		{
			string str = nameof (Connection) + "(";
			IPEndPoint endpointInfo = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
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

		public void Dispose()
		{
			Send (new Packet (Packet.DataType.command, DC_MSG));
			tcpClient.Close ();
			Sending = Reading = false;
			Disconnected = true;
			Flow = FlowState.none;

			if (disconnected != null)
				disconnected (this);
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

		public delegate void DisconnectCallback(Connection connection);
		public delegate void DataReceivedCallback(Connection endpoint, Packet data);
		public delegate bool VerifyTrustCallback(out bool addToTrusted, Identity identity, IPEndPoint endpointInfo);
		#endregion
	}
}
