using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    static class SkillsProjectSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateAISettingsProvider()
        {
            var provider = new SettingsProvider("Project/AI/Skills", SettingsScope.Project)
            {
                label = "Skills",
                activateHandler = (searchContext, rootElement) =>
                {
                    var page = new AssistantSkillsSettingsView();
                    page.Initialize(null);
                    rootElement.Add(page);
                }
            };

            provider.hasSearchInterestHandler =
                SettingsProviderSearchHelper.CreateSearchHandler(provider.activateHandler, "AI", "Skills");

            return provider;
        }
    }
}
