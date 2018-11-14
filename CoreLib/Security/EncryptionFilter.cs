using System;
using System.IO;
using System.Security.Cryptography;

namespace Dullahan.Security
{
	/// <summary>
	/// Encrypts and decrypts raw byte data via a symmetric algorithm after exchanging 
	/// the symmetric key via an asymmetric algorithm with another EncryptionFilter.
	/// </summary>
	public class EncryptionFilter
	{
		#region STATIC_VARS

		private const string CONTAINER_NAME = "DullahanRSA";

		private const int KEY_SIZE = 256;
		private const int BLOCK_SIZE = 32;
		#endregion

		#region INSTANCE_VARS

		private RSAEncryptionPadding Padding { get; set; } = RSAEncryptionPadding.Pkcs1;
		public bool Ready { get { return sessionKey != null; } }

		private RSACryptoServiceProvider selfKeyData;
		private RSACryptoServiceProvider otherKeyData;
		private CspParameters containerInfo;
		private RijndaelManaged sessionKey;
		#endregion

		#region STATIC_METHODS

		#endregion

		#region INSTANCE_METHODS

		public EncryptionFilter()
		{
			containerInfo = new CspParameters () { KeyContainerName = CONTAINER_NAME };
			selfKeyData = new RSACryptoServiceProvider (containerInfo);
			selfKeyData.PersistKeyInCsp = true;

			otherKeyData = null;

			sessionKey = null;
		}

		public byte[] GetPublicKey()
		{
			return selfKeyData.ExportCspBlob (false);
		}

		public void SetOtherPublicKey(byte[] key)
		{
			otherKeyData = new RSACryptoServiceProvider ();
			otherKeyData.ImportCspBlob (key);
		}

		/// <summary>
		/// Returns this filter's symmetric key, encrypted by another filter's public asymmetric key
		/// </summary>
		/// <returns></returns>
		public byte[] GetSymmetricKey()
		{
			if (sessionKey == null)
			{
				sessionKey = new RijndaelManaged () {
					Mode = CipherMode.CBC,
					Padding = PaddingMode.Zeros,
					KeySize = KEY_SIZE,
					BlockSize = BLOCK_SIZE
				};
				sessionKey.GenerateKey ();
			}
			return otherKeyData?.Encrypt (sessionKey.Key, Padding);
		}

		public void SetSymmetricKey(byte[] key)
		{
			sessionKey = new RijndaelManaged () {
				Key = selfKeyData.Decrypt (key, Padding),
				Mode = CipherMode.CBC,
				Padding = PaddingMode.Zeros,
				BlockSize = BLOCK_SIZE
			};

			if (sessionKey.KeySize != KEY_SIZE)
				throw new ArgumentException ("Symmetric key size mismatch");
		}

		/// <summary>
		/// Encrypt data with this filter's symmetric key
		/// </summary>
		/// <param name="data"></param>
		/// <exception cref="InvalidOperationException"/>
		/// <returns></returns>
		public byte[] Encrypt(byte[] data)
		{
			if (sessionKey == null)
				throw new InvalidOperationException ("Cannot encrypt without symmetric key");

			byte[] encryptedData;
			int seekPoint = 0;
			try
			{
				ICryptoTransform transform = sessionKey.CreateEncryptor ();
				using (MemoryStream byteStream = new MemoryStream ())
				using (CryptoStream encryptStream = new CryptoStream (byteStream, transform, CryptoStreamMode.Write))
				{
					while (seekPoint < data.Length)
					{
						encryptStream.Write (data, seekPoint, BLOCK_SIZE);
						seekPoint += BLOCK_SIZE;
					}
					encryptStream.FlushFinalBlock ();
					encryptedData = byteStream.ToArray ();
				}
			}
			catch (CryptographicException ce)
			{
#if DEBUG
				Console.Error.WriteLine ("Encountered Crypto error while encrypting: " + ce.ToString());
#endif
				return null;
			}

			return encryptedData;
		}

		/// <summary>
		/// Decrypt data with this filter's symmetric key
		/// </summary>
		/// <param name="data"></param>
		/// <exception cref="InvalidOperationException"/>
		/// <returns></returns>
		public byte[] Decrypt(byte[] data)
		{
			if (sessionKey == null)
				throw new InvalidOperationException ("Cannot encrypt without symmetric key");

			byte[] decryptedData;
			int seekPoint = 0;
			try
			{
				ICryptoTransform transform = sessionKey.CreateDecryptor ();
				using (MemoryStream byteStream = new MemoryStream ())
				using (CryptoStream decryptStream = new CryptoStream (byteStream, transform, CryptoStreamMode.Write))
				{
					while (seekPoint < data.Length)
					{
						decryptStream.Write (data, seekPoint, BLOCK_SIZE);
						seekPoint += BLOCK_SIZE;
					}
					decryptStream.FlushFinalBlock ();
					decryptedData = byteStream.ToArray ();
				}
			}
			catch (CryptographicException ce)
			{
#if DEBUG
				Console.Error.WriteLine ("Encountered Crypto error while decrypting: " + ce.ToString());
#endif
				return null;
			}

			return decryptedData;
		}
		#endregion
	}
}
