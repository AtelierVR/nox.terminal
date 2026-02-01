using System.Collections.Generic;
using UnityEngine;

namespace Nox.Terminal {
	public interface IContext {
		/// <summary>
		/// Get the id of the context.
		/// </summary>
		/// <returns></returns>
		public int GetId();

		/// <summary>
		/// Get environment variables for the context.
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, object> GetEnvironments();

		/// <summary>
		/// Get an environment variable by key.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="defaultValue"></param>
		/// <returns></returns>
		public T GetEnvironment<T>(string key, T defaultValue = default);

		/// <summary>
		/// Get an environment variable by key.
		/// If the value is null, the key will be removed from the environment.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		public void SetEnvironment(string key, object value);

		/// <summary>
		/// Add text to the terminal output.
		/// </summary>
		/// <param name="message"></param>
		public void Print(string message);

		/// <summary>
		/// Add text to the terminal output with a newline.
		/// </summary>
		/// <param name="message"></param>
		public void PrintLn(string message);

		/// <summary>
		/// Clear the terminal output.
		/// </summary>
		public void Clear();

		/// <summary>
		/// Get the title of the terminal.
		/// </summary>
		/// <returns></returns>
		public string GetTitle();

		/// <summary>
		/// Set the title of the terminal.
		/// </summary>
		/// <param name="title"></param>
		public void SetTitle(string title);

		/// <summary>
		/// Get the result of the last executed command.
		/// </summary>
		/// <returns></returns>
		public object GetResult();

		/// <summary>
		/// Set the result of the last executed command.
		/// </summary>
		/// <param name="result"></param>
		public void SetResult(object result);

		public bool CanPrinting();

		public void SetPrinting(bool printing);
	}
}