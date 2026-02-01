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
			=> $"{CommandManager.CommandPrefix}{GetName()}";

		public string[] AutoComplete(string input, IContext context = null)
			=> CommandWithPrefix.StartsWith(input.ToLower())
				? new[] { CommandWithPrefix }
				: Array.Empty<string>();

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