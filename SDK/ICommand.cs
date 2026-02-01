using Cysharp.Threading.Tasks;

namespace Nox.Terminal {
	/// <summary>
	/// Interface for terminal commands.
	/// </summary>
	public interface ICommand {
		/// <summary>
		/// Get new arguments for the command.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="context"></param>
		/// <returns></returns>
		public string[] AutoComplete(string input, IContext context = null);

		/// <summary>
		/// Executes the command with the given input.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="context"></param>
		/// <returns>if the command was executed.</returns>
		public UniTask<bool> Execute(string input, IContext context = null);
	}
}