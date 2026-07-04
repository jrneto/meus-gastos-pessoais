using Microsoft.Extensions.Configuration;

namespace GastosApp.Infrastructure.Configuration
{
    public static class AwsParameterStoreExtensions
    {
        public static IConfigurationBuilder AddAwsParameterStore(
            this IConfigurationBuilder builder)
        {
            // Adiciona ao pipeline de configuração do .NET
            // antes do container ser construído
            builder.AddSystemsManager(source =>
            {
                source.Path = "/GastosApp/";
                source.Optional = false;
                source.ReloadAfter = TimeSpan.FromMinutes(5);
                source.AwsOptions = new Amazon.Extensions.NETCore.Setup.AWSOptions
                {
                    Profile = "default",
                    Region = Amazon.RegionEndpoint.USEast1
                };
            });

            return builder;
        }
    }
}
