# API Specification Document

## 1. Overview

This document defines the REST API specifications for the Blazor AI PDF Form Population application. The API provides endpoints for document management, AI processing, form template management, and data export functionality.

## 2. API Design Principles

### 2.1 RESTful Design
- Use HTTP verbs appropriately (GET, POST, PUT, DELETE)
- Resource-based URLs
- Stateless communication
- Consistent response formats

### 2.2 Versioning Strategy
- URL-based versioning: `/api/v1/`
- Backward compatibility maintenance
- Deprecation notices for older versions

### 2.3 Authentication
- Bearer token authentication (JWT)
- API key authentication for service-to-service calls
- Role-based access control

## 3. Base Configuration

### 3.1 Base URL
```
Production: https://blazorai-prod.azurewebsites.net/api/v1
Staging: https://blazorai-staging.azurewebsites.net/api/v1
Development: https://localhost:5001/api/v1
```

### 3.2 Common Headers
```http
Content-Type: application/json
Authorization: Bearer {jwt_token}
X-API-Version: 1.0
X-Request-ID: {unique_request_id}
```

### 3.3 Standard Response Format
```json
{
  "success": true,
  "data": {},
  "message": "Operation completed successfully",
  "errors": [],
  "metadata": {
    "requestId": "12345-67890",
    "timestamp": "2025-09-03T10:30:00Z",
    "version": "1.0"
  }
}
```

## 4. Authentication Endpoints

### 4.1 Token Validation
```http
GET /api/v1/auth/validate
Authorization: Bearer {token}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "valid": true,
    "expiresAt": "2025-09-03T12:00:00Z",
    "roles": ["DocumentProcessor"],
    "userId": "user123"
  }
}
```

### 4.2 Token Refresh
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refreshToken": "refresh_token_here"
}
```

## 5. Document Management Endpoints

### 5.1 Upload Document

#### Request
```http
POST /api/v1/documents/upload
Content-Type: multipart/form-data
Authorization: Bearer {token}

file: {pdf_file}
documentType: "invoice" | "contract" | "receipt" | "custom"
templateId: {optional_template_id}
```

#### Response
```json
{
  "success": true,
  "data": {
    "documentId": "550e8400-e29b-41d4-a716-446655440000",
    "fileName": "invoice_001.pdf",
    "fileSize": 1024000,
    "status": "uploaded",
    "blobUrl": "https://storage.blob.core.windows.net/documents/2025/09/03/abc123.pdf",
    "uploadedAt": "2025-09-03T10:30:00Z"
  },
  "message": "Document uploaded successfully"
}
```

#### Error Responses
```json
// File too large
{
  "success": false,
  "errors": [
    {
      "code": "FILE_TOO_LARGE",
      "message": "File size exceeds maximum limit of 50MB",
      "field": "file"
    }
  ]
}

// Invalid file type
{
  "success": false,
  "errors": [
    {
      "code": "INVALID_FILE_TYPE",
      "message": "Only PDF files are supported",
      "field": "file"
    }
  ]
}
```

### 5.2 Get Document Details

#### Request
```http
GET /api/v1/documents/{documentId}
Authorization: Bearer {token}
```

#### Response
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "fileName": "invoice_001.pdf",
    "originalName": "Invoice_Company_ABC.pdf",
    "fileSize": 1024000,
    "contentType": "application/pdf",
    "status": "processed",
    "uploadedAt": "2025-09-03T10:30:00Z",
    "processedAt": "2025-09-03T10:32:15Z",
    "uploadedBy": "user123",
    "documentType": "invoice",
    "extractedData": [
      {
        "fieldName": "invoiceNumber",
        "fieldValue": "INV-2025-001",
        "confidenceScore": 0.95,
        "isValidated": true,
        "correctedValue": null
      },
      {
        "fieldName": "totalAmount",
        "fieldValue": "$1,234.56",
        "confidenceScore": 0.89,
        "isValidated": false,
        "correctedValue": "$1,234.56"
      }
    ]
  }
}
```

### 5.3 List Documents

#### Request
```http
GET /api/v1/documents
Authorization: Bearer {token}

Query Parameters:
- page: integer (default: 1)
- pageSize: integer (default: 10, max: 100)
- status: "uploaded" | "processing" | "processed" | "failed"
- documentType: string
- fromDate: ISO 8601 date
- toDate: ISO 8601 date
- search: string (searches filename and extracted data)
```

#### Response
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "fileName": "invoice_001.pdf",
        "status": "processed",
        "documentType": "invoice",
        "uploadedAt": "2025-09-03T10:30:00Z",
        "processedAt": "2025-09-03T10:32:15Z"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 10,
      "totalCount": 45,
      "totalPages": 5,
      "hasNext": true,
      "hasPrevious": false
    }
  }
}
```

### 5.4 Process Document

#### Request
```http
POST /api/v1/documents/{documentId}/process
Authorization: Bearer {token}
Content-Type: application/json

{
  "templateId": "template123",
  "options": {
    "enhanceWithAI": true,
    "validateResults": true,
    "extractTables": true
  }
}
```

#### Response
```json
{
  "success": true,
  "data": {
    "processingId": "proc-550e8400-e29b-41d4-a716-446655440000",
    "status": "processing",
    "estimatedCompletionTime": "2025-09-03T10:35:00Z"
  },
  "message": "Document processing initiated"
}
```

### 5.5 Get Processing Status

#### Request
```http
GET /api/v1/documents/{documentId}/status
Authorization: Bearer {token}
```

#### Response
```json
{
  "success": true,
  "data": {
    "documentId": "550e8400-e29b-41d4-a716-446655440000",
    "status": "processing",
    "progress": 65,
    "currentStep": "AI Enhancement",
    "steps": [
      {
        "name": "Document Analysis",
        "status": "completed",
        "completedAt": "2025-09-03T10:31:00Z"
      },
      {
        "name": "AI Enhancement",
        "status": "in_progress",
        "startedAt": "2025-09-03T10:31:30Z"
      },
      {
        "name": "Field Mapping",
        "status": "pending"
      },
      {
        "name": "Validation",
        "status": "pending"
      }
    ],
    "estimatedCompletion": "2025-09-03T10:35:00Z"
  }
}
```

### 5.6 Update Extracted Data

#### Request
```http
PUT /api/v1/documents/{documentId}/data
Authorization: Bearer {token}
Content-Type: application/json

{
  "extractedData": [
    {
      "fieldName": "invoiceNumber",
      "fieldValue": "INV-2025-001",
      "isValidated": true
    },
    {
      "fieldName": "totalAmount",
      "fieldValue": "$1,234.56",
      "correctedValue": "$1,234.56",
      "isValidated": true
    }
  ]
}
```

#### Response
```json
{
  "success": true,
  "data": {
    "documentId": "550e8400-e29b-41d4-a716-446655440000",
    "updatedFields": 2,
    "validationResults": {
      "totalFields": 8,
      "validatedFields": 7,
      "pendingValidation": 1
    }
  },
  "message": "Extracted data updated successfully"
}
```

### 5.7 Delete Document

#### Request
```http
DELETE /api/v1/documents/{documentId}
Authorization: Bearer {token}
```

#### Response
```json
{
  "success": true,
  "message": "Document deleted successfully"
}
```

## 6. Form Template Endpoints

### 6.1 Create Form Template

#### Request
```http
POST /api/v1/templates
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Invoice Processing Template",
  "description": "Standard template for processing invoices",
  "documentType": "invoice",
  "fields": [
    {
      "name": "invoiceNumber",
      "label": "Invoice Number",
      "type": "text",
      "required": true,
      "validation": {
        "pattern": "^INV-\\d{4}-\\d{3}$",
        "maxLength": 20
      },
      "aiMapping": {
        "keywords": ["invoice number", "invoice #", "inv #"],
        "patterns": ["INV-\\d{4}-\\d{3}"]
      }
    },
    {
      "name": "totalAmount",
      "label": "Total Amount",
      "type": "currency",
      "required": true,
      "validation": {
        "min": 0,
        "max": 1000000
      },
      "aiMapping": {
        "keywords": ["total", "amount due", "balance"],
        "patterns": ["\\$?\\d+\\.\\d{2}"]
      }
    }
  ]
}
```

#### Response
```json
{
  "success": true,
  "data": {
    "id": "template123",
    "name": "Invoice Processing Template",
    "description": "Standard template for processing invoices",
    "documentType": "invoice",
    "isActive": true,
    "createdAt": "2025-09-03T10:30:00Z",
    "fieldCount": 8
  },
  "message": "Template created successfully"
}
```

### 6.2 Get Form Templates

#### Request
```http
GET /api/v1/templates
Authorization: Bearer {token}

Query Parameters:
- documentType: string
- isActive: boolean
- page: integer
- pageSize: integer
```

#### Response
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "template123",
        "name": "Invoice Processing Template",
        "description": "Standard template for processing invoices",
        "documentType": "invoice",
        "isActive": true,
        "fieldCount": 8,
        "createdAt": "2025-09-03T10:30:00Z",
        "updatedAt": "2025-09-03T10:30:00Z"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 10,
      "totalCount": 5,
      "totalPages": 1
    }
  }
}
```

### 6.3 Get Template Details

#### Request
```http
GET /api/v1/templates/{templateId}
Authorization: Bearer {token}
```

#### Response
```json
{
  "success": true,
  "data": {
    "id": "template123",
    "name": "Invoice Processing Template",
    "description": "Standard template for processing invoices",
    "documentType": "invoice",
    "isActive": true,
    "fields": [
      {
        "name": "invoiceNumber",
        "label": "Invoice Number",
        "type": "text",
        "required": true,
        "validation": {
          "pattern": "^INV-\\d{4}-\\d{3}$",
          "maxLength": 20
        },
        "aiMapping": {
          "keywords": ["invoice number", "invoice #", "inv #"],
          "patterns": ["INV-\\d{4}-\\d{3}"]
        }
      }
    ],
    "createdAt": "2025-09-03T10:30:00Z",
    "updatedAt": "2025-09-03T10:30:00Z"
  }
}
```

## 7. AI Processing Endpoints

### 7.1 Analyze Document

#### Request
```http
POST /api/v1/ai/analyze
Authorization: Bearer {token}
Content-Type: application/json

{
  "documentUrl": "https://storage.blob.core.windows.net/documents/abc123.pdf",
  "analysisType": "comprehensive",
  "options": {
    "extractTables": true,
    "detectSignatures": true,
    "enhanceWithNLP": true
  }
}
```

#### Response
```json
{
  "success": true,
  "data": {
    "analysisId": "analysis-123",
    "documentType": "invoice",
    "confidence": 0.92,
    "extractedFields": {
      "invoiceNumber": {
        "value": "INV-2025-001",
        "confidence": 0.95,
        "boundingBox": {
          "x": 100,
          "y": 50,
          "width": 120,
          "height": 20
        }
      }
    },
    "tables": [
      {
        "rows": 5,
        "columns": 4,
        "data": [
          ["Item", "Quantity", "Price", "Total"],
          ["Widget A", "2", "$10.00", "$20.00"]
        ]
      }
    ],
    "qualityScore": 85,
    "processingTime": 2.5
  }
}
```

### 7.2 Enhance Extraction

#### Request
```http
POST /api/v1/ai/enhance
Authorization: Bearer {token}
Content-Type: application/json

{
  "documentId": "550e8400-e29b-41d4-a716-446655440000",
  "extractedData": {
    "invoiceNumber": "INV-2025-001",
    "totalAmount": "1234.56"
  },
  "context": {
    "documentType": "invoice",
    "vendor": "ABC Company"
  }
}
```

#### Response
```json
{
  "success": true,
  "data": {
    "enhancedFields": {
      "invoiceNumber": {
        "original": "INV-2025-001",
        "enhanced": "INV-2025-001",
        "confidence": 0.98,
        "changes": []
      },
      "totalAmount": {
        "original": "1234.56",
        "enhanced": "$1,234.56",
        "confidence": 0.95,
        "changes": ["formatted_currency"]
      }
    },
    "suggestions": [
      {
        "field": "dueDate",
        "suggestion": "2025-10-03",
        "confidence": 0.75,
        "reason": "Inferred from 30-day payment terms"
      }
    ],
    "qualityImprovement": 12
  }
}
```

## 8. Export Endpoints

### 8.1 Export Document Data

#### Request
```http
POST /api/v1/documents/{documentId}/export
Authorization: Bearer {token}
Content-Type: application/json

{
  "format": "json" | "csv" | "excel" | "xml",
  "includeMetadata": true,
  "fieldsToInclude": ["invoiceNumber", "totalAmount", "dueDate"]
}
```

#### Response
```json
{
  "success": true,
  "data": {
    "exportId": "export-123",
    "downloadUrl": "https://storage.blob.core.windows.net/exports/export-123.json",
    "expiresAt": "2025-09-03T12:00:00Z",
    "format": "json",
    "fileSize": 2048
  }
}
```

### 8.2 Batch Export

#### Request
```http
POST /api/v1/documents/export/batch
Authorization: Bearer {token}
Content-Type: application/json

{
  "documentIds": [
    "550e8400-e29b-41d4-a716-446655440000",
    "550e8400-e29b-41d4-a716-446655440001"
  ],
  "format": "excel",
  "includeMetadata": true
}
```

#### Response
```json
{
  "success": true,
  "data": {
    "exportId": "batch-export-456",
    "downloadUrl": "https://storage.blob.core.windows.net/exports/batch-export-456.xlsx",
    "expiresAt": "2025-09-03T12:00:00Z",
    "totalDocuments": 2,
    "fileSize": 15360
  }
}
```

## 9. Webhook Endpoints

### 9.1 Register Webhook

#### Request
```http
POST /api/v1/webhooks
Authorization: Bearer {token}
Content-Type: application/json

{
  "url": "https://your-app.com/webhooks/blazor-ai",
  "events": ["document.processed", "document.failed"],
  "secret": "your-webhook-secret"
}
```

#### Response
```json
{
  "success": true,
  "data": {
    "webhookId": "webhook-123",
    "url": "https://your-app.com/webhooks/blazor-ai",
    "events": ["document.processed", "document.failed"],
    "isActive": true,
    "createdAt": "2025-09-03T10:30:00Z"
  }
}
```

### 9.2 Webhook Payload Example

```json
{
  "eventType": "document.processed",
  "timestamp": "2025-09-03T10:32:15Z",
  "data": {
    "documentId": "550e8400-e29b-41d4-a716-446655440000",
    "status": "processed",
    "extractedFieldCount": 8,
    "averageConfidence": 0.92,
    "processingTime": 25.3
  },
  "signature": "sha256=generated-signature"
}
```

## 10. Error Handling

### 10.1 Standard Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | INVALID_REQUEST | Invalid request format or parameters |
| 401 | UNAUTHORIZED | Authentication required |
| 403 | FORBIDDEN | Insufficient permissions |
| 404 | NOT_FOUND | Resource not found |
| 409 | CONFLICT | Resource conflict |
| 413 | FILE_TOO_LARGE | File size exceeds limit |
| 422 | VALIDATION_ERROR | Request validation failed |
| 429 | RATE_LIMIT_EXCEEDED | Too many requests |
| 500 | INTERNAL_ERROR | Internal server error |
| 502 | AI_SERVICE_ERROR | AI service unavailable |
| 503 | SERVICE_UNAVAILABLE | Service temporarily unavailable |

### 10.2 Error Response Format

```json
{
  "success": false,
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "message": "The field 'fileName' is required",
      "field": "fileName",
      "details": {
        "allowedValues": null,
        "actualValue": null
      }
    }
  ],
  "metadata": {
    "requestId": "12345-67890",
    "timestamp": "2025-09-03T10:30:00Z",
    "version": "1.0"
  }
}
```

## 11. Rate Limiting

### 11.1 Rate Limit Headers

```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 995
X-RateLimit-Reset: 1630656000
```

### 11.2 Rate Limit Tiers

| User Type | Requests/Hour | Burst Limit |
|-----------|---------------|-------------|
| Free | 100 | 10 |
| Standard | 1,000 | 50 |
| Premium | 10,000 | 100 |
| Enterprise | Unlimited | 500 |

## 12. API Testing

### 12.1 Postman Collection

A Postman collection is available with pre-configured requests for all endpoints:

```json
{
  "info": {
    "name": "Blazor AI PDF API",
    "description": "Complete API collection for testing",
    "version": "1.0.0"
  },
  "auth": {
    "type": "bearer",
    "bearer": [
      {
        "key": "token",
        "value": "{{jwt_token}}",
        "type": "string"
      }
    ]
  }
}
```

### 12.2 Sample Test Scripts

#### JavaScript/Node.js Example
```javascript
const axios = require('axios');

async function uploadDocument(filePath, token) {
  const formData = new FormData();
  formData.append('file', fs.createReadStream(filePath));
  formData.append('documentType', 'invoice');

  try {
    const response = await axios.post('https://localhost:5001/api/v1/documents/upload', formData, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'multipart/form-data'
      }
    });
    
    return response.data;
  } catch (error) {
    console.error('Upload failed:', error.response.data);
    throw error;
  }
}
```

#### C# Example
```csharp
public class BlazorAIApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public async Task<DocumentUploadResponse> UploadDocumentAsync(Stream fileStream, string fileName, string documentType)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(documentType), "documentType");

        var response = await _httpClient.PostAsync("/api/v1/documents/upload", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DocumentUploadResponse>(json);
    }
}
```

---

This API specification provides comprehensive documentation for integrating with the Blazor AI PDF Form Population application. All endpoints support the standard HTTP methods and return consistent JSON responses for easy integration.
