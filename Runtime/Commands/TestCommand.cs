using System;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.Terminal.Runtime;

namespace Nox.Terminal.Commands {
	public class TestCommand : ICommand, IHelper {
		public string GetName()
			=> "test";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> CommandWithPrefix;

		private string CommandWithPrefix
			=> $"{CommandManager.CommandPrefix}{GetName()}";

		public string[] AutoComplete(string input, IContext context = null)
			=> CommandWithPrefix.StartsWith(input.ToLower())
				? new[] { CommandWithPrefix }
				: Array.Empty<string>();

		public UniTask<bool> Execute(string input, IContext context = null) {
			if (input.ToLower() != CommandWithPrefix)
				return UniTask.FromResult(false);

			context?.PrintLn("Test command executed successfully!");
			context?.SetResult(true);
			return UniTask.FromResult(true);
		}
	}
}