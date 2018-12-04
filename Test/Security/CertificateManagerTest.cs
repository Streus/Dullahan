using Dullahan.Security;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace Dullahan.Test.Security
{
	[TestClass]
	public class CertificateManagerTest
	{
		[TestMethod]
		public void Test_GenerateBasicCertificate()
		{
			X509Certificate2 cert = CertificateManager.GenerateCertificate ("DullahanTest");
			Assert.IsNotNull (cert);
			Assert.AreNotEqual (Convert.ToBase64String (cert.GetPublicKey ()), "");
			Console.WriteLine (cert.ToString ());
		}
	}
}
