using System.Collections.Generic;

namespace Dullahan.Logging
{
	public static class Log
	{
		#region COLOR_TAGS

		public const string RED =		"<c=red>{0}</c>";
		public const string ORANGE =	"<c=orn>{0}</c>";
		public const string YELLOW =	"<c=ylw>{0}</c>";
		public const string GREEN =		"<c=grn>{0}</c>";
		public const string BLUE =		"<c=blu>{0}</c>";
		public const string MAGENTA =	"<c=mag>{0}</c>";
		#endregion

		#region STATIC_VARS

		public const string DEFAULT_TAG = "DEF";

		private static ILogWriter writer;

		public static FilterPolicy FilterMode { get; set; }
		private static HashSet<string> tagFilter;
		#endregion

		#region STATIC_METHODS

		static Log()
		{
			tagFilter = new HashSet<string>();
			FilterMode = FilterPolicy.exclusive;
		}

		public static void SetOutput(ILogWriter w)
		{
			writer = w;
		}

		public static bool AddFilteredTag(string tag)
		{
			return tagFilter.Add(tag);
		}

		public static bool RemoveFilteredTag(string tag)
		{
			return tagFilter.Remove(tag);
		}

		/// <summary>
		/// Returns true if the given tag is displayed under the current
		/// filtering conditions, false otherwise
		/// </summary>
		public static bool IsTagDisplayed(string tag)
		{
			return FilterMode == FilterPolicy.exclusive && !tagFilter.Contains(tag) ||
				FilterMode == FilterPolicy.inclusive && tagFilter.Contains(tag);
		}

		private static void WriteMessage(string tag, string msg)
		{
			if (tag == null || tag == "")
				throw new System.ArgumentException("Provided an empty tag");

			if(IsTagDisplayed(tag))
				writer.Write(tag.Substring(0, 3), msg);
		}

		/// <summary>
		/// Write an error message to the logging system
		/// </summary>
		public static void E(string msg)
		{
			E(DEFAULT_TAG, msg);
		}

		/// <summary>
		/// Write an error message to the logging system with a tag
		/// </summary>
		public static void E(string tag, string msg)
		{
			WriteMessage(tag, string.Format(RED, msg));
		}

		/// <summary>
		/// Write a debug message to the logging system
		/// </summary>
		public static void D(string msg)
		{
			D(DEFAULT_TAG, msg);
		}

		/// <summary>
		/// Write a debug message to the logging system with a tag
		/// </summary>
		public static void D(string tag, string msg)
		{
			WriteMessage(tag, msg);
		}

		/// <summary>
		/// Write a warning message to the logging system
		/// </summary>
		public static void W(string msg)
		{
			W(DEFAULT_TAG, msg);
		}

		/// <summary>
		/// Write a warning message to the logging system with a tag
		/// </summary>
		public static void W(string tag, string msg)
		{
			WriteMessage(tag, string.Format(YELLOW, msg));
		}
		#endregion

		#region INTERNAL_TYPES

		public enum FilterPolicy
		{
			/// <summary>
			/// Only tags in the filter will be displayed
			/// </summary>
			inclusive,

			/// <summary>
			/// Only tags not in the filter will be displayed
			/// </summary>
			exclusive
		}
		#endregion
	}
}
