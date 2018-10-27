using System.Collections.Generic;

namespace Dullahan.Logging
{
	public class Filter
	{
		#region INSTANCE_VARS

		public static Policy Mode { get; set; }
		private static HashSet<string> tags;
		#endregion

		#region INSTANCE_METHODS

		public Filter(Policy policy)
		{
			Mode = policy;
			tags = new HashSet<string> ();
		}
		public Filter() : this (Policy.inclusive) { }

		public static bool AddFilteredTag(string tag)
		{
			return tags.Add (tag);
		}

		public static bool RemoveFilteredTag(string tag)
		{
			return tags.Remove (tag);
		}

		/// <summary>
		/// Returns true if the given tag is displayed under the current
		/// filtering conditions, false otherwise
		/// </summary>
		public bool IsTagDisplayed(string tag)
		{
			return Mode == Policy.exclusive && !tags.Contains (tag) ||
				Mode == Policy.inclusive && tags.Contains (tag);
		}
		#endregion

		#region INTERNAL_TYPES

		public enum Policy
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
