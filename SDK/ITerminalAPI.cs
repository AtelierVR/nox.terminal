using Cysharp.Threading.Tasks;

namespace Nox.Terminal {
	/// <summary>
	/// Interface for terminal API.
	/// </summary>
	public interface ITerminalAPI {
		/// <summary>
		/// Get the prefix of the terminal.
		/// </summary>
		/// <returns></returns>
		public string GetPrefix();

		/// <summary>
		/// Get the registered commands.
		/// </summary>
		/// <returns></returns>
		public ICommand[] GetRegistered();

		/// <summary>
		/// Execute a command with the given arguments.
		/// </summary>
		/// <param name="args"></param>
		/// <param name="context"></param>
		/// <returns></returns>
		public UniTask<bool> Execute(string args, IContext context = null);

		/// <summary>
		/// Get new arguments for a command.
		/// </summary>
		/// <param name="args"></param>
		/// <param name="context"></param>
		/// <returns></returns>
		public string[] AutoComplete(string args, IContext context = null);

		/// <summary>
		/// Register a command to the terminal.
		/// </summary>
		/// <param name="command"></param>
		/// <returns></returns>
		public uint Register(ICommand command);

		/// <summary>
		/// Unregister a command from the terminal.
		/// </summary>
		/// <param name="id"></param>
		public void Unregister(uint id);
	}
}