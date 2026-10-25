using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.Terminal.Runtime;

namespace Nox.Terminal.Commands {
	public class TitleCommand : ICommand, IHelper {
		public string GetName()
			=> "title";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> $"{CommandWithPrefix} <string>";

		private string CommandWithPrefix
			=> GetName();

		/// <summary>
		/// Title used when no terminal is attached (command run outside a terminal), and applied
		/// by the next terminal page that opens.
		/// </summary>
		public static string DefaultTitle { get; private set; } = string.Empty;

		public string[] AutoComplete(string input, IContext context = null)
			=> CommandWithPrefix.StartsWith(input.ToLower())
				? new[] { CommandWithPrefix }
				: Array.Empty<string>();

		public UniTask<bool> Execute(string input, IContext context = null)
			=> UniTask.FromResult(ExecuteInternal(input, context));

		private bool ExecuteInternal(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input))
				return false;

			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (!parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return false;

			// Without a terminal the title is only remembered, so the next page opens with it.
			var title = parts.Length == 1
				? context?.GetTitle() ?? DefaultTitle
				: string.Join(' ', parts.Skip(1));

			if (parts.Length > 1) {
				DefaultTitle = title;
				context?.SetTitle(title);
			}

			if (context?.CanPrinting() ?? false)
				context.PrintLn(LanguageManager.Get(
					$"terminal.command.{GetName()}.{(parts.Length == 1 ? "get" : "set")}",
					new object[] { title }
				));

			context?.SetResult(title);

			return true;
		}
	}
}