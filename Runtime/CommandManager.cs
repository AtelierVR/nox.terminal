using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;

namespace Nox.Terminal.Runtime {
	public class CommandManager {
		public readonly List<(uint, ICommand)> Commands = new();

		private uint _nextId = uint.MinValue;

		private uint NextId
			=> _nextId == uint.MaxValue ? _nextId = 0 : ++_nextId;

		public const string CommandPrefix = "/";

		public uint Register(ICommand command) {
			if (command == null) return uint.MaxValue;
			var id = NextId;
			if (Commands.Exists(c => c.Item1 == id))
				return uint.MaxValue;
			Commands.Add((id, command));
			Logger.Log($"Registered command: {command.GetType().Name} with ID {id}");
			return id;
		}

		public void Unregister(uint id) {
			var index = Commands.FindIndex(c => c.Item1 == id);
			if (index >= 0) return;
			Commands.RemoveAt(index);
			Logger.Log($"Unregistered command with ID {id}");
		}

		public async UniTask<bool> ExecuteCommand(string args, IContext context = null) {
			foreach (var command in Commands)
				if (await command.Item2.Execute(args, context))
					return true;
			return false;
		}

		public string[] AutoComplete(string args, IContext context = null)
			=> Commands
				.Select(command => command.Item2.AutoComplete(args, context))
				.SelectMany(s => s)
				.Where(s => !string.IsNullOrEmpty(s))
				.Distinct()
				.ToArray();
	}
}