using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Utils;
using Nox.Terminal.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Terminal.Clients {
	public class TerminalComponent : MonoBehaviour {
		private TerminalPage          _page;
		private GameObject            _navigation;
		public  TMPro.TMP_InputField  input;
		public  TMPro.TextMeshProUGUI output;
		public  Image                 labelIcon;
		public  TextLanguage          label;

		public RectTransform                   viewport;
		public HorizontalOrVerticalLayoutGroup content;

		public (int lines, int columns) GetDimensions() {
			if (!output || output.text == null)
				return (0, 0);

			var screen = new Vector2(
				viewport.rect.size.x - content.padding.left - content.padding.right,
				viewport.rect.size.y - content.padding.top  - content.padding.bottom
			);

			var fontSize   = output.fontSize;
			var charWidth  = fontSize * 0.6f;
			var lineHeight = fontSize * 1.2f;

			var columns = Mathf.FloorToInt(screen.x / charWidth);
			var lines   = Mathf.FloorToInt(screen.y / lineHeight);

			return (
			Mathf.Max(0, lines),
			Mathf.Max(0, columns)
			);
		}

		private void OnSubmit(string command)
			=> OnSubmitAsync(command).Forget();

		private async UniTask OnSubmitAsync(string command) {
			if (string.IsNullOrWhiteSpace(command)) return;
			input.interactable = false;

			command = command.Trim();
			
			_page.AddToHistory(command);
			var printExecuting = _page.CanPrinting();

			if (printExecuting)
				_page.PrintLn(
					LanguageManager.Get(
						"terminal.command.executing",
						new object[] { command }
					)
				);

			if (await Main.Instance.Execute(command.Trim(), _page)) {
				Logger.LogDebug($"Command executed: {command}");
			} else {
				if (printExecuting)
					_page.PrintLn(LanguageManager.Get("terminal.command.not_found"));
				Logger.LogWarning($"Command execution failed: {command}");
			}

			input.text         = string.Empty;
			_page._auto        = Array.Empty<string>();
			input.interactable = true;
			input.ActivateInputField();
		}

		private void OnValueChanged(string value) {
			if (!string.IsNullOrEmpty(value)) 
				_page.ResetHistoryIndex();
			
			_page._auto = !string.IsNullOrEmpty(value)
				? Main.Instance.AutoComplete(value, _page)
				: Array.Empty<string>();
			
			Logger.LogDebug($"Autocomplete: {string.Join(", ", _page._auto)}");
		}

		private void Update() {
			if (input.isFocused) 
				HandleHistoryNavigation();
		}

		private void HandleHistoryNavigation() {
			if (Input.GetKeyDown(KeyCode.UpArrow)) {
				var previousCommand = _page.GetPreviousCommand();
				if (string.IsNullOrEmpty(previousCommand)) return;
				input.text          = previousCommand;
				input.caretPosition = input.text.Length;
			}
			else if (Input.GetKeyDown(KeyCode.DownArrow)) {
				var nextCommand = _page.GetNextCommand();
				input.text = nextCommand;
				input.caretPosition = input.text.Length;
			}
		}


		public static (GameObject, TerminalComponent) Generate(TerminalPage page, RectTransform parent) {
			var content = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/split.prefab"), parent);

			var component = content.AddComponent<TerminalComponent>();
			component._page = page;
			content.name    = $"[{page.GetKey()}_{content.GetInstanceID()}]";
			var splitContent = Reference.GetComponent<RectTransform>("content", content);

			// generate dashboard
			var container = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/container_full.prefab"), splitContent);
			var withTitle = Instantiate(
				Client.GetAsset<GameObject>("ui:prefabs/with_title.prefab"),
				Reference.GetComponent<RectTransform>("content", container)
			);

			var header = Reference.GetReference("header", withTitle);
			var icon = Instantiate(
				Client.GetAsset<GameObject>("ui:prefabs/header_icon.prefab"),
				Reference.GetComponent<RectTransform>("before", header)
			);

			var label = Instantiate(
				Client.GetAsset<GameObject>("ui:prefabs/header_label.prefab"),
				Reference.GetComponent<RectTransform>("content", header)
			);

			component.labelIcon        = Reference.GetComponent<Image>("image", icon);
			component.label            = Reference.GetComponent<TextLanguage>("text", label);
			component.labelIcon.sprite = Client.GetAsset<Sprite>("ui:icons/terminal.png");
			component.label.UpdateText(
				"terminal.page.title",
				new[] { LanguageManager.Get("terminal.page.title.default") }
			);

			var terminal = Instantiate(
				Client.GetAsset<GameObject>("prefabs/terminal.prefab"),
				Reference.GetComponent<RectTransform>("content", withTitle)
			);

			component.input    = Reference.GetComponent<TMPro.TMP_InputField>("input", terminal);
			component.output   = Reference.GetComponent<TMPro.TextMeshProUGUI>("output", terminal);
			component.content  = Reference.GetComponent<HorizontalOrVerticalLayoutGroup>("content", terminal);
			component.viewport = Reference.GetComponent<RectTransform>("viewport", terminal);

			// generate profile
			component._navigation = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/container.prefab"), splitContent);

			component.input.onSubmit.AddListener(component.OnSubmit);
			component.input.onValueChanged.AddListener(component.OnValueChanged);
			component.input.text  = component._page._draft ?? string.Empty;
			component.output.text = string.Empty;

			return (content, component);
		}
	}
}