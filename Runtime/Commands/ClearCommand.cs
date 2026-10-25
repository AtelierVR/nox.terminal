using System;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.Terminal.Runtime;

namespace Nox.Terminal.Commands {
	public class ClearCommand : ICommand, IHelper {
		public string GetName()
			=> "clear";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> CommandWithPrefix;

		private string CommandWithPrefix
			=> GetName();

		public string[] AutoComplete(string input, IContext context = null)
			=> CommandWithPrefix.StartsWith(input.ToLower())
				? new[] { CommandWithPrefix }
				: Array.Empty<string>();

		public UniTask<bool> Execute(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input))
				return UniTask.FromResult(false);

			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (!parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return UniTask.FromResult(false);

			// Works without a terminal: there is simply nothing to clear.
			context?.Clear();
			context?.SetResult(null);

			return UniTask.FromResult(true);
		}
	}
}