# Fishing Regulations API Specification Document

## 1. Overview

This document defines the REST API specifications for the Blazor AI Fishing Regulations application. The API provides endpoints for lake management, fishing regulation retrieval, regulation document processing, and lake search functionality.

## 2. API Design Principles

### 2.1 RESTful Design
- Use HTTP verbs appropriately (GET, POST, PUT, DELETE)
- Resource-based URLs focused on lakes and regulations
- Stateless communication
- Consistent response formats

### 2.2 Versioning Strategy
- URL-based versioning: `/api/v1/`
- Backward compatibility maintenance
- Deprecation notices for older versions

### 2.3 Authentication
- Bearer token authentication (JWT)
- API key authentication for service-to-service calls
- Role-based access control (angler vs administrator)

## 3. Base Configuration

### 3.1 Base URL
```
Production: https://fishing-regs-prod.azurewebsites.net/api/v1
Staging: https://fishing-regs-staging.azurewebsites.net/api/v1
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

## 5. Lake Management Endpoints

### 5.1 Get Lakes

#### Request
```http
GET /api/v1/lakes
Authorization: Bearer {token}

Query Parameters:
- search: string (searches lake name, county)
- state: string (filter by state)
- latitude: decimal (for region-based search)
- longitude: decimal (for region-based search)
- radius: decimal (search radius in miles, requires lat/lng)
- species: string (filter lakes by fish species)
- page: integer (default: 1)
- pageSize: integer (default: 20, max: 100)
```

#### Response
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "name": "Lake Superior",
        "state": "Minnesota",
        "county": "Cook County",
        "latitude": 47.7211,
        "longitude": -89.8794,
        "description": "Large freshwater lake on the Minnesota-Canada border",
        "fishSpecies": ["Lake Trout", "Salmon", "Northern Pike", "Walleye"],
        "hasCurrentRegulations": true,
        "lastUpdated": "2025-09-03T10:30:00Z"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 127,
      "totalPages": 7,
      "hasNext": true,
      "hasPrevious": false
    }
  }
}
```

### 5.2 Get Lake Details

#### Request
```http
GET /api/v1/lakes/{lakeId}
Authorization: Bearer {token}
```

#### Response
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Lake Superior",
    "state": "Minnesota",
    "county": "Cook County",
    "latitude": 47.7211,
    "longitude": -89.8794,
    "description": "Large freshwater lake on the Minnesota-Canada border",
    "fishSpecies": ["Lake Trout", "Salmon", "Northern Pike", "Walleye"],
    "regulations": {
      "regulationCount": 15,
      "lastUpdated": "2025-09-03T10:30:00Z",
      "source": "Minnesota DNR 2025 Regulations"
    }
  }
}
```

### 5.3 Search Lakes by Species

#### Request
```http
GET /api/v1/lakes/species/{species}
Authorization: Bearer {token}

Query Parameters:
- state: string (optional filter)
- page: integer
- pageSize: integer
```

#### Response
```json
{
  "success": true,
  "data": {
    "species": "Lake Trout",
    "lakes": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "name": "Lake Superior",
        "state": "Minnesota",
        "county": "Cook County",
        "hasOpenSeason": true,
        "seasonStatus": "open",
        "nextSeasonChange": "2025-09-30T23:59:59Z"
      }
    ],
    "totalCount": 23
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

## 6. Fishing Regulations Endpoints

### 6.1 Get Lake Regulations

#### Request
```http
GET /api/v1/lakes/{lakeId}/regulations
Authorization: Bearer {token}

Query Parameters:
- species: string (filter by fish species)
- includeExpired: boolean (default: false)
- date: ISO 8601 date (check regulations for specific date)
```

#### Response
```json
{
  "success": true,
  "data": {
    "lakeId": "550e8400-e29b-41d4-a716-446655440000",
    "lakeName": "Lake Superior",
    "effectiveDate": "2025-01-01",
    "regulationYear": "2025",
    "source": "Minnesota DNR",
    "regulations": [
      {
        "id": "reg-123",
        "species": "Lake Trout",
        "seasonOpen": "2025-01-01",
        "seasonClose": "2025-09-30",
        "isCurrentlyOpen": true,
        "dailyBagLimit": 3,
        "possessionLimit": 6,
        "minimumSize": "15 inches",
        "maximumSize": null,
        "protectedSlot": "28-36 inches (1 fish allowed)",
        "specialRegulations": [
          "Barbless hooks required",
          "No live bait below 63 feet"
        ],
        "confidenceScore": 0.95,
        "lastUpdated": "2025-09-03T10:30:00Z"
      }
    ],
    "licenseRequirements": {
      "required": true,
      "type": "Minnesota Fishing License",
      "additionalStamps": ["Lake Superior Stamp"],
      "notes": "Required for all anglers 16 and older"
    }
  }
}
```

### 6.2 Get Current Fishing Status

#### Request
```http
GET /api/v1/lakes/{lakeId}/status
Authorization: Bearer {token}

Query Parameters:
- species: string (optional, get status for specific species)
- date: ISO 8601 date (default: today)
```

#### Response
```json
{
  "success": true,
  "data": {
    "lakeId": "550e8400-e29b-41d4-a716-446655440000",
    "lakeName": "Lake Superior",
    "checkDate": "2025-09-03",
    "overallStatus": "open",
    "speciesStatus": [
      {
        "species": "Lake Trout",
        "status": "open",
        "daysRemaining": 27,
        "seasonCloses": "2025-09-30",
        "bagLimit": 3,
        "currentRestrictions": ["Barbless hooks required"]
      },
      {
        "species": "Northern Pike",
        "status": "closed",
        "reason": "Spawning season",
        "seasonOpens": "2025-05-01",
        "daysUntilOpen": 238
      }
    ]
  }
}
```

### 6.3 Search Regulations

#### Request
```http
GET /api/v1/regulations/search
Authorization: Bearer {token}

Query Parameters:
- species: string
- state: string
- minBagLimit: integer
- maxBagLimit: integer
- openSeasons: boolean (only return currently open seasons)
- hasSpecialRegs: boolean (filter by special regulations)
```

#### Response
```json
{
  "success": true,
  "data": {
    "searchCriteria": {
      "species": "Walleye",
      "state": "Minnesota",
      "openSeasons": true
    },
    "results": [
      {
        "lakeId": "lake-456",
        "lakeName": "Mille Lacs Lake",
        "species": "Walleye",
        "dailyBagLimit": 4,
        "minimumSize": "15 inches",
        "seasonStatus": "open",
        "specialRegulations": ["Slot limit in effect"]
      }
    ],
    "totalCount": 45
  }
}
```

## 7. Regulation Document Management (Admin)

### 7.1 Upload Regulation Document

#### Request
```http
POST /api/v1/admin/regulations/upload
Content-Type: multipart/form-data
Authorization: Bearer {token}

file: {fishing_regulations_text}
regulationYear: "2025"
issuingAuthority: "Minnesota DNR"
effectiveDate: "2025-01-01"
```

#### Response
```json
{
  "success": true,
  "data": {
    "documentId": "550e8400-e29b-41d4-a716-446655440000",
    "fileName": "mn-fishing-regs-2025.txt",
    "fileSize": 1024000,
    "status": "uploaded",
    "regulationYear": "2025",
    "issuingAuthority": "Minnesota DNR",
    "uploadedAt": "2025-09-03T10:30:00Z",
    "processingEstimate": "5-10 minutes"
  },
  "message": "Regulation document uploaded successfully"
}
```

### 7.2 Process Regulation Document

#### Request
```http
POST /api/v1/admin/regulations/{documentId}/process
Authorization: Bearer {token}
Content-Type: application/json

{
  "options": {
    "extractLakeInfo": true,
    "extractRegulations": true,
    "validateWithAI": true,
    "updateExistingLakes": true
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
    "estimatedCompletionTime": "2025-09-03T10:40:00Z",
    "steps": [
      {
        "name": "Document Analysis",
        "status": "in_progress",
        "progress": 25
      },
      {
        "name": "Lake Identification",
        "status": "pending"
      },
      {
        "name": "Regulation Extraction",
        "status": "pending"
      },
      {
        "name": "Data Validation",
        "status": "pending"
      }
    ]
  }
}
```

### 7.3 Get Processing Status

#### Request
```http
GET /api/v1/admin/regulations/{documentId}/status
Authorization: Bearer {token}
```

#### Response
```json
{
  "success": true,
  "data": {
    "documentId": "550e8400-e29b-41d4-a716-446655440000",
    "status": "completed",
    "progress": 100,
    "results": {
      "lakesProcessed": 127,
      "lakesCreated": 5,
      "lakesUpdated": 122,
      "regulationsExtracted": 1834,
      "regulationsValidated": 1798,
      "regulationsRequiringReview": 36
    },
    "completedAt": "2025-09-03T10:38:45Z",
    "processingTime": "8 minutes 45 seconds"
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
    "name": "Blazor AI Fishing Regulations API",
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
        
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
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

This API specification provides comprehensive documentation for integrating with the Blazor AI Fishing Regulations application. All endpoints support the standard HTTP methods and return consistent JSON responses for easy integration.
