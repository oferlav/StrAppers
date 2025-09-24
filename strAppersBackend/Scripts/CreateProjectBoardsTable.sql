-- Create ProjectBoards table
-- This table stores Trello board information for each project

CREATE TABLE ProjectBoards (
    Id NVARCHAR(50) PRIMARY KEY,  -- Trello board ID (e.g., "68cfc87d3369798f4f80b32b")
    ProjectId INT NOT NULL,        -- Foreign key to Projects table
    StartDate DATETIME2 NULL,      -- Project start date
    EndDate DATETIME2 NULL,        -- Project end date
    DueDate DATETIME2 NULL,        -- Project due date
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),  -- Record creation timestamp
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),  -- Record update timestamp
    StatusId INT NULL,             -- Project status ID
    HasAdmin BIT NOT NULL DEFAULT 0,  -- Whether project has admin access
    
    -- Foreign key constraint
    CONSTRAINT FK_ProjectBoards_Projects FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
    
    -- Indexes for performance
    INDEX IX_ProjectBoards_ProjectId (ProjectId),
    INDEX IX_ProjectBoards_CreatedAt (CreatedAt),
    INDEX IX_ProjectBoards_StatusId (StatusId)
);

-- Add comments for documentation
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Stores Trello board information and project metadata for each project', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Trello board ID (primary key)', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'Id';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Foreign key to Projects table', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'ProjectId';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Project start date', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'StartDate';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Project end date', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'EndDate';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Project due date', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'DueDate';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Record creation timestamp', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'CreatedAt';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Record update timestamp', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'UpdatedAt';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Project status ID', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'StatusId';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Whether project has admin access', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'ProjectBoards', 
    @level2type = N'COLUMN', @level2name = N'HasAdmin';
