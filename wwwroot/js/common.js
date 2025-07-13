// Common utilities and constants for PRAL FBR Extension

// Field ID Constants
const FIELD_IDS = {
    // Business Details Fields
    BUSINESS_NAME: '567e4a1f-9b82-4175-bdca-66132cb8c44a',
    NTN_CNIC: '54fbab94-8a3b-48ec-98c8-bc37aa2c64de',
    PROVINCE: 'b9712fb7-24bb-493c-a5a6-f9190ef0e943',
    ADDRESS: 'a9f32674-2b04-4692-b157-0260c14f47ac',
    ENVIRONMENT: 'a8c17945-544f-4590-82c3-08735786f4e7',
    BEARER_TOKEN: '4536b105-2dc1-45a9-9679-57580052fd6a',
    APPLICATION_VERSION: 'fc89fc40-5068-4ceb-81ba-c1b7741a5166',
    // Invoice Fields
    FBR_INVOICE_NUMBER: '24b11f97-46d7-472e-ad4b-e99fbed9197e',
    QR_CODE: 'd2e9265a-460e-4a06-83f9-29a523a4d516'
};


// Load application version with fallback for static environment

async function loadAppVersionWithFallback() {
    try {
        const response = await fetch('/api/BusinessSetup/GetVersion');
        if (response.ok) {
            const data = await response.json();
            if (data.success && data.version) {
                const versionElement = document.getElementById('app-version');
                if (versionElement) {
                    versionElement.innerHTML = '<strong>' + data.version + '</strong>';
                }
                return;
            }
        }
    } catch (error) {
        // API not available, use fallback
    }
    
    // Fallback version for static environment
    const versionElement = document.getElementById('app-version');
    if (versionElement) {
        versionElement.innerHTML = '<strong>*</strong>';
    }
}

// Check if invoice already has FBR number
function getFbrInvoiceNumber(invoice) {
    if (invoice && invoice.customFields2 && invoice.customFields2.strings) {
        return invoice.customFields2.strings[FIELD_IDS.FBR_INVOICE_NUMBER] || null;
    }
    return null;
}

// Get version number from version string
function getVersionNumber(version) {
    if (!version) return 0;
    // Extract only digits from version string
    const numberString = version.replace(/\D/g, '');
    return parseInt(numberString, 10) || 0;
}

// Validate required business details fields
function validateBusinessDetails(businessDetails, minimumVersionNumber = null) {
    const requiredFields = [
        FIELD_IDS.BUSINESS_NAME,
        FIELD_IDS.NTN_CNIC,
        FIELD_IDS.PROVINCE,
        FIELD_IDS.ADDRESS,
        FIELD_IDS.ENVIRONMENT,
        FIELD_IDS.BEARER_TOKEN
    ];
    
    console.log(`Business Details: ${JSON.stringify(businessDetails)}`);
    
    let isComplete = true;
    let missingFields = [];
    let fieldValidationDetails = {};

    // Check version if minimumVersionNumber is provided
    if (minimumVersionNumber !== null) {
        const currentVersionString = businessDetails?.customFields2?.strings?.[FIELD_IDS.APPLICATION_VERSION] || '0';
        const currentVersionNumber = getVersionNumber(currentVersionString);
       
        if (currentVersionNumber < minimumVersionNumber) {
            isComplete = false;
        }
    }

    if (!businessDetails.customFields2 || !businessDetails.customFields2.strings) {
        isComplete = false;
        missingFields = requiredFields;
    } else {
        const customFields = businessDetails.customFields2.strings;
        
        for (const fieldId of requiredFields) {
            const value = customFields[fieldId];
            const isEmpty = !value || value.trim() === '';
            
            fieldValidationDetails[fieldId] = {
                value: value,
                isEmpty: isEmpty,
                status: isEmpty ? 'Missing' : 'Present'
            };
            
            if (isEmpty) {
                isComplete = false;
                missingFields.push(fieldId);
            }
        }
    }
    
    return {
        isComplete,
        missingFields,
        fieldValidationDetails
    };
}

// Create redirect URL for already reported invoice
function createAlreadyReportedUrl(fbrNumber, invoiceId) {
    return `invoice-already-reported.html?fbrNumber=${encodeURIComponent(fbrNumber)}&invoiceId=${encodeURIComponent(invoiceId)}`;
}

// Generate random request ID for API calls
function generateRequestId() {
    return Math.random().toString(36).substr(2, 9);
}

// Load and display server IP

async function loadServerIP() {
    try {
        const response = await fetch('/api/BusinessSetup/GetServerIP');
        if (response.ok) {
            const data = await response.json();

            const serverIP = data.serverIP || data.ip || 'Unable to detect IP';
            const element = document.getElementById('extension-ip');
            if (element) {
                element.textContent = serverIP;
            }
        } else {
            const element = document.getElementById('extension-ip');
            if (element) {
                element.textContent = 'Unable to detect IP';
            }
        }
    } catch (error) {
        const element = document.getElementById('extension-ip');
        if (element) {
            element.textContent = 'Error loading IP';
        }
    }
}

// Display base URL in elements with id="base-url"
function displayBaseUrl() {
    const baseUrl = window.location.protocol + '//' + window.location.host; //getBaseUrl();
    document.querySelectorAll('#base-url').forEach(function(el) {
        el.textContent = baseUrl;
    });
}

// Export constants and functions for use in other modules

if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        FIELD_IDS,
        loadAppVersionWithFallback,
        getFbrInvoiceNumber,
        validateBusinessDetails,
        createAlreadyReportedUrl,
        generateRequestId,
        loadServerIP,
        displayBaseUrl
    };
}