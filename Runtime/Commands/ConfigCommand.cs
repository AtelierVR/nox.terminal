using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nox.CCK.Language;
using Nox.CCK.Utils;
using Nox.Terminal.Runtime;

namespace Nox.Terminal.Commands {
	public class ConfigCommand : ICommand, IHelper {
		public string GetName()
			=> "config";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> $"{CommandWithPrefix} <set|get|has|del> [path] [value]";

		private string CommandWithPrefix
			=> $"{CommandManager.CommandPrefix}{GetName()}";

		private readonly string[] _subCommands = { "set", "get", "has", "del" };

		public string[] AutoComplete(string input, IContext context = null) {
			if (context == null || string.IsNullOrWhiteSpace(input))
				return Array.Empty<string>();

			var inputLower = input.ToLower().Trim();
			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

			// If input doesn't start with our command, return empty
			if (!inputLower.StartsWith(CommandWithPrefix.ToLower()))
				return CommandWithPrefix.StartsWith(inputLower)
					? new[] { CommandWithPrefix + " " }
					: Array.Empty<string>();

			// If we only have the command name (with or without trailing space)
			if (parts.Length == 1 || (parts.Length == 2 && !input.EndsWith(' '))) {
				var partial = parts.Length == 2 ? parts[1].ToLower() : "";
				var matches = _subCommands
					.Where(sc => sc.StartsWith(partial))
					.Select(sc => $"{CommandWithPrefix} {sc} ")
					.ToArray();
				return matches;
			}

			// If we have a subcommand, suggest config paths
			if (parts.Length >= 2) {
				var subCommand = parts[1].ToLower();
				if (!_subCommands.Contains(subCommand))
					return Array.Empty<string>();

				// For get, has, and del: suggest existing config paths
				if (subCommand is "get" or "has" or "del") {
					var partial = parts.Length >= 3 && !input.EndsWith(' ') ? parts[2] : "";
					var configPaths = GetAvailableConfigPaths(context);
					var matches = configPaths
						.Where(path => path.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
						.Select(path => $"{CommandWithPrefix} {subCommand} {path}")
						.ToArray();
					return matches.Length > 0 ? matches : Array.Empty<string>();
				}

				// For set: suggest existing paths but also allow new ones
				if (subCommand == "set" && parts.Length == 2) {
					return new[] { $"{CommandWithPrefix} {subCommand} " };
				}
			}

			return Array.Empty<string>();
		}

		private string[] GetAvailableConfigPaths(IContext context) {
			try {
				var config = Config.Load();
				var allConfigs = config.Get();
				if (allConfigs != null)
					return GetAllPaths(allConfigs).ToArray();
			} catch {
				// If we can't get all configs, return empty
			}
			return Array.Empty<string>();
		}

		private IEnumerable<string> GetAllPaths(JToken token, string prefix = "") {
			switch (token) {
				case JObject obj: {
					foreach (var property in obj.Properties()) {
						var path = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
						yield return path;
						foreach (var subPath in GetAllPaths(property.Value, path))
							yield return subPath;
					}
					break;
				}
				case JArray arr: {
					for (var i = 0; i < arr.Count; i++) {
						var path = $"{prefix}[{i}]";
						yield return path;
						foreach (var subPath in GetAllPaths(arr[i], path))
							yield return subPath;
					}
					break;
				}
			}
		}

		public UniTask<bool> Execute(string input, IContext context = null)
			=> UniTask.FromResult(ExecuteInternal(input, context));

		private bool ExecuteInternal(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input) || context == null)
				return false;

			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (!parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return false;

			if (parts.Length < 2) {
				if (context.CanPrinting())
					context.PrintLn(LanguageManager.Get("terminal.command.config.usage"));
				return true;
			}

			var subCommand = parts[1].ToLower();
			var printing = context.CanPrinting();

			switch (subCommand) {
				case "set":
					return HandleSet(parts, context, printing);
				case "get":
					return HandleGet(parts, context, printing);
				case "has":
					return HandleHas(parts, context, printing);
				case "del":
				case "delete":
					return HandleDel(parts, context, printing);
				default:
					if (printing)
						context.PrintLn(LanguageManager.Get("terminal.command.config.invalid_subcommand", new object[] { subCommand }));
					return true;
			}
		}

		private static bool HandleSet(string[] parts, IContext context, bool printing) {
			if (parts.Length < 4) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.set.usage"));
				return true;
			}

			var path = parts[2];
			var jsonString = string.Join(' ', parts.Skip(3));

			try {
				var value = JToken.Parse(jsonString);
				var config = Config.Load();
				config.Set(path, value);
				config.Save();
				
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.set.success", new object[] { path, jsonString }));
				
				context.SetResult(true);
				return true;
			} catch (JsonException ex) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.set.error", new object[] { ex.Message }));
				context.SetResult(false);
				return true;
			}
		}

		private static bool HandleGet(string[] parts, IContext context, bool printing) {
			var config = Config.Load();
			
			// If no path specified, return entire config
			if (parts.Length < 3) {
				var allConfig = config.Get();
				if (allConfig == null) {
					if (printing)
						context.PrintLn(LanguageManager.Get("terminal.command.config.get.empty"));
					context.SetResult(null);
				} else {
					var jsonString = allConfig.ToString(Formatting.Indented);
					if (printing)
						context.PrintLn(jsonString);
					context.SetResult(allConfig);
				}
				return true;
			}

			var path = parts[2];
			var value = config.Get(path);

			if (value == null) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.get.not_found", new object[] { path }));
				context.SetResult(null);
			} else {
				var jsonString = value.ToString(Formatting.Indented);
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.get.value", new object[] { path, jsonString }));
				context.SetResult(value);
			}

			return true;
		}

		private static bool HandleHas(string[] parts, IContext context, bool printing) {
			if (parts.Length < 3) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.has.usage"));
				return true;
			}

			var path = parts[2];
			var config = Config.Load();
			var exists = config.Has(path);

			if (printing)
				context.PrintLn(LanguageManager.Get("terminal.command.config.has.result", new object[] { path, exists }));
			
			context.SetResult(exists);
			return true;
		}

		private static bool HandleDel(string[] parts, IContext context, bool printing) {
			if (parts.Length < 3) {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.del.usage"));
				return true;
			}

			var path = parts[2];
			var config = Config.Load();
			var existed = config.Has(path);
			
			if (existed) {
				config.Remove(path);
				config.Save();
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.del.success", new object[] { path }));
			} else {
				if (printing)
					context.PrintLn(LanguageManager.Get("terminal.command.config.del.not_found", new object[] { path }));
			}

			context.SetResult(existed);
			return true;
		}
	}
}
