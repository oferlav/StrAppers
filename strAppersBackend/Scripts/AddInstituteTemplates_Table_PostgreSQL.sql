-- InstituteTemplates: institute-scoped Task Builder template saves (FK Institutes, Projects).

CREATE TABLE IF NOT EXISTS "InstituteTemplates" (
    "Id" SERIAL NOT NULL,
    "InstituteId" INTEGER NOT NULL,
    "ProjectId" INTEGER NOT NULL,
    "Name" character varying(100) NOT NULL,
    "TrelloBoardJson" TEXT NOT NULL,
    CONSTRAINT "PK_InstituteTemplates" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_InstituteTemplates_Institutes_InstituteId" FOREIGN KEY ("InstituteId") REFERENCES "Institutes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_InstituteTemplates_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_InstituteTemplates_InstituteId" ON "InstituteTemplates" ("InstituteId");
CREATE INDEX IF NOT EXISTS "IX_InstituteTemplates_ProjectId" ON "InstituteTemplates" ("ProjectId");
CREATE INDEX IF NOT EXISTS "IX_InstituteTemplates_InstituteId_ProjectId" ON "InstituteTemplates" ("InstituteId", "ProjectId");
