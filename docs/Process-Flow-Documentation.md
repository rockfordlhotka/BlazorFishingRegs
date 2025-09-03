# Fishing Regulations Process Flow Documentation

## Overview

This document outlines the high-level process flows for the Blazor AI Fishing Regulations application. The system has two primary workflows:

1. **PDF Ingestion Process** - Converting fishing regulations PDFs into structured, searchable data
2. **Lake Regulation Lookup Process** - Allowing users to select lakes and view regulations

## Process Flow 1: PDF Ingestion and Data Extraction

### High-Level Flow Diagram

```mermaid
graph TD
    A[Administrator uploads fishing regulations PDF] --> B[System validates PDF format and content]
    B --> C[PDF stored in Azure Blob Storage / Azurite]
    C --> D[AI Document Intelligence analyzes PDF structure]
    D --> E[Extract lake names and geographic data]
    E --> F[Extract fishing regulations by species]
    F --> G[Azure OpenAI enhances and standardizes data]
    G --> H[System maps regulations to lake entities]
    H --> I[Validate extracted regulations for accuracy]
    I --> J[Store structured data in SQL database]
    J --> K[Update lake regulation cache]
    K --> L[Notify users of updated regulations]
    
    style A fill:#e1f5fe
    style L fill:#c8e6c9
    style D fill:#fff3e0
    style G fill:#fff3e0
```

### Detailed Process Steps

#### Phase 1: Document Upload and Validation
```mermaid
sequenceDiagram
    participant Admin as Administrator
    participant UI as Blazor UI
    participant API as Web API
    participant Storage as Blob Storage
    participant DB as Database

    Admin->>UI: Upload PDF file
    UI->>API: POST /api/v1/admin/regulations/upload
    API->>API: Validate file format, size, content
    API->>Storage: Store PDF with unique identifier
    API->>DB: Create RegulationDocument record
    API->>UI: Return upload confirmation
    UI->>Admin: Display upload success
```

#### Phase 2: AI Processing and Data Extraction
```mermaid
sequenceDiagram
    participant API as Web API
    participant AI as AI Services
    participant OpenAI as Azure OpenAI
    participant DB as Database
    participant Cache as Redis Cache

    API->>AI: Analyze PDF document
    AI->>AI: Extract text and table structures
    AI->>API: Return raw extracted data
    API->>OpenAI: Enhance and standardize data
    OpenAI->>OpenAI: Identify lake names and normalize
    OpenAI->>OpenAI: Structure regulation data
    OpenAI->>API: Return enhanced regulation data
    API->>DB: Store structured lake and regulation data
    API->>Cache: Update regulation cache
    API->>API: Mark document as processed
```

#### Phase 3: Data Validation and Quality Assurance
```mermaid
flowchart TD
    A[AI Extracted Data] --> B{Confidence Score > 90%?}
    B -->|Yes| C[Auto-approve regulation]
    B -->|No| D[Flag for manual review]
    
    C --> E[Update database]
    D --> F[Administrator review]
    F --> G{Administrator approves?}
    G -->|Yes| H[Update with corrections]
    G -->|No| I[Reject and re-process]
    
    H --> E
    I --> J[Mark for re-extraction]
    E --> K[Update cache]
    K --> L[Notify users of changes]
    
    style A fill:#e3f2fd
    style L fill:#c8e6c9
    style D fill:#fff3e0
    style I fill:#ffebee
```

## Process Flow 2: Lake Selection and Regulation Lookup

### High-Level Flow Diagram

```mermaid
graph TD
    A[User opens fishing regulations app] --> B[Display main dashboard]
    B --> C{How does user want to find lake?}
    C -->|Map| D[Show interactive map with lake markers]
    C -->|Search| E[Show search interface]
    C -->|Browse| F[Show categorized lake list]
    
    D --> G[User clicks on lake marker]
    E --> H[User enters lake name/location]
    F --> I[User selects from lake list]
    
    G --> J[Load lake details]
    H --> K[Show search results]
    I --> J
    K --> L[User selects lake from results]
    L --> J
    
    J --> M[Query regulations for selected lake]
    M --> N[Display comprehensive regulation information]
    N --> O{User wants to filter?}
    O -->|Yes| P[Apply species/season filters]
    O -->|No| Q[Show all regulations]
    P --> Q
    Q --> R[Display final regulation view]
    
    style A fill:#e1f5fe
    style R fill:#c8e6c9
    style M fill:#fff3e0
```

### Detailed User Interaction Flow

#### Lake Selection Interface
```mermaid
sequenceDiagram
    participant User as Angler
    participant UI as Blazor UI
    participant API as Web API
    participant Cache as Redis Cache
    participant DB as Database

    User->>UI: Open fishing regulations app
    UI->>API: GET /api/v1/lakes/featured
    API->>Cache: Check for cached featured lakes
    Cache->>API: Return cached data (if available)
    API->>DB: Query featured lakes (if not cached)
    DB->>API: Return lake list
    API->>Cache: Cache lake data
    API->>UI: Return featured lakes
    UI->>User: Display dashboard with lake options

    User->>UI: Search for "Lake Superior"
    UI->>API: GET /api/v1/lakes/search?q=Lake Superior
    API->>DB: Search lakes by name
    DB->>API: Return matching lakes
    API->>UI: Return search results
    UI->>User: Display search results

    User->>UI: Select Lake Superior
    UI->>API: GET /api/v1/lakes/{lakeId}/regulations
    API->>Cache: Check for cached regulations
    Cache->>API: Return cached regulations (if available)
    API->>DB: Query lake regulations (if not cached)
    DB->>API: Return regulation data
    API->>Cache: Cache regulation data
    API->>UI: Return lake regulations
    UI->>User: Display comprehensive regulation view
```

#### Regulation Display and Filtering
```mermaid
flowchart TD
    A[Lake Selected] --> B[Load all regulations for lake]
    B --> C[Display regulation categories]
    C --> D[Show fishing seasons by species]
    C --> E[Show bag limits and size restrictions]
    C --> F[Show special regulations]
    C --> G[Show license requirements]
    
    H[User applies filters] --> I{Filter type?}
    I -->|Species| J[Filter by fish species]
    I -->|Season| K[Filter by current/upcoming seasons]
    I -->|Regulation type| L[Filter by restriction type]
    
    J --> M[Update displayed regulations]
    K --> M
    L --> M
    M --> N[Highlight relevant information]
    N --> O[Show current status indicators]
    
    style A fill:#e3f2fd
    style O fill:#c8e6c9
    style H fill:#fff3e0
```

## Data Flow Architecture

### System Data Flow
```mermaid
flowchart LR
    A[Fishing Regulations PDF] --> B[AI Document Intelligence]
    B --> C[Azure OpenAI Enhancement]
    C --> D[Structured Regulation Data]
    D --> E[SQL Database]
    E --> F[Redis Cache]
    F --> G[Blazor UI Components]
    G --> H[User Interface]
    
    I[User Lake Selection] --> J[API Query]
    J --> K{Data in Cache?}
    K -->|Yes| L[Return Cached Data]
    K -->|No| M[Query Database]
    M --> N[Cache Results]
    N --> L
    L --> O[Regulation Display]
    
    style A fill:#ffecb3
    style H fill:#c8e6c9
    style E fill:#e1f5fe
    style F fill:#f3e5f5
```

## Integration Points and Dependencies

### External Service Integration
```mermaid
graph TD
    A[Blazor Fishing Regulations App] --> B[Azure AI Document Intelligence]
    A --> C[Azure OpenAI Service]
    A --> D[Azure Blob Storage]
    A --> E[SQL Server Database]
    A --> F[Redis Cache]
    
    B --> G[PDF Text Extraction]
    B --> H[Table Structure Recognition]
    C --> I[Data Enhancement]
    C --> J[Lake Name Standardization]
    D --> K[PDF Document Storage]
    E --> L[Structured Data Storage]
    F --> M[Performance Caching]
    
    style A fill:#e3f2fd
    style B fill:#fff3e0
    style C fill:#fff3e0
    style D fill:#e8f5e8
    style E fill:#e8f5e8
    style F fill:#f3e5f5
```

## Error Handling and Recovery

### Error Flow Diagram
```mermaid
flowchart TD
    A[Process Start] --> B{Error Occurred?}
    B -->|No| C[Continue Normal Flow]
    B -->|Yes| D{Error Type?}
    
    D -->|PDF Processing Error| E[Log error and notify admin]
    D -->|AI Service Error| F[Retry with exponential backoff]
    D -->|Database Error| G[Use cached data if available]
    D -->|Cache Error| H[Query database directly]
    
    E --> I[Mark document for manual review]
    F --> J{Retry Successful?}
    G --> K[Log degraded performance]
    H --> L[Log cache unavailability]
    
    J -->|Yes| C
    J -->|No| M[Fallback to manual processing]
    
    I --> N[Queue for re-processing]
    K --> C
    L --> C
    M --> N
    N --> O[Administrator intervention required]
    
    style A fill:#e3f2fd
    style O fill:#ffcdd2
    style C fill:#c8e6c9
```

## Performance Optimization Flow

### Caching Strategy
```mermaid
flowchart TD
    A[User Request] --> B{Data in L1 Cache?}
    B -->|Yes| C[Return from Memory Cache]
    B -->|No| D{Data in L2 Cache?}
    D -->|Yes| E[Return from Redis Cache]
    D -->|No| F[Query Database]
    
    F --> G[Store in Redis Cache]
    G --> H[Store in Memory Cache]
    H --> I[Return to User]
    E --> H
    C --> I
    
    J[Cache Invalidation] --> K[Clear Memory Cache]
    K --> L[Clear Redis Cache]
    L --> M[Next request hits database]
    
    style A fill:#e3f2fd
    style I fill:#c8e6c9
    style F fill:#fff3e0
    style J fill:#ffecb3
```

## Monitoring and Metrics

### Process Monitoring Points
```mermaid
graph TD
    A[PDF Upload] --> A1[Track upload success rate]
    B[AI Processing] --> B1[Monitor processing time]
    C[Data Extraction] --> C1[Track extraction accuracy]
    D[Database Updates] --> D1[Monitor update performance]
    E[User Queries] --> E1[Track response times]
    F[Cache Performance] --> F1[Monitor hit rates]
    
    A1 --> G[Application Insights]
    B1 --> G
    C1 --> G
    D1 --> G
    E1 --> G
    F1 --> G
    
    G --> H[Dashboards and Alerts]
    H --> I[Performance Optimization]
    
    style G fill:#e8f5e8
    style H fill:#fff3e0
    style I fill:#c8e6c9
```

## Security Considerations

### Security Flow
```mermaid
flowchart TD
    A[User Access] --> B[Authentication Check]
    B --> C{Valid JWT Token?}
    C -->|No| D[Redirect to Login]
    C -->|Yes| E[Authorization Check]
    E --> F{Required Permissions?}
    F -->|No| G[Access Denied]
    F -->|Yes| H[Allow Access]
    
    I[PDF Upload] --> J[File Validation]
    J --> K[Virus Scan]
    K --> L[Content Verification]
    L --> M[Secure Storage]
    
    N[API Calls] --> O[Rate Limiting]
    O --> P[Input Validation]
    P --> Q[SQL Injection Prevention]
    Q --> R[Process Request]
    
    style D fill:#ffcdd2
    style G fill:#ffcdd2
    style H fill:#c8e6c9
    style M fill:#c8e6c9
    style R fill:#c8e6c9
```

## Conclusion

These process flows provide a comprehensive view of how the Blazor AI Fishing Regulations application operates:

1. **PDF Ingestion** transforms unstructured regulation documents into searchable, structured data through AI processing
2. **Lake Selection** provides users with intuitive ways to find and view regulations for specific fishing locations

The flows are designed for scalability, reliability, and user experience, with proper error handling, caching strategies, and security measures throughout the process.
