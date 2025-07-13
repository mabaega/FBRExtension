using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using FBRExtension.Models;
using FBRExtension.Constants;
using System.Text;

namespace FBRExtension.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PralFbrController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PralFbrController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        
        public PralFbrController(IConfiguration configuration, ILogger<PralFbrController> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }
        
        [HttpPost("convert-to-fbr")]
        public IActionResult ConvertToFbr([FromBody] ManagerInvoiceData invoiceData)
        {
            //_logger?.LogInformation("Starting FBR conversion process");
            
            try
            {
                if (invoiceData == null)
                {
                    _logger?.LogWarning("FBR conversion failed: Invoice data is null");
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invoice data is required",
                        Errors = new List<string> { "Request body cannot be null" }
                    });
                }

                // Direct conversion without service dependency
                var fbrData = ConvertManagerDataToFbr(invoiceData);
                
                return Ok(new ApiResponse<PralFbrRequest>
                {
                    Success = true,
                    Data = fbrData,
                    Message = "Successfully converted to FBR format"
                });
            }
            catch (JsonException jsonEx)
            {
                _logger?.LogError(jsonEx, "JSON serialization error during FBR conversion");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "JSON serialization error",
                    Errors = new List<string> { $"Serialization failed: {jsonEx.Message}" }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error converting invoice to FBR format");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error during FBR conversion",
                    Errors = new List<string> { ex.Message, ex.StackTrace ?? "No stack trace available" }
                });
            }
        }

        private PralFbrRequest ConvertManagerDataToFbr(ManagerInvoiceData managerData)
        {
            // Extract seller information from business details if available, otherwise fallback to appsettings
            var sellerNTNCNIC = string.Empty;
            var sellerBusinessName = string.Empty;
            var sellerProvince = string.Empty;
            var sellerAddress = string.Empty;
            var environment = "Sandbox"; 
            var bearerToken = string.Empty;
            
            if (managerData.BusinessDetails?.CustomFields2?.Strings != null)
            {
                sellerNTNCNIC = ExtractCustomField(managerData.BusinessDetails.CustomFields2, FieldConstants.NTN_CNIC_FIELD);
                sellerBusinessName = ExtractCustomField(managerData.BusinessDetails.CustomFields2, FieldConstants.BUSINESS_NAME_FIELD);
                sellerProvince = ExtractCustomField(managerData.BusinessDetails.CustomFields2, FieldConstants.PROVINCE_FIELD);
                sellerAddress = ExtractCustomField(managerData.BusinessDetails.CustomFields2, FieldConstants.BUSINESS_ADDRESS_FIELD);
                environment = ExtractCustomField(managerData.BusinessDetails.CustomFields2, FieldConstants.ENVIRONMENT_FIELD);
                bearerToken = ExtractCustomField(managerData.BusinessDetails.CustomFields2, FieldConstants.BEARER_TOKEN_FIELD);
            }
         
            var invoiceType = managerData.CustomFields2 != null ? ExtractCustomField(managerData.CustomFields2, FieldConstants.INVOICE_TYPE_FIELD) ?? "Sale Invoice" : "Sale Invoice";
            var invoiceRefNo = string.Empty;

            _logger?.LogInformation("Invoice type detected: {InvoiceType}", invoiceType);

            if (invoiceType == "Debit Note")
            {
                invoiceRefNo = managerData.CustomFields2 != null ? ExtractCustomField(managerData.CustomFields2, FieldConstants.DEBITNOTE_REFNO_FIELD) ?? string.Empty : string.Empty;
                _logger?.LogInformation("Debit Note processing: RefNo={RefNo}", invoiceRefNo ?? "N/A");
            }
           
            var request = new PralFbrRequest
            {
                InvoiceType = invoiceType,
                InvoiceDate = managerData.IssueDate.ToString("yyyy-MM-dd"),
                SellerNTNCNIC = sellerNTNCNIC,
                SellerBusinessName = sellerBusinessName,
                SellerProvince = sellerProvince,
                SellerAddress = sellerAddress,
                BuyerNTNCNIC = managerData.Customer?.CustomFields2 != null ? ExtractCustomField(managerData.Customer.CustomFields2, FieldConstants.NTN_CNIC_FIELD) : string.Empty,
                BuyerBusinessName = managerData.Customer?.CustomFields2 != null ? ExtractCustomField(managerData.Customer.CustomFields2, FieldConstants.BUSINESS_NAME_FIELD) : (managerData.Customer?.Name ?? string.Empty),
                BuyerProvince = managerData.Customer?.CustomFields2 != null ? ExtractCustomField(managerData.Customer.CustomFields2, FieldConstants.PROVINCE_FIELD) : string.Empty,
                BuyerAddress = managerData.Customer?.CustomFields2 != null ? ExtractCustomField(managerData.Customer.CustomFields2, FieldConstants.BUSINESS_ADDRESS_FIELD) : string.Empty,
                BuyerRegistrationType = managerData.Customer?.CustomFields2 != null ? ExtractCustomField(managerData.Customer.CustomFields2, FieldConstants.REGISTRATION_TYPE_FIELD) : "UnRegistered",
                InvoiceRefNo = invoiceRefNo ?? string.Empty
            };

            if (environment == "Sandbox"){
                request.ScenarioId = managerData.CustomFields2 != null ? ExtractCustomField(managerData.CustomFields2, FieldConstants.SCENARIOID_FIELD) ?? FieldConstants.DEFAULT_SCENARIOID : FieldConstants.DEFAULT_SCENARIOID;
            }
            
            // Calculate total net amount for WHT distribution if using fixed amount
            var totalNetAmount = 0m;
            var lineNetAmounts = new List<decimal>();
            
            if (managerData.WithholdingTax && managerData.WithholdingTaxType == 1)
            {
                // Pre-calculate net amounts for proportional distribution
                foreach (var line in managerData.Lines ?? new List<ManagerInvoiceLine>())
                {
                    var quantity = line.Qty > 0 ? line.Qty : 1;
                    var totalAmount = quantity * line.SalesUnitPrice;
                    
                    var discountAmount = 0m;
                    if (managerData.Discount)
                    {
                        if (managerData.DiscountType == 1)
                        {
                            if (line.DiscountAmount > 0)
                            {
                                discountAmount = line.DiscountAmount;
                            }
                        }
                        else
                        {
                            if (line.DiscountPercentage > 0)
                            {
                                discountAmount = totalAmount * line.DiscountPercentage / 100;
                            }
                        }
                    }
                    
                    var netAmount = totalAmount - discountAmount;
                    lineNetAmounts.Add(netAmount);
                    totalNetAmount += netAmount;
                }
            }
            
            // Transform line items
            var lineIndex = 0;
            foreach (var line in managerData.Lines ?? new List<ManagerInvoiceLine>())
            {
                // Handle missing qty - default to 1 if not specified
                var quantity = line.Qty > 0 ? line.Qty : 1;
                var totalAmount = quantity * line.SalesUnitPrice;
                
                // Get tax rate from taxCode, fallback to 0% if not available
                var taxRate = line.TaxCode?.Rate ?? 0m;
                
                // Calculate discount based on discount type
                var discountAmount = 0m;
                if (managerData.Discount)
                {
                    // Check discountType to determine calculation method
                    if (managerData.DiscountType == 1) // Exact amount mode
                    {
                        // Use discountAmount as exact amount
                        if (line.DiscountAmount > 0)
                        {
                            discountAmount = line.DiscountAmount;
                        }
                    }
                    else // Rate mode (discountType is null or not 1)
                    {
                        // Use discountPercentage for rate-based calculation
                        if (line.DiscountPercentage > 0)
                        {
                            discountAmount = totalAmount * line.DiscountPercentage / 100;
                        }
                    }
                }
                
                // Calculate net amount after discount
                var netAmount = totalAmount - discountAmount;
                var taxAmount = netAmount * taxRate / 100;
                
                // Calculate withholding tax based on invoice level settings
                var withholdingTaxAmount = 0m;
                if (managerData.WithholdingTax)
                {
                    if (managerData.WithholdingTaxType == 1)
                    {
                        // Distribute fixed WHT amount proportionally based on net amount
                        if (totalNetAmount > 0)
                        {
                            var proportion = netAmount / totalNetAmount;
                            withholdingTaxAmount = Math.Round(managerData.WithholdingTaxAmount * proportion, 6);
                        }
                    }
                    else
                    {
                        // Use percentage calculation from netAmount per item (exclude VAT)
                        withholdingTaxAmount = Math.Round(netAmount * managerData.WithholdingTaxPercentage / 100, 6);
                    }
                }
                
                var item = new PralFbrLineItem
                {
                    HsCode = line.Item?.CustomFields2?.Strings?.GetValueOrDefault(FieldConstants.HS_CODE_FIELD) ?? string.Empty,
                    ProductDescription = !string.IsNullOrEmpty(line.Item?.ItemName) ? line.Item.ItemName : 
                                        (!string.IsNullOrEmpty(line.Item?.Name) ? line.Item.Name : string.Empty),
                    Rate = $"{taxRate}%",
                    UoM = line.Item?.CustomFields2?.Strings?.GetValueOrDefault(FieldConstants.UOM_FIELD) ?? line.Item?.UnitName ?? "Pieces",
                    Quantity = quantity,
                    TotalValues = netAmount, // + taxAmount - withholdingTaxAmount, // Total Sales Value (Including Tax, minus WHT)
                    ValueSalesExcludingST = netAmount,
                    FixedNotifiedValueOrRetailPrice = 0,
                    SalesTaxApplicable = taxAmount,
                    SalesTaxWithheldAtSource = withholdingTaxAmount,
                    ExtraTax = 0,
                    FurtherTax = 0,
                    SroScheduleNo = line.CustomFields2?.Strings?.GetValueOrDefault(FieldConstants.SRO_SCHEDULE_NO_FIELD) ?? line.Item?.CustomFields2?.Strings?.GetValueOrDefault(FieldConstants.SRO_SCHEDULE_NO_FIELD) ?? string.Empty,
                    FedPayable = 0,
                    Discount = discountAmount,
                    SaleType = line.CustomFields2?.Strings?.GetValueOrDefault(FieldConstants.SALE_TYPE_FIELD) ?? line.Item?.CustomFields2?.Strings?.GetValueOrDefault(FieldConstants.SALE_TYPE_FIELD) ??line.TaxCode?.Name ?? string.Empty,
                    SroItemSerialNo = line.CustomFields2?.Strings?.GetValueOrDefault(FieldConstants.SRO_ITEM_SERIAL_NO_FIELD) ?? line.Item?.CustomFields2?.Strings?.GetValueOrDefault(FieldConstants.SRO_ITEM_SERIAL_NO_FIELD) ?? string.Empty
                };
                request.Items.Add(item);
                lineIndex++;
            }

            return request;
        }
        
        private string ExtractCustomField(ManagerCustomFields2 customFields, string fieldId)
        {
            return customFields?.Strings?.GetValueOrDefault(fieldId) ?? string.Empty;
        }


        [HttpPost("ValidateInvoice")]
        public async Task<ActionResult<ApiResponse<object>>> ValidateInvoice([FromBody] SubmitInvoiceRequest request)
        {
            //_logger?.LogInformation("Starting FBR invoice validation process");

            try
            {
                if (request?.InvoiceData == null || request?.FbrInvoiceData == null)
                {
                    _logger?.LogWarning("FBR validation failed: Missing invoice data or FBR data");
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invoice data and FBR invoice data are required",
                        Errors = new List<string> { "Request body cannot be null" }
                    });
                }

                // Extract PRAL configuration from invoice data
                var environment = "Sandbox"; // Default
                var bearerToken = string.Empty;
                
                if (request.InvoiceData.BusinessDetails?.CustomFields2?.Strings != null)
                {
                    environment = ExtractCustomField(request.InvoiceData.BusinessDetails.CustomFields2, FieldConstants.ENVIRONMENT_FIELD) ?? "Sandbox";
                    bearerToken = ExtractCustomField(request.InvoiceData.BusinessDetails.CustomFields2, FieldConstants.BEARER_TOKEN_FIELD);
                }
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "PRAL configuration incomplete: Missing bearer token",
                        Errors = new List<string> { "Bearer token is required for PRAL FBR validation" }
                    });
                }
                
                // Determine the correct validation endpoint based on environment
                var validateEndpoint = environment?.Equals("Production", StringComparison.OrdinalIgnoreCase) == true
                    ? _configuration?["PralFbrEndpoints:Production:Validate"] ?? "https://gw.fbr.gov.pk/di_data/v1/di/validateinvoicedata"
                    : _configuration?["PralFbrEndpoints:Sandbox:Validate"] ?? "https://gw.fbr.gov.pk/di_data/v1/di/validateinvoicedata_sb";
                
                using var httpClient = _httpClientFactory.CreateClient("PralFbrClient");
                
                // Clear any existing headers
                httpClient.DefaultRequestHeaders.Clear();
                
                // Add request-specific headers
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                //httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                
                // Serialize FbrInvoiceData to JSON for the PRAL API
                var jsonContent = JsonConvert.SerializeObject(request.FbrInvoiceData, Formatting.Indented);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Log JSON payload
                Console.WriteLine($"=== VALIDATE INVOICE PAYLOAD ===");
                Console.WriteLine(jsonContent);
                Console.WriteLine($"=== END PAYLOAD ===");

                var response = await httpClient.PostAsync(validateEndpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Log JSON response
                Console.WriteLine($"=== VALIDATE INVOICE RESPONSE ===");
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine(responseContent);
                Console.WriteLine($"=== END RESPONSE ===");

                
                if (response.IsSuccessStatusCode)
                {
                    // Try to parse the response as JSON
                    object responseData;
                    try
                    {
                        responseData = JsonConvert.DeserializeObject(responseContent) ?? responseContent;
                    }
                    catch
                    {
                        responseData = responseContent;
                    }
                    
                    // Analyze response content to determine if validation was actually successful
                    bool isValidationSuccessful = AnalyzePralResponse(responseContent);
                    
                    return Ok(new ApiResponse<object>
                    {
                        Success = isValidationSuccessful,
                        Message = isValidationSuccessful 
                            ? "Validation successful, invoice is ready to be submitted" 
                            : "Validation failed, please review the server response for details",
                        Data = responseData
                    });
                }
                else
                {
                    // HTTP error - connection failed
                    object parsedErrorResponse;
                    try
                    {
                        parsedErrorResponse = JsonConvert.DeserializeObject(responseContent) ?? responseContent;
                    }
                    catch
                    {
                        parsedErrorResponse = responseContent;
                    }
                    
                    return Ok(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Connection failed to PRAL FBR server, please review the server response for details",
                        Data = parsedErrorResponse
                    });
                }
            }
            catch (TaskCanceledException ex)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Connection timeout to PRAL FBR server, please try again later",
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (HttpRequestException ex)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Connection failed to PRAL FBR server, please check your internet connection and try again",
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = "System error occurred during invoice validation",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("SubmitInvoice")]
        public async Task<ActionResult<ApiResponse<object>>> SubmitInvoice([FromBody] SubmitInvoiceRequest request)
        {
            //_logger?.LogInformation("Starting FBR invoice submission process");
            
            try
            {
               
                if (request?.InvoiceData == null || request?.FbrInvoiceData == null)
                {
                    _logger?.LogWarning("FBR submission failed: Missing invoice data or FBR data");
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invoice data and FBR invoice data are required",
                        Errors = new List<string> { "Request body cannot be null" }
                    });
                }

                // Extract PRAL configuration from invoice data
                var environment = "Sandbox"; // Default
                var bearerToken = string.Empty;
                
                if (request.InvoiceData.BusinessDetails?.CustomFields2?.Strings != null)
                {
                    environment = ExtractCustomField(request.InvoiceData.BusinessDetails.CustomFields2, FieldConstants.ENVIRONMENT_FIELD) ?? "Sandbox";
                    bearerToken = ExtractCustomField(request.InvoiceData.BusinessDetails.CustomFields2, FieldConstants.BEARER_TOKEN_FIELD);
                }
                            
                if (string.IsNullOrEmpty(bearerToken))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "PRAL configuration incomplete: Missing bearer token",
                        Errors = new List<string> { "Bearer token is required for PRAL FBR submission" }
                    });
                }
                
                // Determine the correct post endpoint based on environment
                var postEndpoint = environment?.Equals("Production", StringComparison.OrdinalIgnoreCase) == true
                    ? _configuration?["PralFbrEndpoints:Production:Post"] ?? "https://gw.fbr.gov.pk/di_data/v1/di/postinvoicedata"
                    : _configuration?["PralFbrEndpoints:Sandbox:Post"] ?? "https://gw.fbr.gov.pk/di_data/v1/di/postinvoicedata_sb";
                
                // Create HTTP client using factory with enhanced configuration for Azure environment
                using var httpClient = _httpClientFactory.CreateClient("PralFbrClient");
                
                // Clear any existing headers
                httpClient.DefaultRequestHeaders.Clear();
                
                // Add request-specific headers
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                
                var jsonContent = JsonConvert.SerializeObject(request.FbrInvoiceData, Formatting.Indented);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Log JSON payload
                Console.WriteLine($"=== SUBMIT INVOICE PAYLOAD ===");
                Console.WriteLine(jsonContent);
                Console.WriteLine($"=== END PAYLOAD ===");
                
                var response = await httpClient.PostAsync(postEndpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Log JSON response
                Console.WriteLine($"=== SUBMIT INVOICE RESPONSE ===");
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine(responseContent);
                Console.WriteLine($"=== END RESPONSE ===");
                
                if (response.IsSuccessStatusCode)
                {
                    // Try to parse the response as JSON
                    object responseData;
                    try
                    {
                        responseData = JsonConvert.DeserializeObject(responseContent) ?? responseContent;
                    }
                    catch
                    {
                        responseData = responseContent;
                    }
                    
                    // Analyze response content to determine if submission was actually successful
                    bool isSubmissionSuccessful = AnalyzePralResponse(responseContent);
                    
                    // Add invoice key to response data
                    var responseWithKey = new
                    {
                        invoiceKey = request?.InvoiceData?.PageResponseData?.Query?.Key,
                        pralServerResponse = responseData
                    };
                    
                    return Ok(new ApiResponse<object>
                    {
                        Success = isSubmissionSuccessful,
                        Message = isSubmissionSuccessful 
                            ? "Invoice submitted successfully to PRAL FBR" 
                            : "Invoice submission failed, please review the server response for details",
                        Data = responseWithKey
                    });
                }
                else
                {
                    // HTTP error - connection failed
                    object parsedErrorResponse;
                    try
                    {
                        parsedErrorResponse = JsonConvert.DeserializeObject(responseContent) ?? responseContent;
                    }
                    catch
                    {
                        parsedErrorResponse = responseContent;
                    }
                    
                    return Ok(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Connection failed to PRAL FBR server, please review the server response for details",
                        Data = parsedErrorResponse
                    });
                }
            }
            catch (TaskCanceledException ex)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Connection timeout to PRAL FBR server, please try again later",
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (HttpRequestException ex)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Connection failed to PRAL FBR server, please check your internet connection and try again",
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = "System error occurred during invoice submission",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        
        // Analyzes the PRAL FBR server response to determine if the operation was successful
        private bool AnalyzePralResponse(string responseContent)
        {
            if (string.IsNullOrEmpty(responseContent))
                return false;
                
            try
            {
                // Parse as dynamic JSON to handle both validation and submission response structures
                dynamic response = JsonConvert.DeserializeObject(responseContent);
                
                // Both validation and submission responses use validationResponse structure
                if (response?.validationResponse != null)
                {
                    // Check the statusCode - "00" means successful
                    string statusCode = response.validationResponse.statusCode?.ToString() ?? "";
                    return statusCode == "00";
                }
                
                return false;
            }
            catch
            {
                // If JSON parsing fails, return false
                return false;
            }
        }
    }
}