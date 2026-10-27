using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

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
        private static readonly Uri BrevoEndpoint = new("https://api.brevo.com/v3/smtp/email");
        private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(20);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string? _apiKey;
        private readonly string? _fromEmail;
        private readonly string _fromName;
        private readonly ILogger<PasswordResetEmailSender> _logger;

        public PasswordResetEmailSender(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<PasswordResetEmailSender> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _apiKey = configuration["Email:Brevo:ApiKey"];
            _fromEmail = configuration["Email:Brevo:FromEmail"];
            _fromName = configuration["Email:Brevo:FromName"] ?? "FabriTrack";
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_apiKey) &&
            !string.IsNullOrWhiteSpace(_fromEmail);

        public async Task SendResetLinkAsync(
            string emailAddress,
            string resetUrl,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Brevo email settings are incomplete.");

            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint);
            request.Headers.TryAddWithoutValidation("api-key", _apiKey);
            request.Content = JsonContent.Create(new
            {
                sender = new { name = _fromName, email = _fromEmail },
                to = new[] { new { email = emailAddress } },
                subject = "Reset your FabriTrack password",
                htmlContent = $"""
                    <p>We received a request to reset the password for your FabriTrack account.</p>
                    <p><a href="{WebUtility.HtmlEncode(resetUrl)}">Reset your password</a></p>
                    <p>This link can only be used once. If you did not request a reset, you can ignore this email.</p>
                    """
            });

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SendTimeout);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await _httpClientFactory.CreateClient().SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        "Brevo did not accept the password reset email.",
                        null,
                        response.StatusCode);
                }

                _logger.LogInformation(
                    "Password reset email accepted by Brevo in {ElapsedMilliseconds} ms.",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    exception,
                    "Sending password reset email through Brevo timed out after {TimeoutSeconds} seconds.",
                    SendTimeout.TotalSeconds);
                throw;
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(
                    exception,
                    "Brevo failed to accept a password reset email after {ElapsedMilliseconds} ms (status {StatusCode}).",
                    stopwatch.ElapsedMilliseconds,
                    exception.StatusCode);
                throw;
            }
        }
    }
}
