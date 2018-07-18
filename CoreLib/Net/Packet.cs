using System;
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
		#endregion

		#region INSTANCE_VARS

		public DataType type;
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

		public Packet(DataType type) : this(type, null) { }
		public Packet(DataType type, string data)
		{
			this.type = type;
			this.data = data;
		}
		public Packet(SerializationInfo info, StreamingContext context)
		{
			type = (DataType)info.GetInt32("t");

			data = TryGetValue<string>(info, "d", DEFAULT_DATA);
			logResult = TryGetValue<int>(info, "r", DEFAULT_LOG_RESULT);
		}

		public override string ToString()
		{
			string str = "{ ";
			str += "Type: " + type.ToString() + ", ";
			str += "Data: \"" + data + "\" }";

			return str;
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("t", (int)type);

			if (data != DEFAULT_DATA)
				info.AddValue("d", data);
			if (logResult != DEFAULT_LOG_RESULT)
				info.AddValue("r", logResult);
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
			logentry
		}
	}
}
