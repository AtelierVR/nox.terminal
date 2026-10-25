using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Utils;

namespace Nox.Terminal.Commands {
	/// <summary>
	/// "lang" (alias "language"): shows or changes the language used by the interface.
	/// </summary>
	public class LanguageCommand : ICommand, IHelper {
		private const string Alias = "language";

		public string GetName()
			=> "lang";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> $"{GetName()} [<language>]";

		/// <summary>Both the command and its alias are accepted.</summary>
		private static readonly string[] Names = { "lang", Alias };

		/// <summary>
		/// Languages declared by the loaded packs (registered by the mods, plus the project ones in the editor).
		/// </summary>
		private static string[] Languages
			=> LanguageManager.GetPacks()
				.SelectMany(pack => pack.languages ?? Array.Empty<LanguagePack.LanguageData>())
				.Select(language => language?.IETF)
				.Where(language => !string.IsNullOrEmpty(language))
				.Distinct()
				.OrderBy(language => language, StringComparer.OrdinalIgnoreCase)
				.ToArray();

		public string[] AutoComplete(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input))
				return Array.Empty<string>();

			var endsWithSpace = input.EndsWith(' ');
			var parts         = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

			var matchingNames = Names
				.Where(name => name.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase))
				.ToArray();

			var isName = Names.Any(name => name.Equals(parts[0], StringComparison.OrdinalIgnoreCase));

			// "la", "lan ", or another command altogether: only the names can match.
			if (!isName)
				return matchingNames;

			// Exact name, no space yet: keep proposing the name itself.
			if (parts.Length == 1 && !endsWithSpace)
				return matchingNames;

			// "lang " → every available language, keeping the name (or alias) that was typed.
			if (parts.Length < 2)
				return Languages
					.Select(language => $"{parts[0]} {language}")
					.ToArray();

			// "lang fr-FR " : the only argument is complete, there is nothing more to suggest.
			if (endsWithSpace)
				return Array.Empty<string>();

			// "lang fr" → the languages matching the typed prefix.
			var partial = parts[1];

			return Languages
				.Where(language => language.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
				.Select(language => $"{parts[0]} {language}")
				.ToArray();
		}

		public UniTask<bool> Execute(string input, IContext context = null)
			=> UniTask.FromResult(ExecuteInternal(input, context));

		private bool ExecuteInternal(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input) || context == null)
				return false;

			var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

			if (!Names.Any(name => name.Equals(parts[0], StringComparison.OrdinalIgnoreCase)))
				return false;

			var languages = Languages;

			// No argument: show the current language and the available ones.
			if (parts.Length < 2) {
				if (context.CanPrinting())
					context.PrintLn(LanguageManager.Get(
						"terminal.command.lang.current",
						new object[] { LanguageManager.CurrentLanguage, string.Join(", ", languages) }
					));

				context.SetResult(LanguageManager.CurrentLanguage);

				return true;
			}

			var requested = parts[1].Trim();
			var target = languages.FirstOrDefault(language => language.Equals(requested, StringComparison.OrdinalIgnoreCase))
				?? languages.FirstOrDefault(language => language.StartsWith(requested, StringComparison.OrdinalIgnoreCase));

			if (string.IsNullOrEmpty(target)) {
				if (context.CanPrinting())
					context.PrintLn(LanguageManager.Get(
						"terminal.command.lang.not_found",
						new object[] { requested, string.Join(", ", languages) }
					));

				context.SetResult(null);

				return true;
			}

			LanguageManager.CurrentLanguage = target;

			// Same path as the settings entry, so the terminal, the settings UI and the file agree.
			var config = Config.Load();
			config.Set("settings.accessibility.interface.language", target);
			config.Save();

			if (context.CanPrinting())
				context.PrintLn(LanguageManager.Get("terminal.command.lang.set", new object[] { target }));

			context.SetResult(target);

			return true;
		}
	}
}
