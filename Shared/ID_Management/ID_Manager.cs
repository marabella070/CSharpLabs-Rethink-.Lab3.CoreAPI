namespace Shared.ID_Management;

public class IdManager<TGenerator> where TGenerator : IIdGenerator, new()
{
    private readonly TGenerator _idGenerator;
    private readonly Queue<int> _recycledIds = new();
    private readonly object _lock = new();

    public IdManager(TGenerator idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public int GetNextId()
    {
        lock (_lock)
        {
            if (_recycledIds.Count > 0)
            {
                int recycledId = _recycledIds.Dequeue();
                return recycledId;
            }

            return _idGenerator.GetNextId();
        }
    }

    public bool ReleaseId(int id)
    {
        lock (_lock)
        {
            _idGenerator.ReleaseId(id);
            _recycledIds.Enqueue(id);
        }
        return true;
    }

    public bool IsIdActive(int id)
    {
        lock (_lock)
        {
            return _idGenerator.IsIdActive(id);
        }
    }
}