﻿namespace AutomationRunner
{
    public class TestResultEntryEvent
    {
        public string Type { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Context { get; set; } = string.Empty;

        public string Artifact { get; set; } = string.Empty;
    }
}
