using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.Settings;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;

using Unity.Relay.Editor.Acp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using TextField = UnityEngine.UIElements.TextField;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantTextField : ManagedTemplate
    {
        const string k_ChatFocusClass = "mui-mft-input-focused";
        const string k_ChatHoverClass = "mui-mft-input-hovered";
        const string k_ChatActionEnabledClass = "mui-submit-enabled";
        const string k_HasInteractionClass = "mui-mtf-has-interaction";

        const string k_SubmitImage = "arrow-up";
        const string k_StopImage = "stop-square";

        const string k_ActionButtonToolTipSend = "Send prompt";
        const string k_ActionButtonToolTipStop = "Stop response";
        const string k_ActionButtonToolTipNoPrompt = "No prompt entered";
        
        static readonly string k_ImageAttachmentSizeExceeded =
            $"Total image size exceeds {AssistantConstants.MaxTotalAttachmentSizeMB}MB.\n" +
             "Images may use more memory than their file size on disk. Try removing or resizing an image.";

        const string k_PlaceholderAsk = "Ask about Unity";
        const string k_PlaceholderAgent = "Build with Unity";
        const string k_PlaceholderPlan = "Plan with Unity";
        const string k_PlaceholderInitializing = "Initializing session...";
        const string k_PlaceholderError = "Session failed to initialize";

        // Debounce window for committing prompt edits into the Unity Undo stack.
        // Per-keystroke undo entries feel broken to users; 500 ms groups natural typing bursts
        // into a single undo entry (matches the TestBindingWindow reference pattern).
        const long k_PromptUndoDebounceMs = 500;

        VisualElement m_Root;
        UserInteractionBar m_InteractionBar;
        BaseEventSubscriptionTicket m_InteractionQueueSubscription;

        Button m_ActionButton;
        AssistantImage m_SubmitButtonImage;

        TextField m_ChatInput;
        Label m_ChatCharCount;
        Label m_Placeholder;
        VisualElement m_PlaceholderContent;
        VisualElement m_ActionRow;

        VisualElement m_ContextLimitWarning;
        VisualElement m_ImageSizeLimitWarning;
        ContextUsageView m_ContextUsageView;

        Button m_AddContextButton;
        Button m_SettingsButton;
        SettingsPopup m_SettingsPopup;
        PopupTracker m_SettingPopupTracker;

        VisualElement m_PopupRoot;

        string m_SelectedProviderId = "unity";
        readonly List<(string id, string displayName)> m_CommandItems = new();
        readonly List<(string id, string displayName)> m_ModelItems = new();
        string m_SelectedModelId;

        bool m_TextHasFocus;
        bool m_ShowPlaceholder;
        bool m_HighlightFocus;
        bool m_EditContextEnabled;
        bool m_ImageSizeExceeded;

        IVisualElementScheduledItem m_PromptUndoDebounceTask;

        int CursorIndex
        {
            get => m_ChatInput.cursorIndex;
            set => m_ChatInput.cursorIndex = value;
        }

        internal VisualElement AttachmentRow { get; private set; }
        internal ScrollView AttachmentStrip { get; private set; }
        internal Button ClearAllButton { get; private set; }

        public AssistantTextField()
            : base(AssistantUIConstants.UIModulePath)
        {
            m_EditContextEnabled = false;

            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssistantEditorPreferences.AiGatewayDisclaimerAcceptedChanged += RefreshProviderItems;
            AssistantEditorPreferences.ProviderEnabledChanged += RefreshProviderItems;
            AcpProvidersRegistry.OnProvidersChanged += RefreshProviderItems;
            GatewayPreferenceService.Instance.Preferences.OnChange += RefreshProviderItems;
            Context.ConversationLoader.ConversationsLoaded += RefreshProviderItems;
            m_InteractionQueueSubscription = AssistantEvents.Subscribe<EventInteractionQueueChanged>(OnInteractionQueueChanged);
            Context.API.ConversationChanged += OnConversationChanged;
            Context.API.ConversationReload += OnConversationChanged;
            Context.Blackboard.ActiveConversationChanged += OnActiveConversationChanged;
            ExitPlanModeInteractionElement.PlanApproved += OnPlanApproved;
            Undo.undoRedoPerformed += OnPromptUndoRedo;
            UpdateInteractionBarStyle();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssistantEditorPreferences.AiGatewayDisclaimerAcceptedChanged -= RefreshProviderItems;
            AssistantEditorPreferences.ProviderEnabledChanged -= RefreshProviderItems;
            AcpProvidersRegistry.OnProvidersChanged -= RefreshProviderItems;
            GatewayPreferenceService.Instance.Preferences.OnChange -= RefreshProviderItems;
            Context.ConversationLoader.ConversationsLoaded -= RefreshProviderItems;
            ExitPlanModeInteractionElement.PlanApproved -= OnPlanApproved;
            AssistantEvents.Unsubscribe(ref m_InteractionQueueSubscription);
            Context.Blackboard.ActiveConversationChanged -= OnActiveConversationChanged;
            Context.API.ConversationChanged -= OnConversationChanged;
            Context.API.ConversationReload -= OnConversationChanged;
            Undo.undoRedoPerformed -= OnPromptUndoRedo;

            // Flush any pending debounced commit so in-flight edits still land in the undo stack.
            m_PromptUndoDebounceTask?.Pause();
            m_PromptUndoDebounceTask = null;
            CommitPromptToUndo();
        }

        void OnInteractionQueueChanged(EventInteractionQueueChanged evt)
        {
            UpdateInteractionBarStyle();
        }

        void OnActiveConversationChanged(AssistantConversationId previousConversationId, AssistantConversationId currentConversationId)
        {
            OnConversationChanged(currentConversationId);
        }

        void OnConversationChanged(AssistantConversationId conversationId)
        {
            if (conversationId != Context.Blackboard.ActiveConversationId)
                return;

            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation != null && conversation.ContextUsageMaxTokens > 0)
            {
                m_ContextUsageView.SetUsage(conversation.ContextUsageUsedTokens, conversation.ContextUsageMaxTokens);
            }
            else
            {
                m_ContextUsageView.ResetUsage();
            }
        }

        void UpdateInteractionBarStyle()
        {
            var hasInteraction = Context?.InteractionQueue?.HasPending ?? false;
            m_Root?.EnableInClassList(k_HasInteractionClass, hasInteraction);
        }

        public bool ShowPlaceholder
        {
            get => m_ShowPlaceholder;
            set
            {
                if (m_ShowPlaceholder == value)
                {
                    return;
                }

                m_ShowPlaceholder = value;
                RefreshUI();
            }
        }

        public bool HighlightFocus
        {
            get => m_HighlightFocus;
            set
            {
                if (m_HighlightFocus == value)
                {
                    return;
                }

                m_HighlightFocus = value;
                RefreshUI();
            }
        }

        internal string Text => m_ChatInput?.value ?? string.Empty;

        public event Action<string> SubmitRequest;
        public event Action CancelRequest;
        public event Action<string, string> OnProviderChanged;
        public event Action<string> OnCommandSelected;
        public event Action<string> OnModelSelected;
        public event Action MentionTriggered;

        public Button ContextButton => m_AddContextButton;
        public string SelectedProviderId => m_SelectedProviderId;

        /// <summary>
        /// Programmatically set the selected provider.
        /// </summary>
        /// <param name="providerId">The provider ID to select.</param>
        /// <param name="triggerEvent">Whether to fire the OnProviderChanged event.</param>
        public void SetProvider(string providerId, bool triggerEvent = true)
        {
            if (m_SelectedProviderId == providerId)
                return;

            var oldProvider = m_SelectedProviderId;
            m_SelectedProviderId = providerId;
            m_ProviderMenu?.SetSelectedId(providerId);
            UpdateProviderLabel();
            UpdateProviderTooltip();
            // Ensure UI state stays in sync even when we skip provider-change events (e.g., domain reload restore).
            UpdateCommandSelectorVisibility();
            UpdateCommandSelectorEnabled();

            if (triggerEvent)
            {
                OnProviderChangedHandler(oldProvider, providerId);
            }
        }

        public void SetHost(VisualElement popupRoot)
        {
            m_PopupRoot = popupRoot;
            m_EditContextEnabled = true;

            InitializeSettingsPopup();
            InitializeDropdownMenus(m_PopupRoot);

            m_AddContextButton.SetDisplay(m_EditContextEnabled);
        }

        public void ClearText()
        {
            m_ChatInput.value = "";
        }

        public void SetText(string text)
        {
            m_ChatInput.value = text;
            m_ChatInput.Focus();
        }

        public void InsertTextAtCursor(string text)
        {
            m_ChatInput.value = m_ChatInput.value.Insert(CursorIndex, text);
            CursorIndex += text.Length;
            m_ChatInput.selectIndex = CursorIndex;
        }

        public void RemoveCharBeforeCursor()
        {
            if (CursorIndex <= 0) return;

            m_ChatInput.value = m_ChatInput.value.Remove(CursorIndex - 1, 1);
            CursorIndex--;
            m_ChatInput.selectIndex = CursorIndex;
        }

        public void RemoveCharBeforeCursorIfMatch(char c)
        {
            if (CursorIndex <= 0 || m_ChatInput.value[CursorIndex - 1] != c) return;

            RemoveCharBeforeCursor();
        }

        public void FocusInput()
        {
            m_ChatInput.Focus();
        }

        public void Enable()
        {
            m_ChatInput.SetEnabled(true);
        }

        public void Disable(string reason = "")
        {
            m_Placeholder.text = reason;
            m_ChatInput.SetEnabled(false);
        }

        public void ToggleContextLimitWarning(bool enabled)
        {
            m_ContextLimitWarning.SetDisplay(enabled);
        }

        public void ToggleImageSizeLimitWarning(bool enabled)
        {
            m_ImageSizeExceeded = enabled;
            RefreshUI();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("museTextFieldRoot");
            AttachmentRow = view.Q<VisualElement>("attachmentRow");
            AttachmentStrip = view.Q<ScrollView>("attachmentStrip");
            ClearAllButton = view.Q<Button>("clearAllButton");

            m_InteractionBar = new UserInteractionBar();
            m_InteractionBar.Initialize(Context, autoShowControl: false);
            view.Insert(view.IndexOf(m_Root), m_InteractionBar);

            m_AddContextButton = view.Q<Button>("addContextButton");
            m_AddContextButton.SetDisplay(m_EditContextEnabled);

            m_SettingsButton = view.Q<Button>("settingsButton");
            m_SettingsButton.clicked += OnSettingsButtonClicked;

            InitializeDropdowns(view);

            // Subscribe to mode changes for settings popup auto-run update
            m_ModeProvider.ModeChanged += OnModeChanged;

            UpdateSettingsPopupAutoRun();

            m_ActionButton = view.Q<Button>("actionButton");
            m_ActionButton.RegisterCallback<PointerUpEvent>(_ => OnSubmit());

            m_SubmitButtonImage = view.SetupImage("actionButtonImage", k_SubmitImage);

            m_PlaceholderContent = view.Q<VisualElement>("placeholderContent");
            m_Placeholder = view.Q<Label>("placeholderText");

            m_ChatInput = view.Q<TextField>("input");
            m_ChatInput.maxLength = AssistantMessageSizeConstraints.PromptLimit;
            m_ChatInput.multiline = true;
            m_ChatInput.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_ChatInput.selectAllOnFocus = false;
            m_ChatInput.selectAllOnMouseUp = false;
            m_ChatInput.RegisterCallback<ClickEvent>(_ => m_ChatInput.Focus());
            m_ChatInput.RegisterCallback<KeyUpEvent>(OnChatKeyUpEvent);
            // TrickleDown.TrickleDown is a workaround for registering KeyDownEvent type with Unity 6
            m_ChatInput.RegisterCallback<KeyDownEvent>(OnChatKeyDownEvent, TrickleDown.TrickleDown);
            m_ChatInput.RegisterValueChangedCallback(OnTextFieldValueChanged);
            m_PlaceholderContent.RegisterCallback<ClickEvent>(_ => m_ChatInput.Focus());
            m_ChatInput.RegisterCallback<FocusInEvent>(_ => SetTextFocused(true));
            m_ChatInput.RegisterCallback<FocusOutEvent>(_ => SetTextFocused(false));
            m_ChatInput.RegisterCallback<PointerLeaveEvent>(_ => m_ActionButton.RemoveFromClassList(k_ChatHoverClass));

            m_ActionRow = view.Q<VisualElement>("museTextFieldActionRow");

            m_Root.RegisterCallback<ClickEvent>(e =>
            {
                // Focus the input when clicking anywhere in the root, except on focusable elements
                // (focusable elements are interactive controls like buttons, textfields, dropdowns, etc.)
                if (e.target is VisualElement target && !target.focusable)
                {
                    m_ChatInput.Focus();
                }
            });

            m_ChatInput.Q<TextElement>().enableRichText = false;

            m_PlaceholderContent = view.Q<VisualElement>("placeholderContent");
            m_Placeholder = view.Q<Label>("placeholderText");

            UpdatePlaceholderText();

            m_ChatCharCount = view.Q<Label>("characterCount");

            m_ContextLimitWarning = view.Q<VisualElement>("contextLimitWarning");
            m_ImageSizeLimitWarning = view.Q<VisualElement>("imageSizeLimitWarning");

            var contextUsageContainer = view.Q<VisualElement>("contextUsageContainer");
            m_ContextUsageView = new ContextUsageView();
            m_ContextUsageView.Initialize(Context);
            contextUsageContainer.Add(m_ContextUsageView);

            Context.API.APIStateChanged += OnAPIStateChanged;

            ShowPlaceholder = true;
            HighlightFocus = true;

            // SetValueWithoutNotify avoids firing the change callback during construction,
            // which would otherwise schedule a spurious debounced undo commit on every window open.
            m_ChatInput.SetValueWithoutNotify(AssistantUISessionState.instance.Prompt ?? "");
            UpdatePlaceholderText();
            RefreshUI();

            // Apply granular status tracking to disableable elements, keeping ProviderSelector always enabled
            view.Q<VisualElement>(className: "mui-mtf-warning-area")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);
            view.Q<VisualElement>(className: "mui-mtf-input-root")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);
            view.Q<VisualElement>(className: "mui-left-actions")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);
            view.Q<Button>("actionButton")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);

            // Subscribe to provider ready state changes for placeholder updates
            ProviderStateObserver.OnReadyStateChanged += OnProviderReadyStateChanged;
        }

        void OnAPIStateChanged()
        {
            RefreshUI();
        }

        void OnTextFieldValueChanged(ChangeEvent<string> evt)
        {
            OnChatValueChanged();

            if (CursorIndex <= 0 || CursorIndex > evt.newValue.Length || evt.newValue[CursorIndex - 1] != '@') return;
            if (CursorIndex == 1 || char.IsWhiteSpace(evt.newValue[CursorIndex - 2])) MentionTriggered?.Invoke();
        }

        void OnSubmit()
        {
            if (Context.Blackboard.IsAPIWorking)
            {
                CancelRequest?.Invoke();
                return;
            }

            if (!Context.Blackboard.IsAPIReadyForPrompt)
            {
                return;
            }

            // if button is disabled do not submit
            if (!m_ActionButton.enabledSelf)
            {
                return;
            }

            // Flush any pending debounced commit so a submit that races the 500 ms window
            // still sees the latest text and produces a matching undo entry.
            m_PromptUndoDebounceTask?.Pause();
            m_PromptUndoDebounceTask = null;
            CommitPromptToUndo();

            SubmitRequest?.Invoke(AssistantUISessionState.instance.Prompt);
        }

        void SetTextFocused(bool state)
        {
            m_TextHasFocus = state;
            RefreshUI();
        }

        internal void RefreshUI()
        {
            RefreshChatCharCount();

            m_ImageSizeLimitWarning.tooltip = k_ImageAttachmentSizeExceeded;
            m_ImageSizeLimitWarning.SetDisplay(m_ImageSizeExceeded);

            var actionButtonEnabled = Context.Blackboard.IsAPIWorking ||
                                !string.IsNullOrEmpty(Text) &&
                                Context.Blackboard.IsAPIReadyForPrompt &&
                                !m_ImageSizeExceeded;

            m_ActionButton.EnableInClassList(k_ChatActionEnabledClass, actionButtonEnabled);

            var showPlaceholder = ShowPlaceholder && !m_TextHasFocus && string.IsNullOrEmpty(Text);
            m_PlaceholderContent.SetDisplay(showPlaceholder);

            m_Root.EnableInClassList(k_ChatFocusClass, m_TextHasFocus && m_HighlightFocus);

            m_SubmitButtonImage.SetIconClassName(Context.Blackboard.IsAPIWorking ? k_StopImage : k_SubmitImage);

            if (actionButtonEnabled)
            {
                m_ActionButton.tooltip =
                    Context.Blackboard.IsAPIWorking ? k_ActionButtonToolTipStop : k_ActionButtonToolTipSend;
            }
            else
            {
                m_ActionButton.tooltip =
                    m_ImageSizeExceeded ? k_ImageAttachmentSizeExceeded : k_ActionButtonToolTipNoPrompt;
            }
            m_ActionButton.SetEnabled(actionButtonEnabled);
        }

        void OnChatValueChanged()
        {
            // Refresh the UI immediately (placeholder visibility, char count, submit-button enabled state).
            // The actual SessionState/Undo commit is debounced so that a burst of keystrokes
            // coalesces into a single undo entry.
            RefreshUI();
            SchedulePromptUndoCommit();
        }

        void SchedulePromptUndoCommit()
        {
            m_PromptUndoDebounceTask?.Pause();
            m_PromptUndoDebounceTask = schedule.Execute(CommitPromptToUndo).StartingIn(k_PromptUndoDebounceMs);
        }

        void CommitPromptToUndo()
        {
            if (m_ChatInput == null)
                return;

            var state = AssistantUISessionState.instance;
            var current = Text;
            if (current == state.Prompt)
                return;

            // RecordObject snapshots the current m_Prompt before we overwrite it, creating a discrete undo entry.
            Undo.RecordObject(state, "Edit Assistant Prompt");
            state.Prompt = current;
        }

        void OnPromptUndoRedo()
        {
            if (m_ChatInput == null)
                return;

            var restored = AssistantUISessionState.instance.Prompt ?? string.Empty;
            if (m_ChatInput.value != restored)
                m_ChatInput.SetValueWithoutNotify(restored);

            RefreshUI();
        }

        void RefreshChatCharCount()
        {
            m_ChatCharCount.text = $"{Text.Length}/{AssistantMessageSizeConstraints.PromptLimit}";
        }

        void OnProviderChangedHandler(string oldProvider, string newProvider)
        {
            UpdateCommandSelectorVisibility();

            // Clear models and commands when switching providers
            m_ModelItems.Clear();
            m_CommandItems.Clear();
            m_SelectedModelId = null;
            RefreshCommandSelectorItems();

            OnProviderChanged?.Invoke(oldProvider, newProvider);
        }

        string GetProviderDisplayName(string providerId)
        {
            if (AssistantProviderFactory.IsUnityProvider(providerId))
            {
                var profiles = Context.AvailableUnityModelProfiles;
                if (profiles != null)
                {
                    foreach (var (id, displayName) in profiles)
                    {
                        if (id == providerId && !string.IsNullOrEmpty(displayName))
                            return displayName;
                    }
                }
                return providerId;
            }

            var match = AcpProvidersRegistry.Providers.FirstOrDefault(p => p.Id == providerId);
            if (match != null && !string.IsNullOrEmpty(match.DisplayName))
                return match.DisplayName;

            return providerId;
        }

        public void SetAvailableCommands(IReadOnlyList<(string name, string description)> commands)
        {
            m_CommandItems.Clear();
            if (commands != null)
            {
                m_CommandItems.AddRange(commands.Select(c => (c.name, "/" + c.name)));
            }

            RefreshCommandSelectorItems();
        }

        void OnChatKeyUpEvent(KeyUpEvent evt)
        {
            RefreshChatCharCount();
        }

        /// <summary>
        /// macOS: Cmd+Z. Windows/Linux: Ctrl+Z.
        /// </summary>
        static bool IsUndoShortcut(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Z || evt.altKey || evt.shiftKey)
                return false;

            return Application.platform == RuntimePlatform.OSXEditor
                ? evt.commandKey && !evt.ctrlKey
                : evt.ctrlKey && !evt.commandKey;
        }

        /// <summary>
        /// macOS: Cmd+Shift+Z. Windows/Linux: Ctrl+Y or Ctrl+Shift+Z.
        /// </summary>
        static bool IsRedoShortcut(KeyDownEvent evt)
        {
            if (evt.altKey)
                return false;

            if (Application.platform == RuntimePlatform.OSXEditor)
                return evt.commandKey && !evt.ctrlKey && evt.shiftKey && evt.keyCode == KeyCode.Z;

            if (!evt.ctrlKey || evt.commandKey)
                return false;

            return (evt.keyCode == KeyCode.Y && !evt.shiftKey)
                || (evt.keyCode == KeyCode.Z && evt.shiftKey);
        }

        /// <summary>
        /// Flush pending debounced undo commits before Unity's global Undo processes the shortcut.
        /// Without this, fast typing followed by immediate undo would skip the uncommitted edits
        /// entirely (no undo entry exists yet) and revert to a much earlier state.
        /// This handler runs in TrickleDown phase, so it executes before Unity's Undo.PerformUndo —
        /// the flush creates the undo entry just in time for the undo system to revert it.
        /// </summary>
        internal void OnChatKeyDownEvent(KeyDownEvent evt)
        {
            if (m_PromptUndoDebounceTask != null && (IsUndoShortcut(evt) || IsRedoShortcut(evt)))
            {
                m_PromptUndoDebounceTask.Pause();
                m_PromptUndoDebounceTask = null;
                CommitPromptToUndo();
            }

            if (evt.keyCode == KeyCode.V)
            {
                bool isPasteShortcut;

                if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    isPasteShortcut = evt.commandKey && !evt.altKey && !evt.shiftKey && !evt.ctrlKey;
                }
                else
                {
                    isPasteShortcut = evt.ctrlKey && !evt.altKey && !evt.shiftKey && !evt.commandKey;
                }

                if (isPasteShortcut)
                {
                    HandlePaste();
                    evt.StopPropagation();
#pragma warning disable CS0618 // Type or member is obsolete
                    evt.PreventDefault();
#pragma warning restore CS0618 // Type or member is obsolete
                    return;
                }
            }

            // Newline handling
            if (string.IsNullOrEmpty(Text) &&
                evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.UpArrow or KeyCode.DownArrow)
            {
                evt.StopImmediatePropagation();
            }

            if (evt.character == '\n')
            {
                if (evt.shiftKey)
                {
                    string previousText = m_ChatInput.value;
                    var isAtEnd = CursorIndex == previousText.Length;

                    string newText = m_ChatInput.value.Insert(CursorIndex, "\n");
                    SetText(newText);

                    CursorIndex++;

                    if (isAtEnd)
                    {
                        m_ChatInput.selectIndex = CursorIndex + 1;
                    }
                    else
                    {
                        m_ChatInput.selectIndex = CursorIndex;
                    }

                    evt.StopImmediatePropagation();
                    return;
                }

                evt.StopPropagation();
#if !UNITY_2023_1_OR_NEWER
                evt.PreventDefault();
#endif
            }

            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (Text.Trim().Length == 0)
                        return;
                    break;

                default:
                    return;
            }

            if (evt.altKey || evt.shiftKey)
                return;

            bool useModifier = AssistantEditorPreferences.UseModifierKeyToSendPrompt;
            bool hasModifier = Application.platform == RuntimePlatform.OSXEditor ? evt.commandKey : evt.ctrlKey;
            if (hasModifier != useModifier)
                return;

            evt.StopPropagation();

            if (!Context.Blackboard.IsAPIWorking && Context.Blackboard.IsAPIReadyForPrompt)
                OnSubmit();
        }

        void InitializeSettingsPopup()
        {
            if (m_SettingsPopup != null)
                return;

            m_SettingsPopup = new SettingsPopup();
            m_SettingsPopup.Initialize(Context, autoShowControl: false);
            m_PopupRoot.Add(m_SettingsPopup);
        }

        void OnSettingsButtonClicked()
        {
            if (m_SettingsPopup.IsShown)
                HideSettingsPopup();
            else
                ShowSettingsPopup();
        }

        void OnModeChanged(string modeId)
        {
            UpdateSettingsPopupAutoRun();
            UpdatePlaceholderText();
        }

        void UpdatePlaceholderText()
        {
            if (!ProviderStateObserver.IsUnityProvider)
            {
                UpdatePlaceholderFromReadyState();
                return;
            }
            m_Placeholder.text = GetPlaceholderForMode(Context.Blackboard.ActiveMode);
        }

        void OnProviderReadyStateChanged(ProviderStateObserver.ProviderReadyState _, string __)
        {
            UpdatePlaceholderFromReadyState();
        }

        void UpdatePlaceholderFromReadyState()
        {
            if (ProviderStateObserver.IsUnityProvider)
            {
                m_Placeholder.text = GetPlaceholderForMode(Context.Blackboard.ActiveMode);
                return;
            }

            switch (ProviderStateObserver.ReadyState)
            {
                case ProviderStateObserver.ProviderReadyState.Initializing:
                    m_Placeholder.text = k_PlaceholderInitializing;
                    break;
                case ProviderStateObserver.ProviderReadyState.Error:
                    // Show simple message - full error details are shown in chat area
                    m_Placeholder.text = k_PlaceholderError;
                    break;
                case ProviderStateObserver.ProviderReadyState.Ready:
                    m_Placeholder.text = k_PlaceholderAgent;
                    break;
            }
        }

        static string GetPlaceholderForMode(AssistantMode mode)
        {
            return mode switch
            {
                AssistantMode.Plan => k_PlaceholderPlan,
                AssistantMode.Agent => k_PlaceholderAgent,
                _ => k_PlaceholderAsk
            };
        }

        void UpdateSettingsPopupAutoRun() => m_SettingsPopup?.SetAutoRunEnabled(Context.Blackboard.ActiveMode.SupportsAutoRun());

        /// <summary>
        /// Bind the mode provider to an IAssistantProvider.
        /// Call this when switching providers.
        /// </summary>
        public void BindModeProvider(IAssistantProvider provider)
        {
            m_ModeProvider.BindProvider(provider);
        }

        /// <summary>
        /// Set available models for the provider from session/initialized data.
        /// </summary>
        public void SetModels(IEnumerable<(string modelId, string name, string description)> models, string currentModelId)
        {
            m_ModelItems.Clear();

            if (models != null)
            {
                foreach (var (modelId, name, description) in models)
                {
                    m_ModelItems.Add((modelId, name));
                }
            }

            m_SelectedModelId = currentModelId;
            RefreshCommandSelectorItems();
        }

        /// <summary>
        /// Clear models when switching providers or resetting session.
        /// </summary>
        public void ClearModels()
        {
            m_ModelItems.Clear();
            m_SelectedModelId = null;
            RefreshCommandSelectorItems();
        }

        void ShowSettingsPopup()
        {
            using var listPoolHandle = ListPool<IToolPermissions.TemporaryPermission>.Get(out var permissions);
            Context.API.Provider.ToolPermissions.GetTemporaryPermissions(permissions);

            m_SettingsPopup.ShowWithPermissions(permissions);
            m_SettingPopupTracker = new PopupTracker(m_SettingsPopup, m_SettingsButton, new Vector2Int(0, 54), m_SettingsButton);
            m_SettingPopupTracker.Dismiss += HideSettingsPopup;
        }

        void HideSettingsPopup()
        {
            if (m_SettingPopupTracker == null)
                return;

            m_SettingPopupTracker.Dismiss -= HideSettingsPopup;
            m_SettingPopupTracker.Dispose();
            m_SettingPopupTracker = null;

            m_SettingsPopup.Hide();
        }
    }
}
