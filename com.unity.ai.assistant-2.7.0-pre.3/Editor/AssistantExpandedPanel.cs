using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AssistantExpandedPanel : ManagedTemplate
    {
        ScrollView m_Content;

        public AssistantExpandedPanel() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Content = view.Q<ScrollView>("expandedPanelContent");
            m_Content.mode = ScrollViewMode.VerticalAndHorizontal;
            m_Content.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_Content.horizontalScrollerVisibility = ScrollerVisibility.Auto;
        }

        internal bool IsVisible => resolvedStyle.display != DisplayStyle.None;

        internal void ShowPanel(VisualElement element, ScrollViewMode scrollMode = ScrollViewMode.VerticalAndHorizontal)
        {
            m_Content.mode = scrollMode;
            m_Content.Clear();
            m_Content.Add(element);
            this.SetDisplay(true);
        }

        internal void HidePanel()
        {
            this.SetDisplay(false);
            m_Content.Clear();
        }
    }
}
