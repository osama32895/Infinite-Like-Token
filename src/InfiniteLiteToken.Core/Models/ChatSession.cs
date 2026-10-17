using System;
using System.Collections.Generic;

namespace InfiniteLiteToken.Core.Models;

public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Chat";
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public string? WorkspaceFolder { get; set; }
    public List<AgentMessage> Messages { get; set; } = new();
    public bool IsPinned { get; set; } = false;
}
