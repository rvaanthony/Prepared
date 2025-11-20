using System.ComponentModel.DataAnnotations;

namespace Prepared.Business.Options;

/// <summary>
/// Configuration options for Twilio service.
/// Validates configuration at startup to ensure all required values are present.
/// </summary>
public class TwilioOptions
{
    public const string SectionName = "Twilio";

    /// <summary>
    /// Twilio Account SID. Required for all Twilio operations.
    /// </summary>
    [Required(ErrorMessage = "Twilio Account SID is required")]
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>
    /// Twilio API Key SID. Required for API authentication.
    /// </summary>
    [Required(ErrorMessage = "Twilio Key SID is required")]
    public string KeySid { get; set; } = string.Empty;

    /// <summary>
    /// Twilio API Secret Key. Required for API authentication.
    /// </summary>
    [Required(ErrorMessage = "Twilio Secret Key is required")]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Twilio Auth Token. Required for webhook signature validation.
    /// </summary>
    [Required(ErrorMessage = "Twilio Auth Token is required for webhook validation")]
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for Twilio webhooks. Must be a valid URL.
    /// </summary>
    [Required(ErrorMessage = "Twilio Webhook URL is required")]
    [Url(ErrorMessage = "Twilio Webhook URL must be a valid URL")]
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Greeting message to play when a call is answered.
    /// Defaults to a friendly, professional greeting.
    /// </summary>
    public string GreetingMessage { get; set; } = 
        "Hello! Thanks for calling. I'm connecting you now, and your conversation will be transcribed live for your records.";

    /// <summary>
    /// Error message to play when technical difficulties occur.
    /// Defaults to a polite error message.
    /// </summary>
    public string ErrorMessage { get; set; } = 
        "I'm sorry, we're experiencing technical difficulties at the moment. Please try calling back in a few minutes.";

    /// <summary>
    /// Whether to disable webhook signature validation (for debugging only).
    /// Should never be true in production.
    /// </summary>
    public bool DisableWebhookValidation { get; set; } = false;
}

