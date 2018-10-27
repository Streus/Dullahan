using Dullahan.Env;
using Dullahan.Logging;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Dullahan
{
	[Serializable]
	public class User : ISerializable
	{
		#region STATIC_VARS

		private const string USER_FILE_EXT = "user";

		public static string UserRegistryPath { get; set; } = ".";
		#endregion

		#region INSTANCE_VARS

		public string Name { get; private set; }

		private char[] password;
		private bool passwordProtected;

		public Permission permissions { get; set; }

		public Executor Environment { get; private set; }
		#endregion

		#region STATIC_METHODS

		/// <summary>
		/// Load a user from the user registry
		/// </summary>
		/// <param name="name"></param>
		/// <param name="password"></param>
		/// <returns></returns>
		public static User Load(string name)
		{
			FileStream userFile;
			try
			{
				userFile = File.OpenRead (UserRegistryPath + Path.DirectorySeparatorChar + name + "." + USER_FILE_EXT);
			}
			catch (FileNotFoundException)
			{
#if DEBUG
				Console.Error.WriteLine ("Failed to find user file in \"" + UserRegistryPath + "\"");
#endif
				return null;
			}

			BinaryFormatter formatter = new BinaryFormatter ();
			User u = (User)formatter.Deserialize (userFile);
			userFile.Close ();

			return u;
		}

		/// <summary>
		/// Stores all changes to a user to its file
		/// </summary>
		public static void Store(User u)
		{
			FileStream userFile;
			try
			{
				userFile = File.OpenWrite (UserRegistryPath + Path.DirectorySeparatorChar + u.Name + "." + USER_FILE_EXT);
			}
			catch (FileNotFoundException)
			{
#if DEBUG
				Console.Error.WriteLine ("Failed to find user file in \"" + UserRegistryPath + "\"");
#endif
				return;
			}

			BinaryFormatter formatter = new BinaryFormatter ();
			formatter.Serialize (userFile, u);
			userFile.Close ();
		}
		
		#endregion

		#region INSTANCE_METHODS

		private User()
		{
			Name = "";
			password = null;
			passwordProtected = false;

			permissions = Permission.none;
			Environment = null;
		}
		private User(SerializationInfo info, StreamingContext context)
		{
			Name = info.GetString ("name");
			password = (char[])info.GetValue ("psswd", typeof(char[]));
			passwordProtected = info.GetBoolean ("psswdprot");
			permissions = (Permission)info.GetValue ("permis", typeof(Permission));
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue ("name", Name);
			info.AddValue ("psswd", password);
			info.AddValue ("psswdprot", passwordProtected);
			info.AddValue ("permis", permissions);
		}
		#endregion

		#region INTERNAL_TYPES

		[Flags]
		public enum Permission
		{
			none =		0x0,
			read =		0x1,
			write =		0x2,
			delete =	0x4,
			admin =		0x8,
			all =		~0x0
		}
		#endregion
	}
}
