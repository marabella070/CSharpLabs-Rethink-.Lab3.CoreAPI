namespace Shared.Delegates;

public record ExchangeRequestResponse(bool Accept, object? OfferedItem);

public delegate Task<ExchangeRequestResponse> ExchangeRequestHandler(string typeOfIncomingItem);
