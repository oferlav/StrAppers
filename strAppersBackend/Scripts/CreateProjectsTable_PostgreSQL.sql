-- Create Projects table for PostgreSQL (if it doesn't exist)
-- This script creates the main Projects table that ProjectBoards references

-- Create Projects table
CREATE TABLE IF NOT EXISTS Projects (
    Id SERIAL PRIMARY KEY,
    Title VARCHAR(200) NOT NULL,
    Description VARCHAR(1000),
    ExtendedDescription TEXT,
    SystemDesign TEXT,
    SystemDesignDoc BYTEA,
    StatusId INTEGER NOT NULL,
    Priority VARCHAR(50) DEFAULT 'Medium',
    StartDate TIMESTAMP,
    EndDate TIMESTAMP,
    DueDate TIMESTAMP,
    OrganizationId INTEGER,
    HasAdmin BOOLEAN DEFAULT FALSE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP
);

-- Create ProjectStatus table (if it doesn't exist)
CREATE TABLE IF NOT EXISTS ProjectStatuses (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(50) NOT NULL,
    Description VARCHAR(200),
    Color VARCHAR(7) DEFAULT '#6B7280',
    SortOrder INTEGER DEFAULT 0,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP
);

-- Create Organizations table (if it doesn't exist)
CREATE TABLE IF NOT EXISTS Organizations (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Description VARCHAR(500),
    Website VARCHAR(200),
    ContactEmail VARCHAR(100),
    Phone VARCHAR(20),
    Address VARCHAR(200),
    Type VARCHAR(50),
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Add foreign key constraints (if they don't exist)
DO $$ 
BEGIN
    -- Add foreign key from Projects to ProjectStatuses
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_Projects_ProjectStatuses'
    ) THEN
        ALTER TABLE Projects 
        ADD CONSTRAINT FK_Projects_ProjectStatuses 
        FOREIGN KEY (StatusId) REFERENCES ProjectStatuses(Id) ON DELETE RESTRICT;
    END IF;

    -- Add foreign key from Projects to Organizations
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_Projects_Organizations'
    ) THEN
        ALTER TABLE Projects 
        ADD CONSTRAINT FK_Projects_Organizations 
        FOREIGN KEY (OrganizationId) REFERENCES Organizations(Id) ON DELETE SET NULL;
    END IF;
END $$;

-- Create indexes
CREATE INDEX IF NOT EXISTS IX_Projects_StatusId ON Projects(StatusId);
CREATE INDEX IF NOT EXISTS IX_Projects_OrganizationId ON Projects(OrganizationId);
CREATE INDEX IF NOT EXISTS IX_Projects_CreatedAt ON Projects(CreatedAt);

-- Insert default ProjectStatuses if they don't exist
INSERT INTO ProjectStatuses (Id, Name, Description, Color, SortOrder, IsActive, CreatedAt)
VALUES 
    (1, 'New', 'Newly created project', '#10B981', 1, TRUE, CURRENT_TIMESTAMP),
    (2, 'Planning', 'Project in planning phase', '#3B82F6', 2, TRUE, CURRENT_TIMESTAMP),
    (3, 'In Progress', 'Project currently being worked on', '#F59E0B', 3, TRUE, CURRENT_TIMESTAMP),
    (4, 'On Hold', 'Project temporarily paused', '#EF4444', 4, TRUE, CURRENT_TIMESTAMP),
    (5, 'Completed', 'Project successfully completed', '#059669', 5, TRUE, CURRENT_TIMESTAMP),
    (6, 'Cancelled', 'Project cancelled or abandoned', '#6B7280', 6, TRUE, CURRENT_TIMESTAMP)
ON CONFLICT (Id) DO NOTHING;

-- Insert default Organization if it doesn't exist
INSERT INTO Organizations (Id, Name, Description, Website, ContactEmail, Phone, Address, Type, IsActive, CreatedAt)
VALUES 
    (1, 'Tech University', 'Leading technology university', 'https://techuniversity.edu', 'info@techuniversity.edu', '555-0101', '123 Tech Street, Tech City', 'University', TRUE, CURRENT_TIMESTAMP)
ON CONFLICT (Id) DO NOTHING;

-- Add comments
COMMENT ON TABLE Projects IS 'Main projects table';
COMMENT ON TABLE ProjectStatuses IS 'Project status lookup table';
COMMENT ON TABLE Organizations IS 'Organizations table';





