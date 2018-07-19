
namespace Dullahan.Env
{
	/// <summary>
	/// Holds a value reference for reading and writing
	/// </summary>
	public class LiteralVariable : IVariable
	{
		private object value;

		public LiteralVariable(object value)
		{
			this.value = value;
		}

		public T GetValue<T>()
		{
			return (T)value;
		}

		public void SetValue(object val)
		{
			value = val;
		}
	}
}
