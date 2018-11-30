using Dullahan.Logging;
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
			get { return secureStream != null && secureStream.IsEncrypted; }
		}

		/// <summary>
		/// Public key of the other side connected to this endpoint
		/// </summary>
		public string ConnectionIdentity
		{
			get { return secureStream?.RemoteCertificate.GetPublicKeyString(); }
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

		private SslStream secureStream;

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
			secureStream = null;
		}

		/// <summary>
		/// Begins a client connection.
		/// </summary>
		/// <exception cref="AuthenticationException"/>
		public void Start()
		{
			if (!Connected)
			{
				//establish connection
				connection.Connect (address, port);
				netStream = connection.GetStream ();

				while (!HasPendingData ()) { }
				Packet p = Read ()[0];
				bool useEncryption;
				if (!bool.TryParse (p.Data, out useEncryption))
				{
					throw new Exception ("Received malformed encryption setting from server");
				}

				try
				{
					secureStream = new SslStream (netStream, true, ValidateRemoteCert);

					secureStream.AuthenticateAsClient (address.ToString (), null/* TODO client certs*/, true);
				}
				catch (AuthenticationException ae)
				{
					//authentication failed
					Console.ForegroundColor = ConsoleColor.Red;
					Console.Error.WriteLine ("Authentication with " + address + " failed");
					Console.Error.WriteLine ("Cause: " + ae.Message);
					Console.ResetColor ();
					connection.Close ();
					Disconnected = true;
					return;
				}
			}
		}

		/// <summary>
		/// Begins a server connection. Counterpart to Start().
		/// </summary>
		public void Accept(bool useEncryption)
		{
			try
			{
				secureStream = new SslStream (netStream, true, ValidateRemoteCert);

				secureStream.AuthenticateAsServer (null/* TODO Server certs*/, true, true);
			}
			catch (AuthenticationException ae)
			{
				//authentication failed
				Console.Error.WriteLine ("Authentication with " 
					+ ((IPEndPoint)connection.Client.RemoteEndPoint).Address + " failed");
				Console.Error.WriteLine ("Cause: " + ae.Message);
				connection.Close ();
				Disconnected = true;
				return;
			}
		}

		private bool ValidateRemoteCert(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
		{
			if (errors == SslPolicyErrors.None)
				return true;
#if DEBUG
			Console.Error.WriteLine("Server validation error: " + errors);
#endif
			return false;
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
					using (MemoryStream readingStream = new MemoryStream ())
					using (BinaryReader reader = new BinaryReader (secureStream, Encoding.UTF8, true))
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
				sendBytes = packet.ToBytes ();

				lock (secureStream)
				{
					secureStream.Write (sendBytes, 0, sendBytes.Length);
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
			if(secureStream != null)
				secureStream.Close ();
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
