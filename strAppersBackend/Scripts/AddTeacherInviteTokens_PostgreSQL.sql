-- Teacher invite tokens: one-time tokens sent by email to complete teacher registration.
CREATE TABLE "TeacherInviteTokens" (
    "Id"         SERIAL PRIMARY KEY,
    "TeacherId"  INTEGER NOT NULL REFERENCES "Teachers"("Id") ON DELETE CASCADE,
    "Token"      VARCHAR(128) NOT NULL UNIQUE,
    "ExpiresAt"  TIMESTAMP NOT NULL,
    "UsedAt"     TIMESTAMP NULL,
    "CreatedAt"  TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX "IX_TeacherInviteTokens_Token" ON "TeacherInviteTokens"("Token");
CREATE INDEX "IX_TeacherInviteTokens_TeacherId" ON "TeacherInviteTokens"("TeacherId");
