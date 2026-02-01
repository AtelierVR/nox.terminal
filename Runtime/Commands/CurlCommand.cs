using System;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.Terminal.Runtime;

namespace Nox.Terminal.Commands {
	public class CurlCommand : ICommand, IHelper {
		public string GetName()
			=> "curl";

		public string GetDescription()
			=> LanguageManager.Get($"terminal.command.{GetName()}.description");

		public string GetShort()
			=> LanguageManager.Get($"terminal.command.{GetName()}.short");

		public string GetUsage()
			=> $"{CommandWithPrefix} <url>";

		private string CommandWithPrefix
			=> $"{CommandManager.CommandPrefix}{GetName()}";

		public string[] AutoComplete(string input, IContext context = null)
			=> CommandWithPrefix.StartsWith(input.ToLower())
				? new[] { CommandWithPrefix }
				: Array.Empty<string>();

		public async UniTask<bool> Execute(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input) || context == null)
				return false;

			var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length != 2 || !parts[0].Equals(CommandWithPrefix, StringComparison.OrdinalIgnoreCase))
				return false;

			var printing = context.CanPrinting();

			var url = parts[1].Trim();
			if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
				if (printing) context.PrintLn($"Invalid URL: {url}");
				return true;
			}

			try {
				using var httpClient = new System.Net.Http.HttpClient();
				var       response   = await httpClient.GetAsync(uri);
				var       content    = await response.Content.ReadAsStringAsync();
				if (printing) {
					context.PrintLn($"Response Status: {(int)response.StatusCode} {response.ReasonPhrase}");
					context.PrintLn("Response Body:");
					context.PrintLn(content);
				}

				context.SetResult(content);
			} catch (Exception ex) {
				if (printing)
					context.PrintLn($"Error fetching URL: {ex.Message}");
				context.SetResult(ex);
			}

			return true;
		}
	}
}