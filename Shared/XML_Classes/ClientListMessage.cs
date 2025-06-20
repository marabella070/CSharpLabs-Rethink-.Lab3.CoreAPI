namespace Shared.XML_Classes;

using System.Xml.Serialization;

[XmlRoot("client_list")]
public class ClientListMessage
{
    [XmlElement("client")]
    public List<ClientEntry> Clients { get; set; } = new();

    [XmlAttribute("count")]
    public int Count => Clients.Count;
}

public class ClientEntry
{
    [XmlAttribute("id")]
    public string Id { get; set; } = "";

    [XmlElement("name")]
    public string UserName { get; set; } = "";
}
