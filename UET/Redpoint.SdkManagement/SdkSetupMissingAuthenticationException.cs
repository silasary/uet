﻿namespace Redpoint.SdkManagement
{
    public class SdkSetupMissingAuthenticationException : Exception
    {
        public SdkSetupMissingAuthenticationException(string message) : base(message)
        {
        }
    }
}