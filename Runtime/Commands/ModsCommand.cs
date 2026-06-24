using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Mods;
using Nox.CCK.Mods.Metadata;
using Nox.Terminal.Runtime;

namespace Nox.Terminal.Commands {
	public class ModsCommand : ICommand, IHelper {
		public string GetName()
			=> "mods";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> $"{CommandWithPrefix} <list|get> [id]";

		private string CommandWithPrefix
			=> GetName();

		private readonly string[] _subCommands = { "list", "get" };

		public string[] AutoComplete(string input, IContext context = null) {
			if (context == null || string.IsNullOrWhiteSpace(input))
				return Array.Empty<string>();

			var inputLower = input.ToLower().Trim();
			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

			if (!inputLower.StartsWith(CommandWithPrefix.ToLower()))
				return CommandWithPrefix.StartsWith(inputLower)
					? new[] { CommandWithPrefix + " " }
					: Array.Empty<string>();

			// Suggest subcommands
			if (parts.Length == 1 || (parts.Length == 2 && !input.EndsWith(' '))) {
				var partial = parts.Length == 2 ? parts[1].ToLower() : "";
				return _subCommands
					.Where(sc => sc.StartsWith(partial))
					.Select(sc => $"{CommandWithPrefix} {sc} ")
					.ToArray();
			}

			// For "get", suggest mod IDs
			if (parts.Length >= 2 && parts[1].Equals("get", StringComparison.OrdinalIgnoreCase)) {
				var partial = parts.Length >= 3 && !input.EndsWith(' ') ? parts[2] : "";
				var mods = Main.Instance.CoreAPI.ModAPI.GetMods();
				return mods
					.Select(m => m.GetMetadata().GetId())
					.Where(id => id.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
					.Select(id => $"{CommandWithPrefix} get {id}")
					.ToArray();
			}

			return Array.Empty<string>();
		}

		public UniTask<bool> Execute(string input, IContext context = null)
			=> UniTask.FromResult(ExecuteInternal(input, context));

		private bool ExecuteInternal(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input) || context == null)
				return false;

			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (!parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return false;

			var printing = context.CanPrinting();

			if (parts.Length < 2) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.mods.usage"));
				return true;
			}

			var subCommand = parts[1].ToLower();

			switch (subCommand) {
				case "list":
					return HandleList(context, printing);
				case "get":
					return HandleGet(parts, context, printing);
				default:
					if (printing)
						context.PrintLn(LanguageManager.Get("terminal.command.mods.invalid_subcommand", new object[] { subCommand }));
					return true;
			}
		}

		private static bool HandleList(IContext context, bool printing) {
			var mods = Main.Instance.CoreAPI.ModAPI.GetMods();

			if (mods.Length == 0) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.mods.list.empty"));
				context.SetResult(mods);
				return true;
			}

			if (printing) {
				context.PrintLn(LanguageManager.Get("terminal.command.mods.list.header", mods.Length));
				foreach (var mod in mods) {
					var meta = mod.GetMetadata();
					context.PrintLn($"  {meta.GetId()} v{meta.GetVersion()} - {meta.GetName()}");
				}
			}

			context.SetResult(mods);
			return true;
		}

		private static bool HandleGet(string[] parts, IContext context, bool printing) {
			if (parts.Length < 3) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.mods.get.usage"));
				return true;
			}

			var id = parts[2];
			var mod = Main.Instance.CoreAPI.ModAPI.GetMod(id);

			if (mod == null) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.mods.get.not_found", new object[] { id }));
				context.SetResult(null);
				return true;
			}

			var meta = mod.GetMetadata();

			if (printing) {
				context.PrintLn($"ID:          {meta.GetId()}");
				context.PrintLn($"Name:        {meta.GetName()}");
				context.PrintLn($"Version:     {meta.GetVersion()}");
				context.PrintLn($"Description: {meta.GetDescription()}");
				context.PrintLn($"License:     {meta.GetLicense()}");

				var authors = meta.GetAuthors();
				if (authors != null && authors.Length > 0)
					context.PrintLn($"Authors:     {string.Join(", ", authors.Select(a => a.GetName()))}");

				var provides = meta.GetProvides();
				if (provides != null && provides.Length > 0)
					context.PrintLn($"Provides:    {string.Join(", ", provides)}");

				context.PrintLn($"Loaded:      {mod.IsLoaded()}");
			}

			context.SetResult(meta);
			return true;
		}
	}
}
