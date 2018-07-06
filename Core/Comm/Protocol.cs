
namespace Dullahan.Comm
{
	/// <summary>
	/// Contains information for communication between a server and client
	/// </summary>
	public static class Protocol
	{
		#region STATIC_VARS

		public const int DEFAULT_PORT = 8080;
		#endregion

		#region INTERNAL_TYPES

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
			public PacketType type;
			public string data;

			public int logResult;
		}
		#endregion
	}
}
