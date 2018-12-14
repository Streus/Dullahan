using Dullahan.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dullahan.Net
{
	/// <summary>
	/// Wrapper for data sent between clients and servers
	/// </summary>
	public class Packet
	{
		#region STATIC_VARS

		private const string TAG = "[DULPKT]";

		private const string DEFAULT_DATA = "";
		private const int MIN_PACKET_SIZE = 28;

		public const long FOOTER = ~0L;
		#endregion

		#region INSTANCE_VARS

		public DataType Type { get; protected set; }
		private DateTime timeStamp;
		protected string[] tags = new string[] { };
		public string Data { get; protected set; } = DEFAULT_DATA;
		public string Context { get; protected set; } = DEFAULT_DATA;
		#endregion

		#region STATIC_METHODS

		/// <summary>
		/// Converts a byte array into an array of Packets
		/// </summary>
		/// <param name="raw"></param>
		/// <param name="packets">The deserialized packets</param>
		/// <returns>Number of read bytes</returns>
		public static int DeserializeAll (out Packet[] packets, byte[] raw)
		{
			int sk = 0;
			List<Packet> pList = new List<Packet> ();
			while(sk < raw.Length)
			{
				Packet p;
				try
				{
					sk += Deserialize (out p, raw, sk);
					pList.Add (p);
				}
				catch (ArgumentException ae)
				{
					//just be done?
					//TODO figure out why there are hanging bytes on the ends of some packets
#if DEBUG
					Console.Error.WriteLine (TAG + " " + ae.ToString());
					Console.Error.WriteLine (TAG + " Encountered hanging bytes: " + (raw.Length - sk) + "B");
#endif
					break;
				}
			}
			packets = pList.ToArray ();
			return sk;
		}
		private static int Deserialize(out Packet packet, byte[] source, int offset)
		{
			if (source.Length - offset < MIN_PACKET_SIZE)
				throw new ArgumentException ((source.Length - offset) + "B is too small for a packet; must be >= " + MIN_PACKET_SIZE);

			packet = new Packet ();

			int seekPoint = offset;

			//full packet length, 4 bytes
			int fullSize = BitConverter.ToInt32 (source, seekPoint);
			seekPoint += sizeof (int);
#if DEBUG
			Console.WriteLine (TAG + " Deserializing packet of size " + fullSize + "B");
#endif
			byte[] raw = source;

			//packet type, 4 bytes
			packet.Type = (DataType)BitConverter.ToInt32 (raw, seekPoint);
			seekPoint += sizeof (int);

			if (packet.Type == DataType.logentry)
			{
				//time stamp, 8 bytes
				packet.timeStamp = new DateTime (BitConverter.ToInt64 (raw, seekPoint));
				seekPoint += sizeof (long);
			}

			//tags count, 4 bytes
			packet.tags = new string[BitConverter.ToInt32 (raw, seekPoint)];
			seekPoint += sizeof (int);

			//data length, 4 bytes
			int dataLength = BitConverter.ToInt32 (raw, seekPoint);
			seekPoint += sizeof (int);

			//context length, 4 bytes
			int contextLength = BitConverter.ToInt32 (raw, seekPoint);
			seekPoint += sizeof (int);

			//individual tags, ? bytes
			for (int i = 0; i < packet.tags.Length; i++)
			{
				//tag string length, 4 bytes
				int tagLength = BitConverter.ToInt32 (raw, seekPoint);
				seekPoint += sizeof (int);

				//tag bytes, ? bytes
				packet.tags[i] = Encoding.UTF8.GetString (raw, seekPoint, tagLength);
				seekPoint += tagLength;
			}

			//data contents, ? bytes
			packet.Data = Encoding.UTF8.GetString (raw, seekPoint, dataLength);
			seekPoint += dataLength;

			//context contents, ? bytes
			packet.Context = Encoding.UTF8.GetString (raw, seekPoint, contextLength);
			seekPoint += contextLength;

#if DEBUG
			if (seekPoint - offset > fullSize)
				Console.Error.WriteLine ("Deserialized more than packet size (" + (seekPoint - offset) + " > " + fullSize + ")");
#endif

				return fullSize;
		}
		#endregion

		#region INSTANCE_METHODS

		public Packet() : this (DataType.logentry) { }
		public Packet(DataType type) : this(type, DEFAULT_DATA) { }
		public Packet(DataType type, Message msg) : this (type, msg.GetTagList (), msg.Content)
		{
			timeStamp = msg.Time;
			Context = msg.Context;
		}
		public Packet(DataType type, string data) : this(type, Message.TAG_DEFAULT, data) { }
		public Packet(DataType type, string tag, string data) : this (type, new string[] { tag }, data) { }
		public Packet(DataType type, string[] tags, string data)
		{
			timeStamp = default(DateTime);
			Type = type;
			this.tags = tags;
			Data = data;
		}

		/// <summary>
		/// Converts this packet into a dense byte array
		/// </summary>
		/// <returns></returns>
		public virtual byte[] ToBytes()
		{
			List<byte> byteList = new List<byte> ();

			//full packet length stub (filled at the end)
			byteList.AddRange (new byte[sizeof (int)]);

			//packet type, 4 bytes
			byteList.AddRange (BitConverter.GetBytes ((int)Type));

			if (Type == DataType.logentry)
			{
				//time stamp, 8 bytes
				byteList.AddRange (BitConverter.GetBytes (timeStamp.ToBinary()));
			}

			//tags count, 4 bytes
			byteList.AddRange (BitConverter.GetBytes (tags.Length));

			//data length, 4 bytes
			byte[] dataB = Encoding.UTF8.GetBytes (Data);
			byteList.AddRange (BitConverter.GetBytes (dataB.Length));

			//context length, 4 bytes
			byte[] contextB = Encoding.UTF8.GetBytes (Context);
			byteList.AddRange (BitConverter.GetBytes (contextB.Length));

			//individual tags, ? bytes
			for (int i = 0; i < tags.Length; i++)
			{
				//tag, 4 bytes + ? bytes
				byte[] tagB = Encoding.UTF8.GetBytes (tags[i]);
				byteList.AddRange (BitConverter.GetBytes (tagB.Length));
				byteList.AddRange (tagB);
			}

			//data ? bytes
			byteList.AddRange (dataB);

			//context ? bytes
			byteList.AddRange (contextB);

			//send list to raw array
			byte[] bytes = byteList.ToArray ();

			//overwrite first four bytes with length of array
			byte[] finalSizeB = BitConverter.GetBytes (bytes.Length);
			for (int i = 0; i < finalSizeB.Length; i++)
			{
				bytes[i] = finalSizeB[i];
			}

			return bytes;
		}

		public Message ToMessage()
		{
			return new Message (timeStamp, tags, Data);
		}

		public override string ToString()
		{
			string str = "Packet { ";

			str += "Type: " + Type.ToString();

			if(timeStamp != default(DateTime))
				str += ", Time: " + timeStamp.ToString ();

			if (tags.Length > 0)
			{
				str += ", Tags: [";
				str += tags[0];
				for (int i = 1; i < tags.Length; i++)
					str += ", " + tags[i];
				str += "]";
			}

			if(Data != DEFAULT_DATA)
				str += ", Data: \"" + Data + "\"";

			if (Context != DEFAULT_DATA)
				str += ", Context: \"" + Context + "\"";

			return str + " }";
		}
		#endregion

		/// <summary>
		/// Defines the types of packets that can be sent between clients and servers
		/// </summary>
		public enum DataType
		{
			/// <summary>
			/// An operation that the server must perform and for which a response must be returned
			/// </summary>
			command,

			/// <summary>
			/// The direct result of an executed command sent to a client
			/// </summary>
			response,

			/// <summary>
			/// A prompted or unpropmted transmission of data to a client
			/// </summary>
			logentry,

			/// <summary>
			/// A collection of settings that are shared between client and server
			/// </summary>
			settings
		}
	}
}
