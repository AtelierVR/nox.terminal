using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.Terminal;

namespace Nox.Terminal.Editor {
	/// <summary>
	/// Editor entry point of the <c>terminal</c> mod (declared in <c>nox.mod.json</c> as
	/// <c>entrypoints.editor</c>): exposes the terminal API of this mod to the editor panel.
	/// </summary>
	public class EditorTerminal : IEditorModInitializer {
		public static EditorTerminal Instance { get; private set; }

		internal IEditorModCoreAPI CoreAPI;

		/// <summary>Terminal API (execute, autocomplete, registered commands), or <c>null</c>.</summary>
		public static ITerminalAPI TerminalAPI
			=> Instance?.CoreAPI?.ModAPI.GetSelf()?.GetInstance<ITerminalAPI>();

		public void OnInitializeEditor(IEditorModCoreAPI api) {
			CoreAPI  = api;
			Instance = this;
		}

		public void OnDisposeEditor() {
			Instance = null;
			CoreAPI  = null;
		}
	}
}
