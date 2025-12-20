-- Create BoardMeetings table
-- This table stores meeting records for project boards

CREATE TABLE BoardMeetings (
    Id INT PRIMARY KEY IDENTITY(1,1),  -- Auto-increment primary key
    BoardId NVARCHAR(50) NOT NULL,     -- Foreign key to ProjectBoards table
    MeetingTime DATETIME2 NOT NULL,   -- Meeting date and time
    
    -- Foreign key constraint
    CONSTRAINT FK_BoardMeetings_ProjectBoards FOREIGN KEY (BoardId) REFERENCES ProjectBoards(BoardId) ON DELETE CASCADE,
    
    -- Indexes for performance
    INDEX IX_BoardMeetings_BoardId (BoardId),
    INDEX IX_BoardMeetings_MeetingTime (MeetingTime)
);

-- Add comments for documentation
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Stores meeting records for project boards', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'BoardMeetings';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Primary key (auto-increment)', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'BoardMeetings', 
    @level2type = N'COLUMN', @level2name = N'Id';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Foreign key to ProjectBoards table (BoardId)', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'BoardMeetings', 
    @level2type = N'COLUMN', @level2name = N'BoardId';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Meeting date and time', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'BoardMeetings', 
    @level2type = N'COLUMN', @level2name = N'MeetingTime';

