using System;
using System.Collections.Generic;

namespace Dullahan.Logging
{
	/// <summary>
	/// A discrete message logged to the system
	/// </summary>
	public class Message
	{
		#region STATIC_VARS

		public const string TAG_DEFAULT = "DEFAULT";
		public const string TAG_GLOBAL = "GLOBAL";
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_VARS

		private DateTime timeStamp;
		public DateTime Time { get { return timeStamp; } }

		/// <summary>
		/// List of tags applied to this message
		/// </summary>
		public HashSet<string> Tags { get; set; }

		/// <summary>
		/// What was logged
		/// </summary>
		public string Content { get; set; }

		/// <summary>
		/// Stacktrace info on the context of this message
		/// </summary>
		public string Context { get; set; }
		#endregion

		#region INSTANCE_METHODS

		public Message() : this (TAG_DEFAULT, "") { }
		public Message(string content) : this(TAG_DEFAULT, content) { }
		public Message(string tag, string content) : this(new string[] { tag }, content) { }
		public Message(string[] tags, string content) : this (DateTime.Now, tags, content) { }
		public Message(DateTime timeStamp, string[] tags, string content)
		{
			this.timeStamp = timeStamp;

			Tags = new HashSet<string> ();
			Tags.UnionWith (tags);

			Content = content;
			Context = "";
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
				tags += "[" + s.ToUpper() + "]";
			return tags;
		}

		public string[] GetTagList()
		{
			string[] tags = new string[Tags.Count];
			Tags.CopyTo (tags);
			return tags;
		}

		public override string ToString()
		{
			return ToString (true);
		}
		public string ToString(bool includeTime)
		{
			return ToString (includeTime, true);
		}
		public string ToString(bool includeTime, bool includeTags)
		{
			string str = Content;
			if (includeTags)
				str = GetTags () + " " + str;
			if(Context != "")
				str = Context + " " + str;
			if (includeTime && timeStamp != default(DateTime))
				str = timeStamp.ToString () + " " + str;
			return str;
		}
		#endregion
	}
}
