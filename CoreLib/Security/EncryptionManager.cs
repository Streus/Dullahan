using System;
using System.Security.Cryptography;

namespace Dullahan.Security
{
	public class EncryptionManager
	{
		public RSAEncryptionPadding Padding { get; set; }
		private RSAParameters rsaParams;

		public byte[] Encrypt(byte[] data)
		{
			try
			{
				using (RSACryptoServiceProvider provider = new RSACryptoServiceProvider ())
				{
					provider.ImportParameters (rsaParams);
					return provider.Encrypt (data, Padding);
				}
			}
			catch (CryptographicException ce)
			{
#if DEBUG
				Console.Error.WriteLine ("Encountered Crypto error while encrypting: " + ce.Message);
#endif
				return null;
			}
		}

		public byte[] Decrypt(byte[] data)
		{
			try
			{
				using (RSACryptoServiceProvider provider = new RSACryptoServiceProvider ())
				{
					provider.ImportParameters (rsaParams);
					return provider.Decrypt (data, Padding);
				}
			}
			catch (CryptographicException ce)
			{
#if DEBUG
				Console.Error.WriteLine ("Encountered Crypto error while decrypting: " + ce.Message);
#endif
				return null;
			}
		}
	}
}
