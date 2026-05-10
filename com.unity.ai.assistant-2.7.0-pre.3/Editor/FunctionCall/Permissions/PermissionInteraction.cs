using System;
using System.Threading.Tasks;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class PermissionInteraction : IInteractionSource<PermissionUserAnswer>, IApprovalInteraction
    {
        public string Action { get; }
        public string Detail { get; }
        public string AllowLabel => null;
        public string DenyLabel => null;
        public bool ShowScope => true;

        public Func<PermissionUserAnswer?> TryAutoResolve { get; set; }

        public event Action<PermissionUserAnswer> OnCompleted;
        public TaskCompletionSource<PermissionUserAnswer> TaskCompletionSource { get; } = new();

        public PermissionInteraction(string action, string detail = null)
        {
            Action = action;
            Detail = detail;
        }

        public void Respond(PermissionUserAnswer answer) => Complete(answer);

        public void Complete(PermissionUserAnswer answer)
        {
            TaskCompletionSource.TrySetResult(answer);
            OnCompleted?.Invoke(answer);
        }

        public void CancelInteraction()
        {
            TaskCompletionSource.TrySetCanceled();
        }
    }
}
