using System;
using System.Collections.Generic;
using Nox.UI;
using UnityEngine;

namespace Nox.Terminal.Clients {
	public class TerminalPage : IPage, IContext {
		// Limites pour éviter les crashs de rendu TextMeshPro
		private const int MaxLines      = 1000;  // Nombre maximum de lignes
		private const int MaxCharacters = 16384; // Nombre maximum de caractères (16K)
		
		internal static string GetStaticKey()
			=> "terminal";

		public string GetKey()
			=> GetStaticKey();

		private static bool T<T>(object[] o, int index, out T value) {
			if (o.Length > index && o[index] is T t) {
				value = t;
				return true;
			}

			value = default;
			return false;
		}

		internal static IPage OnGotoAction(IMenu menu, object[] context) {
			return new TerminalPage {
				_mId     = menu.GetId(),
				_context = context,
				_draft   = T(context, 0, out string draft) ? draft : null
			};
		}

		private int               _mId;
		private object[]          _context;
		private GameObject        _content;
		private TerminalComponent _component;

		private readonly Dictionary<string, object> _environments = new();

		internal string   _draft = string.Empty;
		internal string[] _auto  = Array.Empty<string>();

		private readonly List<string> _commandHistory = new();
		private          int          _historyIndex   = -1;

		public object[] GetContext()
			=> _context;

		public GameObject GetContent(RectTransform parent) {
			if (_content) return _content;
			(_content, _component) = TerminalComponent.Generate(this, parent);
			return _content;
		}

		public IMenu GetMenu()
			=> Client.UiAPI.Get<IMenu>(_mId);

		public int GetId()
			=> _component.GetInstanceID();

		public Dictionary<string, object> GetEnvironments()
			=> _environments;


		public T GetEnvironment<T>(string key, T defaultValue = default)
			=> _environments.TryGetValue(key.ToUpperInvariant(), out var value) && value is T t
				? t
				: defaultValue;

		public void SetEnvironment(string key, object value) {
			key = key.ToUpperInvariant();
			if (value == null) _environments.Remove(key);
			else _environments[key] = value;
		}

		private void LimitOutputText() {
			if (!_component || string.IsNullOrEmpty(_component.output.text)) 
				return;

			var text = _component.output.text;
			var needsTrimming = false;

			// Vérifier la limite de caractères
			if (text.Length > MaxCharacters) {
				needsTrimming = true;
			}

			// Vérifier la limite de lignes
			var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
			if (lines.Length > MaxLines) {
				needsTrimming = true;
			}

			if (!needsTrimming) return;

			// Garder seulement les dernières lignes/caractères
			var linesToKeep = Math.Min(lines.Length, MaxLines);
			var startIndex = lines.Length - linesToKeep;
			var trimmedText = string.Join(Environment.NewLine, lines, startIndex, linesToKeep);

			// Si toujours trop long, tronquer par caractères
			if (trimmedText.Length > MaxCharacters) {
				var excess = trimmedText.Length - MaxCharacters;
				trimmedText = trimmedText.Substring(excess);
				
				// Supprimer les lignes partielles au début
				var firstNewLine = trimmedText.IndexOf(Environment.NewLine);
				if (firstNewLine > 0) {
					trimmedText = trimmedText.Substring(firstNewLine + Environment.NewLine.Length);
				}
			}

			_component.output.text = "[...]\n" + trimmedText;
		}

		public void Print(string message) {
			if (!_component) return;
			_component.output.text += message;
			LimitOutputText();
			_component.output.ForceMeshUpdate();
		}

		public void PrintLn(string message)
			=> Print(message + Environment.NewLine);

		public void Clear() {
			if (!_component) return;
			_component.output.text = string.Empty;
			_component.output.ForceMeshUpdate();
		}

		public string GetTitle()
			=> _component.label.arguments.Length > 0
				? _component.label.arguments[0]
				: string.Empty;

		public void SetTitle(string title)
			=> _component.label.UpdateText("terminal.page.title", new[] { title });

		public object GetResult()
			=> GetEnvironment<object>("result");

		public void SetResult(object result)
			=> SetEnvironment("result", result);

		public bool CanPrinting()
			=> GetEnvironment("print_executing", true);

		public void SetPrinting(bool printing)
			=> SetEnvironment("print_executing", printing);

		public void AddToHistory(string command) {
			if (string.IsNullOrWhiteSpace(command)) return;

			if (_commandHistory.Count > 0 && _commandHistory[^1] == command)
				return;

			_commandHistory.Add(command);
			_historyIndex = _commandHistory.Count;
		}

		public string GetPreviousCommand() {
			if (_commandHistory.Count == 0)
				return string.Empty;

			if (_historyIndex > 0)
				_historyIndex--;

			return _historyIndex < _commandHistory.Count 
				? _commandHistory[_historyIndex] 
				: string.Empty;
		}

		public string GetNextCommand() {
			if (_commandHistory.Count == 0)
				return string.Empty;

			if (_historyIndex < _commandHistory.Count - 1) {
				_historyIndex++;
				return _commandHistory[_historyIndex];
			}

			_historyIndex = _commandHistory.Count;
			return string.Empty;
		}

		public void ResetHistoryIndex()
			=> _historyIndex = _commandHistory.Count;
	}
}