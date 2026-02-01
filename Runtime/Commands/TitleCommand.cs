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

			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (!parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return false;

			var printExecuting = context.CanPrinting();

			if (parts.Length == 1) {
				if (printExecuting)
					context.PrintLn(LanguageManager.Get($"terminal.command.{GetName()}.get", new object[] { context.GetTitle() }));
				context.SetResult(context.GetTitle());
				return true;
			}

			context.SetTitle(string.Join(' ', parts.Skip(1)));
			if (printExecuting)
				context.PrintLn(LanguageManager.Get($"terminal.command.{GetName()}.set", new object[] { context.GetTitle() }));
			
			context.SetResult(context.GetTitle());
			
			return true;
		}
	}
}