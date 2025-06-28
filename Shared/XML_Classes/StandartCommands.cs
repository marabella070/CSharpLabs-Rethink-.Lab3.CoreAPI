namespace Shared.XML_Classes;

using System.Xml.Serialization;

[XmlRoot("auth")]
public class AuthCommand
{
    [XmlElement("client_name")]
    public string ClientName { get; set; } = "";
}

[XmlRoot("quit")]
public class QuitCommand
{
    [XmlElement("reason")]
    public string Reason { get; set; } = "";

    [XmlAttribute("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

[XmlRoot("quit_approved")]
public class QuitApprovedCommand
{
    [XmlElement("approved_by")]
    public string ApprovedBy { get; set; } = "";

    [XmlAttribute("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
