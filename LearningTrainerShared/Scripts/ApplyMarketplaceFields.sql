-- Apply marketplace fields migration manually
-- Run this script against your database if EF migrations are not working

-- Dictionary marketplace fields
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Dictionaries') AND name = 'IsPublished')
BEGIN
    ALTER TABLE [Dictionaries] ADD [IsPublished] bit NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Dictionaries') AND name = 'Rating')
BEGIN
    ALTER TABLE [Dictionaries] ADD [Rating] float NOT NULL DEFAULT 0.0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Dictionaries') AND name = 'RatingCount')
BEGIN
    ALTER TABLE [Dictionaries] ADD [RatingCount] int NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Dictionaries') AND name = 'DownloadCount')
BEGIN
    ALTER TABLE [Dictionaries] ADD [DownloadCount] int NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Dictionaries') AND name = 'SourceDictionaryId')
BEGIN
    ALTER TABLE [Dictionaries] ADD [SourceDictionaryId] int NULL;
END

-- Rule marketplace fields
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Rules') AND name = 'IsPublished')
BEGIN
    ALTER TABLE [Rules] ADD [IsPublished] bit NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Rules') AND name = 'Rating')
BEGIN
    ALTER TABLE [Rules] ADD [Rating] float NOT NULL DEFAULT 0.0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Rules') AND name = 'RatingCount')
BEGIN
    ALTER TABLE [Rules] ADD [RatingCount] int NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Rules') AND name = 'DownloadCount')
BEGIN
    ALTER TABLE [Rules] ADD [DownloadCount] int NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Rules') AND name = 'SourceRuleId')
BEGIN
    ALTER TABLE [Rules] ADD [SourceRuleId] int NULL;
END

-- Comments table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Comments')
BEGIN
    CREATE TABLE [Comments] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [ContentType] nvarchar(20) NOT NULL,
        [ContentId] int NOT NULL,
        [Rating] int NOT NULL,
        [Text] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Comments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Comments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_Comments_UserId] ON [Comments]([UserId]);
    CREATE INDEX [IX_Comments_ContentType_ContentId] ON [Comments]([ContentType], [ContentId]);
END

-- Downloads table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Downloads')
BEGIN
    CREATE TABLE [Downloads] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [ContentType] nvarchar(20) NOT NULL,
        [ContentId] int NOT NULL,
        [DownloadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Downloads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Downloads_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_Downloads_UserId] ON [Downloads]([UserId]);
END

PRINT 'Marketplace fields migration applied successfully';
