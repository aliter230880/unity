using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [Serializable]
    class AskUserQuestion
    {
        [JsonProperty("question")] public string Question;
        [JsonProperty("header")] public string Header;
        [JsonProperty("type")] public string Type;
        [JsonProperty("options")] public List<AskUserOption> Options;
        [JsonProperty("multiSelect")] public bool MultiSelect;
        [JsonProperty("placeholder")] public string Placeholder;
    }

    [Serializable]
    class AskUserOption
    {
        [JsonProperty("label")] public string Label;
        [JsonProperty("description")] public string Description;
    }

    class AskUserInteractionElement : InteractionContentView, IInteractionSource<string>, INavigableInteractionView
    {
        const string k_OtherPlaceholder = "Other...";
        const string k_OtherPlaceholderClass = "ask-user-other-field-placeholder";

        readonly List<AskUserQuestion> m_Questions;
        readonly List<AskUserQuestionPane> m_Panes = new();

        int m_CurrentIndex;
        bool m_Completed;
        readonly Dictionary<int, string> m_Answers = new();
        Button m_SubmitButton;
        Button m_ContinueButton;

        public string Title => m_Questions.Count == 1 ? "clarify a question" : $"clarify {m_Questions.Count} questions";
        public TaskCompletionSource<string> TaskCompletionSource { get; } = new();

        // INavigableInteractionView
        public int NavigationIndex => m_CurrentIndex;
        public int NavigationCount => m_Questions.Count;

        public event Action<string> OnCompleted;
        public event Action NavigationChanged;

        public AskUserInteractionElement(List<AskUserQuestion> questions)
        {
            m_Questions = questions ?? new List<AskUserQuestion>();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            var tabContentContainer = view.Q<VisualElement>("askUserTabContent");
            view.SetupButton("askUserCancelButton", _ => OnCancel());
            m_ContinueButton = view.SetupButton("askUserContinueButton", _ => NavigateNext());
            m_SubmitButton = view.SetupButton("askUserSubmitButton", _ => OnSubmit());
            m_SubmitButton.SetEnabled(false);

            for (var i = 0; i < m_Questions.Count; i++)
            {
                var pane = new AskUserQuestionPane();
                pane.Initialize(Context, autoShowControl: false);
                pane.SetActive(i == 0);
                pane.SetQuestion(m_Questions[i].Question);

                BuildQuestionContent(pane.ContentSlot, i, m_Questions[i]);
                tabContentContainer.Add(pane);
                m_Panes.Add(pane);
            }

            m_CurrentIndex = 0;
            RefreshContinueButton();
        }

        public void NavigatePrev() => NavigateTo(m_CurrentIndex - 1);
        public void NavigateNext() => NavigateTo(m_CurrentIndex + 1);

        void NavigateTo(int index)
        {
            if (index < 0 || index >= m_Questions.Count)
                return;

            m_Panes[m_CurrentIndex].SetActive(false);
            m_CurrentIndex = index;
            m_Panes[m_CurrentIndex].SetActive(true);
            RefreshContinueButton();
            NavigationChanged?.Invoke();
        }

        void RefreshContinueButton()
        {
            var q = m_Questions[m_CurrentIndex];
            var type = string.IsNullOrEmpty(q.Type) ? "choice" : q.Type.ToLowerInvariant();
            var show = (q.MultiSelect || type == "text")
                       && m_CurrentIndex < m_Questions.Count - 1;
            m_ContinueButton?.SetDisplay(show);
        }

        void SetAnswer(int questionIndex, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                m_Answers.Remove(questionIndex);
            else
                m_Answers[questionIndex] = value;

            UpdateSubmitState();
        }

        void UpdateSubmitState()
        {
            if (m_SubmitButton == null) return;

            for (var i = 0; i < m_Questions.Count; i++)
            {
                if (!m_Answers.TryGetValue(i, out var answer) || string.IsNullOrWhiteSpace(answer))
                {
                    m_SubmitButton.SetEnabled(false);
                    return;
                }
            }

            m_SubmitButton.SetEnabled(true);
        }

        void BuildQuestionContent(VisualElement contentSlot, int questionIndex, AskUserQuestion question)
        {
            var type = string.IsNullOrEmpty(question.Type) ? "choice" : question.Type.ToLowerInvariant();

            switch (type)
            {
                case "text":
                    BuildTextQuestion(contentSlot, questionIndex, question.Placeholder);
                    break;
                case "yesno":
                    BuildYesNoQuestion(contentSlot, questionIndex);
                    break;
                default:
                    if (question.MultiSelect)
                        BuildMultiSelectQuestion(contentSlot, questionIndex, question.Options ?? new List<AskUserOption>(), question.Placeholder);
                    else
                        BuildSingleSelectQuestion(contentSlot, questionIndex, question.Options ?? new List<AskUserOption>(), question.Placeholder);
                    break;
            }
        }

        void BuildMultiSelectQuestion(VisualElement contentSlot, int questionIndex, List<AskUserOption> options, string placeholder = null)
        {
            var isLastQuestion = questionIndex == m_Questions.Count - 1;
            var selected = new HashSet<string>();
            var otherPlaceholder = placeholder ?? k_OtherPlaceholder;

            var choicesContainer = new VisualElement();
            foreach (var option in options)
            {
                var toggle = new Toggle();
                toggle.AddToClassList("ask-user-radio-indicator");
                if (!string.IsNullOrEmpty(option.Description))
                    toggle.tooltip = option.Description;

                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) selected.Add(option.Label);
                    else selected.Remove(option.Label);
                    SetAnswer(questionIndex, string.Join(", ", selected));
                });
                choicesContainer.Add(BuildOptionRow(toggle, option.Label, option.Description));
            }

            // "Other" row
            var otherToggle = new Toggle();
            otherToggle.AddToClassList("ask-user-radio-indicator");
            var otherRoot = new AskUserOptionRow();
            otherRoot.Initialize(Context);
            var other = new AskUserOtherRow(otherRoot, otherToggle, otherPlaceholder);

            other.Field.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            other.Field.RegisterCallback<FocusInEvent>(_ =>
            {
                if (other.IsPlaceholder) other.ClearPlaceholder();
                otherToggle.SetValueWithoutNotify(true);
                other.SaveButton.SetDisplay(true);
                other.ResetSaveButton();
            });
            other.SaveButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            otherToggle.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    selected.RemoveWhere(s => s.StartsWith("Other: "));
                    other.SaveButton.SetDisplay(false);
                    SetAnswer(questionIndex, string.Join(", ", selected));
                }
                else
                {
                    other.SaveButton.SetDisplay(true);
                }
            });

            other.SaveButton.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (other.IsPlaceholder || string.IsNullOrWhiteSpace(other.Field.value)) return;
                selected.RemoveWhere(s => s.StartsWith("Other: "));
                selected.Add("Other: " + other.Field.value);
                SetAnswer(questionIndex, string.Join(", ", selected));
                other.MarkSaved();
                if (!isLastQuestion) NavigateNext();
            });

            choicesContainer.Add(other.Root);
            contentSlot.Add(choicesContainer);
        }

        void BuildSingleSelectQuestion(VisualElement contentSlot, int questionIndex, List<AskUserOption> options, string placeholder = null)
        {
            var isLastQuestion = questionIndex == m_Questions.Count - 1;
            var indicators = new List<RadioButton>();
            var otherPlaceholder = placeholder ?? k_OtherPlaceholder;

            void SelectIndicator(int idx)
            {
                for (var k = 0; k < indicators.Count; k++)
                    indicators[k].SetValueWithoutNotify(k == idx);
            }

            var optionsContainer = new VisualElement();
            for (var i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                var idx = i;

                var indicator = new RadioButton();
                indicator.AddToClassList("ask-user-radio-indicator");
                indicator.pickingMode = PickingMode.Position;
                indicators.Add(indicator);

                AddSelectableRow(optionsContainer, indicator, opt.Label, opt.Description, () =>
                {
                    SelectIndicator(idx);
                    SetAnswer(questionIndex, opt.Label);
                    if (!isLastQuestion) NavigateNext();
                });
            }

            // "Other" row
            var otherIndicator = new RadioButton();
            otherIndicator.AddToClassList("ask-user-radio-indicator");
            otherIndicator.pickingMode = PickingMode.Position;
            indicators.Add(otherIndicator);
            var otherRoot = new AskUserOptionRow();
            otherRoot.Initialize(Context);
            var other = new AskUserOtherRow(otherRoot, otherIndicator, otherPlaceholder);

            other.Field.RegisterCallback<FocusInEvent>(_ =>
            {
                if (other.IsPlaceholder) other.ClearPlaceholder();
                SelectIndicator(options.Count);
                m_Answers.Remove(questionIndex);
                other.SaveButton.SetDisplay(true);
                other.ResetSaveButton();
                UpdateSubmitState();
            });

            WireSelectableRow(other.Root, otherIndicator, () =>
            {
                SelectIndicator(options.Count);
                m_Answers.Remove(questionIndex);
                other.SaveButton.SetDisplay(true);
                UpdateSubmitState();
                other.Field.Focus();
            });

            other.SaveButton.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (other.IsPlaceholder || string.IsNullOrWhiteSpace(other.Field.value)) return;
                SetAnswer(questionIndex, other.Field.value);
                other.MarkSaved();
                if (!isLastQuestion) NavigateNext();
            });

            optionsContainer.Add(other.Root);
            contentSlot.Add(optionsContainer);
        }

        void BuildTextQuestion(VisualElement contentSlot, int questionIndex, string placeholder)
        {
            var textField = new TextField { multiline = true };
            textField.AddToClassList("ask-user-text-field");

            if (!string.IsNullOrEmpty(placeholder))
            {
                textField.SetValueWithoutNotify(placeholder);
                textField.AddToClassList(k_OtherPlaceholderClass);
                textField.RegisterCallback<FocusInEvent>(_ =>
                {
                    if (textField.ClassListContains(k_OtherPlaceholderClass))
                    {
                        textField.value = "";
                        textField.RemoveFromClassList(k_OtherPlaceholderClass);
                    }
                });
            }

            textField.RegisterValueChangedCallback(evt => SetAnswer(questionIndex, evt.newValue));
            contentSlot.Add(textField);
        }

        void BuildYesNoQuestion(VisualElement contentSlot, int questionIndex)
        {
            var isLastQuestion = questionIndex == m_Questions.Count - 1;
            var optionsContainer = new VisualElement();
            var indicators = new List<RadioButton>();

            void SelectIndicator(int idx)
            {
                for (var k = 0; k < indicators.Count; k++)
                    indicators[k].SetValueWithoutNotify(k == idx);
            }

            foreach (var label in new[] { "Yes", "No" })
            {
                var idx = indicators.Count;
                var indicator = new RadioButton();
                indicator.AddToClassList("ask-user-radio-indicator");
                indicator.pickingMode = PickingMode.Position;
                indicators.Add(indicator);

                AddSelectableRow(optionsContainer, indicator, label, null, () =>
                {
                    SelectIndicator(idx);
                    SetAnswer(questionIndex, label);
                    if (!isLastQuestion) NavigateNext();
                });
            }

            contentSlot.Add(optionsContainer);
        }

        AskUserOptionRow BuildOptionRow(VisualElement indicator, string label, string description = null)
        {
            var row = new AskUserOptionRow();
            row.Initialize(Context);
            row.SetData(indicator, label, description);
            return row;
        }

        void AddSelectableRow(VisualElement container, RadioButton indicator, string label, string description, Action onSelect)
        {
            var row = BuildOptionRow(indicator, label, description);
            WireSelectableRow(row, indicator, onSelect);
            container.Add(row);
        }

        void WireSelectableRow(VisualElement row, RadioButton indicator, Action onSelect)
        {
            indicator.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            row.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                indicator.ReleasePointer(evt.pointerId);
                onSelect();
            }, TrickleDown.TrickleDown);
        }

        void OnSubmit()
        {
            if (m_Completed) return;
            m_Completed = true;
            AIAssistantAnalytics.ReportUITriggerLocalClarifyingQuestionSubmittedEvent(Context.Blackboard.ActiveConversationId, m_Questions.Count);
            var result = JsonConvert.SerializeObject(new { answers = m_Answers });
            SetResult(result);
            InvokeCompleted();
        }

        void OnCancel()
        {
            if (m_Completed) return;
            m_Completed = true;
            AIAssistantAnalytics.ReportUITriggerLocalClarifyingQuestionCancelledEvent(Context.Blackboard.ActiveConversationId, m_Questions.Count);
            SetResult("User dismissed the ask_user dialog without answering. Do not ask these questions again. Proceed with your best judgment or wait for the user to provide new direction.");
            InvokeCompleted();
        }

        void SetResult(string result)
        {
            TaskCompletionSource.TrySetResult(result);
            OnCompleted?.Invoke(result);
        }

        public void CancelInteraction()
        {
            TaskCompletionSource.TrySetCanceled();
        }
    }

}
