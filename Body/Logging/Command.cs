
namespace Dullahan.Logging
{
	/// <summary>
	/// Simple binder for command data
	/// </summary>
	public  class Command
	{
		public string invocation;
		public CommandDelegate function;
		public string helpText;
	}
}
