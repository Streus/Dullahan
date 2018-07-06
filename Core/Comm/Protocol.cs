
namespace Dullahan.Comm
{
	/// <summary>
	/// Defines the types of packets that can be sent between clients and servers
	/// </summary>
	public enum PacketType
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

	/// <summary>
	/// Wrapper for data sent between clients and servers
	/// </summary>
	[System.Serializable]
	public class Packet
	{
		public Packet(PacketType type) : this(type, null) { }
		public Packet(PacketType type, string data)
		{
			this.type = type;
			this.data = data;
		}

		public PacketType type;

		public char[] tag;
		public string data;

		public int logResult;
	}
}
