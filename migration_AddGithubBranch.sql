-- Migration: Add GithubBranch to BoardStates
-- Generated: 2026-01-17

-- Add GithubBranch column to BoardStates table
ALTER TABLE "BoardStates" 
ADD COLUMN "GithubBranch" character varying(255) NULL;

-- Note: The migration also drops and recreates the unique index IX_BoardStates_BoardId_Source
-- This is handled automatically by EF Core migrations
