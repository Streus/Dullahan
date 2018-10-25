using System;

namespace Dullahan.Env
{
	/// <summary>
	/// Tags Assemblies and classes as CommandProviders
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly
		| AttributeTargets.Class
		| AttributeTargets.Struct,
		AllowMultiple = false, Inherited = false)]
	public class CommandProviderAttribute : Attribute
	{

	}
}
