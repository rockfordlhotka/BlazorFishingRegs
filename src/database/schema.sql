-- =====================================================
-- Blazor Fishing Regulations Database Schema (PostgreSQL)
-- =====================================================
-- This schema is designed for storing fishing regulations,
-- lake information, and user management data.
-- =====================================================

-- Enable necessary PostgreSQL extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "postgis"; -- For geographic data if needed later

-- =====================================================
-- 1. CORE ENTITIES
-- =====================================================

-- States/Provinces table
CREATE TABLE states (
    id SERIAL PRIMARY KEY,
    code VARCHAR(2) NOT NULL UNIQUE, -- 'MN', 'WI', etc.
    name VARCHAR(100) NOT NULL,
    country VARCHAR(2) NOT NULL DEFAULT 'US',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Counties table
CREATE TABLE counties (
    id SERIAL PRIMARY KEY,
    state_id INTEGER NOT NULL REFERENCES states(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    fips_code VARCHAR(5), -- Federal Information Processing Standard code
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(state_id, name)
);

-- Fish species table
CREATE TABLE fish_species (
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
CREATE TABLE water_bodies (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    state_id INTEGER NOT NULL REFERENCES states(id),
    county_id INTEGER REFERENCES counties(id),
    water_type VARCHAR(20) NOT NULL DEFAULT 'lake', -- 'lake', 'river', 'stream', 'pond'
    dnr_id VARCHAR(50), -- State DNR identifier
    latitude DECIMAL(10, 8),
    longitude DECIMAL(11, 8),
    surface_area_acres DECIMAL(10, 2),
    max_depth_feet INTEGER,
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Indexes for spatial and text searches
    CONSTRAINT chk_water_type CHECK (water_type IN ('lake', 'river', 'stream', 'pond', 'reservoir'))
);

-- Many-to-many relationship between water bodies and fish species
CREATE TABLE water_body_species (
    id SERIAL PRIMARY KEY,
    water_body_id INTEGER NOT NULL REFERENCES water_bodies(id) ON DELETE CASCADE,
    species_id INTEGER NOT NULL REFERENCES fish_species(id) ON DELETE CASCADE,
    is_stocked BOOLEAN DEFAULT false,
    stocking_frequency VARCHAR(20), -- 'annual', 'biennial', 'irregular'
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(water_body_id, species_id)
);

-- =====================================================
-- 2. REGULATION DOCUMENTS AND PROCESSING
-- =====================================================

-- Regulation documents (source PDFs)
CREATE TABLE regulation_documents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    file_name VARCHAR(255) NOT NULL,
    original_file_name VARCHAR(255) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    mime_type VARCHAR(100) NOT NULL,
    blob_storage_url TEXT NOT NULL,
    blob_container VARCHAR(100) NOT NULL,
    state_id INTEGER NOT NULL REFERENCES states(id),
    regulation_year INTEGER NOT NULL,
    document_type VARCHAR(50) NOT NULL DEFAULT 'fishing_regulations',
    upload_source VARCHAR(50) NOT NULL DEFAULT 'manual', -- 'manual', 'automated', 'api'
    processing_status VARCHAR(20) NOT NULL DEFAULT 'pending',
    processing_started_at TIMESTAMPTZ,
    processing_completed_at TIMESTAMPTZ,
    processing_error TEXT,
    extracted_data JSONB, -- Raw AI extracted data
    confidence_score DECIMAL(5, 4), -- AI confidence (0.0000 to 1.0000)
    created_by VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT chk_processing_status CHECK (processing_status IN ('pending', 'processing', 'completed', 'failed', 'review_required')),
    CONSTRAINT chk_document_type CHECK (document_type IN ('fishing_regulations', 'special_regulations', 'emergency_closure')),
    CONSTRAINT chk_upload_source CHECK (upload_source IN ('manual', 'automated', 'api'))
);

-- =====================================================
-- 3. FISHING REGULATIONS
-- =====================================================

-- Main fishing regulations table
CREATE TABLE fishing_regulations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    water_body_id INTEGER NOT NULL REFERENCES water_bodies(id),
    species_id INTEGER NOT NULL REFERENCES fish_species(id),
    regulation_year INTEGER NOT NULL,
    source_document_id UUID REFERENCES regulation_documents(id),
    effective_date DATE NOT NULL,
    expiration_date DATE,
    
    -- Season information
    season_open_date DATE,
    season_close_date DATE,
    is_year_round BOOLEAN DEFAULT false,
    season_notes TEXT,
    
    -- Bag limits
    daily_limit INTEGER,
    possession_limit INTEGER,
    bag_limit_notes TEXT,
    
    -- Size limits (in inches)
    minimum_size_inches DECIMAL(5, 2),
    maximum_size_inches DECIMAL(5, 2),
    protected_slot_min_inches DECIMAL(5, 2), -- Slot limits
    protected_slot_max_inches DECIMAL(5, 2),
    protected_slot_exceptions INTEGER, -- Number allowed in slot
    size_limit_notes TEXT,
    
    -- Special regulations
    special_regulations TEXT[], -- Array of special regulation strings
    bait_restrictions TEXT,
    gear_restrictions TEXT,
    method_restrictions TEXT,
    
    -- License requirements
    requires_special_stamp BOOLEAN DEFAULT false,
    required_stamps TEXT[], -- Array of required stamp names
    
    -- Metadata
    is_active BOOLEAN NOT NULL DEFAULT true,
    confidence_score DECIMAL(5, 4), -- AI extraction confidence
    review_status VARCHAR(20) NOT NULL DEFAULT 'pending',
    reviewed_by VARCHAR(255),
    reviewed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT chk_review_status CHECK (review_status IN ('pending', 'approved', 'rejected', 'needs_revision')),
    CONSTRAINT chk_season_logic CHECK (
        (is_year_round = true AND season_open_date IS NULL AND season_close_date IS NULL) OR
        (is_year_round = false AND season_open_date IS NOT NULL)
    ),
    CONSTRAINT chk_protected_slot CHECK (
        (protected_slot_min_inches IS NULL AND protected_slot_max_inches IS NULL) OR
        (protected_slot_min_inches IS NOT NULL AND protected_slot_max_inches IS NOT NULL AND protected_slot_min_inches < protected_slot_max_inches)
    )
);

-- =====================================================
-- 4. USER MANAGEMENT
-- =====================================================

-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    azure_ad_object_id VARCHAR(255) UNIQUE, -- Azure AD B2C object ID
    email VARCHAR(255) NOT NULL UNIQUE,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    display_name VARCHAR(200),
    phone_number VARCHAR(20),
    preferred_state_id INTEGER REFERENCES states(id),
    user_role VARCHAR(20) NOT NULL DEFAULT 'angler',
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_login_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT chk_user_role CHECK (user_role IN ('angler', 'contributor', 'moderator', 'admin'))
);

-- User favorites (saved lakes/water bodies)
CREATE TABLE user_favorites (
    id SERIAL PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    water_body_id INTEGER NOT NULL REFERENCES water_bodies(id) ON DELETE CASCADE,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, water_body_id)
);

-- =====================================================
-- 5. SEARCH AND AUDIT
-- =====================================================

-- Search history for analytics
CREATE TABLE search_history (
    id SERIAL PRIMARY KEY,
    user_id UUID REFERENCES users(id),
    search_query TEXT NOT NULL,
    search_type VARCHAR(20) NOT NULL, -- 'lake', 'species', 'regulation', 'location'
    results_count INTEGER NOT NULL DEFAULT 0,
    ip_address INET,
    user_agent TEXT,
    session_id VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT chk_search_type CHECK (search_type IN ('lake', 'species', 'regulation', 'location', 'general'))
);

-- Audit log for regulation changes
CREATE TABLE regulation_audit_log (
    id SERIAL PRIMARY KEY,
    regulation_id UUID NOT NULL REFERENCES fishing_regulations(id),
    action VARCHAR(20) NOT NULL, -- 'created', 'updated', 'deleted', 'reviewed'
    changed_fields TEXT[], -- Array of field names that changed
    old_values JSONB, -- Previous values
    new_values JSONB, -- New values
    changed_by UUID REFERENCES users(id),
    change_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT chk_audit_action CHECK (action IN ('created', 'updated', 'deleted', 'reviewed', 'approved', 'rejected'))
);

-- =====================================================
-- 6. INDEXES FOR PERFORMANCE
-- =====================================================

-- Water bodies indexes
CREATE INDEX idx_water_bodies_state_county ON water_bodies(state_id, county_id);
CREATE INDEX idx_water_bodies_name_text ON water_bodies USING gin(to_tsvector('english', name));
CREATE INDEX idx_water_bodies_location ON water_bodies(latitude, longitude) WHERE latitude IS NOT NULL AND longitude IS NOT NULL;
CREATE INDEX idx_water_bodies_type ON water_bodies(water_type);

-- Fishing regulations indexes
CREATE INDEX idx_fishing_regulations_water_body ON fishing_regulations(water_body_id);
CREATE INDEX idx_fishing_regulations_species ON fishing_regulations(species_id);
CREATE INDEX idx_fishing_regulations_year ON fishing_regulations(regulation_year);
CREATE INDEX idx_fishing_regulations_active ON fishing_regulations(is_active) WHERE is_active = true;
CREATE INDEX idx_fishing_regulations_season ON fishing_regulations(season_open_date, season_close_date);
CREATE INDEX idx_fishing_regulations_effective ON fishing_regulations(effective_date, expiration_date);

-- Fish species indexes
CREATE INDEX idx_fish_species_name_text ON fish_species USING gin(to_tsvector('english', common_name));
CREATE INDEX idx_fish_species_active ON fish_species(is_active) WHERE is_active = true;

-- Document processing indexes
CREATE INDEX idx_regulation_documents_status ON regulation_documents(processing_status);
CREATE INDEX idx_regulation_documents_state_year ON regulation_documents(state_id, regulation_year);

-- User and search indexes
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_azure_id ON users(azure_ad_object_id);
CREATE INDEX idx_search_history_user_date ON search_history(user_id, created_at);
CREATE INDEX idx_audit_log_regulation_date ON regulation_audit_log(regulation_id, created_at);

-- =====================================================
-- 7. FUNCTIONS AND TRIGGERS
-- =====================================================

-- Function to update the updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers for automatic updated_at management
CREATE TRIGGER update_states_updated_at BEFORE UPDATE ON states FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_counties_updated_at BEFORE UPDATE ON counties FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_fish_species_updated_at BEFORE UPDATE ON fish_species FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_water_bodies_updated_at BEFORE UPDATE ON water_bodies FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_regulation_documents_updated_at BEFORE UPDATE ON regulation_documents FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_fishing_regulations_updated_at BEFORE UPDATE ON fishing_regulations FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_users_updated_at BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Audit trigger for fishing regulations
CREATE OR REPLACE FUNCTION audit_fishing_regulations_changes()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        INSERT INTO regulation_audit_log (regulation_id, action, old_values, created_at)
        VALUES (OLD.id, 'deleted', to_jsonb(OLD), NOW());
        RETURN OLD;
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO regulation_audit_log (regulation_id, action, old_values, new_values, created_at)
        VALUES (NEW.id, 'updated', to_jsonb(OLD), to_jsonb(NEW), NOW());
        RETURN NEW;
    ELSIF TG_OP = 'INSERT' THEN
        INSERT INTO regulation_audit_log (regulation_id, action, new_values, created_at)
        VALUES (NEW.id, 'created', to_jsonb(NEW), NOW());
        RETURN NEW;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER audit_fishing_regulations_trigger
    AFTER INSERT OR UPDATE OR DELETE ON fishing_regulations
    FOR EACH ROW EXECUTE FUNCTION audit_fishing_regulations_changes();

-- =====================================================
-- 8. VIEWS FOR COMMON QUERIES
-- =====================================================

-- View for complete lake information with regulations count
CREATE VIEW lake_summary AS
SELECT 
    wb.id,
    wb.name,
    wb.water_type,
    s.name as state_name,
    s.code as state_code,
    c.name as county_name,
    wb.latitude,
    wb.longitude,
    wb.surface_area_acres,
    wb.description,
    COUNT(fr.id) as regulation_count,
    COUNT(DISTINCT fr.species_id) as species_count,
    string_agg(DISTINCT fs.common_name, ', ' ORDER BY fs.common_name) as species_list
FROM water_bodies wb
LEFT JOIN states s ON wb.state_id = s.id
LEFT JOIN counties c ON wb.county_id = c.id
LEFT JOIN fishing_regulations fr ON wb.id = fr.water_body_id AND fr.is_active = true
LEFT JOIN fish_species fs ON fr.species_id = fs.id AND fs.is_active = true
WHERE wb.is_active = true
GROUP BY wb.id, wb.name, wb.water_type, s.name, s.code, c.name, 
         wb.latitude, wb.longitude, wb.surface_area_acres, wb.description;

-- View for current fishing regulations with all details
CREATE VIEW current_fishing_regulations AS
SELECT 
    fr.id,
    wb.name as water_body_name,
    fs.common_name as species_name,
    s.name as state_name,
    fr.regulation_year,
    fr.effective_date,
    fr.expiration_date,
    fr.season_open_date,
    fr.season_close_date,
    fr.is_year_round,
    fr.daily_limit,
    fr.possession_limit,
    fr.minimum_size_inches,
    fr.maximum_size_inches,
    fr.protected_slot_min_inches,
    fr.protected_slot_max_inches,
    fr.protected_slot_exceptions,
    fr.special_regulations,
    fr.bait_restrictions,
    fr.gear_restrictions,
    fr.requires_special_stamp,
    fr.required_stamps,
    fr.confidence_score,
    fr.review_status
FROM fishing_regulations fr
JOIN water_bodies wb ON fr.water_body_id = wb.id
JOIN fish_species fs ON fr.species_id = fs.id
JOIN states s ON wb.state_id = s.id
WHERE fr.is_active = true 
    AND wb.is_active = true 
    AND fs.is_active = true
    AND (fr.expiration_date IS NULL OR fr.expiration_date > CURRENT_DATE);

-- =====================================================
-- 9. SAMPLE DATA SETUP
-- =====================================================

-- Insert sample states
INSERT INTO states (code, name) VALUES 
('MN', 'Minnesota'),
('WI', 'Wisconsin'),
('MI', 'Michigan'),
('IA', 'Iowa'),
('ND', 'North Dakota'),
('SD', 'South Dakota');

-- Insert sample counties for Minnesota
INSERT INTO counties (state_id, name, fips_code) VALUES 
((SELECT id FROM states WHERE code = 'MN'), 'Cook County', '27031'),
((SELECT id FROM states WHERE code = 'MN'), 'Mille Lacs County', '27095'),
((SELECT id FROM states WHERE code = 'MN'), 'Hennepin County', '27053'),
((SELECT id FROM states WHERE code = 'MN'), 'Cass County', '27021'),
((SELECT id FROM states WHERE code = 'MN'), 'St. Louis County', '27137');

-- Insert common fish species
INSERT INTO fish_species (common_name, scientific_name, species_code) VALUES 
('Walleye', 'Sander vitreus', 'WAE'),
('Northern Pike', 'Esox lucius', 'NOP'),
('Largemouth Bass', 'Micropterus salmoides', 'LMB'),
('Smallmouth Bass', 'Micropterus dolomieu', 'SMB'),
('Lake Trout', 'Salvelinus namaycush', 'LAT'),
('Muskie', 'Esox masquinongy', 'MUE'),
('Yellow Perch', 'Perca flavescens', 'YEP'),
('Bluegill', 'Lepomis macrochirus', 'BLG'),
('Crappie', 'Pomoxis nigromaculatus', 'CRP'),
('Salmon', 'Salmo salar', 'SAL');

-- Comments for documentation
COMMENT ON TABLE water_bodies IS 'Stores information about lakes, rivers, and other water bodies';
COMMENT ON TABLE fishing_regulations IS 'Core table storing fishing regulations for each water body and species combination';
COMMENT ON TABLE regulation_documents IS 'Tracks uploaded PDF documents and their processing status';
COMMENT ON COLUMN fishing_regulations.confidence_score IS 'AI extraction confidence score (0.0 to 1.0)';
COMMENT ON COLUMN fishing_regulations.special_regulations IS 'Array of special regulation text extracted from documents';
COMMENT ON VIEW current_fishing_regulations IS 'Consolidated view of active fishing regulations with denormalized data for easy querying';
