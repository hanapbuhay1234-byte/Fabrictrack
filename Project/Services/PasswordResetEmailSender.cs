using System.Net;
using System.Net.Mail;

namespace FabriTrack.Services
{
    public interface IPasswordResetEmailSender
    {
        bool IsConfigured { get; }
        Task SendResetLinkAsync(string emailAddress, string resetUrl);
    }

    public sealed class PasswordResetEmailSender : IPasswordResetEmailSender
    {
        private readonly string? _host;
        private readonly int _port;
        private readonly string? _username;
        private readonly string? _password;
        private readonly string? _fromEmail;
        private readonly string _fromName;

        public PasswordResetEmailSender(IConfiguration configuration)
        {
            _host = configuration["Email:Smtp:Host"];
            _username = configuration["Email:Smtp:Username"];
            _password = configuration["Email:Smtp:Password"];
            _fromEmail = configuration["Email:Smtp:FromEmail"];
            _fromName = configuration["Email:Smtp:FromName"] ?? "FabriTrack";
            _port = int.TryParse(configuration["Email:Smtp:Port"], out var port) ? port : 0;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_host) &&
            _port is > 0 and <= 65535 &&
            !string.IsNullOrWhiteSpace(_username) &&
            !string.IsNullOrWhiteSpace(_password) &&
            !string.IsNullOrWhiteSpace(_fromEmail);

        public async Task SendResetLinkAsync(string emailAddress, string resetUrl)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("SMTP email settings are incomplete.");

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail!, _fromName),
                Subject = "Reset your FabriTrack password",
                Body = $"""
                    <p>We received a request to reset the password for your FabriTrack account.</p>
                    <p><a href="{WebUtility.HtmlEncode(resetUrl)}">Reset your password</a></p>
                    <p>This link can only be used once. If you did not request a reset, you can ignore this email.</p>
                    """,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(emailAddress));

            using var client = new SmtpClient(_host, _port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_username, _password)
            };
            await client.SendMailAsync(message);
        }
    }
}
