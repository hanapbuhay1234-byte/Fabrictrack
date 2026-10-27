using System.Diagnostics;
using System.Net;
using System.Net.Mail;

namespace FabriTrack.Services
{
    public interface IPasswordResetEmailSender
    {
        bool IsConfigured { get; }
        Task SendResetLinkAsync(
            string emailAddress,
            string resetUrl,
            CancellationToken cancellationToken = default);
    }

    public sealed class PasswordResetEmailSender : IPasswordResetEmailSender
    {
        private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(20);
        private readonly string? _host;
        private readonly int _port;
        private readonly string? _username;
        private readonly string? _password;
        private readonly string? _fromEmail;
        private readonly string _fromName;
        private readonly ILogger<PasswordResetEmailSender> _logger;

        public PasswordResetEmailSender(
            IConfiguration configuration,
            ILogger<PasswordResetEmailSender> logger)
        {
            _logger = logger;
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

        public async Task SendResetLinkAsync(
            string emailAddress,
            string resetUrl,
            CancellationToken cancellationToken = default)
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
                Credentials = new NetworkCredential(_username, _password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SendTimeout);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await client.SendMailAsync(message, timeout.Token);
                _logger.LogInformation(
                    "Password reset email accepted by SMTP host {SmtpHost} in {ElapsedMilliseconds} ms.",
                    _host,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    exception,
                    "Sending password reset email through SMTP host {SmtpHost} timed out after {TimeoutSeconds} seconds.",
                    _host,
                    SendTimeout.TotalSeconds);
                throw;
            }
            catch (SmtpException exception)
            {
                _logger.LogError(
                    exception,
                    "SMTP host {SmtpHost} rejected or failed to accept a password reset email after {ElapsedMilliseconds} ms.",
                    _host,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
