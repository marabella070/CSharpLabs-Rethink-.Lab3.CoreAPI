namespace Shared.ID_Management;

public class SimpleIdGenerator : IIdGenerator
{
    private int _nextId = 1;
    private readonly HashSet<int> _activeIds = new();

    public int GetNextId()
    {
        int newId = _nextId++;
        _activeIds.Add(newId);
        return newId;
    }

    public void ReleaseId(int id)
    {
        _activeIds.Remove(id);
    }

    public bool IsIdActive(int id)
    {
        return _activeIds.Contains(id);
    }
}