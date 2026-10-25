using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.Terminal.Runtime;

namespace Nox.Terminal.Commands {
	public class HelpCommand : ICommand, IHelper {
		public string GetName()
			=> "help";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> $"{CommandWithPrefix} [<command>]";

		private string CommandWithPrefix
			=> GetName();

		public string[] AutoComplete(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input))
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

			// "help test " : the only argument is complete, there is nothing more to suggest.
			if (endsWithSpace && parts.Length >= 2)
				return Array.Empty<string>();

			// Completing the name of the command to describe.
			var partial = parts.Length > 1 ? parts[1] : string.Empty;

			return (Main.Instance?.GetRegistered() ?? Array.Empty<ICommand>())
				.OfType<IHelper>()
				.Select(helper => helper.GetName())
				.Where(name => !string.IsNullOrEmpty(name) && name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
				.Select(name => $"{CommandWithPrefix} {name}")
				.ToArray();
		}

		public UniTask<bool> Execute(string input, IContext context = null)
			=> UniTask.FromResult(ExecuteInternal(input, context));

		private bool ExecuteInternal(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input) || context == null)
				return false;

			var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length > 2 || !parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return false;

			var commandName = parts.Length == 2 ? parts[1] : null;
			var commands = Main.Instance.GetRegistered()
				.Select(c => c is IHelper ch ? ch : null)
				.Where(c => c != null)
				.ToArray();

			if (!string.IsNullOrEmpty(commandName)) {
				var command = commands.FirstOrDefault(c => c.GetName().Equals(commandName, StringComparison.OrdinalIgnoreCase));
				if (command == null) {
					context.PrintLn(LanguageManager.Get("terminal.command.help.no_help", commands.Length));
					return true;
				}

				context.PrintLn(
					LanguageManager.Get(
						"terminal.command.help.content",
						new object[] {
							command.GetName(),
							command.GetDescription(),
							command.GetUsage()
						}
					)
				);
				return true;
			}

			if (commands.Length == 0) {
				context.PrintLn(LanguageManager.Get("terminal.command.help.no_commands"));
				return true;
			}

			context.PrintLn(LanguageManager.Get("terminal.command.help.list_header", commands.Length));
			foreach (var command in commands)
				context.PrintLn(
					LanguageManager.Get(
						"terminal.command.help.list_item",
						new object[] { command.GetName(), command.GetShort() }
					)
				);

			return true;
		}
	}
}