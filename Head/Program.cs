using System;
using System.Net;

namespace Dullahan
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Client c = new Client(IPAddress.Parse("127.0.0.1"));
			Console.ReadLine();
			c.Start();
		}
	}
}
