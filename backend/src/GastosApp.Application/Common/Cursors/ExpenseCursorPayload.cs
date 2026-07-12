namespace GastosApp.Application.Common.Cursors;

public sealed record ExpenseCursorPayload(
    string Index,
    Dictionary<string, string> LastEvaluatedKey);
