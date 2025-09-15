@echo off
echo Creating JoinRequests table...

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "CREATE TABLE IF NOT EXISTS \"JoinRequests\" (\"Id\" SERIAL PRIMARY KEY, \"ChannelName\" VARCHAR(100) NOT NULL, \"ChannelId\" VARCHAR(50) NOT NULL, \"StudentId\" INTEGER NOT NULL, \"StudentEmail\" VARCHAR(255) NOT NULL, \"StudentFirstName\" VARCHAR(100), \"StudentLastName\" VARCHAR(100), \"ProjectId\" INTEGER NOT NULL, \"ProjectTitle\" VARCHAR(200), \"JoinDate\" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP, \"Added\" BOOLEAN NOT NULL DEFAULT FALSE, \"AddedDate\" TIMESTAMP WITH TIME ZONE, \"Notes\" VARCHAR(500), \"ErrorMessage\" VARCHAR(1000));"

echo Creating indexes...

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "CREATE INDEX IF NOT EXISTS \"IX_JoinRequests_Added\" ON \"JoinRequests\"(\"Added\");"

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "CREATE INDEX IF NOT EXISTS \"IX_JoinRequests_ChannelId\" ON \"JoinRequests\"(\"ChannelId\");"

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "CREATE INDEX IF NOT EXISTS \"IX_JoinRequests_JoinDate\" ON \"JoinRequests\"(\"JoinDate\");"

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "CREATE INDEX IF NOT EXISTS \"IX_JoinRequests_ProjectId\" ON \"JoinRequests\"(\"ProjectId\");"

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "CREATE INDEX IF NOT EXISTS \"IX_JoinRequests_StudentEmail\" ON \"JoinRequests\"(\"StudentEmail\");"

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "CREATE INDEX IF NOT EXISTS \"IX_JoinRequests_StudentId\" ON \"JoinRequests\"(\"StudentId\");"

echo Adding foreign key constraints...

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "ALTER TABLE \"JoinRequests\" ADD CONSTRAINT \"FK_JoinRequests_Projects_ProjectId\" FOREIGN KEY (\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE SET NULL;"

psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "ALTER TABLE \"JoinRequests\" ADD CONSTRAINT \"FK_JoinRequests_Students_StudentId\" FOREIGN KEY (\"StudentId\") REFERENCES \"Students\"(\"Id\") ON DELETE SET NULL;"

echo Done! JoinRequests table created successfully.
pause
