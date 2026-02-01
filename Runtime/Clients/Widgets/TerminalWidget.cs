using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using Nox.Terminal.Clients;
using Nox.UI;
using Nox.UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.Terminal.Widgets {
	public class TerminalWidget : MonoBehaviour, IWidget {
		public static string GetDefaultKey()
			=> "terminal";

		public string GetKey()
			=> GetDefaultKey();

		private int        _mid;
		private GameObject _content;

		private void OnClick()
			=> Client.UiAPI?.SendGoto(_mid, TerminalPage.GetStaticKey());

		public Vector2Int GetSize()
			=> Vector2Int.one;

		public int GetPriority()
			=> 70;

		public static bool TryMake(IMenu menu, RectTransform parent, out (GameObject, IWidget) values) {
			var prefab    = Client.GetAsset<GameObject>("ui:prefabs/grid_item.prefab");
			var instance  = Instantiate(prefab, parent);
			var component = instance.AddComponent<TerminalWidget>();
			component._mid = menu.GetId();

			var button = Reference.GetComponent<Button>("button", instance);
			button.onClick.AddListener(component.OnClick);
			instance.name = $"[{component.GetKey()}_{instance.GetInstanceID()}]";
			values        = (instance, component);

			prefab             = Client.GetAsset<GameObject>("ui:prefabs/widget.prefab");
			component._content = Instantiate(prefab, Reference.GetComponent<RectTransform>("content", instance));

			component.UpdateIcon().Forget();

			return true;
		}

		private async UniTask UpdateIcon() {
			var icon      = await Client.GetAssetAsync<Sprite>("ui:icons/terminal.png");
			var labelIcon = Reference.GetComponent<Image>("icon", _content);
			labelIcon.sprite = icon;
		}
	}
}