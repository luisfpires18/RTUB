using System.Collections.Concurrent;

namespace RTUB.Integration.Tests;

/// <summary>
/// Records executed SQL, but only between <see cref="Recording"/> and the disposal of its handle,
/// so the application's background services cannot colour a measurement taken around one request.
/// </summary>
public sealed class SqlRecorder
{
    private readonly ConcurrentQueue<string> _statements = new();
    private volatile bool _enabled;

    public IDisposable Recording()
    {
        _statements.Clear();
        _enabled = true;
        return new Handle(this);
    }

    internal void Add(string sql)
    {
        if (_enabled)
        {
            _statements.Enqueue(sql);
        }
    }

    public int Count(string fragment) =>
        _statements.Count(s => s.Contains(fragment, StringComparison.Ordinal));

    private sealed class Handle : IDisposable
    {
        private readonly SqlRecorder _recorder;
        public Handle(SqlRecorder recorder) => _recorder = recorder;
        public void Dispose() => _recorder._enabled = false;
    }
}
