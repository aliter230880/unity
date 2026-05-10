using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Markup;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ExitPlanModeInteractionElement : InteractionContentView, IInteractionSource<string>
    {
        internal static event Action PlanApproved;

        public string Title => "review the implementation plan";
        public event Action<string> OnCompleted;
        public TaskCompletionSource<string> TaskCompletionSource { get; } = new();

        readonly string m_PlanPath;
        readonly string m_PlanContent;

        const string k_FeedbackPlaceholderClass = "exit-plan-feedback-placeholder";
        const string k_CopyBtnCopiedClass = "plan-review-copy-btn--copied";
        readonly string m_FeedbackPlaceholder;

        bool m_Completed;
        TextField m_FeedbackField;
        Button m_FeedbackButton;
        Button m_CopyIconButton;
        CancellationTokenSource m_CopyActiveTokenSource;
        VisualElement m_FeedbackSection;
        VisualElement m_ButtonRow;
        Label m_FeedbackConfirmedLabel;

        public ExitPlanModeInteractionElement(string planPath, string planContent)
        {
            m_PlanPath = planPath ?? string.Empty;
            m_PlanContent = planContent ?? string.Empty;
            m_FeedbackPlaceholder = string.IsNullOrEmpty(m_PlanPath)
                ? "Describe your feedback, or open the plan file to modify it directly."
                : $"Describe your feedback, or open {m_PlanPath} to modify it directly.";
        }

        protected override void InitializeView(TemplateContainer view)
        {
            var pathLabel = view.Q<Label>("pathLabel");
            pathLabel.text = m_PlanPath;

            var scrollView = view.Q<ScrollView>("planContentScroll");
            var markdownElements = new List<VisualElement>();
            MarkdownAPI.MarkupText(Context, m_PlanContent, null, markdownElements, null);
            foreach (var el in markdownElements)
                scrollView.Add(el);

            m_CopyIconButton = view.SetupButton("copyButton", _ => OnCopyClicked());

            m_FeedbackSection = view.Q<VisualElement>("feedbackSection");
            m_ButtonRow = view.Q<VisualElement>("buttonRow");
            m_FeedbackConfirmedLabel = view.Q<Label>("feedbackConfirmedLabel");

            m_FeedbackField = view.Q<TextField>("feedbackField");
            m_FeedbackField.SetValueWithoutNotify(m_FeedbackPlaceholder);
            m_FeedbackField.AddToClassList(k_FeedbackPlaceholderClass);
            m_FeedbackField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (m_FeedbackField.ClassListContains(k_FeedbackPlaceholderClass))
                {
                    m_FeedbackField.SetValueWithoutNotify("");
                    m_FeedbackField.RemoveFromClassList(k_FeedbackPlaceholderClass);
                }
            });
            m_FeedbackField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrWhiteSpace(m_FeedbackField.value))
                {
                    m_FeedbackField.SetValueWithoutNotify(m_FeedbackPlaceholder);
                    m_FeedbackField.AddToClassList(k_FeedbackPlaceholderClass);
                }
            });
            m_FeedbackField.RegisterValueChangedCallback(_ => UpdateFeedbackButtonState());

            view.SetupButton("cancelButton", _ => OnCancel());
            m_FeedbackButton = view.SetupButton("feedbackButton", _ => OnSendFeedback());
            view.SetupButton("approveButton", _ => OnApprove());

            m_FeedbackButton.SetEnabled(false);
            m_FeedbackConfirmedLabel.SetDisplay(false);
        }

        void UpdateFeedbackButtonState()
        {
            var hasContent = !string.IsNullOrWhiteSpace(m_FeedbackField?.value)
                             && !(m_FeedbackField?.ClassListContains(k_FeedbackPlaceholderClass) ?? false);
            m_FeedbackButton?.SetEnabled(hasContent);
        }

        void OnCopyClicked()
        {
            GUIUtility.systemCopyBuffer = m_PlanContent;
            m_CopyIconButton.AddToClassList(k_CopyBtnCopiedClass);
            m_CopyIconButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            TimerUtils.DelayedAction(ref m_CopyActiveTokenSource, () =>
            {
                m_CopyIconButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
                m_CopyIconButton.RemoveFromClassList(k_CopyBtnCopiedClass);
            });
        }

        void OnApprove()
        {
            if (m_Completed)
                return;
            m_Completed = true;

            ShowCompletionState("✓ Plan approved");

            var approvalMode = "agent";
            var modeDescription = "Agent mode";

            var result = JsonConvert.SerializeObject(new
            {
                approved = true,
                approvalMode,
                message = $"Plan approved. Switching to {modeDescription}.\n\n" +
                          $"The approved implementation plan is stored at: {m_PlanPath}\n" +
                          "Stop here — do not call any more tools or attempt implementation.\n" +
                          "Ask the user if they want to execute the entire plan. If yes, " +
                          "read and follow the plan strictly during implementation."
            });

            AIAssistantAnalytics.ReportUITriggerLocalPlanReviewApprovedEvent(Context.Blackboard.ActiveConversationId, m_PlanPath);

            PlanApproved?.Invoke();
            SetResult(result);
            InvokeCompleted();
        }

        void OnSendFeedback()
        {
            if (m_Completed)
                return;

            var feedback = m_FeedbackField?.value ?? "";
            if (string.IsNullOrWhiteSpace(feedback))
                return;

            m_Completed = true;

            AIAssistantAnalytics.ReportUITriggerLocalPlanReviewFeedbackSentEvent(Context.Blackboard.ActiveConversationId, m_PlanPath, feedback);

            ShowCompletionState("✓ Feedback recorded");

            var result = JsonConvert.SerializeObject(new
            {
                approved = false,
                feedback,
                message = $"Plan rejected. User feedback: \"{feedback}\"\n\n" +
                          $"The plan is stored at: {m_PlanPath}\n" +
                          "Revise the plan based on the feedback."
            });
            SetResult(result);
            InvokeCompleted();
        }

        void OnCancel()
        {
            if (m_Completed)
                return;
            m_Completed = true;

            ShowCompletionState("Plan cancelled");

            AIAssistantAnalytics.ReportUITriggerLocalPlanReviewCancelledEvent(Context.Blackboard.ActiveConversationId, m_PlanPath);

            var result = JsonConvert.SerializeObject(new
            {
                approved = false,
                message = "The user chose not to proceed with this plan. Treat this as a hard stop — do not revise, re-present, or re-attempt planning unless the user explicitly asks. Wait for the user to provide new direction."
            });
            SetResult(result);
            InvokeCompleted();
        }

        void ShowCompletionState(string statusText)
        {
            m_FeedbackSection?.SetDisplay(false);
            m_ButtonRow?.SetDisplay(false);
            if (m_FeedbackConfirmedLabel != null)
            {
                m_FeedbackConfirmedLabel.text = statusText;
                m_FeedbackConfirmedLabel.SetDisplay(true);
            }
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

        /// <summary>
        /// Reads a plan file from disk, trying both the raw path and a project-relative resolution.
        /// </summary>
        [ToolPermissionIgnore]
        internal static string ReadPlanFile(string planPath)
        {
            if (string.IsNullOrEmpty(planPath))
                return "(No plan path provided)";

            try
            {
                var fullPath = Path.GetFullPath(planPath);
                var fullDataPath = Path.GetFullPath(Application.dataPath);

                if (!fullPath.StartsWith(fullDataPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !fullPath.Equals(fullDataPath, StringComparison.OrdinalIgnoreCase))
                    return "(Plan file outside project's Assets directory)";

                if (File.Exists(fullPath))
                    return File.ReadAllText(fullPath);

                return $"(Plan file not found: {planPath})";
            }
            catch (Exception e)
            {
                return $"(Error reading plan file: {e.Message})";
            }
        }
    }
}
