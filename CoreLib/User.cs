﻿using Dullahan.Env;
using Dullahan.Net;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Dullahan
{
	/// <summary>
	/// 
	/// </summary>
	[Serializable]
	public class User : ISerializable
	{
		#region STATIC_VARS

		private const string TAG = "[USER]";

		private const string USER_FILE_EXT = "user";

		public static string RegistryPath { get; set; } = ".";
		#endregion

		#region INSTANCE_VARS

		public string Name { get; private set; }

		private char[] password;
		private bool passwordProtected;

		public Permission permissions { get; set; }

		public Executor Environment { get; private set; } //TODO serialize variables?

		public Endpoint Host { get; set; }
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
			if (!Directory.Exists (RegistryPath))
			{
				Directory.CreateDirectory (RegistryPath);
			}

			FileStream userFile;
			try
			{
				userFile = File.OpenRead (RegistryPath + Path.DirectorySeparatorChar + name + "." + USER_FILE_EXT);
			}
			catch (IOException)
			{
#if DEBUG
				Console.Error.WriteLine (TAG + " Failed to find user file in \"" + RegistryPath + "\"");
#endif
				return new User () {
					Name = "user",
					Environment = Executor.Build ("user")
				};
			}

			BinaryFormatter formatter = new BinaryFormatter ();
			User u = (User)formatter.Deserialize (userFile);
			userFile.Close ();

			//HACK temporarily make an env for users
			u.Environment = Executor.Build (u.Name);

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
				userFile = File.OpenWrite (RegistryPath + Path.DirectorySeparatorChar + u.Name + "." + USER_FILE_EXT);
			}
			catch (FileNotFoundException)
			{
#if DEBUG
				Console.Error.WriteLine (TAG + " Failed to find user file in \"" + RegistryPath + "\"");
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
			//TODO user environment serialization
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
