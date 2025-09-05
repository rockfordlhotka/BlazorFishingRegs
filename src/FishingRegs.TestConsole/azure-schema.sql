-- =====================================================
-- Blazor Fishing Regulations Database Schema (PostgreSQL - Azure Compatible)
-- =====================================================
-- This schema is designed for storing fishing regulations,
-- lake information, and user management data.
-- Modified for Azure PostgreSQL compatibility (no extensions)
-- =====================================================

-- =====================================================
-- 1. CORE ENTITIES
-- =====================================================

-- States/Provinces table
CREATE TABLE IF NOT EXISTS states (
    id SERIAL PRIMARY KEY,
    code VARCHAR(2) NOT NULL UNIQUE, -- 'MN', 'WI', etc.
    name VARCHAR(100) NOT NULL,
    country VARCHAR(2) NOT NULL DEFAULT 'US',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Counties table
CREATE TABLE IF NOT EXISTS counties (
    id SERIAL PRIMARY KEY,
    state_id INTEGER NOT NULL REFERENCES states(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    fips_code VARCHAR(5), -- Federal Information Processing Standard code
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(state_id, name)
);

-- Fish species table
CREATE TABLE IF NOT EXISTS fish_species (
    id SERIAL PRIMARY KEY,
    common_name VARCHAR(100) NOT NULL,
    scientific_name VARCHAR(150),
    species_code VARCHAR(10), -- DNR species codes
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(common_name)
);

-- Water bodies (lakes, rivers, streams)
CREATE TABLE IF NOT EXISTS water_bodies (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    type VARCHAR(50) NOT NULL, -- 'lake', 'river', 'stream', 'pond', etc.
    state_id INTEGER NOT NULL REFERENCES states(id) ON DELETE CASCADE,
    county_id INTEGER REFERENCES counties(id) ON DELETE SET NULL,
    
    -- Physical characteristics
    surface_area_acres DECIMAL(10,2),
    max_depth_feet DECIMAL(8,2),
    
    -- Location data (basic lat/lon for now, no PostGIS)
    latitude DECIMAL(10, 8),
    longitude DECIMAL(11, 8),
    
    -- Administrative
    dnr_water_id VARCHAR(50), -- DNR's internal ID for this water body
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    UNIQUE(name, state_id, county_id)
);

-- =====================================================
-- 2. REGULATION DOCUMENTS
-- =====================================================

-- Documents uploaded for processing (PDFs, text files, etc.)
CREATE TABLE IF NOT EXISTS regulation_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    file_name VARCHAR(255) NOT NULL,
    original_file_name VARCHAR(255) NOT NULL,
    document_type VARCHAR(50) NOT NULL, -- 'pdf', 'text', 'html', etc.
    mime_type VARCHAR(100) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    
    -- Blob storage information
    blob_storage_url TEXT NOT NULL,
    blob_container VARCHAR(100) NOT NULL,
    
    -- Processing information
    processing_status VARCHAR(50) NOT NULL DEFAULT 'pending', -- 'pending', 'processing', 'completed', 'failed'
    processing_started_at TIMESTAMPTZ,
    processing_completed_at TIMESTAMPTZ,
    processing_error TEXT,
    extracted_data JSONB, -- Store structured data extracted from document
    confidence_score DECIMAL(5,4), -- AI confidence score for extraction
    
    -- Regulation metadata
    state_id INTEGER NOT NULL REFERENCES states(id) ON DELETE CASCADE,
    regulation_year INTEGER NOT NULL,
    upload_source VARCHAR(100) NOT NULL, -- 'web_upload', 'automated_import', etc.
    created_by VARCHAR(100), -- User ID or system identifier
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =====================================================
-- 3. FISHING REGULATIONS
-- =====================================================

-- Core fishing regulations table
CREATE TABLE IF NOT EXISTS fishing_regulations (
    id SERIAL PRIMARY KEY,
    water_body_id INTEGER NOT NULL REFERENCES water_bodies(id) ON DELETE CASCADE,
    species_id INTEGER NOT NULL REFERENCES fish_species(id) ON DELETE CASCADE,
    regulation_document_id UUID REFERENCES regulation_documents(id) ON DELETE SET NULL,
    
    -- Regulation details
    regulation_type VARCHAR(50) NOT NULL, -- 'daily_limit', 'possession_limit', 'size_limit', 'season', etc.
    daily_limit INTEGER,
    possession_limit INTEGER,
    minimum_size_inches DECIMAL(5,2),
    maximum_size_inches DECIMAL(5,2),
    
    -- Season information
    season_start_month INTEGER, -- 1-12
    season_start_day INTEGER,   -- 1-31
    season_end_month INTEGER,   -- 1-12
    season_end_day INTEGER,     -- 1-31
    
    -- Special regulations
    is_catch_and_release BOOLEAN NOT NULL DEFAULT false,
    protected_slot_min_inches DECIMAL(5,2),
    protected_slot_max_inches DECIMAL(5,2),
    special_regulations TEXT[], -- Array of special regulation text
    required_stamps TEXT[], -- Array of required fishing stamps/licenses
    
    -- Administrative
    effective_date DATE NOT NULL,
    expiration_date DATE,
    regulation_year INTEGER NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    notes TEXT,
    
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Ensure one regulation per species per water body per year
    UNIQUE(water_body_id, species_id, regulation_year, regulation_type)
);

-- =====================================================
-- 4. ANALYTICS AND VIEWS
-- =====================================================

-- Materialized view for current fishing regulations (performance optimization)
CREATE MATERIALIZED VIEW IF NOT EXISTS current_fishing_regulations_view AS
SELECT 
    fr.id,
    fr.water_body_id,
    wb.name as water_body_name,
    wb.type as water_body_type,
    wb.county_id,
    c.name as county_name,
    wb.state_id,
    s.name as state_name,
    s.code as state_code,
    fr.species_id,
    fs.common_name as species_name,
    fs.scientific_name,
    fr.regulation_type,
    fr.daily_limit,
    fr.possession_limit,
    fr.minimum_size_inches,
    fr.maximum_size_inches,
    fr.season_start_month,
    fr.season_start_day,
    fr.season_end_month,
    fr.season_end_day,
    fr.is_catch_and_release,
    fr.protected_slot_min_inches,
    fr.protected_slot_max_inches,
    fr.special_regulations,
    fr.required_stamps,
    fr.effective_date,
    fr.expiration_date,
    fr.regulation_year,
    fr.notes,
    fr.updated_at
FROM fishing_regulations fr
JOIN water_bodies wb ON fr.water_body_id = wb.id
JOIN fish_species fs ON fr.species_id = fs.id
JOIN states s ON wb.state_id = s.id
LEFT JOIN counties c ON wb.county_id = c.id
WHERE fr.is_active = true 
  AND wb.is_active = true 
  AND fs.is_active = true
  AND (fr.expiration_date IS NULL OR fr.expiration_date > CURRENT_DATE);

-- =====================================================
-- 5. AUDIT AND HISTORY
-- =====================================================

-- Audit log for regulation changes
CREATE TABLE IF NOT EXISTS regulation_audit_log (
    id SERIAL PRIMARY KEY,
    regulation_id INTEGER NOT NULL, -- References fishing_regulations.id
    action VARCHAR(20) NOT NULL, -- 'INSERT', 'UPDATE', 'DELETE'
    changed_fields JSONB, -- What fields were changed
    old_values JSONB, -- Previous values
    new_values JSONB, -- New values
    changed_by VARCHAR(100), -- User who made the change
    changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason TEXT -- Optional reason for the change
);

-- =====================================================
-- 6. SEARCH AND PERFORMANCE INDEXES
-- =====================================================

-- Primary search indexes
CREATE INDEX IF NOT EXISTS idx_water_bodies_name ON water_bodies(name);
CREATE INDEX IF NOT EXISTS idx_water_bodies_state_county ON water_bodies(state_id, county_id);
CREATE INDEX IF NOT EXISTS idx_water_bodies_location ON water_bodies(latitude, longitude) WHERE latitude IS NOT NULL AND longitude IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_fish_species_common_name ON fish_species(common_name);
CREATE INDEX IF NOT EXISTS idx_fish_species_active ON fish_species(is_active) WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_fishing_regulations_water_body ON fishing_regulations(water_body_id);
CREATE INDEX IF NOT EXISTS idx_fishing_regulations_species ON fishing_regulations(species_id);
CREATE INDEX IF NOT EXISTS idx_fishing_regulations_year ON fishing_regulations(regulation_year);
CREATE INDEX IF NOT EXISTS idx_fishing_regulations_active ON fishing_regulations(is_active) WHERE is_active = true;
CREATE INDEX IF NOT EXISTS idx_fishing_regulations_effective ON fishing_regulations(effective_date, expiration_date);

CREATE INDEX IF NOT EXISTS idx_regulation_documents_status ON regulation_documents(processing_status);
CREATE INDEX IF NOT EXISTS idx_regulation_documents_year ON regulation_documents(regulation_year);

-- =====================================================
-- 7. INITIAL DATA
-- =====================================================

-- Insert initial states
INSERT INTO states (code, name, country) VALUES 
    ('MN', 'Minnesota', 'US'),
    ('WI', 'Wisconsin', 'US'),
    ('IA', 'Iowa', 'US'),
    ('ND', 'North Dakota', 'US'),
    ('SD', 'South Dakota', 'US')
ON CONFLICT (code) DO NOTHING;

-- Insert some common fish species
INSERT INTO fish_species (common_name, scientific_name, species_code) VALUES 
    ('Northern Pike', 'Esox lucius', 'NP'),
    ('Walleye', 'Sander vitreus', 'WAE'),
    ('Largemouth Bass', 'Micropterus salmoides', 'LMB'),
    ('Smallmouth Bass', 'Micropterus dolomieu', 'SMB'),
    ('Lake Trout', 'Salvelinus namaycush', 'LT'),
    ('Brook Trout', 'Salvelinus fontinalis', 'BT'),
    ('Brown Trout', 'Salmo trutta', 'BNT'),
    ('Rainbow Trout', 'Oncorhynchus mykiss', 'RBT'),
    ('Muskellunge', 'Esox masquinongy', 'MUE'),
    ('Yellow Perch', 'Perca flavescens', 'YP'),
    ('Bluegill', 'Lepomis macrochirus', 'BG'),
    ('Crappie', 'Pomoxis spp.', 'CRP'),
    ('Steelhead', 'Oncorhynchus mykiss', 'STH'),
    ('Chinook Salmon', 'Oncorhynchus tshawytscha', 'CHS'),
    ('Coho Salmon', 'Oncorhynchus kisutch', 'COS')
ON CONFLICT (common_name) DO NOTHING;

-- Insert some Minnesota counties (partial list)
INSERT INTO counties (state_id, name) 
SELECT s.id, county_name 
FROM states s, (VALUES 
    ('Aitkin'), ('Anoka'), ('Becker'), ('Beltrami'), ('Benton'),
    ('Big Stone'), ('Blue Earth'), ('Brown'), ('Carlton'), ('Carver'),
    ('Cass'), ('Chippewa'), ('Chisago'), ('Clay'), ('Clearwater'),
    ('Cook'), ('Cottonwood'), ('Crow Wing'), ('Dakota'), ('Dodge'),
    ('Douglas'), ('Faribault'), ('Fillmore'), ('Freeborn'), ('Goodhue'),
    ('Grant'), ('Hennepin'), ('Houston'), ('Hubbard'), ('Isanti'),
    ('Itasca'), ('Jackson'), ('Kanabec'), ('Kandiyohi'), ('Kittson'),
    ('Koochiching'), ('Lac qui Parle'), ('Lake'), ('Lake of the Woods'), ('Le Sueur'),
    ('Lincoln'), ('Lyon'), ('McLeod'), ('Mahnomen'), ('Marshall'),
    ('Martin'), ('Meeker'), ('Mille Lacs'), ('Morrison'), ('Mower'),
    ('Murray'), ('Nicollet'), ('Nobles'), ('Norman'), ('Olmsted'),
    ('Otter Tail'), ('Pennington'), ('Pine'), ('Pipestone'), ('Polk'),
    ('Pope'), ('Ramsey'), ('Red Lake'), ('Redwood'), ('Renville'),
    ('Rice'), ('Rock'), ('Roseau'), ('St. Louis'), ('Scott'),
    ('Sherburne'), ('Sibley'), ('Stearns'), ('Steele'), ('Stevens'),
    ('Swift'), ('Todd'), ('Traverse'), ('Wabasha'), ('Wadena'),
    ('Waseca'), ('Washington'), ('Watonwan'), ('Wilkin'), ('Winona'),
    ('Wright'), ('Yellow Medicine')
) AS county_data(county_name)
WHERE s.code = 'MN'
ON CONFLICT (state_id, name) DO NOTHING;

-- Refresh the materialized view
REFRESH MATERIALIZED VIEW current_fishing_regulations_view;
