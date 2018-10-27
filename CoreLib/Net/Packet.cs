using Dullahan.Logging;
using System.Runtime.Serialization;

namespace Dullahan.Net
{
	/// <summary>
	/// Wrapper for data sent between clients and servers
	/// </summary>
	[System.Serializable]
	public class Packet : ISerializable
	{
		#region STATIC_VARS

		public const string DEFAULT_DATA = "";
		public const int DEFAULT_LOG_RESULT = -1;

		private const string TYPE_KEY = "t", TAG_KEY = "g", DATA_KEY = "d", LOGRES_KEY = "r";
		#endregion

		#region INSTANCE_VARS

		public DataType type;
		public string tag = Message.TAG_DEFAULT;
		public string data = DEFAULT_DATA;
		public int logResult = DEFAULT_LOG_RESULT;
		#endregion

		#region STATIC_METHODS

		private static T TryGetValue<T>(SerializationInfo info, string key, T defaultValue)
		{
			try
			{
				return (T)info.GetValue(key, typeof(T));
			}
			//value wasn't saved, pass back the default
			catch (SerializationException)
			{
				return defaultValue;
			}
		}
		#endregion

		#region INSTANCE_METHODS

		public Packet(DataType type) : this(type, DEFAULT_DATA) { }
		public Packet(DataType type, string data) : this(type, Message.TAG_DEFAULT, data) { }
		public Packet(DataType type, string tag, string data)
		{
			this.type = type;
			this.tag = tag;
			this.data = data;
		}
		public Packet(SerializationInfo info, StreamingContext context)
		{
			type = (DataType)info.GetInt32(TYPE_KEY);
			tag = info.GetString(TAG_KEY);

			data = TryGetValue<string>(info, DATA_KEY, DEFAULT_DATA);
			logResult = TryGetValue<int>(info, LOGRES_KEY, DEFAULT_LOG_RESULT);
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

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue(TYPE_KEY, (int)type);
			info.AddValue(TAG_KEY, tag);

			if (data != DEFAULT_DATA)
				info.AddValue(DATA_KEY, data);
			if (logResult != DEFAULT_LOG_RESULT)
				info.AddValue(LOGRES_KEY, logResult);
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
