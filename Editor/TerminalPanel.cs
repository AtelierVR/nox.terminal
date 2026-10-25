using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;	using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using Nox.Editor.Panel;
using Nox.Terminal.Runtime;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using IPanel = Nox.Editor.Panel.IPanel;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Terminal.Editor {
	/// <summary>
	/// Terminal panel of the editor: an output console, a command input with history (↑/↓) and
	/// completion (Tab), running through <see cref="ITerminalAPI"/> — the same command set as the
	/// in-game terminal.
	/// </summary>
	public class TerminalPanel : IPanel, IEditorModInitializer {
		public static TerminalPanel Panel { get; private set; }

		public TerminalInstance Instance { get; internal set; }

		public void OnInitializeEditor(IEditorModCoreAPI api)
			=> Panel = this;

		public void OnDisposeEditor()
			=> Panel = null;

		public string[] GetPath()
			=> new[] { "tools", "terminal" };

		public IInstance[] GetInstances()
			=> Instance != null ? new IInstance[] { Instance } : Array.Empty<IInstance>();

		/// <summary>Menu entry of the panel, Title-cased from the path.</summary>
		public string GetLabel()
			=> string.Join("/", GetPath().Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));

		public IInstance Instantiate(IWindow window, Dictionary<string, object> data)
			=> Instance = new TerminalInstance(this, window, data);

		internal void Destroyed()
			=> Instance = null;
	}

	/// <summary>One opened terminal: console + input, driven by the terminal mod.</summary>
	public class TerminalInstance : IInstance {
		private const int MaxLines = 500;

		/// <summary>Height of the command input, in pixels.</summary>
		private const float InputHeight = 30f;

		/// <summary>Horizontal padding of the text inside the command input.</summary>
		private const float InputPadding = 8f;

		/// <summary>Monospace font used by the whole terminal, as a Resources path of nox.loader.</summary>
		private const string FontPath = "fonts/Ubuntu_Mono/UbuntuMono-Regular";

		/// <summary>Cache of the loaded font (Unity keeps the asset alive once loaded).</summary>
		private static Font _font;

		/// <summary>Font of the terminal, loaded on first use (null when the asset is missing).</summary>
		private static Font TerminalFont {
			get {
				if (_font == null) {
					_font = Resources.Load<Font>(FontPath);

					if (_font == null)
						Logger.LogWarning($"Terminal font not found in Resources at '{FontPath}'.", nameof(TerminalInstance));
				}

				return _font;
			}
		}

		/// <summary>Applies the mono font to an element; the children inherit it.</summary>
		private static void ApplyFont(VisualElement element) {
			var font = TerminalFont;

			if (font == null || element == null)
				return;

			element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
		}

		/// <summary>Removes the padding, margins, borders and radius of an element (nox.style utilities).</summary>
		private static void Flatten(VisualElement element) {
			if (element == null)
				return;

			element.AddToClassList("p-0");
			element.AddToClassList("m-0");
			element.AddToClassList("rounded-none");
			element.AddToClassList("border-none");
		}

		/// <summary>
		/// Removes the frame of a text field: the editor theme draws its background, border and radius
		/// on the inner text element, which only exists once the field is rendered.
		/// </summary>
		private static void MakeBareField(TextField field) {
			if (field == null)
				return;

			Flatten(field);
			field.AddToClassList("bg-transparent");
			field.style.height    = InputHeight;
			field.style.minHeight = InputHeight;
			field.style.maxHeight = InputHeight;

			field.RegisterCallback<AttachToPanelEvent>(_ => {
				foreach (var name in new[] { "unity-text-field__input", "unity-text-input" }) {
					var inner = field.Q<VisualElement>(className: name) ?? field.Q<VisualElement>(name);

					if (inner == null)
						continue;

					// Inline as well: the editor theme targets this element with a heavier selector.
					Flatten(inner);
					inner.AddToClassList("bg-transparent");
					inner.style.backgroundColor = Color.clear;
					inner.style.height          = InputHeight;
					inner.style.minHeight       = InputHeight;
					inner.style.maxHeight       = InputHeight;

					// The padding is removed by "p-0": set the horizontal one back, inside the field.
					inner.style.paddingLeft  = InputPadding;
					inner.style.paddingRight = InputPadding;
				}
			});
		}

		private static readonly Color ConsoleBackground    = new(0.04f, 0.04f, 0.04f, 1f);
		private static readonly Color ConsoleText          = new(0.84f, 0.84f, 0.84f, 1f);
		private static readonly Color SuggestionBackground = new(0.09f, 0.09f, 0.09f, 1f);
		private static readonly Color SelectionBackground  = new(0.22f, 0.22f, 0.22f, 1f);

		private readonly IPanel  _panel;
		private readonly IWindow _window;

		private readonly List<string> _history = new();
		private readonly List<string> _suggestions = new();
		private readonly EditorContext _context;

		private VisualElement _root;
		private ScrollView    _output;
		private Label         _console;
		private VisualElement _suggestionPanel;
		private TextField     _input;

		/// <summary>Lines currently displayed in the console (kept to rebuild the selectable label).</summary>
		private readonly List<string> _consoleLines = new();

		private int  _historyIndex = -1;
		private int  _suggestionIndex;
		private bool _running;

		/// <summary>Command being typed when the history browsing started, restored by ↓.</summary>
		private string _draft = string.Empty;

		public TerminalInstance(IPanel panel, IWindow window, Dictionary<string, object> data) {
			_panel  = panel;
			_window = window;
			_context = new EditorContext(PrintLine, ClearConsole, OnTitleChanged);
			LoadHistory();
		}

		// --- IInstance -------------------------------------------------------------------------

		public IPanel GetPanel()
			=> _panel;

		public IWindow GetWindow()
			=> _window;

		public string GetTitle()
			=> string.IsNullOrEmpty(_context.GetTitle())
				? LanguageManager.Get("terminal.editor.title")
				: _context.GetTitle();

		public VisualElement GetContent() {
			if (_root != null)
				return _root;

			_root = new VisualElement { style = { flexGrow = 1 } };

			// --- Console --------------------------------------------------------------------
			_output = new ScrollView {
				style = {
					flexGrow = 1,
					flexShrink = 1,
					minHeight = 0, // lets it shrink instead of pushing the other blocks out
					backgroundColor = ConsoleBackground
				}
			};
			ApplyFont(_output);
			Flatten(_output);

			// One selectable label for the whole console: text can be selected across lines and copied
			// (Ctrl+C). A label per line would only allow a line at a time.
			_console = new Label {
				style = { whiteSpace = WhiteSpace.Normal, color = ConsoleText }
			};
			_console.selection.isSelectable = true;

			_output.Add(_console);

			_root.Add(_output);

			// --- Suggestions -----------------------------------------------------------------
			// Minecraft-like completion: the candidates are listed above the input, the arrows move
			// the selection and Tab accepts it.
			_suggestionPanel = new VisualElement {
				style = {
					display = DisplayStyle.None,
					// The list keeps its full height: a full console shrinks, it never crushes the list.
					flexShrink = 0
				}
			};
			_suggestionPanel.style.backgroundColor = SuggestionBackground;
			ApplyFont(_suggestionPanel);

			// Paddings, margins and radius are removed, but not the border: the rule is kept to separate
			// the console from the candidates.
			_suggestionPanel.AddToClassList("p-0");
			_suggestionPanel.AddToClassList("m-0");
			_suggestionPanel.AddToClassList("rounded-none");
			_suggestionPanel.style.borderTopWidth = 1;
			_suggestionPanel.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f, 1f);
			_root.Add(_suggestionPanel);

			// --- Input ----------------------------------------------------------------------
			// Single line footer: the input has no frame and no radius, the button runs the command
			// (Enter does too).
			var footer = new VisualElement {
				style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 0 }
			};
			footer.AddToClassList("p-0");
			footer.AddToClassList("m-0");

			_input = new TextField { style = { flexGrow = 1, flexShrink = 1 } };
			ApplyFont(_input);
			MakeBareField(_input);
			_input.textEdition.placeholder = LanguageManager.Get("terminal.editor.placeholder");
			_input.RegisterCallback<KeyDownEvent>(OnInputKey, TrickleDown.TrickleDown);
			_input.RegisterValueChangedCallback(_ => RefreshSuggestions());
			footer.Add(_input);

			footer.Add(LocalizedButton("terminal.editor.execute", RunAsync));

			_root.Add(footer);

			PrintLine(LanguageManager.Get(
				"terminal.editor.ready",
				EditorTerminal.TerminalAPI?.GetRegistered().Length ?? 0
			));

			return _root;
		}

		/// <summary>Flat button (no radius) of the footer, as tall as the input.</summary>
		private static Button LocalizedButton(string key, Action clicked) {
			var button = new LocalizedButton { LangKey = key };
			button.clicked += clicked;
			button.AddToClassList("m-0");
			button.AddToClassList("rounded-none");
			button.style.height    = InputHeight;
			button.style.minHeight = InputHeight;
			button.style.maxHeight = InputHeight;
			return button;
		}

		public void OnFocus()
			=> _input?.Focus();

		/// <summary>Gives the keyboard back to the input, now and once the current event is over.</summary>
		private void KeepFocus() {
			if (_input == null)
				return;

			_input.Focus();

			// The focus can be stolen when the event that changed the value ends (Enter, click on a
			// suggestion): re-apply it on the next tick so typing can continue immediately.
			_input.schedule.Execute(() => _input?.Focus()).ExecuteLater(0);
		}

		public void OnDestroy()
			=> (_panel as TerminalPanel)?.Destroyed();

		// --- Console ----------------------------------------------------------------------------

		/// <summary>Clears the console (the "clear" command works without a dedicated button).</summary>
		private void ClearConsole() {
			_consoleLines.Clear();

			if (_console != null)
				_console.text = string.Empty;

			_historyIndex = -1;
			KeepFocus();
		}

		/// <summary>Repaints the window so the tab title follows the "title" command.</summary>
		private void OnTitleChanged()
			=> _window?.Repaint();

		private void PrintLine(string line) {
			if (_console == null || line == null)
				return;

			_consoleLines.Add(line);

			while (_consoleLines.Count > MaxLines)
				_consoleLines.RemoveAt(0);

			_console.text = string.Join(Environment.NewLine, _consoleLines);

			// Follow the tail.
			_output.schedule.Execute(() => _output.scrollOffset = new Vector2(0f, float.MaxValue));
		}

		// --- Command ----------------------------------------------------------------------------

		private void OnInputKey(KeyDownEvent evt) {
			switch (evt.keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					HideSuggestions();
					RunAsync();
					KeepFocus();
					evt.StopPropagation();
					break;

				case KeyCode.Tab:
					// Accepts the highlighted candidate; when the list is closed, opens it.
					if (_suggestionPanel.style.display == DisplayStyle.Flex)
						ApplySuggestion();
					else
						RefreshSuggestions(force: true);

					evt.StopPropagation();
					break;

				case KeyCode.UpArrow:
					if (_suggestionPanel.style.display == DisplayStyle.Flex)
						MoveSuggestion(-1);
					else
						Browse(-1);

					evt.StopPropagation();
					break;

				case KeyCode.DownArrow:
					if (_suggestionPanel.style.display == DisplayStyle.Flex)
						MoveSuggestion(1);
					else
						Browse(1);

					evt.StopPropagation();
					break;

				case KeyCode.Escape:
					HideSuggestions();
					evt.StopPropagation();
					break;
			}
		}

		private async void RunAsync() {
			if (_running)
				return;

			var command = _input?.value?.Trim();

			if (string.IsNullOrEmpty(command))
				return;

			_history.Add(command);
			_historyIndex = -1;
			_input.SetValueWithoutNotify(string.Empty);
			HideSuggestions();
			SaveHistory();

			PrintLine($"> {command}");

			var api = EditorTerminal.TerminalAPI;

			if (api == null) {
				PrintLine(LanguageManager.Get("terminal.editor.unknown", (object)command));
				return;
			}

			// Commands are matched on their first word: report an unknown one right away instead of
			// relying on the Execute result (a known command can legitimately return false).
			var name = command.Split(' ')[0];
			var known = (api.GetRegistered() ?? Array.Empty<ICommand>())
				.Any(candidate => candidate is IHelper helper
					&& string.Equals(helper.GetName(), name, StringComparison.OrdinalIgnoreCase));

			if (!known) {
				// The cast is required: with a string argument, the (language, key) overload would win over
				// the (key, params object[]) one and resolve nothing.
				PrintLine(LanguageManager.Get("terminal.editor.unknown", (object)name));
				PrintLine(LanguageManager.Get("terminal.editor.hint"));
				return;
			}

			_running = true;

			try {
				var handled = await api.Execute(command, _context);

				if (!handled)
					PrintLine(LanguageManager.Get("terminal.editor.unknown", (object)command));
			} catch (Exception exception) {
				PrintLine(exception.Message);
			} finally {
				_running = false;
			}

			// The command may have run synchronously (and stolen the focus): give it back.
			KeepFocus();
		}

		// --- Completion -------------------------------------------------------------------------

		/// <summary>Current word being typed, and everything before it.</summary>
		private void SplitToken(out string head, out string token) {
			var text  = _input?.value ?? string.Empty;
			var index = text.LastIndexOf(' ');

			token = index < 0 ? text : text[(index + 1)..];
			head  = index < 0 ? string.Empty : text[..(index + 1)];
		}

		/// <summary>
		/// Recomputes the candidates of the word being typed. <paramref name="force"/> shows the list even
		/// when the word did not change (Tab on a closed list).
		/// </summary>
		private void RefreshSuggestions(bool force = false) {
			var api = EditorTerminal.TerminalAPI;

			if (api == null || _suggestionPanel == null || _input == null)
				return;

			var text = _input.value ?? string.Empty;

			if (string.IsNullOrWhiteSpace(text)) {
				HideSuggestions();
				return;
			}

			SplitToken(out _, out var token);

			var whole         = text.Trim();
			var endsWithSpace = text.EndsWith(' ');

			// A space opens the list of what the current command accepts ("help ", "get ", …).
			if (!force && string.IsNullOrEmpty(token) && !endsWithSpace) {
				HideSuggestions();
				return;
			}

			string[] raw;

			try {
				raw = api.AutoComplete(_input.value ?? string.Empty, _context) ?? Array.Empty<string>();
			} catch (Exception exception) {
				// A command must never break the input: its completion is simply ignored.
				Logger.LogWarning($"Completion failed: {exception.Message}", nameof(TerminalInstance));
				HideSuggestions();
				return;
			}

			var candidates = raw
				.Where(candidate => MatchesTyped(candidate, whole, token))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
				.Take(8)
				.ToList();

			if (candidates.Count == 0) {
				HideSuggestions();
				return;
			}

			_suggestions.Clear();
			_suggestions.AddRange(candidates);
			_suggestionIndex = Mathf.Clamp(_suggestionIndex, 0, _suggestions.Count - 1);

			RenderSuggestions();
		}

		/// <summary>
		/// Whether a candidate matches what is typed: either it completes the current word, it starts with
		/// the whole input, or its own last word completes the current word ("settings get a.b.c" while
		/// typing "a.b").
		/// </summary>
		private static bool MatchesTyped(string candidate, string whole, string token) {
			if (string.IsNullOrEmpty(candidate))
				return false;

			if (string.IsNullOrEmpty(token) || candidate.StartsWith(whole, StringComparison.OrdinalIgnoreCase))
				return true;

			if (candidate.StartsWith(token, StringComparison.OrdinalIgnoreCase))
				return true;

			var lastSpace = candidate.LastIndexOf(' ');
			var lastWord  = lastSpace < 0 ? candidate : candidate[(lastSpace + 1)..];

			return lastWord.StartsWith(token, StringComparison.OrdinalIgnoreCase);
		}

		private void RenderSuggestions() {
			_suggestionPanel.Clear();
			_suggestionPanel.style.display = DisplayStyle.Flex;

			for (var i = 0; i < _suggestions.Count; i++) {
				var index = i;
				var row = new Label(_suggestions[i]) {
					style = {
						color           = ConsoleText,
						backgroundColor = i == _suggestionIndex ? SelectionBackground : Color.clear,
						paddingLeft     = InputPadding,
						paddingRight    = InputPadding,
						paddingTop      = 2,
						paddingBottom   = 2
					}
				};

				row.AddToClassList("m-0");

				row.RegisterCallback<ClickEvent>(_ => {
					_suggestionIndex = index;
					ApplySuggestion();
				});

				_suggestionPanel.Add(row);
			}
		}

		private void MoveSuggestion(int delta) {
			if (_suggestions.Count == 0)
				return;

			_suggestionIndex = (_suggestionIndex + delta + _suggestions.Count) % _suggestions.Count;
			RenderSuggestions();
		}

		/// <summary>Replaces the word being typed with the highlighted candidate.</summary>
		private void ApplySuggestion() {
			if (_suggestions.Count == 0 || _input == null)
				return;

			SplitToken(out var head, out _);

			var completion = _suggestions[Mathf.Clamp(_suggestionIndex, 0, _suggestions.Count - 1)];
			var current    = (_input.value ?? string.Empty).Trim();

			// A candidate holding a whole line ("lang fr-FR", "help clear") replaces the input instead of
			// being appended to it: the command name may be an alias ("language" → "language fr-FR").
			var value = completion.Contains(' ')
				? completion
				: current.Length > 0 && completion.StartsWith(current, StringComparison.OrdinalIgnoreCase)
					? completion
					: head + completion;

			// A completed command word is followed by a space, ready for its arguments.
			if (!value.Contains(' '))
				value += " ";

			_input.SetValueWithoutNotify(value);
			_input.cursorIndex = _input.value.Length;
			_input.selectIndex = _input.value.Length;
			_input.Focus();

			_suggestionIndex = 0;
			RefreshSuggestions();
		}

		private void HideSuggestions() {
			if (_suggestionPanel == null)
				return;

			_suggestions.Clear();
			_suggestionIndex = 0;
			_suggestionPanel.Clear();
			_suggestionPanel.style.display = DisplayStyle.None;
		}

		/// <summary>Walks the history: -1 being the "new command" position, where the draft is kept.</summary>
		private void Browse(int delta) {
			if (_history.Count == 0 || _input == null)
				return;

			if (_historyIndex < 0 && delta < 0) {
				// Leaving the draft: remember it so moving back down restores it.
				_draft        = _input.value ?? string.Empty;
				_historyIndex = _history.Count - 1;
			} else if (delta < 0) {
				_historyIndex = Math.Max(0, _historyIndex - 1);
			} else if (_historyIndex < 0) {
				return;
			} else if (_historyIndex >= _history.Count - 1) {
				_historyIndex = -1;
			} else {
				_historyIndex++;
			}

			var value = _historyIndex < 0 ? _draft : _history[_historyIndex];

			_input.SetValueWithoutNotify(value);
			_input.cursorIndex = value.Length;
			_input.selectIndex = value.Length;
		}

		// --- Persisted history ------------------------------------------------------------------

		/// <summary>Restores the commands typed in the previous sessions (history.txt in the config folder).</summary>
		private void LoadHistory() {
			foreach (var entry in CommandHistory.Load())
				if (!_history.Contains(entry))
					_history.Add(entry);
		}

		/// <summary>
		/// Appends the last command to the history file right away (append-only, so a domain reload
		/// cannot lose it and other instances can write to the same file).
		/// </summary>
		private void SaveHistory() {
			while (_history.Count > CommandHistory.MaxEntries)
				_history.RemoveAt(0);

			if (_history.Count > 0)
				CommandHistory.Append(_history[^1]);
		}

		// --- IContext ---------------------------------------------------------------------------

		/// <summary>
		/// <see cref="IContext"/> handed to the commands: output goes to the console, environment and
		/// result live as long as the panel is open.
		/// </summary>
		private sealed class EditorContext : IContext {
			private readonly Action<string> _print;
			private readonly Action         _clear;
			private readonly Action         _titleChanged;
			private readonly Dictionary<string, object> _environments = new();

			private string _title = string.Empty;
			private bool   _printing = true;

			public EditorContext(Action<string> print, Action clear, Action titleChanged) {
				_print        = print;
				_clear        = clear;
				_titleChanged = titleChanged;
			}

			public int GetId()
				=> GetHashCode();

			public Dictionary<string, object> GetEnvironments()
				=> _environments;

			public T GetEnvironment<T>(string key, T defaultValue = default)
				=> !string.IsNullOrEmpty(key) && _environments.TryGetValue(key.ToUpperInvariant(), out var value) && value is T typed
					? typed
					: defaultValue;

			public void SetEnvironment(string key, object value) {
				if (string.IsNullOrEmpty(key))
					return;

				key = key.ToUpperInvariant();

				if (value == null)
					_environments.Remove(key);
				else
					_environments[key] = value;
			}

			public void Print(string message) {
				if (_printing && !string.IsNullOrEmpty(message))
					_print(message);
			}

			public void PrintLn(string message)
				=> Print(message);

			public void Clear()
				=> _clear?.Invoke();

			public string GetTitle()
				=> _title;

			public void SetTitle(string title) {
				_title = title ?? string.Empty;
				_titleChanged?.Invoke();
			}

			public object GetResult()
				=> GetEnvironment<object>("result");

			public void SetResult(object result)
				=> SetEnvironment("result", result);

			public bool CanPrinting()
				=> _printing;

			public void SetPrinting(bool printing)
				=> _printing = printing;
		}
	}
}
