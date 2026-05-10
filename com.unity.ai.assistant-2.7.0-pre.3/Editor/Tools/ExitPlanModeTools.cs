using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;

namespace Unity.AI.Assistant.Tools.Editor
{
    class ExitPlanModeTools
    {
        const string k_ToolName = "Unity.ExitPlanMode";

        [AgentTool(
            "Signals that the planning phase is complete and requests user approval to start implementation. " +
            "Call this ONLY after you have written the complete plan file to Assets/Plans/ using Unity.WritePlan. " +
            "The plan file must exist and contain a valid implementation plan before calling this tool. " +
            "The user will be shown the plan content inline with options to: " +
            "(1) Approve and auto-accept edits, (2) Approve with manual edit confirmation, " +
            "(3) Send feedback for plan revision, or (4) Cancel. " +
            "If approved, you will receive instructions to execute the plan. " +
            "If feedback is provided, revise the plan and call this tool again. " +
            "If cancelled, remain in plan mode and wait for further user input.",
            k_ToolName)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Plan,
            toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_PlanningTag)]
        internal static async Task<string> ExitPlanMode(
            ToolExecutionContext context,
            [ToolParameter("Absolute or project-relative path to the plan file (e.g. Assets/Plans/feature-x.md). The file must already exist.")]
            string planPath)
        {
            var planContent = ExitPlanModeInteractionElement.ReadPlanFile(planPath);
            var element = new ExitPlanModeInteractionElement(planPath, planContent);

            return await context.Interactions.WaitForUser(element);
        }
    }
}
