// Data enrichment functions for PRAL FBR Extension

// Function to fetch business details

async function fetchBusinessDetails(invoice) {
    try {
        const businessDetail = await makeManagerApiRequest("/api3/business-details-form");

        invoice.businessDetails = businessDetail;
    } catch (e) { 
 
    }
}

// Function to fetch customer details

async function fetchCustomerDetails(invoice) {
    if (invoice.customer) {
        try {
            const customerDetail = await makeManagerApiRequest("/api3/customer-form/" + invoice.customer);
    
            invoice.customer = customerDetail;
        } catch (e) { 
     
        }
    }
}

// Function to fetch item details with multi-endpoint support

async function fetchItemDetails(line, lineIndex) {
    if (line.item) {
        try {
            // Try multiple endpoints for item details
            const itemEndpoints = [
                "/api3/inventory-item-form/" + line.item,
                "/api3/non-inventory-item-form/" + line.item,
                "/api3/inventory-kit-form/" + line.item
            ];
            
            let itemDetail = null;
            let lastError = null;
            
            for (const endpoint of itemEndpoints) {
                try {
                    itemDetail = await makeManagerApiRequest(endpoint);
                    
                    if (itemDetail) {
            
                    break; // Found item, exit loop
                }
            } catch (e) {
                lastError = e;
                continue; // Try next endpoint
            }
        }
        
        if (itemDetail) {
            line.item = itemDetail;
        } else {

        }
        } catch (e) { 
     
        }
    }
}

// Function to fetch tax code details

async function fetchTaxCodeDetails(line, lineIndex) {
    if (line.taxCode) {
        try {
            const taxCodeDetail = await makeManagerApiRequest("/api3/tax-code-form/" + line.taxCode);
    
            line.taxCode = taxCodeDetail;
        } catch (e) { 
     
        }
    }
}

// Function to fetch sales invoice details for credit note

async function fetchSalesInvoiceForCreditNote(invoice) {
    if (invoice.salesInvoice) {
        try {
            const salesInvoiceDetail = await makeManagerApiRequest("/api3/sales-invoice-form/" + invoice.salesInvoice);
            
            if (salesInvoiceDetail) {
                // Extract only required fields: issueDate, reference, and customFields2.strings with specific key
                const extractedData = {
                    issueDate: salesInvoiceDetail.issueDate,
                    reference: salesInvoiceDetail.reference
                };
                
                // Extract specific customFields2.strings field (FBR Invoice Number)
                if (salesInvoiceDetail.customFields2 && 
                    salesInvoiceDetail.customFields2.strings && 
                    salesInvoiceDetail.customFields2.strings["24b11f97-46d7-472e-ad4b-e99fbed9197e"]) {
                    extractedData.fbrInvoiceNumber = salesInvoiceDetail.customFields2.strings["24b11f97-46d7-472e-ad4b-e99fbed9197e"];
                }
                
                // Replace the GUID with extracted invoice data
                invoice.salesInvoice = extractedData;
            }
        } catch (e) {
            console.error('Error fetching sales invoice for credit note:', e);
        }
    }
}

// Function to enrich invoice with all related data

async function enrichInvoiceData(invoice) {
    
    // Check if this is a credit note and fetch related sales invoice data
    await fetchSalesInvoiceForCreditNote(invoice);
    
    // Fetch business details for Seller information
    await fetchBusinessDetails(invoice);
    
    // Fetch customer details
    await fetchCustomerDetails(invoice);
    
    // Fetch item and tax code details for each line
    if (Array.isArray(invoice.lines)) {
        for (let i = 0; i < invoice.lines.length; i++) {
            const line = invoice.lines[i];
            
            // Fetch item details
            await fetchItemDetails(line, i);
            
            // Fetch tax code details
            await fetchTaxCodeDetails(line, i);
        }
    }
    
    return invoice;
}