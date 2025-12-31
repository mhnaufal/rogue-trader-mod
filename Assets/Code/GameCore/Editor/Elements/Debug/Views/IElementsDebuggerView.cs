using UnityEngine;

namespace Code.GameCore.Editor.Elements.Debug.Views
{
	public interface IElementsDebuggerView
	{
		void OnEnable();
		void OnDisable();
		void OnGUI(Rect position);
	}
}