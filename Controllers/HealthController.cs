using Microsoft.AspNetCore.Mvc;
using System.Text;
using Newtonsoft.Json;

namespace FBRExtension.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<HealthController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                version = "1.0.0"
            });
        }

        [HttpGet("connectivity")]
        public async Task<IActionResult> CheckConnectivity()
        {
            var results = new List<object>();

            try
            {
                // Test connectivity to PRAL FBR endpoints
                var sandboxValidateUrl = _configuration["PralFbrEndpoints:Sandbox:Validate"];
                var productionValidateUrl = _configuration["PralFbrEndpoints:Production:Validate"];

                if (!string.IsNullOrEmpty(sandboxValidateUrl))
                {
                    var sandboxResult = await TestEndpointConnectivity("Sandbox Validate", sandboxValidateUrl);
                    results.Add(sandboxResult);
                }

                if (!string.IsNullOrEmpty(productionValidateUrl))
                {
                    var productionResult = await TestEndpointConnectivity("Production Validate", productionValidateUrl);
                    results.Add(productionResult);
                }

                return Ok(new
                {
                    status = "Connectivity Check Complete",
                    timestamp = DateTime.UtcNow,
                    results = results
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during connectivity check");
                return StatusCode(500, new
                {
                    status = "Error",
                    timestamp = DateTime.UtcNow,
                    error = ex.Message
                });
            }
        }

        private async Task<object> TestEndpointConnectivity(string endpointName, string url)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                using var httpClient = _httpClientFactory.CreateClient("PralFbrClient");
                
                // Remove authorization header for connectivity test
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "PralFbrExtension-HealthCheck/1.0");
                
                // Create a simple test request (this will likely fail with 401/403, but that's expected)
                var testData = new { test = "connectivity" };
                var jsonContent = JsonConvert.SerializeObject(testData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync(url, content);
                var endTime = DateTime.UtcNow;
                var duration = (endTime - startTime).TotalMilliseconds;
                
                return new
                {
                    endpoint = endpointName,
                    url = url,
                    status = "Reachable",
                    statusCode = (int)response.StatusCode,
                    statusDescription = response.StatusCode.ToString(),
                    durationMs = duration,
                    timestamp = endTime,
                    note = "Expected to receive 401/403 for unauthorized request"
                };
            }
            catch (HttpRequestException httpEx)
            {
                var endTime = DateTime.UtcNow;
                var duration = (endTime - startTime).TotalMilliseconds;
                
                return new
                {
                    endpoint = endpointName,
                    url = url,
                    status = "Connection Failed",
                    error = httpEx.Message,
                    durationMs = duration,
                    timestamp = endTime,
                    errorType = GetErrorType(httpEx.Message)
                };
            }
            catch (TaskCanceledException ex)
            {
                var endTime = DateTime.UtcNow;
                var duration = (endTime - startTime).TotalMilliseconds;
                
                return new
                {
                    endpoint = endpointName,
                    url = url,
                    status = "Timeout",
                    error = ex.Message,
                    durationMs = duration,
                    timestamp = endTime,
                    errorType = "Timeout"
                };
            }
            catch (Exception ex)
            {
                var endTime = DateTime.UtcNow;
                var duration = (endTime - startTime).TotalMilliseconds;
                
                return new
                {
                    endpoint = endpointName,
                    url = url,
                    status = "Error",
                    error = ex.Message,
                    durationMs = duration,
                    timestamp = endTime,
                    errorType = "Unknown"
                };
            }
        }

        private string GetErrorType(string errorMessage)
        {
            if (errorMessage.Contains("SSL") || errorMessage.Contains("certificate") || errorMessage.Contains("TLS"))
                return "SSL/TLS Error";
            if (errorMessage.Contains("DNS") || errorMessage.Contains("resolve"))
                return "DNS Error";
            if (errorMessage.Contains("timeout") || errorMessage.Contains("timed out"))
                return "Timeout Error";
            if (errorMessage.Contains("refused") || errorMessage.Contains("failed") || errorMessage.Contains("connected party did not properly respond"))
                return "Connection Error";
            
            return "Network Error";
        }
    }
}