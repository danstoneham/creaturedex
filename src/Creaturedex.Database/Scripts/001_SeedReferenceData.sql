-- ============================================================
-- 001_SeedReferenceData.sql
-- Idempotent seed data for all reference tables.
-- Safe to run multiple times; all INSERTs are guarded with
-- IF NOT EXISTS checks on the Code column.
-- ============================================================

-- ------------------------------------------------------------
-- Conservation Statuses
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM ReferenceConservationStatuses WHERE Code = 'EX')
INSERT INTO ReferenceConservationStatuses (Code, Name, Description, Severity, Colour) VALUES
('EX', 'Extinct', 'No known living individuals remain', 1, '#000000'),
('EW', 'Extinct in the Wild', 'Survives only in captivity or outside native range', 2, '#3D0751'),
('CR', 'Critically Endangered', 'Facing an extremely high risk of extinction in the wild', 3, '#CC3333'),
('EN', 'Endangered', 'Facing a very high risk of extinction in the wild', 4, '#CC6633'),
('VU', 'Vulnerable', 'Facing a high risk of extinction in the wild', 5, '#CC9900'),
('NT', 'Near Threatened', 'Close to qualifying for a threatened category in the near future', 6, '#CCcc00'),
('LC', 'Least Concern', 'Evaluated with a lower risk of extinction', 7, '#006666'),
('DD', 'Data Deficient', 'Not enough data to assess extinction risk', 8, '#808080'),
('NE', 'Not Evaluated', 'Has not been evaluated against the criteria', 9, '#FFFFFF');

-- ------------------------------------------------------------
-- Diet Types
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM ReferenceDietTypes WHERE Code = 'carnivore')
INSERT INTO ReferenceDietTypes (Code, Name, SortOrder) VALUES
('carnivore', 'Carnivore', 1),
('herbivore', 'Herbivore', 2),
('omnivore', 'Omnivore', 3),
('insectivore', 'Insectivore', 4),
('piscivore', 'Piscivore', 5),
('frugivore', 'Frugivore', 6),
('nectarivore', 'Nectarivore', 7),
('filter-feeder', 'Filter Feeder', 8),
('scavenger', 'Scavenger', 9),
('detritivore', 'Detritivore', 10),
('granivore', 'Granivore', 11),
('folivore', 'Folivore', 12),
('myrmecophage', 'Myrmecophage', 13);

-- ------------------------------------------------------------
-- Activity Patterns
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM ReferenceActivityPatterns WHERE Code = 'diurnal')
INSERT INTO ReferenceActivityPatterns (Code, Name, SortOrder) VALUES
('diurnal', 'Diurnal', 1),
('nocturnal', 'Nocturnal', 2),
('crepuscular', 'Crepuscular', 3),
('cathemeral', 'Cathemeral', 4);

-- ------------------------------------------------------------
-- Domestication Statuses
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM ReferenceDomesticationStatuses WHERE Code = 'domesticated')
INSERT INTO ReferenceDomesticationStatuses (Code, Name, IsPet, SortOrder) VALUES
('domesticated', 'Domesticated', 1, 1),
('semi-domesticated', 'Semi-domesticated', 1, 2),
('captive-bred', 'Captive-bred', 0, 3),
('wild', 'Wild', 0, 4);

-- ------------------------------------------------------------
-- Colours
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM ReferenceColours WHERE Code = 'black')
INSERT INTO ReferenceColours (Code, Name, HexValue, SortOrder) VALUES
('black', 'Black', '#000000', 1),
('white', 'White', '#FFFFFF', 2),
('grey', 'Grey', '#808080', 3),
('silver', 'Silver', '#C0C0C0', 4),
('brown', 'Brown', '#8B4513', 5),
('tan', 'Tan', '#D2B48C', 6),
('cream', 'Cream', '#FFFDD0', 7),
('ivory', 'Ivory', '#FFFFF0', 8),
('gold', 'Gold', '#FFD700', 9),
('yellow', 'Yellow', '#FFD500', 10),
('orange', 'Orange', '#FF8C00', 11),
('red', 'Red', '#CC0000', 12),
('russet', 'Russet', '#80461B', 13),
('chestnut', 'Chestnut', '#954535', 14),
('tawny', 'Tawny', '#CD7F32', 15),
('copper', 'Copper', '#B87333', 16),
('pink', 'Pink', '#FFC0CB', 17),
('blue', 'Blue', '#0000FF', 18),
('green', 'Green', '#008000', 19),
('teal', 'Teal', '#008080', 20),
('olive', 'Olive', '#808000', 21),
('purple', 'Purple', '#800080', 22),
('violet', 'Violet', '#7F00FF', 23),
('buff', 'Buff', '#F0DC82', 24),
('albino', 'Albino', '#FAFAFA', 25),
('melanistic', 'Melanistic', '#1A1A1A', 26),
('spotted', 'Spotted Pattern', NULL, 27),
('striped', 'Striped Pattern', NULL, 28),
('banded', 'Banded Pattern', NULL, 29),
('mottled', 'Mottled Pattern', NULL, 30),
('iridescent', 'Iridescent', NULL, 31),
('translucent', 'Translucent', NULL, 32);

-- ------------------------------------------------------------
-- Habitat Types
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM ReferenceHabitatTypes WHERE Code = 'forest')
INSERT INTO ReferenceHabitatTypes (Code, Name, SortOrder) VALUES
('forest', 'Forest', 1),
('rainforest', 'Rainforest', 2),
('woodland', 'Woodland', 3),
('savanna', 'Savanna', 4),
('grassland', 'Grassland', 5),
('shrubland', 'Shrubland', 6),
('desert', 'Desert', 7),
('tundra', 'Tundra', 8),
('wetland', 'Wetland', 9),
('marsh', 'Marsh', 10),
('swamp', 'Swamp', 11),
('mangrove', 'Mangrove', 12),
('freshwater', 'Freshwater', 13),
('river', 'River/Stream', 14),
('lake', 'Lake', 15),
('marine', 'Marine', 16),
('reef', 'Coral Reef', 17),
('deep-sea', 'Deep Sea', 18),
('coastal', 'Coastal', 19),
('cave', 'Cave', 20),
('mountain', 'Mountain', 21),
('farmland', 'Farmland', 22),
('urban', 'Urban', 23),
('estuary', 'Estuary', 24),
('alpine', 'Alpine', 25);

-- ------------------------------------------------------------
-- Tags
-- ------------------------------------------------------------

-- Body type tags
IF NOT EXISTS (SELECT 1 FROM ReferenceTags WHERE Code = 'big-cat')
INSERT INTO ReferenceTags (Code, Name, TagGroup, SortOrder) VALUES
('big-cat', 'Big Cat', 'body-type', 1),
('raptor', 'Raptor', 'body-type', 2),
('primate', 'Primate', 'body-type', 3),
('canine', 'Canine', 'body-type', 4),
('rodent', 'Rodent', 'body-type', 5),
('marsupial', 'Marsupial', 'body-type', 6),
('cetacean', 'Cetacean', 'body-type', 7),
('ungulate', 'Ungulate', 'body-type', 8),
('reptile', 'Reptile', 'body-type', 9),
('amphibian', 'Amphibian', 'body-type', 10),
('arachnid', 'Arachnid', 'body-type', 11),
('crustacean', 'Crustacean', 'body-type', 12),
('mollusc', 'Mollusc', 'body-type', 13),
('insect', 'Insect', 'body-type', 14),
('shark', 'Shark', 'body-type', 15),
('ray', 'Ray', 'body-type', 16),
('parrot', 'Parrot', 'body-type', 17),
('songbird', 'Songbird', 'body-type', 18),
('wading-bird', 'Wading Bird', 'body-type', 19),
('waterfowl', 'Waterfowl', 'body-type', 20),
('snake', 'Snake', 'body-type', 21),
('lizard', 'Lizard', 'body-type', 22),
('turtle', 'Turtle/Tortoise', 'body-type', 23),
('bear', 'Bear', 'body-type', 24),
('bat', 'Bat', 'body-type', 25),
('pinniped', 'Pinniped', 'body-type', 26),
('feline', 'Feline', 'body-type', 27),
('cephalopod', 'Cephalopod', 'body-type', 28),
('coral', 'Coral', 'body-type', 29),
('fish', 'Fish', 'body-type', 30);

-- Behaviour tags
IF NOT EXISTS (SELECT 1 FROM ReferenceTags WHERE Code = 'predator')
INSERT INTO ReferenceTags (Code, Name, TagGroup, SortOrder) VALUES
('predator', 'Predator', 'behaviour', 1),
('apex-predator', 'Apex Predator', 'behaviour', 2),
('scavenger', 'Scavenger', 'behaviour', 3),
('migratory', 'Migratory', 'behaviour', 4),
('pack-hunter', 'Pack Hunter', 'behaviour', 5),
('solitary', 'Solitary', 'behaviour', 6),
('social', 'Social', 'behaviour', 7),
('burrowing', 'Burrowing', 'behaviour', 8),
('arboreal', 'Arboreal', 'behaviour', 9),
('aquatic', 'Aquatic', 'behaviour', 10),
('semi-aquatic', 'Semi-aquatic', 'behaviour', 11),
('camouflage', 'Camouflage', 'behaviour', 12),
('territorial', 'Territorial', 'behaviour', 13),
('ambush-hunter', 'Ambush Hunter', 'behaviour', 14),
('parasitic', 'Parasitic', 'behaviour', 15),
('symbiotic', 'Symbiotic', 'behaviour', 16);

-- Habitat association tags
IF NOT EXISTS (SELECT 1 FROM ReferenceTags WHERE Code = 'arctic')
INSERT INTO ReferenceTags (Code, Name, TagGroup, SortOrder) VALUES
('arctic', 'Arctic', 'habitat', 1),
('desert-adapted', 'Desert Adapted', 'habitat', 2),
('tropical', 'Tropical', 'habitat', 3),
('marine', 'Marine', 'habitat', 4),
('freshwater', 'Freshwater', 'habitat', 5),
('mountain', 'Mountain', 'habitat', 6),
('grassland', 'Grassland', 'habitat', 7),
('urban-adapted', 'Urban Adapted', 'habitat', 8),
('deep-sea', 'Deep Sea', 'habitat', 9),
('cave-dwelling', 'Cave Dwelling', 'habitat', 10);

-- Human relevance tags
IF NOT EXISTS (SELECT 1 FROM ReferenceTags WHERE Code = 'endangered')
INSERT INTO ReferenceTags (Code, Name, TagGroup, SortOrder) VALUES
('endangered', 'Endangered', 'human-relevance', 1),
('pet', 'Pet', 'human-relevance', 2),
('working-animal', 'Working Animal', 'human-relevance', 3),
('invasive', 'Invasive Species', 'human-relevance', 4),
('livestock', 'Livestock', 'human-relevance', 5),
('pollinator', 'Pollinator', 'human-relevance', 6),
('pest', 'Pest', 'human-relevance', 7),
('keystone-species', 'Keystone Species', 'human-relevance', 8),
('flagship-species', 'Flagship Species', 'human-relevance', 9);

-- Notable trait tags
IF NOT EXISTS (SELECT 1 FROM ReferenceTags WHERE Code = 'flightless')
INSERT INTO ReferenceTags (Code, Name, TagGroup, SortOrder) VALUES
('flightless', 'Flightless', 'notable-trait', 1),
('hibernating', 'Hibernating', 'notable-trait', 2),
('bioluminescent', 'Bioluminescent', 'notable-trait', 3),
('tool-user', 'Tool User', 'notable-trait', 4),
('electric', 'Electric', 'notable-trait', 5),
('colour-changing', 'Colour Changing', 'notable-trait', 6),
('echolocation', 'Echolocation', 'notable-trait', 7),
('regeneration', 'Regeneration', 'notable-trait', 8),
('longest-lived', 'Long-lived', 'notable-trait', 9),
('fastest', 'Fastest', 'notable-trait', 10),
('largest', 'Largest', 'notable-trait', 11),
('smallest', 'Smallest', 'notable-trait', 12),
('venomous', 'Venomous', 'notable-trait', 13),
('poisonous', 'Poisonous', 'notable-trait', 14),
('nocturnal', 'Nocturnal', 'notable-trait', 15);
