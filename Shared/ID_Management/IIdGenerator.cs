namespace Shared.ID_Management;

public interface IIdGenerator
{
    int GetNextId();
    void ReleaseId(int id);
    bool IsIdActive(int id);
}