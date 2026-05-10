using Unity.AI.Assistant.Editor.Utils.Event;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Events
{
    class EventExpandedViewRequested : IAssistantEvent
    {
        public string TitleText { get; }
        public VisualElement ExpandedElement { get; }
        public VisualElement HeaderActions { get; }
        public ScrollViewMode ScrollMode { get; }

        public EventExpandedViewRequested(string titleText, VisualElement expandedElement, VisualElement headerActions = null, ScrollViewMode scrollMode = ScrollViewMode.VerticalAndHorizontal)
        {
            TitleText = titleText;
            ExpandedElement = expandedElement;
            HeaderActions = headerActions;
            ScrollMode = scrollMode;
        }
    }
}
