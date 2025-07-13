// Function to send resize message to parent
function sendResize() {
    window.parent.postMessage({
        type: "resize",
        width: document.documentElement.scrollWidth + 1,
        height: document.documentElement.scrollHeight + 1
    }, "*");
}

// Function to make API calls to our backend

async function apiCall(endpoint, method = 'GET', data = null) {
    const options = {
        method: method,
        headers: {
            'Content-Type': 'application/json'
        }
    };
    if (data) {
        options.body = JSON.stringify(data);
    }
    try {
        // Use relative URL to avoid port conflicts
        const apiUrl = `/api/PralFbr/${endpoint}`;
        
        const response = await fetch(apiUrl, options);
        
        if (!response.ok) {
            let errorDetails = '';
            try {
                const errorText = await response.text();
                if (errorText) {
                    errorDetails = ` - ${errorText.substring(0, 200)}`;
                }
            } catch (e) {
                // Ignore error text parsing errors
            }
            const errorMsg = `HTTP ${response.status}: ${response.statusText}${errorDetails}`;
            throw new Error(errorMsg);
        }
        
        const contentType = response.headers.get('content-type');
        if (!contentType || !contentType.includes('application/json')) {
            const text = await response.text();
            throw new Error(`Invalid response format. Expected JSON, got: ${text.substring(0, 100)}`);
        }
        
        const text = await response.text();
        if (!text.trim()) {
            throw new Error('Empty response received from server');
        }
        
        let parsedResponse;
        try {
            parsedResponse = JSON.parse(text);
        } catch (parseError) {
            throw new Error(`Failed to parse JSON response: ${parseError.message}`);
        }
       
        return parsedResponse;
    } catch (error) {
        if (error instanceof SyntaxError) {
            throw new Error(`Invalid JSON response: ${error.message}`);
        }
        
        throw error;
    }
}

// Function to make API request to Manager via postMessage
function makeManagerApiRequest(path, method = 'GET', body = null) {

    return new Promise(function(resolve, reject) {
        const reqId = generateRequestId();
        function handler(event) {
            if (event.source !== window.parent) {
                return;
            }
            if (!event.data || event.data.requestId !== reqId) {
                return;
            }
            
            window.removeEventListener("message", handler);
            
            if (event.data.status && event.data.status >= 400) {
                const errorMsg = `HTTP ${event.data.status}: ${event.data.statusText || 'Request failed'}`;
                reject(new Error(errorMsg));
            } else {
                // Return the body directly as before
                resolve(event.data.body);
            }
        }
        
        window.addEventListener("message", handler);
        
        const message = { 
            type: "api-request", 
            requestId: reqId, 
            path: path, 
            method: method
        };
        
        if (body) {
            message.body = body;
        }
        
        window.parent.postMessage(message, "*");
    });
}

// Function to convert Manager invoice to FBR format using server-side conversion

async function convertToFbrFormat(invoiceData) {
    try {
        const response = await apiCall('convert-to-fbr', 'POST', invoiceData);
        
        if (response.success) {
            return response.data;
        } else {
            const errorMessage = response.message || 'Failed to convert invoice to FBR format';
            const detailedErrors = response.errors ? `\nDetails: ${response.errors.join(', ')}` : '';
            throw new Error(errorMessage + detailedErrors);
        }
    } catch (error) {
        throw error;
    }
}

// Validate invoice with PRAL FBR

async function validateInvoice(invoiceData, fbrInvoiceData) {
    const payload = {
        InvoiceData: invoiceData,
        FbrInvoiceData: fbrInvoiceData
    };

    try {
        const response = await apiCall('ValidateInvoice', 'POST', payload);
        return response;
    } catch (error) {
        throw error;
    }
}

// Submit invoice to PRAL FBR

async function submitInvoice(invoiceData, fbrInvoiceData) {
    const payload = {
        InvoiceData: invoiceData,
        FbrInvoiceData: fbrInvoiceData
    };
    try {
        const response = await apiCall('SubmitInvoice', 'POST', payload);
        return response;
    } catch (error) {
        throw error;
    }
}