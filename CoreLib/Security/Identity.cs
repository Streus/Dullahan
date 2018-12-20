using System;
using System.Security.Cryptography;
using System.Text;

namespace Dullahan.Security
{
	[Serializable]
	public class Identity
	{
		#region INSTANCE_VARS

		[NonSerialized]
		private CspParameters container;

		public string Name { get; private set; }
		public OriginType Origin { get; private set; }
		#endregion

		#region STATIC_METHODS

		public static bool operator ==(Identity left, Identity right)
		{
			return left.Equals (right);
		}

		public static bool operator !=(Identity left, Identity right)
		{
			return !left.Equals (right);
		}
		#endregion

		#region INSTANCE_METHODS

		public Identity() : this(Environment.UserDomainName + "/" + Environment.UserName, OriginType.local)
		{
			//needs to be done to make keypair?
			using (RSACryptoServiceProvider keypair = new RSACryptoServiceProvider (container))
			{
				keypair.PersistKeyInCsp = true;
			}
		}

		public Identity(string blob) : this("", OriginType.remote)
		{
			string[] parts = blob.Split (',');
			if (parts.Length != 2)
				throw new ArgumentException ("Malformed identity string: length=" + parts.Length);
			byte[] blobBytes = Convert.FromBase64String (parts[1]);

			Name = parts[0];

			using (RSACryptoServiceProvider keypair = new RSACryptoServiceProvider (container))
			{
				keypair.ImportCspBlob (blobBytes);
			}
		}

		public Identity(string name, byte[] cspBlob) : this(name, OriginType.remote)
		{
			using (RSACryptoServiceProvider keypair = new RSACryptoServiceProvider (container))
			{
				keypair.ImportCspBlob (cspBlob);
			}
		}

		private Identity(string name, OriginType origin)
		{
			Name = name;
			Origin = origin;

			container = new CspParameters ()
			{
				KeyContainerName = Name,
				Flags = CspProviderFlags.NoPrompt | CspProviderFlags.UseDefaultKeyContainer
			};
		}

		public void Drop()
		{
			RSACryptoServiceProvider keypair = new RSACryptoServiceProvider (container);
			keypair.PersistKeyInCsp = false;
			keypair.Clear ();
		}

		public void Regenerate()
		{
			Drop ();
			new RSACryptoServiceProvider (container).Clear ();
		}

		public byte[] GetSignature()
		{
			byte[] nameBytes = Encoding.UTF8.GetBytes (Name);
			using (RSACryptoServiceProvider keypair = new RSACryptoServiceProvider (container))
			{
				return keypair.SignData (nameBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			}
		}

		public bool Verify(byte[] signature)
		{
			byte[] nameBytes = Encoding.UTF8.GetBytes (Name);
			using (RSACryptoServiceProvider keypair = new RSACryptoServiceProvider (container))
			{
				return keypair.VerifyData (signature, nameBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			}
		}

		public byte[] GetPublicKey()
		{
			using (RSACryptoServiceProvider keypair = new RSACryptoServiceProvider (container))
			{
				return keypair.ExportCspBlob (false);
			}
		}

		public override string ToString()
		{
			return Name + "," + Convert.ToBase64String (GetPublicKey ());
		}

		public override bool Equals(object obj)
		{
			return ToString ().Equals (((Identity)obj).ToString ());
		}

		public override int GetHashCode()
		{
			return ToString ().GetHashCode ();
		}
		#endregion

		#region INTERNAL_TYPES
		public enum OriginType { local, remote }
		#endregion
	}
}
