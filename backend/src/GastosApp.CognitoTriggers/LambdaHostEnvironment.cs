using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GastosApp.CognitoTriggers;

// IHostEnvironment mínimo — AddInfrastructure(IConfiguration, IHostEnvironment)
// exige o parâmetro por assinatura, mas hoje não o usa (ver
// InfrastructureServiceCollectionExtensions.cs). Este Lambda não hospeda
// nada via Microsoft.Extensions.Hosting de verdade — só compõe o
// ServiceProvider manualmente no Function.cs — então não precisa de mais
// que isso pra satisfazer a assinatura.
internal sealed class LambdaHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Lambda";
    public string ApplicationName { get; set; } = "GastosApp.CognitoTriggers";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
