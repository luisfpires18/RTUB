using System.Collections.Concurrent;
using System.Linq;

namespace RTUB.Application.Services.MyTuno;

public class MyTunoPresenceService
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

    public void AddConnection(string userId, string connectionId)
    {
        var connections = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (connections)
        {
            connections.Add(connectionId);
        }
    }

    public void RemoveConnection(string userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var connections))
        {
            return;
        }

        lock (connections)
        {
            connections.Remove(connectionId);
            if (connections.Count == 0)
            {
                _connections.TryRemove(userId, out _);
            }
        }
    }

    public IReadOnlyList<string> GetOnlineUserIds()
    {
        return _connections.Keys.ToList();
    }

    public bool IsOnline(string userId)
    {
        return _connections.ContainsKey(userId);
    }
}
