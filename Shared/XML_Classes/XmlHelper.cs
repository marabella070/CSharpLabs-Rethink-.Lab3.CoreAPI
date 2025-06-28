namespace Shared.XML_Classes;

using System.Text;
using System.Xml;
using System.Xml.Serialization;

public static class XmlHelper
{
    public static string ReadUntilEOF(TextReader reader)
    {
        var builder = new StringBuilder();
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (line.Trim() == "<EOF>") { break; }
            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    public static bool TryGetRootTagName(string xml, out string? tagName)
    {
        tagName = null;

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            tagName = xmlDoc.DocumentElement?.Name;
            return tagName != null;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    public static string? SerializeToXml<T>(T objectToSerialize)
    {
        try
        {
            var serializer = new XmlSerializer(typeof(T));

            using (var sw = new StringWriter())
            {
                serializer.Serialize(sw, objectToSerialize);
                return sw.ToString();
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static T? DeserializeXml<T>(string xml) where T : class
    {
        try
        {
            var serializer = new XmlSerializer(typeof(T));
            using var reader = new StringReader(xml);
            return serializer.Deserialize(reader) as T;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static object? DeserializeXmlFromElement(Type type, XmlElement element)
    {
        using var reader = new XmlNodeReader(element);
        var serializer = new XmlSerializer(type);
        return serializer.Deserialize(reader);
    }

    // десериализация по строке типа
    public static object? DeserializeXmlAsType(string xml, string typeName)
    {
        Type? type = Type.GetType(typeName);
        if (type == null) return null;

        using var sr = new StringReader(xml);
        var serializer = new XmlSerializer(type);
        return serializer.Deserialize(sr);
    }
}