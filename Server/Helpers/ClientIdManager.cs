namespace Server.Helpers;

public class ClientIdManager
{
    private int _nextId = 1;
    private readonly Queue<int> _recycledIds = new();
    private readonly HashSet<int> _activeIds = new();
    private readonly object _lock = new();

    public int GetNextId()
    {
        lock (_lock)
        {
            if (_recycledIds.TryDequeue(out int recycledId))
            {
                _activeIds.Add(recycledId);
                return recycledId;
            }

            int newId = _nextId++;
            _activeIds.Add(newId);
            return newId;
        }
    }

    public void ReleaseId(int id)
    {
        lock (_lock)
        {
            if (_activeIds.Remove(id))
            {
                _recycledIds.Enqueue(id);
            }
        }
    }

    public bool IsIdActive(int id)
    {
        lock (_lock)
        {
            return _activeIds.Contains(id);
        }
    }
}