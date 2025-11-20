namespace Prepared.Business.Interfaces;

/// <summary>
/// Configuration service for Twilio settings.
/// Provides read-only access to Twilio configuration values with defaults.
/// </summary>
public interface ITwilioConfigurationService
{
    string AccountSid { get; }
    string KeySid { get; }
    string SecretKey { get; }
    string AuthToken { get; }
    string WebhookUrl { get; }
    string GreetingMessage { get; }
    string ErrorMessage { get; }
    bool DisableWebhookValidation { get; }
}

