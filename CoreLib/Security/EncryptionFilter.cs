using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Dullahan.Security
{
	/// <summary>
	/// Encrypts and decrypts raw byte data via a symmetric algorithm after exchanging 
	/// the symmetric key via an asymmetric algorithm with another EncryptionFilter.
	/// </summary>
	public sealed class EncryptionFilter
	{
		#region STATIC_VARS

		private const string TAG = "[DULSEC]";

		private const string CONTAINER_NAME = "DullahanRSA";

		private const int KEY_SIZE = 256;
		private const int BLOCK_SIZE = 256;
		private const int BLOCK_SIZE_B = BLOCK_SIZE / 8;
		#endregion

		#region INSTANCE_VARS

		public bool Enabled { get; set; } = false;

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

		public byte[] GetOtherPublicKey()
		{
			return otherKeyData.ExportCspBlob (false);
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
					Padding = PaddingMode.PKCS7,
					BlockSize = BLOCK_SIZE
				};
			}

			List<byte> keyAndIV = new List<byte> (sizeof (int) + sessionKey.Key.Length
				+ sizeof (int) + sessionKey.IV.Length);

			keyAndIV.AddRange (BitConverter.GetBytes (sessionKey.Key.Length));
			keyAndIV.AddRange (BitConverter.GetBytes (sessionKey.IV.Length));
			keyAndIV.AddRange (sessionKey.Key);
			keyAndIV.AddRange (sessionKey.IV);

			return otherKeyData?.Encrypt (keyAndIV.ToArray (), false);
		}

		public void SetSymmetricKey(byte[] keyData)
		{
			if(keyData.Length < sizeof(int) * 2)
				throw new ArgumentException ("keyData is too short: " + keyData.Length + "B");

			byte[] data = selfKeyData.Decrypt (keyData, false);

			int seekPoint = 0;

			int keyLength = BitConverter.ToInt32 (data, seekPoint);
			seekPoint += sizeof (int);

			int ivLength = BitConverter.ToInt32(data, seekPoint);
			seekPoint += sizeof (int);

			byte[] key = new byte[keyLength];
			for (int i = 0; i < keyLength; i++)
			{
				key[i] = data[seekPoint + i];
			}
			seekPoint += keyLength;

			byte[] iv = new byte[ivLength];
			for (int i = 0; i < ivLength; i++)
			{
				iv[i] = data[seekPoint + i];
			}

			sessionKey = new RijndaelManaged () {
				Mode = CipherMode.CBC,
				Padding = PaddingMode.PKCS7,
				BlockSize = BLOCK_SIZE,
				Key = key,
				IV = iv
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
			if (!Enabled)
				return data;

			if (sessionKey == null)
				throw new InvalidOperationException ("Cannot encrypt without symmetric key");
#if DEBUG
			Console.WriteLine (TAG + " Encrypting " + data.Length + "B");
#endif

			byte[] encryptedData;
			try
			{
				ICryptoTransform transform = sessionKey.CreateEncryptor ();
				using (MemoryStream byteStream = new MemoryStream ())
				using (CryptoStream encryptStream = new CryptoStream (byteStream, transform, CryptoStreamMode.Write))
				{
					encryptStream.Write (data, 0, data.Length);
					encryptStream.FlushFinalBlock ();

					encryptedData = byteStream.ToArray ();
				}
			}
			catch (CryptographicException ce)
			{
#if DEBUG
				Console.Error.WriteLine (TAG + " Encountered Crypto error while encrypting: " + ce.ToString());
#endif
				return null;
			}

#if DEBUG
			Console.WriteLine (TAG + " Size of encrypted data: " + encryptedData.Length + "B");
#endif
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
			if (!Enabled)
				return data;

			if (sessionKey == null)
				throw new InvalidOperationException ("Cannot encrypt without symmetric key");
#if DEBUG
			Console.WriteLine (TAG + " Decrypting " + data.Length + "B");
#endif

			byte[] decryptedData;
			try
			{
				ICryptoTransform transform = sessionKey.CreateDecryptor ();
				using (MemoryStream inStream = new MemoryStream (data))
				using (MemoryStream outStream = new MemoryStream ())
				using (CryptoStream decryptStream = new CryptoStream (inStream, transform, CryptoStreamMode.Read))
				using (BinaryReader decryptReader = new BinaryReader (decryptStream))
				{
					byte[] buffer = new byte[BLOCK_SIZE_B];
					int byteC;
					while ((byteC = decryptReader.Read (buffer, 0, buffer.Length)) != 0)
					{
						outStream.Write (buffer, 0, byteC);
					}
					decryptedData = outStream.ToArray ();
				}
			}
			catch (CryptographicException ce)
			{
#if DEBUG
				Console.Error.WriteLine (TAG + " Encountered Crypto error while decrypting: " + ce.ToString());
#endif
				return null;
			}

#if DEBUG
			Console.WriteLine (TAG + " Size of decrypted data: " + decryptedData.Length + "B");
#endif
			return decryptedData;
		}
		#endregion
	}
}
