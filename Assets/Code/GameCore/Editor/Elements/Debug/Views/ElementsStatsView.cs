using System;
using Code.GameCore.Editor.Utility;
using JetBrains.Annotations;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Properties.BaseGetter;
using Kingmaker.Utility.EditorPreferences;
using Owlcat.Editor.Core.Utility;
using Owlcat.Editor.Utility;
using UnityEditor;
using UnityEngine;

namespace Code.GameCore.Editor.Elements.Debug.Views
{
	public class ElementsStatsView : IElementsDebuggerView
	{
		private Vector2 m_ElementsListScrollPosition;
		private Vector2 m_AssetsListScrollPosition;
		
		private int m_ElementsPage;
		private int m_AssetsPage;

		private object m_SplitterState;
		
		[CanBeNull]
		private string m_AssetsStringFilter;
		
		[CanBeNull]
		private string m_ElementsStringFilter;

		[CanBeNull]
		private SimpleBlueprint m_SelectedAsset;

		void IElementsDebuggerView.OnEnable()
		{
			m_SelectedAsset = null;
			m_SplitterState = SplitterLayout.CreateSplitterState(0.3f, 0.7f);
		}

		void IElementsDebuggerView.OnDisable()
		{
		}

		void IElementsDebuggerView.OnGUI(Rect position)
		{
			DrawToolbar();
			
			GUILayout.Space(5);

			using (new GUILayout.HorizontalScope())
			{
				SplitterLayout.BeginVertical(m_SplitterState);

				using (new EditorGUILayout.HorizontalScope())
				{
					DrawAssetsList();

					GUILayout.Box(
						"", OwlcatEditorStyles.Instance.Splitter, GUILayout.ExpandHeight(true), GUILayout.Width(1));
				}

				DrawElementsList();
				
				SplitterLayout.EndVertical();
			}
		}

		private static void DrawToolbar()
		{
			using EditorGUILayout.HorizontalScope _ = new(EditorStyles.toolbar);
			
			ElementsDebuggerDatabase.ExceptionsOnly =
				GUILayout.Toggle(ElementsDebuggerDatabase.ExceptionsOnly, "Exceptions Only");

			GUILayout.FlexibleSpace();
				
			if (GUILayout.Button("Clear"))
			{
				ElementsDebuggerDatabase.Clear();
			}
		}

		private void DrawAssetsList()
		{
			using var scope = new EditorGUILayout.VerticalScope();
			
			GUI.Box(scope.rect, GUIContent.none);

			if (m_SelectedAsset == null)
			{
				GUILayout.Button("No asset selected");
			}
			else
			{
				var debugInfo = ElementsDebuggerDatabase.Get(m_SelectedAsset);

				bool pressed;
				using (debugInfo?.LastException != null ? GuiScopes.Color(Color.red) : null)
					pressed = GUILayout.Button(m_SelectedAsset.name);

				if (pressed)
					m_SelectedAsset = null;
			}

			GUILayout.Space(5);

			DrawFilter(ref m_AssetsStringFilter);
			
			GUILayout.Space(5);

			var assets = ElementsDebuggerDatabase.SelectAssets(
				m_AssetsStringFilter,
				ref m_AssetsPage,
				out int pagesCount);
			m_AssetsPage = Pagination(m_AssetsPage, pagesCount);
			
			GUILayout.Space(5);

			using var scroll = new EditorGUILayout.ScrollViewScope(m_AssetsListScrollPosition, GUILayout.ExpandWidth(true));
			using var _ = GuiScopes.Vertical(GUILayout.ExpandWidth(true));

			GUILayout.Space(5);

			foreach (var asset in assets)
			{
				DrawAsset(asset.Asset);
			}

			GUILayout.Space(5);

			m_AssetsListScrollPosition = scroll.scrollPosition;
		}

		private void DrawElementsList()
		{
			using var scope = new EditorGUILayout.VerticalScope();
			
			GUI.Box(scope.rect, GUIContent.none);

			GUILayout.Space(5);
			
			DrawFilter(ref m_ElementsStringFilter);
			
			GUILayout.Space(5);

			var elements = ElementsDebuggerDatabase.SelectElements(
				m_SelectedAsset,
				m_ElementsStringFilter,
				ref m_ElementsPage,
				out int pagesCount);
			m_ElementsPage = Pagination(m_ElementsPage, pagesCount);

			GUILayout.Space(5);

			using var scroll = new EditorGUILayout.ScrollViewScope(m_ElementsListScrollPosition, GUILayout.ExpandWidth(true));
			using var _ = GuiScopes.Vertical(GUILayout.ExpandWidth(true));

			GUILayout.Space(5);

			foreach (var element in elements)
			{
				GUILayout.Space(5);
				DrawElement(element.Element);
				GUILayout.Space(5);
			}

			GUILayout.Space(5);

			m_ElementsListScrollPosition = scroll.scrollPosition;
		}

		private static void DrawFilter(ref string filterString)
		{
			using (new GUILayout.HorizontalScope())
			{
				filterString = GUILayout.TextField(filterString, GUILayout.ExpandWidth(true));
				if (GUILayout.Button("clear", GUILayout.Width(50)))
					filterString = null;
			}
		}

		private static int Pagination(int page, int pagesCount)
		{
			page = Math.Clamp(page, 0, Math.Max(0, pagesCount - 1));

			using EditorGUILayout.HorizontalScope _ = new();
			
			GUILayout.FlexibleSpace();

			if (GUILayout.Button("< "))
				page = 0;
			if (GUILayout.Button("<<"))
				page = Math.Max(0, page - 1);
				
			GUILayout.Space(10);
			GUILayout.Label($"{page + 1}/{pagesCount}");
			GUILayout.Space(10);
				
			if (GUILayout.Button(">>"))
				page = Math.Min(pagesCount - 1, page + 1);
			if (GUILayout.Button(" >"))
				page = pagesCount - 1;
				
			GUILayout.FlexibleSpace();

			return page;
		}

		private void ShowContextMenu(Rect rect, object obj)
		{
			if (!rect.Contains(Event.current.mousePosition) || Event.current.button != 1)
				return;
			
			var maybeBlueprint = obj as SimpleBlueprint;
			var maybeElement = obj as Element;
			var debug =
				ElementsDebuggerDatabase.Get(maybeBlueprint) ??
				ElementsDebuggerDatabase.Get(maybeElement?.Owner);

			string objName = maybeBlueprint?.name ?? maybeElement?.name ?? "unknown";
			
			var contextMenu = new GenericMenu();
				
			contextMenu.AddDisabledItem(new GUIContent(objName));
			
			contextMenu.AddSeparator("");
			
			contextMenu.AddItem(
				new GUIContent("Select Asset"),
				false,
				() => Selection.activeObject = BlueprintEditorWrapper.Wrap(maybeBlueprint ?? maybeElement?.Owner));
			
			contextMenu.AddItem(
				new GUIContent("Copy Asset Guid"),
				false,
				() => GUIUtility.systemCopyBuffer = (maybeBlueprint ?? maybeElement?.Owner)?.AssetGuid);
			
			if (debug?.LastException != null)
			{
				contextMenu.AddItem(
					new GUIContent("Copy Exception"),
					false,
					() => GUIUtility.systemCopyBuffer = debug.LastException.Message + "\n" + debug.LastException.StackTrace);
			}


			contextMenu.ShowAsContext();
		}

		private void DrawAsset(SimpleBlueprint asset)
		{
			var debugInfo = ElementsDebuggerDatabase.Get(asset);

			using (debugInfo?.LastException != null ? GuiScopes.Color(Color.red) : null)
				GUILayout.Label(m_SelectedAsset == asset ? "* " + asset.name : asset.name);

			var rect = GUILayoutUtility.GetLastRect();
			bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
			if (pressed)
			{
				m_SelectedAsset = asset;
				Selection.activeObject = BlueprintEditorWrapper.Wrap(asset);
			}
			
			ShowContextMenu(rect, asset);
		}

		private void DrawElement(Element e)
		{
			var debugInfo = ElementsDebuggerDatabase.Get(e);
			if (debugInfo == null)
			{
				GUILayout.Label("Debug info missing");
				return;
			}

			var exception = debugInfo.LastException?.InnerException ?? debugInfo.LastException;
			
			using var horizontal = new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true));
			GUI.Box(horizontal.rect, GUIContent.none);
			
			var owner = e.Owner;
			using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.Label($"{e.GetType().Name} #{e.AssetGuidShort}");
					
					GUILayout.Space(5);

					Color color;
					string value;
					if (debugInfo.LastException != null)
					{
						color = Color.red;
						value = "[exception]";
					}
					else
					{
						bool isCondition = e is Condition;
						bool isFalse = isCondition && debugInfo.LastResult == 0;
						bool isGetter = e is PropertyGetter;
						
						if (isCondition)
						{
							color = isFalse ? Color.yellow : Color.green;
							value = isFalse ? "[false]" : "[true]";
						}
						else if (isGetter)
						{
							color = Color.green;
							value = $"[{debugInfo.LastResult}]";
						}
						else
						{
							color = Color.green;
							value = "[success]";
						}
					}

					using (GuiScopes.Color(color))
						GUILayout.Label(value);
					
					GUILayout.FlexibleSpace();
				}
					
				GUILayout.Label(e.GetCaption());

				if (owner != null && m_SelectedAsset == null)
				{
					string path = BlueprintsDatabase.GetAssetPath(owner);
					GUILayout.Label(path);
				}

				if (exception != null)
					using (GuiScopes.Color(Color.red))
						GUILayout.Label(exception.Message);
			}
			
			GUILayout.FlexibleSpace();
			
			ShowContextMenu(horizontal.rect, e);
		}
	}
}