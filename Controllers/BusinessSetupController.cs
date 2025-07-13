using FBRExtension.Constants;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FBRExtension.Helpers;
using System.Text;

namespace FBRExtension.Controllers
{
    // Controller for business setup operations
    [ApiController]
    [Route("api/[controller]")]
    public class BusinessSetupController : ControllerBase
    {
        private readonly ILogger<BusinessSetupController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        public BusinessSetupController(ILogger<BusinessSetupController> logger, HttpClient httpClient, IConfiguration configuration) 
        {
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }


        [HttpGet]
        public async Task<IActionResult> LoadBusinessSetup([FromQuery] string? data = null)
        {
            try
            {
                //Log access information
                var referer = Request.Headers["Referer"].ToString();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                _logger.LogInformation("Business Setup Access - IP: {IpAddress}, Referer: {Referer}",
                    ipAddress ?? "Unknown", string.IsNullOrEmpty(referer) ? "Direct Access" : referer);

                var htmlPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "business-setup.html");
                if (!System.IO.File.Exists(htmlPath))
                    return NotFound("Business setup page not found");

                var htmlContent = await System.IO.File.ReadAllTextAsync(htmlPath);
                string currentVersion = VersionHelper.GetVersion();

                JObject json;

                try
                {
                    json = !string.IsNullOrWhiteSpace(data) ? JObject.Parse(data) : new JObject();
                }
                catch
                {
                    json = new JObject();
                }

                // Pastikan struktur lengkap tersedia
                var businessDetails = (JObject)(json["businessDetails"] ??= new JObject());
                businessDetails["country"] = FieldConstants.DEFAULT_COUNTRY;

                var customFields2 = (JObject)(businessDetails["customFields2"] ??= new JObject());
                var strings = (JObject)(customFields2["strings"] ??= new JObject());

                strings[FieldConstants.APPLICATION_VERSION_FIELD] = currentVersion;

                var dataScript = $"<script>window.initialData = {json.ToString(Formatting.None)};</script>";
                htmlContent = htmlContent.Replace("</head>", $"{dataScript}</head>");

                return Content(htmlContent, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error serving page: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }


        // Retrieves custom field configuration from embedded JSON resource

        [HttpGet("CustomFieldJson")]
        public IActionResult CustomFieldJson()
        {
            var assembly = typeof(BusinessSetupController).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("CustomFields.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                _logger.LogError("Embedded resource 'CustomFields.json' not found.");
                return Ok(new { jsondata = new object[0] });
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger.LogError("Failed to load embedded resource stream for 'CustomFields.json'.");
                return Ok(new { jsondata = new object[0] });
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var jsonContent = reader.ReadToEnd();
            
            try
            {
                var jsonObject = JsonConvert.DeserializeObject(jsonContent);
                return Ok(jsonObject);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing CustomFields.json: {ex.Message}");
                return Ok(new { jsondata = new object[0] });
            }
        }

        // Gets the application version
        [HttpGet("GetVersion")]
        public IActionResult GetVersion()
        {
            try
            {
                var version = VersionHelper.GetVersion();
                //_logger.LogInformation($"Application version requested: {version}");
                
                return Ok(new 
                {
                    success = true,
                    version = version,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting application version: {ex.Message}");
                return StatusCode(500, new 
                {
                    success = false,
                    message = "Error retrieving application version",
                    error = ex.Message
                });
            }
        }

        // Gets the server's public IP address for FBR whitelist registration
        [HttpGet("GetServerIP")]
        public async Task<IActionResult> GetServerIP()
        {
            try
            {
                //_logger.LogInformation("Getting server public IP address");
                
                // Try to get server's public IP using external service
                var ipServices = new[]
                {
                    "https://api.ipify.org?format=json",
                    "https://ipinfo.io/json",
                    "https://httpbin.org/ip"
                };
                
                foreach (var service in ipServices)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(service);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var ipData = JsonConvert.DeserializeObject<dynamic>(content);
                            
                            string serverIP = "";
                            if (ipData?.ip != null)
                            {
                                serverIP = ipData.ip.ToString();
                            }
                            else if (ipData?.origin != null) // httpbin format
                            {
                                serverIP = ipData.origin.ToString();
                            }
                            
                            if (!string.IsNullOrEmpty(serverIP))
                            {
                                _logger.LogInformation($"Server IP detected: {serverIP}");
                                return Ok(new { success = true, serverIP = serverIP, service = service });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to get IP from {service}: {ex.Message}");
                        continue;
                    }
                }
                
                return Ok(new { success = false, message = "Unable to detect server IP", serverIP = "Unknown" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting server IP: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error", serverIP = "Error" });
            }
        }

        
        // Tests the bearer token by making a request to FBR gateway

        [HttpPost("TestToken")]
        public async Task<IActionResult> TestToken([FromBody] dynamic businessDetailJson)
        {
            try
            {
                _logger.LogInformation("Testing token with business details");
                
                // Extract bearer token and environment from business details
                string bearerToken = "";
                string environment = "";
                
                if (businessDetailJson?.customFields2?.strings != null)
                {
                    var strings = businessDetailJson.customFields2.strings as Newtonsoft.Json.Linq.JObject;
                    if (strings != null)
                    {
                        bearerToken = strings[FieldConstants.BEARER_TOKEN_FIELD]?.ToString() ?? "";
                        environment = strings[FieldConstants.ENVIRONMENT_FIELD]?.ToString() ?? FieldConstants.DEFAULT_ENVIRONMENT;
                    }
                }
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    return BadRequest(new { success = false, message = "Bearer token is required" });
                }
                
                // Use FBR gateway URL for provinces endpoint
                var testUrl = "https://gw.fbr.gov.pk/pdi/v1/provinces";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, testUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                request.Headers.Add("Accept", "application/json");
                
                try
                {
                    var response = await _httpClient.SendAsync(request);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation($"PRAL test successful: {response.StatusCode}");
                        
                        return Ok(new 
                        { 
                            success = true, 
                            message = "Token validation successful",
                            environment = environment,
                            statusCode = (int)response.StatusCode,
                            response = responseContent
                        });
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning($"PRAL test failed: {response.StatusCode} - {errorContent}");
                        
                        return Ok(new 
                        { 
                            success = false, 
                            message = $"Token validation failed: {response.StatusCode}",
                            environment = environment,
                            statusCode = (int)response.StatusCode,
                            error = errorContent
                        });
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError($"Network error during PRAL test: {ex.Message}");
                    return Ok(new 
                    { 
                        success = false, 
                        message = $"Network error: {ex.Message}",
                        environment = environment
                    });
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError($"Timeout during PRAL test: {ex.Message}");
                    return Ok(new 
                    { 
                        success = false, 
                        message = "Request timeout",
                        environment = environment
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during token test: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }
}