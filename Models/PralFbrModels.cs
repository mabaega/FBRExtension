using Newtonsoft.Json;

namespace FBRExtension.Models
{
    public class ManagerInvoiceData
    {
        [JsonProperty("issueDate")]
        public DateTime IssueDate { get; set; }
        
        [JsonProperty("reference")]
        public string Reference { get; set; } = string.Empty;
        
        [JsonProperty("customer")]
        public ManagerCustomer? Customer { get; set; }
       
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonProperty("lines")]
        public List<ManagerInvoiceLine>? Lines { get; set; }
        
        [JsonProperty("discount")]
        public bool Discount { get; set; }
        
        [JsonProperty("discountType")]
        public int DiscountType { get; set; }
        
        [JsonProperty("withholdingTax")]
        public bool WithholdingTax { get; set; }
        
        [JsonProperty("withholdingTaxType")]
        public int? WithholdingTaxType { get; set; }
        
        [JsonProperty("withholdingTaxPercentage")]
        public decimal WithholdingTaxPercentage { get; set; }
        
        [JsonProperty("withholdingTaxAmount")]
        public decimal WithholdingTaxAmount { get; set; }
        
        [JsonProperty("customFields2")]
        public ManagerCustomFields2? CustomFields2 { get; set; }
        
        //===================
        
        [JsonProperty("salesInvoice")]
        public SalesInvoiceData? SalesInvoice { get; set; }

        [JsonProperty("businessDetails")]
        public ManagerBusinessDetails? BusinessDetails { get; set; }

        [JsonProperty("pageResponseData")]
        public PageResponseData? PageResponseData { get; set; }
    }
    
    public class PageResponseData
    {
        [JsonProperty("handler")]
        public string Handler { get; set; } = string.Empty;
        
        [JsonProperty("query")]
        public QueryData? Query { get; set; }
    }
    
    public class QueryData
    {
        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;
        
        [JsonProperty("fileID")]
        public string FileID { get; set; } = string.Empty;
        
        [JsonProperty("referrer")]
        public string Referrer { get; set; } = string.Empty;
    }
    
    public class SalesInvoiceData
    {
        [JsonProperty("issueDate")]
        public DateTime IssueDate { get; set; }
        
        [JsonProperty("reference")]
        public string Reference { get; set; } = string.Empty;
        
        [JsonProperty("fbrInvoiceNumber")]
        public string FbrInvoiceNumber { get; set; } = string.Empty;
    }
    
    public class ManagerCustomer
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonProperty("customFields2")]
        public ManagerCustomFields2? CustomFields2 { get; set; }
    }
    
    public class ManagerBusinessDetails
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonProperty("country")]
        public string Country { get; set; } = string.Empty;
        
        [JsonProperty("customFields2")]
        public ManagerCustomFields2? CustomFields2 { get; set; }
    }
    
    public class ManagerInvoiceLine
    {
        [JsonProperty("item")]
        public ManagerItem? Item { get; set; }
        
        [JsonProperty("qty")]
        public decimal Qty { get; set; }
        
        [JsonProperty("salesUnitPrice")]
        public decimal SalesUnitPrice { get; set; }
        
        [JsonProperty("taxCode")]
        public ManagerTaxCode? TaxCode { get; set; }
        
        [JsonProperty("discountAmount")]
        public decimal DiscountAmount { get; set; }
        
        [JsonProperty("discountPercentage")]
        public decimal DiscountPercentage { get; set; }
        
        [JsonProperty("customFields2")]
        public ManagerCustomFields2? CustomFields2 { get; set; }
    }
    
    public class ManagerItem
    {
        [JsonProperty("itemName")]
        public string ItemName { get; set; } = string.Empty;
        
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonProperty("unitName")]
        public string UnitName { get; set; } = string.Empty;
        
        [JsonProperty("customFields2")]
        public ManagerCustomFields2? CustomFields2 { get; set; }
    }
    
    public class ManagerTaxCode
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonProperty("label")]
        public string Label { get; set; } = string.Empty;
        
        [JsonProperty("taxRate")]
        public decimal TaxRate { get; set; }
        
        [JsonProperty("rate")]
        public decimal Rate { get; set; }
        
        [JsonProperty("account")]
        public string Account { get; set; } = string.Empty;
        
        [JsonProperty("customFields2")]
        public ManagerCustomFields2? CustomFields2 { get; set; }
    }
    
    public class ManagerCustomFields2
    {
        [JsonProperty("strings")]
        public Dictionary<string, string>? Strings { get; set; }
        
        [JsonProperty("decimals")]
        public Dictionary<string, decimal>? Decimals { get; set; }
        
        [JsonProperty("dates")]
        public Dictionary<string, DateTime>? Dates { get; set; }
        
        [JsonProperty("booleans")]
        public Dictionary<string, bool>? Booleans { get; set; }

        [JsonProperty("images")]
        public Dictionary<string, string>? Images { get; set; }
    }
    
   
    // PRAL FBR API Models
    public class PralFbrRequest
    {
        [JsonProperty("invoiceType")]
        public string InvoiceType { get; set; } = "Sale Invoice";
        
        [JsonProperty("invoiceDate")]
        public string InvoiceDate { get; set; } = string.Empty;
        
        [JsonProperty("sellerNTNCNIC")]
        public string SellerNTNCNIC { get; set; } = string.Empty;
        
        [JsonProperty("sellerBusinessName")]
        public string SellerBusinessName { get; set; } = string.Empty;
        
        [JsonProperty("sellerProvince")]
        public string SellerProvince { get; set; } = string.Empty;
        
        [JsonProperty("sellerAddress")]
        public string SellerAddress { get; set; } = string.Empty;
        
        [JsonProperty("buyerNTNCNIC")]
        public string BuyerNTNCNIC { get; set; } = string.Empty;
        
        [JsonProperty("buyerBusinessName")]
        public string BuyerBusinessName { get; set; } = string.Empty;
        
        [JsonProperty("buyerProvince")]
        public string BuyerProvince { get; set; } = string.Empty;
        
        [JsonProperty("buyerAddress")]
        public string BuyerAddress { get; set; } = string.Empty;
        
        [JsonProperty("buyerRegistrationType")]
        public string BuyerRegistrationType { get; set; } = string.Empty;
        
        [JsonProperty("invoiceRefNo")]
        public string InvoiceRefNo { get; set; } = string.Empty;
        
        [JsonProperty("scenarioId")]
        public string ScenarioId { get; set; } = string.Empty;
        
        [JsonProperty("items")]
        public List<PralFbrLineItem> Items { get; set; } = new List<PralFbrLineItem>();
    }
    
    public class PralFbrLineItem
    {
        [JsonProperty("hsCode")]
        public string HsCode { get; set; } = string.Empty;
        
        [JsonProperty("productDescription")]
        public string ProductDescription { get; set; } = string.Empty;
        
        [JsonProperty("rate")]
        public string Rate { get; set; } = string.Empty;
        
        [JsonProperty("uoM")]
        public string UoM { get; set; } = string.Empty;
        
        [JsonProperty("quantity")]
        public decimal Quantity { get; set; }
        
        [JsonProperty("totalValues")]
        public decimal TotalValues { get; set; }
        
        [JsonProperty("valueSalesExcludingST")]
        public decimal ValueSalesExcludingST { get; set; }
        
        [JsonProperty("fixedNotifiedValueOrRetailPrice")]
        public decimal FixedNotifiedValueOrRetailPrice { get; set; }
        
        [JsonProperty("salesTaxApplicable")]
        public decimal SalesTaxApplicable { get; set; }
        
        [JsonProperty("salesTaxWithheldAtSource")]
        public decimal SalesTaxWithheldAtSource { get; set; }
        
        [JsonProperty("extraTax")]
        public decimal ExtraTax { get; set; } = 0;
        
        [JsonProperty("furtherTax")]
        public decimal FurtherTax { get; set; }
        
        [JsonProperty("sroScheduleNo")]
        public string SroScheduleNo { get; set; } = string.Empty;
        
        [JsonProperty("fedPayable")]
        public decimal FedPayable { get; set; }
        
        [JsonProperty("discount")]
        public decimal Discount { get; set; }
        
        [JsonProperty("saleType")]
        public string SaleType { get; set; } = string.Empty;
        
        [JsonProperty("sroItemSerialNo")]
        public string SroItemSerialNo { get; set; } = string.Empty;
    }
    
        
    public class ApiResponse<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        
        [JsonProperty("data")]
        public T? Data { get; set; }
        
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        
        [JsonProperty("errors")]
        public List<string>? Errors { get; set; }
    }
    
    public class SubmitInvoiceRequest
    {
        [JsonProperty("invoiceData")]
        public ManagerInvoiceData InvoiceData { get; set; } = new ManagerInvoiceData();
        
        [JsonProperty("fbrInvoiceData")]
        public PralFbrRequest FbrInvoiceData { get; set; } = new PralFbrRequest();
    }
    
}