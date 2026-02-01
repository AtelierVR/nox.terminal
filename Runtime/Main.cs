using System;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.Terminal.Commands;

namespace Nox.Terminal.Runtime {
	public class Main : IMainModInitializer, ITerminalAPI {
		internal        IMainModCoreAPI CoreAPI;
		internal static Main           Instance;
		private         CommandManager _manager;
		private         LanguagePack   _lang;

		private (uint, ICommand)[] _defaultCommands = Array.Empty<(uint, ICommand)>();

		public void OnInitializeMain(IMainModCoreAPI api) {
			CoreAPI  = api;
			Instance = this;
			_manager = new CommandManager();
			_lang    = CoreAPI.AssetAPI.GetAsset<LanguagePack>("lang.asset");
			LanguageManager.AddPack(_lang);

			_defaultCommands = new (uint, ICommand)[] {
				(0u, new TestCommand()),
				(0u, new CurlCommand()),
				(0u, new HelpCommand()),
				(0u, new TitleCommand()),
				(0u, new ClearCommand()),
				(0u, new EchoCommand()),
				(0u, new SetEnvCommand()),
				(0u, new GetEnvCommand()),
				(0u, new UnsetEnvCommand()),
			};

			for (var i = 0; i < _defaultCommands.Length; i++)
				_defaultCommands[i].Item1 = _manager.Register(_defaultCommands[i].Item2);
		}

		public void OnDisposeMain() {
			for (var i = 0; i < _defaultCommands.Length; i++)
				_manager.Unregister(_defaultCommands[i].Item1);
			_defaultCommands = Array.Empty<(uint, ICommand)>();
			LanguageManager.RemovePack(_lang);
			CoreAPI  = null;
			Instance = null;
			_manager = null;
		}

		public ICommand[] GetRegistered()
			=> _manager.Commands
				.ConvertAll(c => c.Item2)
				.ToArray();

		public async UniTask<bool> Execute(string args, IContext context = null)
			=> await _manager.ExecuteCommand(args, context);

		public string[] AutoComplete(string args, IContext context = null)
			=> _manager.AutoComplete(args, context);

		public uint Register(ICommand command)
			=> _manager.Register(command);

		public void Unregister(uint id)
			=> _manager.Unregister(id);

		public string GetPrefix()
			=> CommandManager.CommandPrefix;
	}
}