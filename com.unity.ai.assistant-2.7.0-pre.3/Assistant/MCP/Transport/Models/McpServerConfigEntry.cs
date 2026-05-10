using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Unity.AI.Assistant.Editor.Mcp.Transport.Models
{
    /// <summary>
    /// Server configuration entry as stored in the mcp.json config file.
    /// The server name is the dictionary key, not a property of this class.
    /// </summary>
#if !UNITY_6000_5_OR_NEWER
    [Serializable]
#endif
    class McpServerConfigEntry
    {
        /// <summary>
        /// Transport type: "stdio"
        /// </summary>
        [JsonProperty("type")]
        public string Type = "stdio";

        /// <summary>
        /// Executable command
        /// </summary>
        [JsonProperty("command")]
        public string Command;

        /// <summary>
        /// Command arguments (optional)
        /// </summary>
        [JsonProperty("args")]
        public string[] Args = Array.Empty<string>();

        /// <summary>
        /// Environment variables (optional)
        /// </summary>
        [JsonProperty("env")]
        public Dictionary<string, string> Env = new();
    }
}
