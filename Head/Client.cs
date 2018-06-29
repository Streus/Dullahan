using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using Dullahan.Comm;

namespace Dullahan
{
	/// <summary>
	/// Connects to a Dullahan Server running in a Unity instance.
	/// </summary>
	public class Client
	{
		#region STATIC_VARS

		#endregion

		#region INSTANCE_VARS

		public int Port { get; set; }

		private IPAddress serverAddress;

		private string response;

		private ManualResetEvent connectDone, sendDone, recieveDone;
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_METHODS

		public Client(IPAddress serverAddress, int port = Server.DEFAULT_PORT)
		{
			this.serverAddress = serverAddress;
			Port = port;

			connectDone = new ManualResetEvent(false);
			sendDone = new ManualResetEvent(false);
			recieveDone = new ManualResetEvent(false);
		}

		public void Start()
		{
			try
			{
				IPEndPoint remoteEP = new IPEndPoint(serverAddress, Port);

				Socket client = new Socket(serverAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
				connectDone.WaitOne();

				Send(client, "test string" + Server.EOF);
				sendDone.WaitOne();

				Receive(client);
				recieveDone.WaitOne();

				Console.WriteLine("Received " + response);
				Console.ReadLine();
				client.Close();
			}
			catch(Exception e)
			{
				Console.Error.WriteLine(e);
			}
		}

		private void ConnectCallback(IAsyncResult res)
		{
			try
			{
				Socket client = (Socket)res.AsyncState;
				client.EndConnect(res);

				Console.WriteLine("Connected to " + client.RemoteEndPoint.ToString());

				connectDone.Set();
			}
			catch(Exception e)
			{
				Console.Error.WriteLine(e);
			}
		}

		private void Receive(Socket client)
		{
			try
			{
				Packet pack = new Packet();
				pack.workSock = client;

				client.BeginReceive(pack.buffer, 0, Packet.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallback), pack);
			}
			catch(Exception e)
			{
				Console.Error.WriteLine(e);
			}
		}

		private void ReceiveCallback(IAsyncResult res)
		{
			try
			{
				Packet pack = (Packet)res.AsyncState;
				Socket client = pack.workSock;

				int bytesRead = client.EndReceive(res);

				if (bytesRead > 0)
				{
					pack.data += Encoding.ASCII.GetString(pack.buffer, 0, bytesRead);
					client.BeginReceive(pack.buffer, 0, Packet.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallback), pack);
				}
				else
				{
					if (pack.data.Length > 1)
						response = pack.data;

					recieveDone.Set();
				}
			}
			catch(Exception e)
			{
				Console.Error.WriteLine(e);
			}
		}

		private void Send(Socket client, string data)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(data);
			client.BeginSend(bytes, 0, bytes.Length, 0, new AsyncCallback(SendCallback), client);
		}

		private void SendCallback(IAsyncResult res)
		{
			try
			{
				Socket client = (Socket)res.AsyncState;

				int bytesSent = client.EndSend(res);
				Console.WriteLine("Sent " + bytesSent + "B to server.");

				sendDone.Set();
			}
			catch(Exception e)
			{
				Console.Error.WriteLine(e);
			}
		}
		#endregion

		#region INTERNAL_TYPES

		#endregion
	}
}
