using System;
using System.Collections.Generic;

namespace InfiniteLikeToken.Core.Models;

public record AgentMessage(
    string Role,
    string Content,
    string? ModelUsed = null,
    string? KeyNickname = null,
    DateTime Timestamp = default,
    System.Collections.Generic.List<string>? AttachedImages = null
)
{
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.Now : Timestamp;
}

public record ToolCallRequest(
    string Id,
    string ToolName,
    Dictionary<string, object?> Arguments
);

public record ToolCallResponse(
    string Id,
    bool Success,
    string Output,
    string? Error = null
);
