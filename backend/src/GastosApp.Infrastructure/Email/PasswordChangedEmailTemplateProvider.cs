using System.Reflection;

namespace GastosApp.Infrastructure.Email;

// Carrega o template embutido (EmbeddedResource, ver .csproj) uma única
// vez, no cold start — leitura via GetManifestResourceStream, sem
// reflection de configuração nem JSON, seguro sob Native AOT. Mesmo
// padrão de EmailTemplateProvider (GastosApp.CognitoTriggers.CustomMessage).
internal static class PasswordChangedEmailTemplateProvider
{
    public static string Template { get; } = Load("03-senha-alterada.html");

    private static string Load(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{assembly.GetName().Name}.Email.Templates.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Template embutido não encontrado: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
