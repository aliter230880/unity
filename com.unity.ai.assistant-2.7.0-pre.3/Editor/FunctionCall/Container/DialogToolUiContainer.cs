using System;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class DialogToolUiContainer : IToolUiContainer
    {
        readonly UserInteractionQueue m_Queue = new();

        DialogWindow m_DialogWindow;
        UserInteractionBar m_InteractionBar;

        public void PushElement<TOutput>(ToolExecutionContext.CallInfo callInfo, IInteractionSource<TOutput> userInteraction)
        {
            if (userInteraction == null)
            {
                return;
            }

            if (userInteraction is IApprovalInteraction approval)
            {
                var entry = EnqueueApproval(approval.Action, approval.Detail,
                    approval.AllowLabel, approval.DenyLabel,
                    approval.Respond, approval.ShowScope,
                    userInteraction.CancelInteraction);

                if (userInteraction is PermissionInteraction pi && pi.TryAutoResolve != null)
                {
                    entry.TryAutoResolve = () =>
                    {
                        var answer = pi.TryAutoResolve();
                        if (!answer.HasValue) return false;
                        pi.Complete(answer.Value);
                        return true;
                    };
                }

                ShowInteractionBar(userInteraction);
                return;
            }

            if (userInteraction is VisualElement visualElement)
            {
                ShowDialog(visualElement, userInteraction);
                return;
            }

            // Bare IInteractionSource with no IApprovalInteraction or VisualElement implementation:
            // fall back to a default Allow/Deny approval so the interaction isn't silently dropped.
            EnqueueApproval(null, null, null, null, answer =>
            {
                if (answer == PermissionUserAnswer.DenyOnce || answer == PermissionUserAnswer.DenyAlways)
                    userInteraction.CancelInteraction();
                else
                    userInteraction.TaskCompletionSource.TrySetResult(default);
            }, false, userInteraction.CancelInteraction);
            ShowInteractionBar(userInteraction);
        }

        UserInteractionEntry EnqueueApproval(string action, string detail,
            string allowLabel, string denyLabel,
            Action<PermissionUserAnswer> onRespond, bool showScope,
            Action onCancel)
        {
            var content = new ApprovalInteractionContent();
            content.SetApprovalData(allowLabel, denyLabel, onRespond, showScope);

            var entry = new UserInteractionEntry
            {
                Title = action != null ? "Assistant wants to <b>" + action + "</b>" : null,
                Detail = detail,
                ContentView = content,
                OnCancel = onCancel
            };

            m_Queue.Enqueue(entry);
            return entry;
        }

        void ShowInteractionBar<TOutput>(IInteractionSource<TOutput> userInteraction)
        {
            if (m_InteractionBar == null)
            {
                m_InteractionBar = new UserInteractionBar(m_Queue);
                m_InteractionBar.Initialize(null);
            }

            ShowDialog(m_InteractionBar, userInteraction);
        }

        void ShowDialog<TOutput>(VisualElement content, IInteractionSource<TOutput> userInteraction)
        {
            if (m_DialogWindow == null)
            {
                m_DialogWindow = ScriptableObject.CreateInstance<DialogWindow>();
                m_DialogWindow.titleContent = new GUIContent("Assistant Dialog");
            }

            m_DialogWindow.SetContent(content);

            // Center the dialog relative to the entire Unity Editor application window
            var editorMainWindowRect = EditorGUIUtility.GetMainWindowPosition();
            var dialogSize = new Vector2(500, 250);

            var centeredPosition = new Rect(
                editorMainWindowRect.x + (editorMainWindowRect.width - dialogSize.x) * 0.5f,
                editorMainWindowRect.y + (editorMainWindowRect.height - dialogSize.y) * 0.5f,
                dialogSize.x,
                dialogSize.y
            );

            m_DialogWindow.position = centeredPosition;

            userInteraction.OnCompleted += Close;
            m_DialogWindow.ShowModalUtility();

            userInteraction.CancelInteraction();
        }

        public void PopElement<TOutput>(ToolExecutionContext.CallInfo callInfo, IInteractionSource<TOutput> userInteraction)
        {
            if (m_DialogWindow != null)
            {
                m_DialogWindow.Close();
                m_DialogWindow = null;
            }
        }

        void Close<TOutput>(TOutput output)
        {
            m_DialogWindow?.Close();
        }
    }
}
