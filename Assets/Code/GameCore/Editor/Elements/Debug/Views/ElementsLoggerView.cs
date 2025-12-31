using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Kingmaker.Editor;
using Kingmaker.Editor.Elements;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Properties.BaseGetter;
using Kingmaker.UnitLogic.Progression.Prerequisites;
using Kingmaker.Utility.DotNetExtensions;
using Kingmaker.Utility.EditorPreferences;
using Owlcat.Editor.Core.Utility;
using Owlcat.Editor.Utility;
using Pathfinding.RVO;
using RectEx;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using GameAction = Kingmaker.ElementsSystem.GameAction;

namespace Code.GameCore.Editor.Elements.Debug.Views
{
    public class ElementsLoggerView : IElementsDebuggerView
    {
        [MenuItem("Design/Elements Logger", false, 4002)]
        public static void Open()
            => ElementsDebuggerWindow.ShowWindow(ElementsDebuggerWindow.View.Logger);

        private Vector2 m_Scroll;
        private bool m_ShowActions = true;
        private bool m_ShowConditions = true;
        private bool m_ShowEvaluators = true;
        private bool m_ShowPropertyGetters = true;
        private bool m_ShowPrerequisites = true;
        private bool m_ErrorsOnly;
        
        private readonly HashSet<SimpleBlueprint> m_ExcludedAssets = new();


        void IElementsDebuggerView.OnEnable()
        {
        }

        void IElementsDebuggerView.OnDisable()
        {
        }

        void IElementsDebuggerView.OnGUI(Rect position)
        {
            DrawToolbar();
            DrawEntries(position);
        }

        private bool ShouldShow(ElementsDebuggerDatabase.ElementLogEntry entry)
        {
            if (m_ErrorsOnly && entry.Exception == null)
                return false;

            if (m_ExcludedAssets.Contains(entry.Element.Owner))
                return false;
            
            var e = entry.Element;
            return e is GameAction && m_ShowActions ||
                   e is Condition && m_ShowConditions ||
                   e is Evaluator && m_ShowEvaluators ||
                   e is PropertyGetter && m_ShowPropertyGetters ||
                   e is Prerequisite && m_ShowPrerequisites;
        }

        private void DrawEntries(Rect position)
        {
            using var scope = new EditorGUILayout.ScrollViewScope(m_Scroll);
            
            m_Scroll = scope.scrollPosition;
            float lh = EditorGUIUtility.singleLineHeight;

            int count = 0;
            SimpleBlueprint prevAsset = null;
            var filteredEntries = ElementsDebuggerDatabase.ElementsLog.Where(ShouldShow);
            foreach (var entry in filteredEntries)
            {
                count += 1;
                
                var element = entry.Element;
                if (prevAsset != element.Owner)
                {
                    prevAsset = element.Owner;
                    count += 1;
                }
            }
            
            var r = GUILayoutUtility.GetRect(0, position.width, count * lh, count * lh);
            int maxWidth = 0;
            float y = 0;
            prevAsset = null;
            bool even = false;
            foreach (var entry in filteredEntries)
            {
                var element = entry.Element;
                if (y < m_Scroll.y - lh || y > position.height + m_Scroll.y)
                {
                    y += lh;
                    if (prevAsset != element.Owner)
                    {
                        prevAsset = element.Owner;
                        even = !even;
                        y += lh;
                    }

                    continue;
                }

                var line = new Rect(r.x, y, r.width, lh);
                    
                if (prevAsset != element.Owner)
                {
                    prevAsset = element.Owner;
                    even = !even;
                    
                    if (even)
                        GUI.Box(line, GUIContent.none);

                    DrawAsset(line, element.Owner);

                    y += lh;
                    line = new Rect(r.x, y, r.width, lh);
                }
                
                if (even)
                    GUI.Box(line, GUIContent.none);
                    
                int w = DrawIndent(line, entry);
                w += DrawActionLine(new Rect(line.x + w, line.y, line.width - w, line.height), element);
                maxWidth = math.max(maxWidth, w);
                    
                y += lh;
            }

            GUILayoutUtility.GetRect(maxWidth, maxWidth, 0, 0); // for horizontal scrolling
        }

        private void DrawAsset(Rect r, SimpleBlueprint asset)
        {
            EditorGUI.Foldout(CutFromExtensions.SliceFromLeft(ref r, 400), true, asset.NameSafe());
            EditorGUI.LabelField(CutFromExtensions.SliceFromLeft(ref r, 250), asset.GetType().Name);
            
            bool select = GUI.Button(CutFromExtensions.SliceFromRight(ref r, 75), "select");
            if (select)
                Selection.activeObject = BlueprintEditorWrapper.Wrap(asset);
            
            bool exclude = GUI.Button(CutFromExtensions.SliceFromRight(ref r, 75), "exclude");
            if (exclude)
                m_ExcludedAssets.Add(asset);
        }

        private static int DrawIndent(Rect r, ElementsDebuggerDatabase.ElementLogEntry e)
        {
            string typeMarker = e.Element switch
            {
                GameAction => "[A]",
                Condition => "[C]",
                Evaluator => "[E]",
                PropertyGetter => "[G]",
                Prerequisite => "[P]",
                _ => "[?]"
            };

            GUI.Label(CutFromExtensions.SliceFromLeft(ref r, r.height * 1.5f), typeMarker);

            (string result, Color color) = ElementsBaseDrawer.GetResultAndColor(e.Element, e.Result, e.Exception);

            using (GuiScopes.Color(color))
                GUI.Label(CutFromExtensions.SliceFromLeft(ref r, 75), result);
            
            GUI.Label(CutFromExtensions.SliceFromLeft(ref r, 75), e.Element.AssetGuidShort);
            
            if (e.Depth == 0)
                GUI.Label(CutFromExtensions.SliceFromLeft(ref r, r.height), ">");

            return (int)(e.Depth * 10 + 75 * 2 + r.height * 2.5f + 1);
        }

        private int DrawActionLine(Rect r, Element e)
        {
            var title = new GUIContent($"{e.GetType().Name}: {e.GetCaption()}");
            float labelWidth =  GUI.skin.label.CalcSize(title).x; 
            GUI.Label(CutFromExtensions.SliceFromLeft(ref r, labelWidth), title);
            
            return (int)r.xMax;
        }

        private void DrawToolbar()
        {
            using var scope = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true));

            bool clicked;
            using (GuiScopes.Color(ElementsDebugger.LogEnabled && Application.isPlaying ? Color.green : GUI.color))
            {
                clicked = GUILayout.Button(
                    ElementsDebugger.LogEnabled ? "Stop" : "Activate",
                    EditorStyles.toolbarButton,
                    GUILayout.ExpandWidth(false));
            }

            ElementsDebugger.LogEnabled = clicked ? !ElementsDebugger.LogEnabled : ElementsDebugger.LogEnabled;  
            
            GUILayout.FlexibleSpace();

            m_ShowActions = GUILayout.Toggle(m_ShowActions, "Actions");
            m_ShowConditions = GUILayout.Toggle(m_ShowConditions, "Conditions");
            m_ShowEvaluators = GUILayout.Toggle(m_ShowEvaluators, "Evaluators");
            m_ShowPropertyGetters = GUILayout.Toggle(m_ShowPropertyGetters, "Getters");
            m_ShowPrerequisites = GUILayout.Toggle(m_ShowPrerequisites, "Prerequisites");

            if (GUILayout.Button("Select All"))
                m_ShowActions = m_ShowConditions = m_ShowEvaluators = m_ShowPropertyGetters = m_ShowPrerequisites = true;
            if (GUILayout.Button("Select None"))
                m_ShowActions = m_ShowConditions = m_ShowEvaluators = m_ShowPropertyGetters = m_ShowPrerequisites = false;
            
            GUILayout.FlexibleSpace();
            
            m_ErrorsOnly = GUILayout.Toggle(m_ErrorsOnly, "Errors Only");
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            {
                ElementsDebuggerDatabase.ElementsLog.Clear();
                m_ExcludedAssets.Clear();
            }
        }
    }
}