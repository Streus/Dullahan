using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Dullahan.Security
{
	public static class IdentityRepository
	{
		#region STATIC_VARS
		private const string TAG = "[IDNREP]";

		private static HashSet<Identity> repo;
		#endregion

		#region STATIC_METHODS
		static IdentityRepository()
		{
			repo = new HashSet<Identity>();
		}

		public static bool Load(string path)
		{
			try
			{
				using (FileStream fileStream = new FileStream (path, FileMode.OpenOrCreate))
				{
					BinaryFormatter formatter = new BinaryFormatter ();
					repo = (HashSet<Identity>)formatter.Deserialize (fileStream);
				}
			}
			catch(Exception e) when (e is IOException || e is SerializationException)
			{
#if DEBUG
				Console.WriteLine (TAG + " Error loading from \"" + path + "\": " + e.ToString ());
#endif
				return false;
			}
			return true;
		}

		public static bool Store(string path)
		{
			try
			{
				using (FileStream fileStream = new FileStream (path, FileMode.OpenOrCreate))
				{
					BinaryFormatter formatter = new BinaryFormatter ();
					formatter.Serialize (fileStream, repo);
				}
			}
			catch (Exception e) when (e is IOException || e is SerializationException)
			{
#if DEBUG
				Console.WriteLine (TAG + " Error storing to \"" + path + "\": " + e.ToString ());
#endif
				return false;
			}
			return true;
		}

		public static bool AddTrusted(Identity identity)
		{
#if DEBUG
			Console.WriteLine (TAG + " Adding " + identity.ToString() + " to trusted set");
#endif
			return repo.Add (identity);
		}

		public static bool RemoveTrusted(Identity identity)
		{
#if DEBUG
			Console.WriteLine (TAG + " Removing \"" + identity.ToString () + "\" from trusted set");
#endif
			return repo.Remove (identity);
		}

		public static bool RemoveTrusted(string name)
		{
#if DEBUG
			Console.WriteLine (TAG + " Removing identities with Name = \"" + name + "\" from trusted set");
#endif
			return 0 <= repo.RemoveWhere ((Identity i) => { return i.Name == name; });
		}

		public static bool CheckTrusted(Identity identity)
		{
			return repo.Contains (identity);
		}

		#endregion
	}
}
