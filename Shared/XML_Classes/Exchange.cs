namespace Shared.XML_Classes;

using System.Xml.Serialization;

/*
 * Outcoming xml requests 
 */

[XmlRoot("exchange_request")]
public class ExchangeRequest
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }


    [XmlElement("to_client_id")]
    public int toClientId { get; set; }


    [XmlElement("type_of_exchange_object")]
    public string TypeOfExchangeObject { get; set; } = "";
}

[XmlRoot("exchange_response")]
public class ExchangeResponse
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }


    [XmlElement("to_client_id")]
    public int toClientId { get; set; }


    [XmlElement("success")]
    public bool Success { get; set; }
}

[XmlRoot("send_item")]
public class SendItem
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }

    [XmlElement("to_client_id")]
    public int toClientId { get; set; }

    [XmlElement("type_name")]
    public string TypeName { get; set; } = string.Empty;

    [XmlElement("data")]
    public string XmlPayload { get; set; } = string.Empty;
}

[XmlRoot("receipt_confirmation")]
public class ReceiptConfirmation
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }


    [XmlElement("to_client_id")]
    public int toClientId { get; set; }


    [XmlElement("success")]
    public bool Success { get; set; }
}

[XmlRoot("reverse_exchange_request")]
public class ReverseExchangeRequest
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }


    [XmlElement("to_client_id")]
    public int toClientId { get; set; }
}






/*
 * Incoming xml requests 
 */

[XmlRoot("exchange_offer")]
public class ExchangeOffer
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }


    [XmlElement("from_client_id")]
    public int fromClientId { get; set; }


    [XmlElement("type_of_exchange_object")]
    public string TypeOfExchangeObject { get; set; } = "";
}


[XmlRoot("exchange_response_result")]
public class ExchangeResponseResult
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }


    [XmlElement("from_client_id")]
    public int fromClientId { get; set; }


    [XmlElement("success")]
    public bool Success { get; set; }
}


[XmlRoot("incoming_item")]
public class IncomingItem
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }

    [XmlElement("from_client_id")]
    public int fromClientId { get; set; }

    [XmlElement("type_name")]
    public string TypeName { get; set; } = string.Empty;

    [XmlElement("data")]
    public string XmlPayload { get; set; } = string.Empty;

    // [XmlAnyElement("item")]
    // public XmlElement? Item { get; set; }
}


[XmlRoot("receipt_confirmation_result")]
public class ReceiptConfirmationResult
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }


    [XmlElement("from_client_id")]
    public int fromClientId { get; set; }


    [XmlElement("success")]
    public bool Success { get; set; }
}


[XmlRoot("reverse_exchange_offer")]
public class ReverseExchangeOffer
{
    [XmlElement("server_transaction_id")]
    public int ServerTransactionId { get; set; }


    [XmlElement("from_client_id")]
    public int fromClientId { get; set; }
}
