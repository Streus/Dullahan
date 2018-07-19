
namespace Dullahan.Env
{
	public interface IVariable
	{
		T GetValue<T>();

		void SetValue(object val);
	}
}
