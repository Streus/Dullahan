using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Dullahan.Security
{
	public class CertificateManager
	{
		#region STATIC_VARS

		private const string CERT_STORE_NAME = "TrustedDullahan";
		#endregion

		#region INSTANCE_VARS

		private X509Store trustedConnections;
		#endregion

		#region STATIC_METHODS

		public static X509Certificate2 GenerateCertificate(string subjectName)
		{
			X509V3CertificateGenerator certGen = new X509V3CertificateGenerator ();
			SecureRandom random = new SecureRandom (new CryptoApiRandomGenerator ());

			//generate subject keypair
			KeyGenerationParameters keyGenParams = new KeyGenerationParameters (random, 2048);
			RsaKeyPairGenerator kpGen = new RsaKeyPairGenerator ();
			kpGen.Init (keyGenParams);
			AsymmetricCipherKeyPair subjectKeyPair = kpGen.GenerateKeyPair ();
			certGen.SetPublicKey (subjectKeyPair.Public);

			//serial
			BigInteger serial = BigIntegers.CreateRandomInRange (BigInteger.One, BigInteger.ValueOf (long.MaxValue), random);
			certGen.SetSerialNumber (serial);

			//signature algo
			ISignatureFactory sigFactory = new Asn1SignatureFactory ("SHA256WithRSA", subjectKeyPair.Private, random);

			//subject and issuer names
			X509Name subject = new X509Name (
				new DerObjectIdentifier[] { X509Name.CN }, 
				new string[] { subjectName });
			certGen.SetSubjectDN (subject);
			certGen.SetIssuerDN (subject);

			//validity
			DateTime now = DateTime.UtcNow.Date;
			DateTime then = now.AddYears (1);
			certGen.SetNotBefore (now);
			certGen.SetNotAfter (then);

			//extensions
			SubjectKeyIdentifier subKeyIdent = new SubjectKeyIdentifier (SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo (subjectKeyPair.Public));
			certGen.AddExtension (X509Extensions.SubjectKeyIdentifier, true, subKeyIdent);
			certGen.AddExtension (X509Extensions.ExtendedKeyUsage, true, new ExtendedKeyUsage(new KeyPurposeID[] { KeyPurposeID.AnyExtendedKeyUsage }));

			//generate
			Org.BouncyCastle.X509.X509Certificate bouncyCert = certGen.Generate (sigFactory);

			return new X509Certificate2 (DotNetUtilities.ToX509Certificate (bouncyCert).Export (X509ContentType.Cert));
		}
		#endregion

		#region INSTANCE_METHODS

		public CertificateManager()
		{
			trustedConnections = new X509Store (CERT_STORE_NAME, StoreLocation.CurrentUser);
			trustedConnections.Open (OpenFlags.ReadWrite);
		}

		~CertificateManager()
		{
			trustedConnections.Close ();
		}

		/// <summary>
		/// Identifies this Dullahan instance to other instances
		/// </summary>
		public X509Certificate2Collection GetSelfCertificate()
		{
			X509Certificate2Collection certs = trustedConnections.Certificates
				.Find (X509FindType.FindBySubjectName, Environment.UserDomainName + "/" + Environment.UserName, true);

			if (certs == null)
			{
				//existing cert not found, generate a new one
				certs = new X509Certificate2Collection ();
				X509Certificate2 newSelfCert = GenerateCertificate (Environment.UserDomainName + "/" + Environment.UserName);
				trustedConnections.Add (newSelfCert);
				certs.Add (newSelfCert);
			}
			return certs;
		}

		/// <summary>
		/// Save a certificate as a trusted identity
		/// </summary>
		/// <param name="certificate"></param>
		public void AddToTrusted(X509Certificate2 certificate)
		{
			trustedConnections.Add (certificate);
		}

		/// <summary>
		/// Check a certificate to see if it was previously trusted
		/// </summary>
		/// <param name="certificate"></param>
		/// <returns></returns>
		public bool isTrusted(X509Certificate2 certificate)
		{
			return trustedConnections.Certificates.Contains (certificate);
		}

		#endregion
	}
}
