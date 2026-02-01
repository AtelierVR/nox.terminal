namespace Nox.Terminal
{
	/// <summary>
	/// Interface for terminal command helpers.
	/// </summary>
	public interface IHelper
	{
		/// <summary>
		/// Get the name of the helper.
		/// </summary>
		/// <returns></returns>
		public string GetName();

		/// <summary>
		/// Get the description of the helper.
		/// </summary>
		/// <returns></returns>
		public string GetDescription();

		/// <summary>
		/// Get the short description of the helper.
		/// </summary>
		/// <returns></returns>
		public string GetShort();

		/// <summary>
		/// Get the usage of the helper.
		/// </summary>
		/// <returns></returns>
		public string GetUsage();
	}
}