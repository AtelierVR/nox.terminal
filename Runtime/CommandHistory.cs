using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nox.CCK.Utils;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Terminal.Runtime {
	/// <summary>
	/// Command history persisted in <c>history.txt</c> inside the config folder of the mod
	/// (<see cref="Nox.CCK.Mods.Configs.IConfigAPI.GetFolder"/>), shared by the in-game terminal and
	/// the editor panel.
	/// </summary>
	/// <remarks>
	/// The file is append-only: several game instances can write to it at the same time without
	/// overwriting each other, and it is never rewritten.
	/// </remarks>
	public static class CommandHistory {
		/// <summary>Maximum number of commands kept in the file.</summary>
		public const int MaxEntries = 200;

		private const string FileName = "history.txt";

		/// <summary>
		/// Config folder of the mod (<see cref="Nox.CCK.Mods.Configs.IConfigAPI.GetFolder"/>).
		/// </summary>
		/// <exception cref="InvalidOperationException">The mod is not initialized.</exception>
		public static string Folder {
			get {
				var folder = Main.Instance?.CoreAPI?.ConfigAPI?.GetFolder();

				if (string.IsNullOrEmpty(folder))
					throw new InvalidOperationException(
						"The config API of the terminal mod is not available: Main must be initialized before the history can be stored."
					);

				return folder;
			}
		}

		/// <summary>Absolute path of the history file (<c>&lt;config folder&gt;/history.txt</c>).</summary>
		public static string FilePath
			=> Path.Combine(Folder, FileName);

		/// <summary>Reads the history, oldest first. A missing file is not an error.</summary>
		public static List<string> Load() {
			var entries = new List<string>();

			try {
				var path = FilePath;

				if (!File.Exists(path))
					return entries;

				foreach (var line in File.ReadAllLines(path)) {
					var entry = line?.Trim();

					if (string.IsNullOrEmpty(entry))
						continue;

					// Consecutive duplicates are pointless in a history.
					if (entries.Count > 0 && entries[^1] == entry)
						continue;

					entries.Add(entry);
				}
			} catch (Exception exception) {
				Logger.LogWarning($"Unable to read the terminal history: {exception.Message}", nameof(CommandHistory));
			}

			return Trim(entries);
		}

		/// <summary>Appends a command to the history file right away (one command per line).</summary>
		/// <remarks>
		/// Append-only on purpose: the file is opened with <see cref="FileShare.ReadWrite"/> and each
		/// entry is written in a single call, so concurrent writers never truncate each other.
		/// </remarks>
		public static void Append(string entry) {
			if (string.IsNullOrWhiteSpace(entry))
				return;

			try {
				using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
				using var writer = new StreamWriter(stream, new UTF8Encoding(false));

				writer.WriteLine(entry.Trim());
				writer.Flush();
			} catch (Exception exception) {
				Logger.LogWarning($"Unable to append to the terminal history: {exception.Message}", nameof(CommandHistory));
			}
		}

		/// <summary>Drops the empty entries and keeps only the most recent ones.</summary>
		private static List<string> Trim(IEnumerable<string> entries) {
			var trimmed = entries
				.Select(entry => entry?.Trim())
				.Where(entry => !string.IsNullOrEmpty(entry))
				.ToList();

			if (trimmed.Count > MaxEntries)
				trimmed.RemoveRange(0, trimmed.Count - MaxEntries);

			return trimmed;
		}
	}
}
