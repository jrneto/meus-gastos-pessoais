using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Email;

public sealed class SesEmailService : IEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly SesOptions _options;

    public SesEmailService(IAmazonSimpleEmailServiceV2 sesClient, IOptions<SesOptions> options)
    {
        _sesClient = sesClient;
        _options = options.Value;
    }

    public Task SendAsync(
        string toEmail, string subject, string htmlBody,
        CancellationToken cancellationToken = default) =>
        _sesClient.SendEmailAsync(new SendEmailRequest
        {
            FromEmailAddress = _options.SenderEmail,
            Destination = new Destination { ToAddresses = [toEmail] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject },
                    Body = new Body { Html = new Content { Data = htmlBody } }
                }
            }
        }, cancellationToken);
}
