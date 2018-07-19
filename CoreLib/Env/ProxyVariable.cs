
namespace Dullahan.Env
{
	/// <summary>
	/// For referencing values that cannot be directly saved
	/// in an object reference
	/// </summary>
	public class ProxyVariable : IVariable
	{
		private ProxiedValueGet get;
		private ProxiedValueSet set;

		public ProxyVariable(ProxiedValueGet get, ProxiedValueSet set) : this(get)
		{
			this.set = set;
		}
		public ProxyVariable(ProxiedValueGet get)
		{
			this.get = get;
		}

		public T GetValue<T>()
		{
			return (T)get();
		}

		public void SetValue(object val)
		{
			if(set != null)
				set(val);
		}

		public delegate object ProxiedValueGet();
		public delegate void ProxiedValueSet(object value);
	}
}
