namespace Shared.XML_Classes;

using System.Xml.Serialization;

[XmlRoot("transition_id_request")]
public class TransitionIdRequest
{
    [XmlElement("client_transaction_id")]
    public int ClientTransactionId { get; set; }
}


[XmlRoot("transition_id_request_response")]
public class TransitionIdRequestResponse
{
    [XmlElement("client_transaction_id")]
    public int ClientTransactionId { get; set; }

    [XmlElement("server_transaction_id")]
    public int? ServerTransactionId { get; set; }

    [XmlElement("success")]
    public bool Success { get; set; }
}


[XmlRoot("transition_id_release")]
public class TransitionIdRelease
{
    [XmlElement("client_transaction_id")]
    public int ClientTransactionId { get; set; }

    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }
}


[XmlRoot("transition_id_release_response")]
public class TransitionIdReleaseResponse
{
    [XmlElement("client_transaction_id")]
    public int ClientTransactionId { get; set; }

    [XmlElement("success")]
    public bool Success { get; set; }
}
