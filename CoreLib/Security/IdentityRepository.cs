using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Dullahan.Security
{
	public static class IdentityRepository
	{
		#region STATIC_VARS
		private const string TAG = "[IDNREP]";

		private const string IDENTITY_REP_FILENAME = "identities.dat";

		private static HashSet<Identity> repo;

		public static bool Loaded { get; private set; }
		public static bool PendingChanges { get; private set; }

		public static string RepoDir { get; set; }
		#endregion

		#region STATIC_METHODS
		static IdentityRepository()
		{
			repo = new HashSet<Identity>();
			Loaded = false;
			PendingChanges = false;
		}

		public static bool Load()
		{
			try
			{
				using (FileStream fileStream = new FileStream (RepoDir + Path.DirectorySeparatorChar + IDENTITY_REP_FILENAME, FileMode.OpenOrCreate))
				{
					if (fileStream.Length <= 0)
					{
						//created new empty file; nothing to read
						return true;
					}
					BinaryFormatter formatter = new BinaryFormatter ();
					repo = (HashSet<Identity>)formatter.Deserialize (fileStream);
#if DEBUG
					Console.WriteLine (TAG + " Loaded identities from \"" + RepoDir + "\"");
					Console.WriteLine (TAG + " Set contains " + repo.Count + " entries");
#endif
				}
			}
			catch(Exception e)
			{
#if DEBUG
				Console.WriteLine (TAG + " Error loading from \"" + RepoDir + Path.DirectorySeparatorChar + IDENTITY_REP_FILENAME + "\": " + e.ToString ());
#endif
				return false;
			}
			Loaded = true;
			return true;
		}

		public static bool Store()
		{
			try
			{
				using (FileStream fileStream = new FileStream (RepoDir + Path.DirectorySeparatorChar + IDENTITY_REP_FILENAME, FileMode.OpenOrCreate))
				{
					BinaryFormatter formatter = new BinaryFormatter ();
					formatter.Serialize (fileStream, repo);
				}
			}
			catch (Exception e)
			{
#if DEBUG
				Console.WriteLine (TAG + " Error storing to \"" + RepoDir + Path.DirectorySeparatorChar + IDENTITY_REP_FILENAME + "\": " + e.ToString ());
#endif
				return false;
			}
			PendingChanges = false;
			return true;
		}

		public static bool AddTrusted(Identity identity)
		{
#if DEBUG
			Console.WriteLine (TAG + " Adding " + identity.ToString() + " to trusted set");
#endif
			PendingChanges = true;
			return repo.Add (identity);
		}

		public static bool RemoveTrusted(Identity identity)
		{
#if DEBUG
			Console.WriteLine (TAG + " Removing \"" + identity.ToString () + "\" from trusted set");
#endif
			PendingChanges = true;
			return repo.Remove (identity);
		}

		public static bool RemoveTrusted(string name)
		{
#if DEBUG
			Console.WriteLine (TAG + " Removing identities with Name = \"" + name + "\" from trusted set");
#endif
			PendingChanges = true;
			return 0 <= repo.RemoveWhere ((Identity i) => { return i.Name == name; });
		}

		public static bool CheckTrusted(Identity identity)
		{
			return repo.Contains (identity);
		}

		#endregion
	}
}
