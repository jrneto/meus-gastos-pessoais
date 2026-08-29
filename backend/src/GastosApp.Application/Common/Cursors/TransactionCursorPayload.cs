namespace GastosApp.Application.Common.Cursors;

public sealed record TransactionCursorPayload(
    string Index,
    Dictionary<string, string> LastEvaluatedKey);
