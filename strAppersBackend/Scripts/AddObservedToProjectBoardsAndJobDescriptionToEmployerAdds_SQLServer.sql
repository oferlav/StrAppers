-- Add Observed field to ProjectBoards table and JobDescription field to EmployerAdds table
-- Run this script on both DEV and PROD SQL Server databases

-- ============================================
-- 1. Add 'Observed' column to ProjectBoards table
-- ============================================
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[ProjectBoards]') 
    AND name = 'Observed'
)
BEGIN
    ALTER TABLE [dbo].[ProjectBoards]
    ADD [Observed] BIT NOT NULL DEFAULT 0;
    
    -- Add extended property for documentation
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', 
        @value = N'Whether the project board is being observed', 
        @level0type = N'SCHEMA', @level0name = N'dbo', 
        @level1type = N'TABLE', @level1name = N'ProjectBoards', 
        @level2type = N'COLUMN', @level2name = N'Observed';
END
GO

-- ============================================
-- 2. Add 'JobDescription' column to EmployerAdds table
-- ============================================
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[EmployerAdds]') 
    AND name = 'JobDescription'
)
BEGIN
    ALTER TABLE [dbo].[EmployerAdds]
    ADD [JobDescription] NVARCHAR(MAX) NULL;
    
    -- Add extended property for documentation
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', 
        @value = N'Job description text for the employer add', 
        @level0type = N'SCHEMA', @level0name = N'dbo', 
        @level1type = N'TABLE', @level1name = N'EmployerAdds', 
        @level2type = N'COLUMN', @level2name = N'JobDescription';
END
GO

-- ============================================
-- Verification queries (optional - can be run separately)
-- ============================================
-- Verify ProjectBoards.Observed column
-- SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
-- FROM INFORMATION_SCHEMA.COLUMNS
-- WHERE TABLE_NAME = 'ProjectBoards'
-- AND COLUMN_NAME = 'Observed';

-- Verify EmployerAdds.JobDescription column
-- SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
-- FROM INFORMATION_SCHEMA.COLUMNS
-- WHERE TABLE_NAME = 'EmployerAdds'
-- AND COLUMN_NAME = 'JobDescription';




