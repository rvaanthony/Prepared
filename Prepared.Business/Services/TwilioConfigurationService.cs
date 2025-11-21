using Microsoft.Extensions.Configuration;
using Prepared.Business.Interfaces;

namespace Prepared.Business.Services;

/// <summary>
/// Configuration service for Twilio settings.
/// Reads configuration values from IConfiguration with sensible defaults.
/// </summary>
public class TwilioConfigurationService(IConfiguration configuration) : ITwilioConfigurationService
{
    public string AccountSid => configuration["Twilio:AccountSid"] ?? "";

    public string KeySid => configuration["Twilio:KeySid"] ?? "";

    public string SecretKey => configuration["Twilio:SecretKey"] ?? "";

    public string AuthToken => configuration["Twilio:AuthToken"] ?? "";

    public string WebhookUrl => configuration["Twilio:WebhookUrl"] ?? "";

    public string GreetingMessage => configuration["Twilio:GreetingMessage"] 
        ?? "Hello! Thanks for calling. I'm connecting you now..";

    public string ErrorMessage => configuration["Twilio:ErrorMessage"] 
        ?? "I'm sorry, we're experiencing technical difficulties at the moment. Please try calling back in a few minutes.";

    public bool DisableWebhookValidation => configuration.GetValue<bool>("Twilio:DisableWebhookValidation", false);
}

