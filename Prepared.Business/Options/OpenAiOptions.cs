namespace Prepared.Business.Options;

/// <summary>
/// Shared OpenAI configuration for summarization/location services.
/// </summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.openai.com/v1/";
    public string DefaultModel { get; set; } = "gpt-4o-mini";
    public string SummarizationModel { get; set; } = "gpt-4o-mini";
    public string LocationModel { get; set; } = "gpt-4o-mini";
}

