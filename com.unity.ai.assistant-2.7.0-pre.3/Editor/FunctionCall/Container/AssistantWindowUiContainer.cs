using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class AssistantWindowUiContainer : IToolUiContainer, IDisposable
    {
        readonly AssistantUIContext m_Context;

        TodoProgressInteractionElement m_TodoInteraction;
        UserInteractionEntry m_TodoEntry;
        string m_TodoConversationId;
        Action<bool> m_TodoExpandedChangedHandler;
        bool m_Disposed;

        public AssistantWindowUiContainer(AssistantUIContext context)
        {
            m_Context = context;
            TodoUpdateEvent.OnTodoListUpdated += OnTodoListUpdated;
            m_Context.API.ConversationReload += OnConversationReload;
            m_Context.API.APIStateChanged += OnAPIStateChanged;

            // ConversationReload may have already fired before this container was constructed
            // (InitializeState runs before the container is created). Defer a session-state
            // restore to the next editor frame so the full UI is wired up, and so we don't
            // race with the incoming ConversationReload DispatchAndForget callback.
            // Use LastActiveConversationId from session state — ActiveConversationId is set
            // asynchronously inside LoadConversationAsync and is not available yet at this point.
            EditorApplication.delayCall += TryRestoreFromSessionState;
        }

        void TryRestoreFromSessionState()
        {
            if (m_Disposed || m_TodoEntry != null) return;

            var lastActiveId = AssistantUISessionState.instance.LastActiveConversationId;
            if (string.IsNullOrEmpty(lastActiveId)) return;

            var (items, planPath, expanded) = AssistantUISessionState.instance.GetTodoState(lastActiveId);
            if (items == null || items.Count == 0) return;

            m_TodoConversationId = lastActiveId;
            RestoreTodoPanel(items, planPath, expanded);
        }

        public void PushElement<TOutput>(ToolExecutionContext.CallInfo callInfo, IInteractionSource<TOutput> userInteraction)
        {
            if (userInteraction is IApprovalInteraction interaction)
            {
                var entry = EnqueueApproval(interaction.Action, interaction.Detail,
                    interaction.AllowLabel, interaction.DenyLabel,
                    interaction.Respond, interaction.ShowScope,
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

                return;
            }

            if (userInteraction is ExitPlanModeInteractionElement exitPlanMode)
            {
                m_Context.PendingInlineInteractions[callInfo.CallId] = exitPlanMode;
                AssistantEvents.Send(new EventInlineInteractionPushed(callInfo.CallId, exitPlanMode));
                return;
            }

            if (userInteraction is AskUserInteractionElement askUser)
            {
                var entry = new UserInteractionEntry
                {
                    Title = "Assistant wants to <b>" + askUser.Title + "</b>",
                    ContentView = askUser,
                    OnCancel = userInteraction.CancelInteraction
                };

                m_Context.InteractionQueue.EnqueueFront(entry);
                userInteraction.OnCompleted += _ => m_Context.InteractionQueue.Complete(entry);
                return;
            }

            if (userInteraction is VisualElement visualElement)
            {
                EnqueueCustomContent(visualElement, userInteraction);
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

            m_Context.InteractionQueue.EnqueueFront(entry);
            return entry;
        }

        void EnqueueCustomContent<TOutput>(VisualElement visualElement, IInteractionSource<TOutput> userInteraction)
        {
            var entry = new UserInteractionEntry
            {
                CustomContent = visualElement,
                OnCancel = userInteraction.CancelInteraction
            };

            m_Context.InteractionQueue.Enqueue(entry);
            userInteraction.OnCompleted += _ => m_Context.InteractionQueue.Complete(entry);
        }

        public void PopElement<TOutput>(ToolExecutionContext.CallInfo callInfo, IInteractionSource<TOutput> userInteraction)
        {
            // No-op: the queue auto-advances when an entry is completed or cancelled.
        }

        void CancelPendingInlineInteractions()
        {
            foreach (var interaction in m_Context.PendingInlineInteractions.Values)
            {
                if (interaction is IInteractionSource<string> source)
                    source.CancelInteraction();
            }
            m_Context.PendingInlineInteractions.Clear();
        }

        void OnAPIStateChanged()
        {
            // When the active conversation is cleared (new chat initiated), tear down the todo panel.
            // TearDownTodoPanel preserves the session state so switching back to the old conversation
            // can still restore its todos.
            if (!m_Context.Blackboard.ActiveConversationId.IsValid)
            {
                CancelPendingInlineInteractions();
                if (m_TodoEntry != null)
                    TearDownTodoPanel();
            }
        }

        void OnConversationReload(AssistantConversationId conversationId)
        {
            CancelPendingInlineInteractions();

            var id = conversationId.Value;
            if (string.IsNullOrEmpty(id) || m_TodoConversationId == id)
                return;

            // Switching to a different conversation — tear down current panel (preserve its stored state)
            TearDownTodoPanel();

            // Restore the new conversation's todos if any were previously stored
            var (items, planPath, expanded) = AssistantUISessionState.instance.GetTodoState(id);
            if (items != null && items.Count > 0)
            {
                m_TodoConversationId = id;
                RestoreTodoPanel(items, planPath, expanded);
            }
        }

        void OnTodoListUpdated(List<TodoItem> items, string planPath, string conversationId)
        {
            if (m_Disposed) return;

            // Preserve the current expanded state so it isn't overwritten when new todo data arrives.
            // For background conversations, read from session state — the live panel's IsExpanded only
            // reflects the active conversation and must not be used for a different conversation.
            var currentExpanded = (m_TodoInteraction != null && conversationId == m_TodoConversationId)
                ? m_TodoInteraction.IsExpanded
                : AssistantUISessionState.instance.GetTodoState(conversationId).expanded;
            AssistantUISessionState.instance.SetTodoState(conversationId, items, planPath, currentExpanded);

            // Only update the live panel when the update belongs to the currently active conversation.
            // Background tool calls for a different conversation must not overwrite the current UI.
            if (conversationId != m_Context.Blackboard.ActiveConversationId.Value)
                return;

            m_TodoConversationId = conversationId;
            RestoreTodoPanel(items, planPath, currentExpanded);
        }

        // Removes the panel from the queue without erasing the conversation's persisted state.
        void TearDownTodoPanel()
        {
            if (m_TodoEntry != null)
                m_Context.InteractionQueue.Complete(m_TodoEntry);

            if (m_TodoInteraction != null)
            {
                m_TodoInteraction.Completed -= ClearTodoPanel;
                if (m_TodoExpandedChangedHandler != null)
                    m_TodoInteraction.ExpandedChanged -= m_TodoExpandedChangedHandler;
            }

            m_TodoInteraction = null;
            m_TodoEntry = null;
            m_TodoConversationId = null;
            m_TodoExpandedChangedHandler = null;
        }

        // Removes the panel and erases the conversation's persisted state (plan completed).
        void ClearTodoPanel()
        {
            if (!string.IsNullOrEmpty(m_TodoConversationId))
                AssistantUISessionState.instance.ClearTodoState(m_TodoConversationId);

            TearDownTodoPanel();
        }

        void RestoreTodoPanel(List<TodoItem> items, string planPath, bool expanded)
        {
            if (m_TodoInteraction == null)
            {
                m_TodoInteraction = new TodoProgressInteractionElement(planPath, expanded);
                m_TodoInteraction.Initialize(m_Context);
                m_TodoInteraction.Completed += ClearTodoPanel;

                // Use m_TodoInteraction.PlanPath instead of capturing the planPath parameter so that
                // the handler always reflects the latest plan path (not the one from the first call).
                m_TodoExpandedChangedHandler = isExpanded =>
                    AssistantUISessionState.instance.SetTodoState(m_TodoConversationId, m_TodoInteraction.CurrentItems, m_TodoInteraction.PlanPath, isExpanded);
                m_TodoInteraction.ExpandedChanged += m_TodoExpandedChangedHandler;

                m_TodoEntry = new UserInteractionEntry
                {
                    ContentView = m_TodoInteraction,
                    HideCounter = true,
                    HideHeader = true,
                    Persistent = true
                };

                m_Context.InteractionQueue.Enqueue(m_TodoEntry);
            }

            m_TodoInteraction.UpdateTodos(items, planPath);
        }

        public void Dispose()
        {
            m_Disposed = true;
            TodoUpdateEvent.OnTodoListUpdated -= OnTodoListUpdated;
            m_Context.API.ConversationReload -= OnConversationReload;
            m_Context.API.APIStateChanged -= OnAPIStateChanged;
            m_Context.InteractionQueue.CancelAll();
        }

        ~AssistantWindowUiContainer()
        {
            Dispose();
        }
    }
}
