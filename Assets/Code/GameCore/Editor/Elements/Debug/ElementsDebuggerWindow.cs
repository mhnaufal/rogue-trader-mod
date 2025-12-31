using System;
using System.Collections.Generic;
using Code.GameCore.Editor.Elements.Debug.Views;
using JetBrains.Annotations;
using Kingmaker.ElementsSystem;
using Kingmaker.Utility.DotNetExtensions;
using Kingmaker.Utility.EditorPreferences;
using UnityEditor;
using UnityEngine;

namespace Code.GameCore.Editor.Elements.Debug
{
	public class ElementsDebuggerWindow : EditorWindow
	{
		public enum View
		{
			Stats = 0,
			Logger = 1,
		}

		private View m_ActiveView;
		private Dictionary<View, IElementsDebuggerView> m_Views = new();
		
		[MenuItem("Design/Elements Debugger", false, 4001)]
		public static void ShowWindow()
			=> ShowWindow(View.Stats);
		
		public static void ShowWindow(View activeView)
		{
			var window = GetWindow<ElementsDebuggerWindow>();
			window.m_ActiveView = activeView;
			window.Show();
		}

		[NotNull]
		private IElementsDebuggerView GetActiveView()
			=> m_Views.Get(m_ActiveView) ?? throw new Exception("Active view is missing");

		private void OnEnable()
		{
			titleContent = new GUIContent("Elements Debugger");
			
			m_Views[View.Stats] = new ElementsStatsView();
			m_Views[View.Logger] = new ElementsLoggerView();

			foreach (var (_, view) in m_Views)
			{
				view.OnEnable();
			}
		}

		private void OnDisable()
		{
			foreach (var (_, view) in m_Views)
			{
				view.OnDisable();
			}
			
			m_Views.Clear();
		}

		private void OnGUI()
		{
			DrawToolbar();
			GetActiveView().OnGUI(position);
		}

		private void DrawToolbar()
		{
			using EditorGUILayout.HorizontalScope _ = new(EditorStyles.toolbar);
			
			ElementsDebugger.Enabled = GUILayout.Toggle(ElementsDebugger.Enabled, "Enabled");
			
			GUILayout.Space(25);

			bool switchToStats = GUILayout.Toggle(m_ActiveView == View.Stats, "Stats", "Button");
			bool switchToLogger = GUILayout.Toggle(m_ActiveView == View.Logger, "Logger", "Button");

			if (switchToStats && m_ActiveView != View.Stats)
				m_ActiveView = View.Stats;
			else if (switchToLogger && m_ActiveView != View.Logger)
				m_ActiveView = View.Logger;
		}
	}
}