-- AddQuestBoardsTable
-- Per-candidate infrastructure table for QuestMode.
-- One row per student per board; mirrors the squad-level infra fields in ProjectBoards.

CREATE TABLE IF NOT EXISTS "QuestBoards" (
    "Id"                 serial          PRIMARY KEY,
    "StudentId"          integer         NOT NULL,
    "BoardId"            varchar(50)     NOT NULL,
    "PublishUrl"         varchar(500)    NULL,
    "GithubFrontendUrl"  varchar(1000)   NULL,
    "GithubBackendUrl"   varchar(1000)   NULL,
    "WebApiUrl"          varchar(1000)   NULL,
    "DBPassword"         varchar(200)    NULL,
    "NeonBranchId"       varchar(100)    NULL,
    "NeonProjectId"      varchar(100)    NULL,
    "CreatedAt"          timestamptz     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt"          timestamptz     NULL,

    CONSTRAINT "FK_QuestBoards_Students_StudentId"
        FOREIGN KEY ("StudentId") REFERENCES "Students"("Id") ON DELETE CASCADE,

    CONSTRAINT "FK_QuestBoards_ProjectBoards_BoardId"
        FOREIGN KEY ("BoardId") REFERENCES "ProjectBoards"("BoardId") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_QuestBoards_StudentId"
    ON "QuestBoards" ("StudentId");

CREATE INDEX IF NOT EXISTS "IX_QuestBoards_BoardId"
    ON "QuestBoards" ("BoardId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_QuestBoards_StudentId_BoardId"
    ON "QuestBoards" ("StudentId", "BoardId");
