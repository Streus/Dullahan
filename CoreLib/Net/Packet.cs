using Dullahan.Logging;

namespace Dullahan.Net
{
	/// <summary>
	/// Wrapper for data sent between clients and servers
	/// </summary>
	public class Packet
	{
		#region STATIC_VARS

		public const string DEFAULT_DATA = "";
		public const int DEFAULT_LOG_RESULT = -1;

		private const string TYPE_KEY = "t", TAG_KEY = "g", DATA_KEY = "d", LOGRES_KEY = "r";
		#endregion

		#region INSTANCE_VARS

		public DataType type;
		public string[] tags = new string[] { };
		public string data = DEFAULT_DATA;
		public int logResult = DEFAULT_LOG_RESULT;
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_METHODS

		public Packet(DataType type) : this(type, DEFAULT_DATA) { }
		public Packet(DataType type, string data) : this(type, Message.TAG_DEFAULT, data) { }
		public Packet(DataType type, string tag, string data) : this (type, new string[] { tag }, data) { }
		public Packet(DataType type, string[] tags, string data)
		{
			this.type = type;
			this.tags = tags;
			this.data = data;
		}

		public byte[] ToBytes()
		{

		}

		public override string ToString()
		{
			string str = "{ ";
			str += "Type: " + type.ToString();

			if(data != DEFAULT_DATA)
				str += ", Data: \"" + data + "\"";
			if (logResult != DEFAULT_LOG_RESULT)
				str += ", Log Result: " + logResult;
			return str + " }";
		}
		#endregion

		/// <summary>
		/// Defines the types of packets that can be sent between clients and servers
		/// </summary>
		public enum DataType
		{
			/// <summary>
			/// For direct communication between clients and the server during 
			/// management operations, like establishing connections, communicating
			/// network information, etc.
			/// </summary>
			management,

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
			logentry
		}
	}
}
