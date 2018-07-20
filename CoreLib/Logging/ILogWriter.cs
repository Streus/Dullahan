
namespace Dullahan.Logging
{
	public interface ILogWriter
	{
		void Write(string tag, string msg);
	}
}
