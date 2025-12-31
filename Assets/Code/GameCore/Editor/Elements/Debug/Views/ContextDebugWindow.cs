using JetBrains.Annotations;
using Kingmaker.Editor;
using UnityEditor;
using UnityEngine;

namespace Code.GameCore.Editor.Elements.Debug
{
    public class ContextDebugWindow : KingmakerWindowBase
    {
        private string m_Text;

        [CanBeNull]
        private string m_FieldName;

        private static GUIStyle TextAreaStyle;

        public static void Show(string name, string textToDisplay)
        {
            var window = GetWindow<ContextDebugWindow>(name);
            window.m_Text = textToDisplay;

            window.ShowAuxWindow();
            window.Focus();
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (TextAreaStyle == null)
            {
                TextAreaStyle = new("TextArea")
                {
                    richText = true
                };
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                GUILayout.TextArea(m_Text, TextAreaStyle, GUILayout.MinHeight(400), GUILayout.ExpandHeight(true));
                //GUILayout.TextArea(m_Formula, GUILayout.MinHeight(400), GUILayout.ExpandHeight(true));
            }
        }
    }
}