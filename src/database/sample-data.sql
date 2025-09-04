-- Sample data for testing the Fishing Regulations database
-- This file populates the database with realistic test data

-- Insert sample counties for Minnesota
INSERT INTO counties (state_id, name, fips_code) VALUES 
((SELECT id FROM states WHERE code = 'MN'), 'Cook County', '27031'),
((SELECT id FROM states WHERE code = 'MN'), 'Mille Lacs County', '27095'),
((SELECT id FROM states WHERE code = 'MN'), 'Hennepin County', '27053'),
((SELECT id FROM states WHERE code = 'MN'), 'Cass County', '27021'),
((SELECT id FROM states WHERE code = 'MN'), 'St. Louis County', '27137'),
((SELECT id FROM states WHERE code = 'MN'), 'Itasca County', '27061'),
((SELECT id FROM states WHERE code = 'MN'), 'Crow Wing County', '27035');

-- Insert sample water bodies
INSERT INTO water_bodies (name, state_id, county_id, water_type, latitude, longitude, surface_area_acres, max_depth_feet, description) VALUES 
(
    'Lake Superior',
    (SELECT id FROM states WHERE code = 'MN'),
    (SELECT id FROM counties WHERE name = 'Cook County' AND state_id = (SELECT id FROM states WHERE code = 'MN')),
    'lake',
    47.7211,
    -89.8794,
    1400000,
    1332,
    'Large freshwater lake on the Minnesota-Canada border, known for lake trout and salmon fishing'
),
(
    'Mille Lacs Lake',
    (SELECT id FROM states WHERE code = 'MN'),
    (SELECT id FROM counties WHERE name = 'Mille Lacs County' AND state_id = (SELECT id FROM states WHERE code = 'MN')),
    'lake',
    46.1833,
    -93.6500,
    132516,
    42,
    'Popular walleye fishing destination in central Minnesota'
),
(
    'Lake Minnetonka',
    (SELECT id FROM states WHERE code = 'MN'),
    (SELECT id FROM counties WHERE name = 'Hennepin County' AND state_id = (SELECT id FROM states WHERE code = 'MN')),
    'lake',
    44.9167,
    -93.5833,
    14528,
    113,
    'Large recreational lake west of Minneapolis with diverse fish species'
),
(
    'Leech Lake',
    (SELECT id FROM states WHERE code = 'MN'),
    (SELECT id FROM counties WHERE name = 'Cass County' AND state_id = (SELECT id FROM states WHERE code = 'MN')),
    'lake',
    47.1500,
    -94.3500,
    111527,
    156,
    'Third largest lake in Minnesota, known for walleye and muskie'
),
(
    'Lake Vermilion',
    (SELECT id FROM states WHERE code = 'MN'),
    (SELECT id FROM counties WHERE name = 'St. Louis County' AND state_id = (SELECT id FROM states WHERE code = 'MN')),
    'lake',
    47.9167,
    -92.2833,
    39271,
    76,
    'Clear water lake in the Boundary Waters region'
);

-- Insert water body species relationships
INSERT INTO water_body_species (water_body_id, species_id, is_stocked, stocking_frequency, notes) VALUES 
-- Lake Superior species
((SELECT id FROM water_bodies WHERE name = 'Lake Superior'), (SELECT id FROM fish_species WHERE common_name = 'Lake Trout'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Lake Superior'), (SELECT id FROM fish_species WHERE common_name = 'Salmon'), true, 'annual', 'Stocked annually by DNR'),
((SELECT id FROM water_bodies WHERE name = 'Lake Superior'), (SELECT id FROM fish_species WHERE common_name = 'Northern Pike'), false, null, 'Native population'),

-- Mille Lacs Lake species
((SELECT id FROM water_bodies WHERE name = 'Mille Lacs Lake'), (SELECT id FROM fish_species WHERE common_name = 'Walleye'), false, null, 'Native population - famous walleye lake'),
((SELECT id FROM water_bodies WHERE name = 'Mille Lacs Lake'), (SELECT id FROM fish_species WHERE common_name = 'Northern Pike'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Mille Lacs Lake'), (SELECT id FROM fish_species WHERE common_name = 'Smallmouth Bass'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Mille Lacs Lake'), (SELECT id FROM fish_species WHERE common_name = 'Muskie'), false, null, 'Native population'),

-- Lake Minnetonka species
((SELECT id FROM water_bodies WHERE name = 'Lake Minnetonka'), (SELECT id FROM fish_species WHERE common_name = 'Largemouth Bass'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Lake Minnetonka'), (SELECT id FROM fish_species WHERE common_name = 'Northern Pike'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Lake Minnetonka'), (SELECT id FROM fish_species WHERE common_name = 'Walleye'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Lake Minnetonka'), (SELECT id FROM fish_species WHERE common_name = 'Bluegill'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Lake Minnetonka'), (SELECT id FROM fish_species WHERE common_name = 'Crappie'), false, null, 'Native population'),

-- Leech Lake species
((SELECT id FROM water_bodies WHERE name = 'Leech Lake'), (SELECT id FROM fish_species WHERE common_name = 'Walleye'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Leech Lake'), (SELECT id FROM fish_species WHERE common_name = 'Northern Pike'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Leech Lake'), (SELECT id FROM fish_species WHERE common_name = 'Muskie'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Leech Lake'), (SELECT id FROM fish_species WHERE common_name = 'Yellow Perch'), false, null, 'Native population'),

-- Lake Vermilion species
((SELECT id FROM water_bodies WHERE name = 'Lake Vermilion'), (SELECT id FROM fish_species WHERE common_name = 'Walleye'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Lake Vermilion'), (SELECT id FROM fish_species WHERE common_name = 'Northern Pike'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Lake Vermilion'), (SELECT id FROM fish_species WHERE common_name = 'Smallmouth Bass'), false, null, 'Native population'),
((SELECT id FROM water_bodies WHERE name = 'Lake Vermilion'), (SELECT id FROM fish_species WHERE common_name = 'Lake Trout'), false, null, 'Native population');

-- Insert sample regulation document
INSERT INTO regulation_documents (
    id,
    file_name,
    original_file_name,
    file_size_bytes,
    mime_type,
    blob_storage_url,
    blob_container,
    state_id,
    regulation_year,
    document_type,
    processing_status,
    processing_completed_at,
    confidence_score
) VALUES (
    uuid_generate_v4(),
    'mn_fishing_regs_2025.pdf',
    'Minnesota Fishing Regulations 2025.pdf',
    2048576,
    'application/pdf',
    'https://storage.blob.core.windows.net/regulations/mn_fishing_regs_2025.pdf',
    'regulations',
    (SELECT id FROM states WHERE code = 'MN'),
    2025,
    'fishing_regulations',
    'completed',
    NOW(),
    0.9524
);

-- Insert sample fishing regulations
INSERT INTO fishing_regulations (
    id,
    water_body_id,
    species_id,
    regulation_year,
    source_document_id,
    effective_date,
    expiration_date,
    season_open_date,
    season_close_date,
    is_year_round,
    daily_limit,
    possession_limit,
    minimum_size_inches,
    protected_slot_min_inches,
    protected_slot_max_inches,
    protected_slot_exceptions,
    special_regulations,
    bait_restrictions,
    requires_special_stamp,
    required_stamps,
    confidence_score,
    review_status
) VALUES 
-- Lake Superior - Lake Trout
(
    uuid_generate_v4(),
    (SELECT id FROM water_bodies WHERE name = 'Lake Superior'),
    (SELECT id FROM fish_species WHERE common_name = 'Lake Trout'),
    2025,
    (SELECT id FROM regulation_documents WHERE file_name = 'mn_fishing_regs_2025.pdf'),
    '2025-01-01',
    '2025-12-31',
    '2025-01-01',
    '2025-09-30',
    false,
    3,
    6,
    15.00,
    28.00,
    36.00,
    1,
    '["Barbless hooks required", "No live bait below 63 feet"]',
    'Artificial lures only in deep water',
    true,
    '["Lake Superior Stamp"]',
    0.9245,
    'approved'
),
-- Lake Superior - Salmon
(
    uuid_generate_v4(),
    (SELECT id FROM water_bodies WHERE name = 'Lake Superior'),
    (SELECT id FROM fish_species WHERE common_name = 'Salmon'),
    2025,
    (SELECT id FROM regulation_documents WHERE file_name = 'mn_fishing_regs_2025.pdf'),
    '2025-01-01',
    '2025-12-31',
    '2025-04-15',
    '2025-10-31',
    false,
    3,
    6,
    10.00,
    null,
    null,
    null,
    '["Standard salmon regulations"]',
    null,
    true,
    '["Lake Superior Stamp"]',
    0.9156,
    'approved'
),
-- Mille Lacs Lake - Walleye
(
    uuid_generate_v4(),
    (SELECT id FROM water_bodies WHERE name = 'Mille Lacs Lake'),
    (SELECT id FROM fish_species WHERE common_name = 'Walleye'),
    2025,
    (SELECT id FROM regulation_documents WHERE file_name = 'mn_fishing_regs_2025.pdf'),
    '2025-01-01',
    '2025-12-31',
    '2025-05-13',
    '2025-02-28',
    false,
    1,
    1,
    15.00,
    21.00,
    23.00,
    0,
    '["Immediate release of all walleye between 21-23 inches", "Special harvest regulations"]',
    null,
    false,
    '[]',
    0.9421,
    'approved'
),
-- Lake Minnetonka - Largemouth Bass
(
    uuid_generate_v4(),
    (SELECT id FROM water_bodies WHERE name = 'Lake Minnetonka'),
    (SELECT id FROM fish_species WHERE common_name = 'Largemouth Bass'),
    2025,
    (SELECT id FROM regulation_documents WHERE file_name = 'mn_fishing_regs_2025.pdf'),
    '2025-01-01',
    '2025-12-31',
    '2025-05-27',
    '2025-03-05',
    false,
    6,
    6,
    14.00,
    null,
    null,
    null,
    '["Standard bass regulations"]',
    null,
    false,
    '[]',
    0.8956,
    'approved'
),
-- Leech Lake - Muskie
(
    uuid_generate_v4(),
    (SELECT id FROM water_bodies WHERE name = 'Leech Lake'),
    (SELECT id FROM fish_species WHERE common_name = 'Muskie'),
    2025,
    (SELECT id FROM regulation_documents WHERE file_name = 'mn_fishing_regs_2025.pdf'),
    '2025-01-01',
    '2025-12-31',
    '2025-05-27',
    '2025-11-30',
    false,
    1,
    1,
    48.00,
    null,
    null,
    null,
    '["Muskie season opens Memorial Day weekend"]',
    null,
    false,
    '[]',
    0.9234,
    'approved'
);

-- Insert sample user
INSERT INTO users (
    id,
    email,
    first_name,
    last_name,
    display_name,
    preferred_state_id,
    user_role,
    last_login_at
) VALUES (
    uuid_generate_v4(),
    'angler@example.com',
    'John',
    'Fisher',
    'John Fisher',
    (SELECT id FROM states WHERE code = 'MN'),
    'angler',
    NOW() - INTERVAL '2 days'
);

-- Insert sample user favorites
INSERT INTO user_favorites (user_id, water_body_id, notes) VALUES 
(
    (SELECT id FROM users WHERE email = 'angler@example.com'),
    (SELECT id FROM water_bodies WHERE name = 'Mille Lacs Lake'),
    'Great walleye fishing - my go-to lake'
),
(
    (SELECT id FROM users WHERE email = 'angler@example.com'),
    (SELECT id FROM water_bodies WHERE name = 'Lake Minnetonka'),
    'Close to home, good for bass'
);

-- Insert sample search history
INSERT INTO search_history (user_id, search_query, search_type, results_count, ip_address) VALUES 
(
    (SELECT id FROM users WHERE email = 'angler@example.com'),
    'walleye regulations mille lacs',
    'regulation',
    3,
    '192.168.1.100'
),
(
    null, -- Anonymous search
    'lake superior fishing',
    'lake',
    5,
    '10.0.0.1'
),
(
    (SELECT id FROM users WHERE email = 'angler@example.com'),
    'bass season minnesota',
    'species',
    12,
    '192.168.1.100'
);

-- Verify data was inserted correctly
SELECT 'Data insertion completed successfully';
SELECT 'States:', COUNT(*) FROM states;
SELECT 'Counties:', COUNT(*) FROM counties;
SELECT 'Fish Species:', COUNT(*) FROM fish_species;
SELECT 'Water Bodies:', COUNT(*) FROM water_bodies;
SELECT 'Water Body Species:', COUNT(*) FROM water_body_species;
SELECT 'Regulation Documents:', COUNT(*) FROM regulation_documents;
SELECT 'Fishing Regulations:', COUNT(*) FROM fishing_regulations;
SELECT 'Users:', COUNT(*) FROM users;
SELECT 'User Favorites:', COUNT(*) FROM user_favorites;
SELECT 'Search History:', COUNT(*) FROM search_history;
