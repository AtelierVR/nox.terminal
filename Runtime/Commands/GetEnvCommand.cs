using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.Terminal.Runtime;

namespace Nox.Terminal.Commands {
	public class GetEnvCommand : ICommand, IHelper {
		public string GetName()
			=> "get";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> $"{CommandWithPrefix}";

		private string CommandWithPrefix
			=> GetName();

		public string[] AutoComplete(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input) || context == null)
				return Array.Empty<string>();

			var endsWithSpace = input.EndsWith(' ');
			var parts         = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

			// Still completing the command itself.
			if (parts.Length == 1 && !endsWithSpace)
				return CommandWithPrefix.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)
					? new[] { CommandWithPrefix }
					: Array.Empty<string>();

			if (!parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return Array.Empty<string>();

			// "get NAME " : the only argument is complete, there is nothing more to suggest.
			if (endsWithSpace && parts.Length >= 2)
				return Array.Empty<string>();

			// Completing the variable name.
			var partial = parts.Length > 1 ? parts[1] : string.Empty;

			return context.GetEnvironments()
				.Keys
				.Where(key => key.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
				.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
				.Select(key => $"{CommandWithPrefix} {key}")
				.ToArray();
		}

		public UniTask<bool> Execute(string input, IContext context = null)
			=> UniTask.FromResult(ExecuteInternal(input, context));

		private bool ExecuteInternal(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input) || context == null)
				return false;

			var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
			if (!parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return false;

			// Without a name: list the whole environment.
			if (parts.Length < 2) {
				var environments = context.GetEnvironments();

				if (context.CanPrinting()) {
					if (environments == null || environments.Count == 0)
						context.PrintLn(LanguageManager.Get("terminal.command.getenv.empty"));
					else {
						context.PrintLn(LanguageManager.Get(
							"terminal.command.getenv.list_header",
							new object[] { environments.Count }
						));

						foreach (var environment in environments.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
							context.PrintLn(LanguageManager.Get(
								"terminal.command.getenv.list_item",
								new object[] { environment.Key, environment.Value }
							));
					}
				}

				context.SetResult(environments);

				return true;
			}

			var key   = parts[1].Trim();
			var value = context.GetEnvironment<object>(key);

			if (context.CanPrinting())
				context.PrintLn(
					LanguageManager.Get(
						"terminal.command.getenv.label",
						new[] { key, value }
					)
				);

			context.SetResult(value);

			return true;
		}
	}
}