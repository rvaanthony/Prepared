using Microsoft.AspNetCore.Mvc;

namespace Prepared.Client.Controllers;

/// <summary>
/// Controller for the calls monitoring dashboard.
/// </summary>
public class CallsController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CallsController> _logger;

    public CallsController(IConfiguration configuration, ILogger<CallsController> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Displays the calls monitoring dashboard with real-time updates.
    /// </summary>
    public IActionResult Index()
    {
        // Inject configuration into view for JavaScript
        ViewBag.GoogleMapsApiKey = _configuration["GoogleMaps:ApiKey"] ?? string.Empty;
        ViewBag.SignalRHubUrl = "/hubs/transcript";
        
        return View();
    }
}

