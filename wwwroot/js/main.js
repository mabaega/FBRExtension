// Global variables
let currentInvoiceData = null;
let originalBusinessDataJson = null; // Store original business data for future updates to Manager
let fbrInvoiceData = null; // Store converted FBR invoice data

// Field ID constants for invoice updates - using GUID directly for better performance
const QR_CODE_FIELD_ID = 'd2e9265a-460e-4a06-83f9-29a523a4d516';
const FBR_INVOICE_NUMBER_FIELD_ID = '24b11f97-46d7-472e-ad4b-e99fbed9197e';

// Function to switch tabs (for invoice sub-tabs)
function switchTab(tabName) {
    // Remove active class from all invoice tab buttons and panes
    document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
    document.querySelectorAll('.tab-pane').forEach(pane => pane.classList.remove('active'));
    
    // Add active class to selected tab button and pane
    const tabButton = document.querySelector(`.tab-btn[data-tab="${tabName}"]`);
    const tabPane = document.getElementById(tabName);
    
    if (tabButton) {
        tabButton.classList.add('active');
    }
    
    if (tabPane) {
        tabPane.classList.add('active');
    }
    
    sendResize();
}

// Function to reset button states to initial condition
function resetButtonStates() {
    const validateBtn = document.getElementById('validate-invoice-btn');
    const submitBtn = document.getElementById('submit-invoice-btn');
    
    // Reset validate button - initially disabled and hidden until conversion succeeds
    if (validateBtn) {
        validateBtn.disabled = true;
        validateBtn.style.display = 'none';
        validateBtn.innerHTML = '<i class="fas fa-check-circle"></i> Validate Invoice';
    }
    
    // Reset submit button - initially disabled and hidden
    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.style.display = 'none';
        submitBtn.innerHTML = '<i class="fas fa-paper-plane"></i> Submit Invoice to PRAL FBR';
    }
}

// Function to hide submit button - with error handling
function hideSubmitButton() {
    try {
        const submitBtn = document.getElementById('submit-invoice-btn');
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.style.display = 'none';
            submitBtn.innerHTML = '<i class="fas fa-paper-plane"></i> Submit Invoice to PRAL FBR';
        }
    } catch (error) {
        console.error('Error in hideSubmitButton:', error);
    }
}

// Function to show validate button - with error handling
function showValidateButton() {
    try {
        const validateBtn = document.getElementById('validate-invoice-btn');
        if (validateBtn) {
            validateBtn.disabled = false;
            validateBtn.style.display = 'inline-block';
            validateBtn.innerHTML = '<i class="fas fa-check-circle"></i> Validate Invoice';
        }
    } catch (error) {
        console.error('Error in showValidateButton:', error);
    }
}

// Make functions globally available to prevent ReferenceError
window.hideSubmitButton = hideSubmitButton;
window.showValidateButton = showValidateButton;

// Event handler for invoice data received from Manager

async function handleInvoiceData(invoice) {
    
    if (invoice) {
        // Check if invoice already has FBR Invoice Number to prevent duplicate reporting
        const existingInvoiceNumber = getFbrInvoiceNumber(invoice);
        if (existingInvoiceNumber) {
            
            // Redirect to dedicated page for already reported invoices
            const redirectUrl = createAlreadyReportedUrl(existingInvoiceNumber, invoice.key);
            window.location.href = redirectUrl;
            return; // Exit early to prevent further processing
        }
        
        // Reset UI state when loading new invoice data
        resetButtonStates();
        
        // Clear previous server response
        const serverResponseContainer = document.getElementById('server-response-data');
        if (serverResponseContainer) {
            serverResponseContainer.value = '';
        }
        
        // Store original business data JSON for future updates to Manager
        originalBusinessDataJson = JSON.parse(JSON.stringify(invoice));
        
        // Store the invoice data for processing
        currentInvoiceData = invoice;
        
        // Add page response data to invoice if available (similar to businessDetails)
        if (window.pageResponseData) {
            invoice.pageResponseData = window.pageResponseData;
        }
        
        // Enrich invoice with related data
        await enrichInvoiceData(invoice);
        
        // Update current invoice data with enriched version
        currentInvoiceData = invoice;
        
        try {
            // Convert to FBR format using server-side conversion
            fbrInvoiceData = await convertToFbrFormat(invoice);
            
            // Display Manager invoice in first tab
            const managerContainer = document.getElementById('invoice-data');
            if (managerContainer) {
                managerContainer.value = JSON.stringify(invoice, null, 2);
            }
            
            // Display FBR invoice in second tab
            const fbrContainer = document.getElementById('fbr-invoice-data');
            if (fbrContainer) {
                fbrContainer.value = JSON.stringify(fbrInvoiceData, null, 2);
            }
            
            const submitBtn = document.getElementById('submit-invoice-btn');
            const validateBtn = document.getElementById('validate-invoice-btn');
            
            if (validateBtn) {
                validateBtn.disabled = false;
                validateBtn.style.display = 'inline-block';
            }
            
            // Keep submit button disabled and hidden until validation succeeds
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.style.display = 'none';
            }
            
            // Switch to first tab
            switchTab('manager-invoice');
    
        } catch (error) {
            const managerContainer = document.getElementById('invoice-data');
            if (managerContainer) {
                managerContainer.value = JSON.stringify(invoice, null, 2);
            }
            
            // Show error in FBR tab
            const fbrContainer = document.getElementById('fbr-invoice-data');
            if (fbrContainer) {
                fbrContainer.value = `Failed to convert to FBR format: ${error.message}`;
            }
            
            // Hide submit button if conversion fails (validate button will be enabled later)
            const submitBtn = document.getElementById('submit-invoice-btn');
            
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.style.display = 'none';
            }
            
            // Switch to first tab
            switchTab('manager-invoice');
        }
       
        sendResize();
    }
}

// Event handler for request invoice button
function handleRequestInvoiceClick() {
    window.parent.postMessage({ type: "page-request" }, "*");
}

// Event handler for validate invoice button

async function handleValidateInvoiceClick() {
    const validateBtn = document.getElementById('validate-invoice-btn');
    const serverResponseContainer = document.getElementById('server-response-data');
    
    if (!currentInvoiceData) {
        Swal.fire({
            icon: 'warning',
            title: 'No Data',
            text: 'No invoice data available to validate'
        });
        return;
    }
    
    try {
        // Disable validate button and show loading
        validateBtn.disabled = true;
        validateBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Validating...';
        
        // Show loading message in server response tab
        if (serverResponseContainer) {
            serverResponseContainer.value = "Validating Invoice with PRAL FBR...\nPlease wait while we validate your invoice data.";
        }
        
        // Switch to server response tab
        switchTab('server-response');
        
        // Check if fbrInvoiceData is available
        if (!fbrInvoiceData) {
            Swal.fire({
                icon: 'warning',
                title: 'No FBR Data',
                text: 'FBR invoice data is not available. Please reload the invoice.'
            });
            return;
        }

        // Call our server API to validate invoice with PRAL FBR
        const response = await validateInvoice(currentInvoiceData, fbrInvoiceData);
        
        // Display server response
        if (serverResponseContainer) {
            serverResponseContainer.value = JSON.stringify(response, null, 2);
        }
        
        // Check if validation was successful
        if (response && response.success === true) {
            // Hide validate button and show submit button when validation is successful
            const validateBtn = document.getElementById('validate-invoice-btn');
            const submitBtn = document.getElementById('submit-invoice-btn');
            
            if (validateBtn) {
                validateBtn.style.display = 'none';
            }
            
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.style.display = 'inline-block';
            }
            
            Swal.fire({
                icon: 'success',
                title: 'Validation Successful',
                text: response?.message || 'Invoice has been validated successfully with PRAL FBR'
            });
        } else {
            // Hide submit button if validation failed, keep validate button visible
            const submitBtn = document.getElementById('submit-invoice-btn');
            if (submitBtn) {
                submitBtn.style.display = 'none';
            }
            
            Swal.fire({
                icon: 'error',
                title: 'Validation Failed',
                text: response?.message || 'Invoice validation failed'
            });
        }
        
    } catch (error) {
        if (serverResponseContainer) {
            serverResponseContainer.value = `Error: ${error.message || 'Unknown error occurred'}`;
        }
       
        // Hide submit button on error
        const submitBtn = document.getElementById('submit-invoice-btn');
        if (submitBtn) {
            submitBtn.style.display = 'none';
        }
        
        Swal.fire({
            icon: 'error',
            title: 'Validation Error',
            text: error.message || 'An error occurred during validation'
        });
    } finally {
        // Re-enable validate button only if it's still visible (validation failed or error occurred)
        const validateBtn = document.getElementById('validate-invoice-btn');
        if (validateBtn && validateBtn.style.display !== 'none') {
            validateBtn.disabled = false;
            validateBtn.innerHTML = '<i class="fas fa-check-circle"></i> Validate Invoice';
        }
    }
}

// Function to generate QR code SVG containing invoice number
function generateInvoiceQrSvg(invoiceNumber) {
    try {
        const qr = qrcode(0, 'L'); // Error correction level 'L' (Low)
        qr.addData(invoiceNumber);
        qr.make();
        return qr.createSvgTag({ scalable: true });
    } catch (error) {
        console.error('Error generating QR code:', error);
        throw new Error('Failed to generate QR code: ' + error.message);
    }
}

// Update only the invoice number in Manager

async function updateInvoiceNumber(invoiceKey, invoiceNumber) {
    try {
        // Update the invoice in Manager with invoice number only
        const updateResponse = await makeManagerApiRequest(`/api3/sales-invoice-form/${invoiceKey}`, 'PATCH', {
            customFields2: {
                strings: {
                    [FBR_INVOICE_NUMBER_FIELD_ID]: invoiceNumber
                }
            }
        });
        
        // Manager API returns status 200/202 for success, not a success property
        // If we get here without error, the update was successful
        return { success: true, message: 'Invoice number updated successfully' };
        
    } catch (error) {
        let detailedError = new Error(`Manager Invoice Number Update Failed: ${error.message}`);
        detailedError.originalError = error;
        detailedError.invoiceNumber = invoiceNumber;
        detailedError.invoiceKey = invoiceKey;
        
        throw detailedError;
    }
}

// Update QR code in Manager (separate function)

async function updateInvoiceQrCode(invoiceKey, invoiceNumber) {
    try {
        // Ensure QR Code custom field exists
        let qrField = await makeManagerApiRequest(`/api3/image-custom-field-form/${QR_CODE_FIELD_ID}`, 'GET');
        if (qrField.status === 404) {
            await makeManagerApiRequest(`/api3/image-custom-field-form/${QR_CODE_FIELD_ID}`, 'PATCH', {
                name: "QR Code",
                height: 100,
                width: 100,
                position: 5,
                placement: [
                    "ad12b60b-23bf-4421-94df-8be79cef533e",
                    "245e5943-0092-409d-96ae-e2ee10eac75b"
                ],
                displayOnView: true,
                excludeFromCopyingOrCloning: true
            });
        }
        
        // Generate QR code SVG
        const qrSvg = generateInvoiceQrSvg(invoiceNumber);
        const qrBase64 = btoa(qrSvg);
        
        // Upload QR code as blob
        const blobResponse = await makeManagerApiRequest('/api3/blobs', 'POST', {
            name: `${invoiceKey}-${invoiceNumber}`,
            contentType: "image/svg+xml",
            content: qrBase64
        });
        
        if (!blobResponse) {
            throw new Error('Failed to upload QR code blob: No response received');
        }
        
        const blobGuid = blobResponse;
        
        // Update the invoice in Manager with QR code only
        const updateResponse = await makeManagerApiRequest(`/api3/sales-invoice-form/${invoiceKey}`, 'PATCH', {
            customFields2: {
                images: {
                    [QR_CODE_FIELD_ID]: blobGuid
                }
            }
        });

        return { success: true, message: 'QR code updated successfully' };
        
    } catch (error) {
        let detailedError = new Error(`Manager QR Code Update Failed: ${error.message}`);
        detailedError.originalError = error;
        detailedError.invoiceNumber = invoiceNumber;
        detailedError.invoiceKey = invoiceKey;
        
        throw detailedError;
    }
}

// Event handler for submit invoice button

async function handleSubmitInvoiceClick() {
    const submitBtn = document.getElementById('submit-invoice-btn');
    const serverResponseContainer = document.getElementById('server-response-data');
    
    if (!fbrInvoiceData) {
        Swal.fire({
            icon: 'warning',
            title: 'No Data',
            text: 'No validated invoice data available to submit'
        });
        return;
    }
    
    try {
        // Disable submit button and show loading
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Submitting...';
        
        // Show loading message in server response tab
        if (serverResponseContainer) {
            serverResponseContainer.value = "Submitting Invoice to PRAL FBR...\nPlease wait while we submit your invoice.";
        }
        
        // Switch to server response tab
        switchTab('server-response');
        
        // Call our server API to submit invoice to PRAL FBR
        const response = await submitInvoice(currentInvoiceData, fbrInvoiceData);
        
        // Display server response
        if (serverResponseContainer) {
            serverResponseContainer.value = JSON.stringify(response, null, 2);
        }
        
        // Check if submission was successful
        if (response && response.success === true) {
            // Extract invoice number from pralServerResponse root level (structure is predictable)
            const invoiceNumber = response.data?.pralServerResponse?.invoiceNumber;
            const invoiceKey = response.data?.invoiceKey;
            
            console.log('Extracted invoice number:', invoiceNumber);

            // Try to update Manager
            let invoiceUpdateSuccess = false;
            let qrUpdateSuccess = false;
            let updateErrors = [];
            
            try {
                await updateInvoiceNumber(invoiceKey, invoiceNumber);
                invoiceUpdateSuccess = true;
            } catch (error) {
                updateErrors.push(`Invoice: ${error.message}`);
            }
            
            try {
                await updateInvoiceQrCode(invoiceKey, invoiceNumber);
                qrUpdateSuccess = true;
            } catch (error) {
                updateErrors.push(`QR Code: ${error.message}`);
            }
            
            // Show result based on update success
            const bothSuccess = invoiceUpdateSuccess && qrUpdateSuccess;
            const partialSuccess = invoiceUpdateSuccess || qrUpdateSuccess;
            
            await Swal.fire({
                icon: bothSuccess ? 'success' : 'warning',
                title: bothSuccess ? 'Submission Successful' : 'Submission Successful - Manual Action Required',
                html: `
                    <div style="text-align: center; line-height: 1.5; font-size: 14px; color: #495057;">
                        <p><strong>Invoice submitted to PRAL FBR successfully</strong></p>
                        <hr style="margin: 12px 0; border: none; border-top: 1px solid #dee2e6;">
                        ${invoiceUpdateSuccess ? '<p>Manager invoice updated successfully</p>' : '<p>Invoice number update failed</p>'}
                        ${qrUpdateSuccess ? '<p>QR code added successfully</p>' : '<p>QR code update failed</p>'}
                        <div style="background: #f8f9fa; padding: 12px; border-radius: 6px; margin: 15px 0; border-left: 4px solid #2c5aa0;">
                            <strong>FBR Invoice Number: ${invoiceNumber}</strong>
                        </div>
                        ${!bothSuccess ? '<p style="color: #666; font-size: 13px;">You may need to manually update Manager if automatic updates failed.</p>' : ''}
                        ${updateErrors.length > 0 ? `<p style="color: #666; font-size: 13px;">Errors: ${updateErrors.join(', ')}</p>` : ''}
                    </div>
                `,
                confirmButtonText: bothSuccess ? 'OK' : 'I will update manually'
            });
            
            window.location.reload();
        } else {
            // Hide submit button and show validate button again if submission failed
            try {
                hideSubmitButton();
                showValidateButton();
            } catch (error) {
                console.error('Error calling button functions:', error);
                // Fallback: directly manipulate buttons if functions fail
                try {
                    const submitBtn = document.getElementById('submit-invoice-btn');
                    const validateBtn = document.getElementById('validate-invoice-btn');
                    if (submitBtn) {
                        submitBtn.disabled = true;
                        submitBtn.style.display = 'none';
                    }
                    if (validateBtn) {
                        validateBtn.disabled = false;
                        validateBtn.style.display = 'inline-block';
                    }
                } catch (fallbackError) {
                    console.error('Fallback button manipulation also failed:', fallbackError);
                }
            }
            
            // Display detailed error information in server response
            if (serverResponseContainer) {
                let errorDetails = "=== SUBMISSION FAILED ===\n\n";
                errorDetails += `Status: ${response?.success ? 'Success' : 'Failed'}\n`;
                errorDetails += `Message: ${response?.message || 'No message provided'}\n\n`;
                
                if (response?.errors && Array.isArray(response.errors)) {
                    errorDetails += "Errors:\n";
                    response.errors.forEach((error, index) => {
                        errorDetails += `${index + 1}. ${error}\n`;
                    });
                    errorDetails += "\n";
                }
                
                errorDetails += "Full Server Response:\n";
                errorDetails += JSON.stringify(response, null, 2);
                
                serverResponseContainer.value = errorDetails;
            }
            
            // Show error message with more details
            let errorMessage = response?.message || 'Invoice submission failed';
            let errorHtml = `<div style="text-align: center; line-height: 1.5; font-size: 14px; color: #495057;">
                <p>${errorMessage}</p>`;
            
            if (response?.errors && Array.isArray(response.errors) && response.errors.length > 0) {
                errorHtml += `<hr style="margin: 12px 0; border: none; border-top: 1px solid #dee2e6;">
                <p><strong>Details:</strong></p>`;
                response.errors.forEach(error => {
                    errorHtml += `<p>${error}</p>`;
                });
            }
            
            errorHtml += `</div>`;
            
            Swal.fire({
                icon: 'error',
                title: 'Submission Failed',
                html: errorHtml,
                footer: 'Check the Server Response tab for detailed information'
            });
        }
        
    } catch (error) {
        console.error('Error submitting invoice:', error);
        
        // Display detailed error information in server response
        if (serverResponseContainer) {
            let errorDetails = "=== SUBMISSION ERROR ===\n\n";
            errorDetails += `Error Type: ${error.name || 'Unknown Error'}\n`;
            errorDetails += `Error Message: ${error.message || 'Unknown error occurred'}\n\n`;
            
            if (error.stack) {
                errorDetails += "Stack Trace:\n";
                errorDetails += error.stack + "\n\n";
            }
            
            errorDetails += "Error Object:\n";
            errorDetails += JSON.stringify(error, Object.getOwnPropertyNames(error), 2);
            
            serverResponseContainer.value = errorDetails;
        }
        
        // Hide submit button and show validate button again on error
        try {
            hideSubmitButton();
            showValidateButton();
        } catch (error) {
            console.error('Error calling button functions in catch block:', error);
            // Fallback: directly manipulate buttons if functions fail
            try {
                const submitBtn = document.getElementById('submit-invoice-btn');
                const validateBtn = document.getElementById('validate-invoice-btn');
                if (submitBtn) {
                    submitBtn.disabled = true;
                    submitBtn.style.display = 'none';
                }
                if (validateBtn) {
                    validateBtn.disabled = false;
                    validateBtn.style.display = 'inline-block';
                }
            } catch (fallbackError) {
                console.error('Fallback button manipulation also failed in catch block:', fallbackError);
            }
        }
        
        Swal.fire({
            icon: 'error',
            title: 'Submission Error',
            text: error.message || 'An error occurred during submission',
            footer: 'Check the Server Response tab for detailed error information'
        });
    } finally {
        // Reset submit button text
        submitBtn.innerHTML = '<i class="fas fa-paper-plane"></i> Submit Invoice to PRAL FBR';
    }
}

// Event handler for page response from Manager
function handlePageResponse(event) {
    if (event.data && event.data.type === "page-response") {
        // Store page response data globally for use in invoice processing
        window.pageResponseData = event.data;
        var invoiceKey = event.data.body && event.data.body.query && event.data.body.query.key;
        if (invoiceKey) {
    
            var viewRequestId = generateRequestId();
            function viewApiHandler(event) {
                if (event.source !== window.parent) return;
                if (!event.data || event.data.requestId !== viewRequestId) return;
                
                window.removeEventListener("message", viewApiHandler);
            }
            
            window.addEventListener("message", viewApiHandler);
            window.parent.postMessage({ 
                type: "api-request", 
                requestId: viewRequestId, 
                path: "/api3/sales-invoice-view/" + invoiceKey, 
                method: "GET" 
            }, "*");
            
            // Make API request for sales-invoice-form (main data)
            var formRequestId = generateRequestId();
            function formApiHandler(event) {
                if (event.source !== window.parent) return;
                if (!event.data || event.data.requestId !== formRequestId) return;
        
                window.removeEventListener("message", formApiHandler);
                
                // Add page response data to invoice before processing
                var invoiceData = event.data.body;
                if (window.pageResponseData) {
                    invoiceData.pageResponseData = window.pageResponseData;
                }
                
                // Process the form data
                handleInvoiceData(invoiceData);
            }
            
            window.addEventListener("message", formApiHandler);
            window.parent.postMessage({ 
                type: "api-request", 
                requestId: formRequestId, 
                path: "/api3/sales-invoice-form/" + invoiceKey, 
                method: "GET" 
            }, "*");
        } else {
            // Add page response data to invoice before processing
              var invoiceData = event.data.body;
              if (window.pageResponseData) {
                  invoiceData.pageResponseData = window.pageResponseData;
              }
            handleInvoiceData(invoiceData);
        }
    }
}

// Initialize event listeners
function initializeEventListeners() {
    // Request invoice button
    const requestBtn = document.getElementById('request-invoice-btn');
    if (requestBtn) {
        requestBtn.disabled = false;
        requestBtn.addEventListener("click", handleRequestInvoiceClick);
    }
    
    // Validate invoice button
    const validateBtn = document.getElementById('validate-invoice-btn');
    if (validateBtn) {
        validateBtn.addEventListener("click", handleValidateInvoiceClick);
    }
    
    // Submit invoice button
    const submitBtn = document.getElementById('submit-invoice-btn');
    if (submitBtn) {
        submitBtn.addEventListener("click", handleSubmitInvoiceClick);
    }
    
    // Invoice tab buttons (inside Invoice Processing tab)
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', function() {
            if (!this.disabled) {
                const tabName = this.getAttribute('data-tab');
                switchTab(tabName);
            }
        });
    });
    
    // Listen for messages from Manager
    window.addEventListener("message", function(event) {
        if (event.data && event.data.type === 'page-response') {
            handlePageResponse(event);
        } else if (event.data && event.data.type === 'invoice-data') {
            handleInvoiceData(event.data.invoice);
        }
    });
}

// Initialize all functionality when DOM is loaded

document.addEventListener('DOMContentLoaded', function() {

    // Initialize event listeners
    initializeEventListeners();
    
    // Initialize tab system properly
    switchTab('manager-invoice');
});