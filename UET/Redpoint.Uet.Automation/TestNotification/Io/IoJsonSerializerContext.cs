﻿namespace Redpoint.Uet.Automation.TestNotification.Io
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(List<IoChange>))]
    internal sealed partial class IoJsonSerializerContext : JsonSerializerContext
    {
    }
}
