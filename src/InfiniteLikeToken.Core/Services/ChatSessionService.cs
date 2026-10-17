using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using InfiniteLikeToken.Core.Models;

namespace InfiniteLikeToken.Core.Services;

public class ChatSessionService
{
    private readonly string _sessionsPath;

    public ChatSessionService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _sessionsPath = Path.Combine(localAppData, "InfiniteLikeToken", "sessions.json");
    }

    public async Task<List<ChatSession>> LoadSessionsAsync()
    {
        if (!File.Exists(_sessionsPath)) return new List<ChatSession>();
        try
        {
            var json = await File.ReadAllTextAsync(_sessionsPath);
            var sessions = JsonSerializer.Deserialize<List<ChatSession>>(json) ?? new List<ChatSession>();
            return sessions.OrderByDescending(s => s.LastUpdated).ToList();
        }
        catch { return new List<ChatSession>(); }
    }

    public async Task SaveSessionsAsync(IEnumerable<ChatSession> sessions)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_sessionsPath)!);
            var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_sessionsPath, json);
        }
        catch { }
    }
}
