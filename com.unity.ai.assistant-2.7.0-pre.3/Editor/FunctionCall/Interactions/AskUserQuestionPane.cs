using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class AskUserQuestionPane : ManagedTemplate
    {
        const string k_PaneClass = "ask-user-tab-pane";
        const string k_PaneActiveClass = "ask-user-tab-pane-active";

        Label m_QuestionLabel;

        public VisualElement ContentSlot { get; private set; }

        public AskUserQuestionPane()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            AddToClassList(k_PaneClass);
            m_QuestionLabel = view.Q<Label>("questionLabel");
            ContentSlot = view.Q<VisualElement>("questionContent");
        }

        public void SetQuestion(string question)
        {
            m_QuestionLabel.text = question;
        }

        public void SetActive(bool active)
        {
            EnableInClassList(k_PaneActiveClass, active);
        }
    }
}
