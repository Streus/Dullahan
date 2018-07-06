
namespace Dullahan.Comm
{
	/// <summary>
	/// Wrapper for data sent between clients and servers
	/// </summary>
	[System.Serializable]
	public class Packet
	{
		public DataType type;
		public string data;
		public int logResult;

		public Packet(DataType type) : this(type, null) { }
		public Packet(DataType type, string data)
		{
			this.type = type;
			this.data = data;
		}

		public override string ToString()
		{
			string str = "{ ";
			str += "Type: " + type.ToString() + ", ";
			str += "Data: \"" + data + "\" }";

			return str;
		}

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
			logentry
		}
	}
}
