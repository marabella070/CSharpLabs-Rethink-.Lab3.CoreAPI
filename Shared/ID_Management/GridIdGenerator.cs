namespace Shared.ID_Management;

public class GridIdGenerator : IIdGenerator
{
    private readonly HashSet<int> _activeIds = new();
    private int _counter = 0;
    private readonly string _nodeId;

    public GridIdGenerator()
    {
        _nodeId = "default-node"; // Задаём дефолтное значение для узла
    }

    public GridIdGenerator(string nodeId)
    {
        _nodeId = nodeId; // Уникальный идентификатор узла
    }

    // Генерация уникального ID, включая временную метку, ID узла и счётчик
    public int GetNextId()
    {
        // Получаем текущую метку времени (миллисекунды с начала эпохи UNIX)
        int timestamp = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0xFFFFFFFF);  // Ограничиваем значением 32 бита

        // Хешируем NodeID и используем 12 бит для узла (для уникальности на уровне узла)
        int nodeHash = _nodeId.GetHashCode() & 0xFFF;  // Используем 12 бит для узла (макс. 4095 узлов)

        // Генерация ID через комбинацию временной метки, хеша узла и счётчика
        int newId = (timestamp << 20) | (nodeHash << 8) | (_counter & 0xFF); // Сдвигаем timestamp на 20 бит, nodeHash на 8 бит, счётчик на 8 бит

        // Увеличиваем счётчик для уникальности в пределах одной временной метки
        _counter = (_counter + 1) % 256; // Счётчик от 0 до 255 (8 бит)

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