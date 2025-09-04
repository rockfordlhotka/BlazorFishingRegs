# Database Schema Design Documentation

## Overview

The Blazor Fishing Regulations database is designed to store fishing regulations, water body information, and user data for the AI-powered fishing regulations application. The schema uses PostgreSQL with support for JSON data types, arrays, and advanced indexing.

## 🏗️ **Architecture Principles**

### **Normalized Design**
- Core entities are properly normalized to reduce redundancy
- Junction tables for many-to-many relationships
- Referential integrity maintained through foreign keys

### **Performance Optimized**
- Strategic indexes for common query patterns
- Views for complex, frequently-used queries
- JSONB for flexible, searchable JSON data
- Array types for PostgreSQL-native list storage

### **Audit & Compliance**
- Complete audit trail for regulation changes
- Soft deletes with `is_active` flags
- Timestamps for all entities
- Confidence scores for AI-extracted data

### **Scalability**
- UUID primary keys for distributed systems
- Partitioning-ready design for large datasets
- Efficient pagination support
- Full-text search capabilities

## 📊 **Core Entities**

### **Geographic Hierarchy**
```
States (MN, WI, MI...)
├── Counties (Cook County, Hennepin County...)
└── Water Bodies (Lake Superior, Mille Lacs...)
    └── Fish Species (Walleye, Northern Pike...)
```

### **Regulation Processing**
```
PDF Documents → AI Processing → Fishing Regulations
├── Processing Status Tracking
├── Confidence Scoring
└── Review Workflow
```

### **User Management**
```
Users (Azure AD B2C Integration)
├── Favorites (Saved Lakes)
├── Search History (Analytics)
└── Role-Based Access Control
```

## 🐟 **Key Features**

### **1. Flexible Regulation Storage**
- **Seasons**: Open/close dates, year-round flags
- **Bag Limits**: Daily and possession limits
- **Size Limits**: Min/max sizes, protected slots
- **Special Rules**: Arrays of regulation text
- **License Requirements**: Required stamps and permits

### **2. AI Integration Support**
- **Confidence Scores**: Track AI extraction reliability
- **Review Workflow**: Human verification process
- **Source Tracking**: Link regulations to original documents
- **Processing Status**: Track document processing pipeline

### **3. Advanced Search & Analytics**
- **Full-Text Search**: PostgreSQL's built-in search
- **Geographic Search**: Lat/long coordinates
- **Usage Analytics**: Search history and patterns
- **User Preferences**: Favorites and state preferences

### **4. Data Quality & Governance**
- **Audit Logging**: Complete change history
- **Soft Deletes**: Maintain data integrity
- **Validation**: Database constraints and business rules
- **Versioning**: Year-based regulation tracking

## 📋 **Entity Definitions**

### **Water Bodies**
- **Primary**: Lakes, rivers, streams, ponds
- **Location**: GPS coordinates, state/county
- **Metadata**: Surface area, depth, descriptions
- **Species**: Many-to-many with fish species

### **Fishing Regulations**
```sql
-- Core regulation structure
regulation_year INTEGER          -- 2025, 2026, etc.
effective_date DATE             -- When regulation takes effect
season_open_date DATE           -- Season start
season_close_date DATE          -- Season end
daily_limit INTEGER             -- Fish per day
minimum_size_inches DECIMAL     -- Minimum size
protected_slot_min_inches       -- Protected slot start
protected_slot_max_inches       -- Protected slot end
special_regulations TEXT[]      -- Array of special rules
```

### **Users & Access Control**
```sql
user_role VARCHAR(20)           -- 'angler', 'contributor', 'moderator', 'admin'
- angler: Read-only access
- contributor: Can upload documents
- moderator: Can review regulations
- admin: Full system access
```

## 🔧 **Performance Features**

### **Indexes**
```sql
-- Text search indexes
CREATE INDEX idx_water_bodies_name_text ON water_bodies 
USING gin(to_tsvector('english', name));

-- Geographic indexes
CREATE INDEX idx_water_bodies_location ON water_bodies(latitude, longitude);

-- Regulation lookup indexes
CREATE INDEX idx_fishing_regulations_water_body ON fishing_regulations(water_body_id);
CREATE INDEX idx_fishing_regulations_season ON fishing_regulations(season_open_date, season_close_date);
```

### **Views**
```sql
-- Pre-computed lake summary
lake_summary: Water body + regulation counts + species lists

-- Current regulations view
current_fishing_regulations: Active regulations with denormalized data
```

### **Triggers**
```sql
-- Automatic timestamp updates
update_updated_at_column()

-- Audit logging
audit_fishing_regulations_changes()
```

## 🔍 **Query Patterns**

### **Common Use Cases**
1. **Lake Lookup**: Find regulations for specific water body
2. **Species Search**: Find regulations for specific fish species
3. **Geographic Search**: Find nearby lakes
4. **Season Check**: Current open/closed seasons
5. **User Favorites**: Personal lake lists

### **Example Queries**
```sql
-- Find current walleye regulations for Mille Lacs
SELECT * FROM current_fishing_regulations 
WHERE water_body_name = 'Mille Lacs Lake' 
  AND species_name = 'Walleye';

-- Find lakes near coordinates with active regulations
SELECT * FROM lake_summary 
WHERE latitude BETWEEN 46.0 AND 47.0 
  AND longitude BETWEEN -95.0 AND -93.0 
  AND regulation_count > 0;

-- Search lakes by name
SELECT * FROM water_bodies 
WHERE to_tsvector('english', name) @@ plainto_tsquery('english', 'superior');
```

## 🚀 **Setup Instructions**

### **1. Database Creation**
```bash
# Create database
createdb fishing_regs

# Run schema
psql -d fishing_regs -f schema.sql

# Load sample data
psql -d fishing_regs -f sample-data.sql
```

### **2. Entity Framework Setup**
```bash
# Install EF tools
dotnet tool install --global dotnet-ef

# Add migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

### **3. Connection String**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fishing_regs;Username=postgres;Password=your_password"
  }
}
```

## 📈 **Data Volumes & Scaling**

### **Expected Data Sizes**
- **States**: ~50 records
- **Counties**: ~3,000 records  
- **Water Bodies**: ~50,000 records
- **Fish Species**: ~200 records
- **Regulations**: ~500,000+ records (growing annually)
- **Users**: Unlimited growth
- **Search History**: High volume, consider archiving

### **Scaling Considerations**
- **Partitioning**: Regulations by year for large datasets
- **Archiving**: Old regulations and search history
- **Read Replicas**: For high-traffic read operations
- **Caching**: Redis for frequently accessed data

## 🔐 **Security & Compliance**

### **Data Protection**
- **PII Handling**: User data encrypted at rest
- **Audit Trail**: Complete change tracking
- **Access Control**: Role-based permissions
- **Data Retention**: Configurable retention policies

### **PostgreSQL Security**
- **SSL Required**: All connections encrypted
- **Row Level Security**: Future enhancement for multi-tenant
- **Backup Encryption**: Azure automated backups
- **Connection Pooling**: PgBouncer for production

## 🧪 **Testing Strategy**

### **Sample Data**
- Real Minnesota lakes and species
- Realistic regulation scenarios
- Multiple years of data
- Various edge cases (year-round seasons, slot limits)

### **Test Scenarios**
- Season boundary calculations
- Slot limit logic
- AI confidence scoring
- User role permissions
- Search performance

## 📚 **Additional Resources**

- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Entity Framework Core with PostgreSQL](https://www.npgsql.org/efcore/)
- [Azure Database for PostgreSQL](https://docs.microsoft.com/en-us/azure/postgresql/)
- [PostGIS for Geographic Data](https://postgis.net/) (future enhancement)
