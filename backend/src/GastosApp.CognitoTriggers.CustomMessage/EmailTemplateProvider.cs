using System.Reflection;

namespace GastosApp.CognitoTriggers.CustomMessage;

// Carrega os dois templates embutidos (EmbeddedResource, ver .csproj) uma
// única vez, no cold start — leitura via GetManifestResourceStream, sem
// reflection nem JSON, seguro sob Native AOT.
internal static class EmailTemplateProvider
{
    public static string SignUpTemplate { get; } = Load("01-confirmacao-cadastro.html");
    public static string ForgotPasswordTemplate { get; } = Load("02-recuperacao-senha.html");

    private static string Load(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{assembly.GetName().Name}.Templates.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Template embutido não encontrado: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
