﻿namespace BuildRunner.Configuration.Engine
{
    using System.Text.Json.Serialization;

    internal class BuildConfigEngineDeployment
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("Platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("Target")]
        public string Target { get; set; } = string.Empty;
    }
}
