using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Dullahan.Logging
{
	/// <summary>
	/// A discrete message logged to the system
	/// </summary>
	public class Message : ISerializable
	{
		#region STATIC_VARS

		public const string TAG_DEFAULT = "DEFAULT";
		public const string TAG_GLOBAL = "GLOBAL";
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_VARS

		/// <summary>
		/// List of tags applied to this message
		/// </summary>
		public HashSet<string> Tags { get; set; }

		/// <summary>
		/// What was logged
		/// </summary>
		public string Content { get; set; }

		#endregion

		#region INSTANCE_METHODS

		public Message() : this (TAG_DEFAULT, "") { }
		public Message(string content) : this(TAG_DEFAULT, content) { }
		public Message(string tag, string content) : this(new string[] { tag }, content) { }
		public Message(string[] tags, string content)
		{
			Tags = new HashSet<string> ();
			Tags.UnionWith (tags);

			Content = content;
		}
		public Message(SerializationInfo info, StreamingContext context)
		{
			Tags = (HashSet<string>)info.GetValue ("t", typeof (HashSet<string>));
			Content = info.GetString ("c");
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue ("t", Tags);
			info.AddValue ("c", Content);
		}

		/// <summary>
		/// Returns a formatted string containing the list of tags on this message
		/// (e.g. [tag1][tag2][tag3] )
		/// </summary>
		/// <returns></returns>
		public string GetTags()
		{
			string tags = "";
			foreach (string s in Tags)
				tags += "[" + s + "]";
			return tags;
		}

		public override string ToString()
		{
			return GetTags () + " " + Content;
		}
		#endregion
	}
}
