IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FrameResults' AND xtype='U')
BEGIN
    CREATE TABLE FrameResults (
        Id NVARCHAR(50) PRIMARY KEY,
        Summary NVARCHAR(MAX),
        ChildYesNo NVARCHAR(MAX),
        MD5Hash NVARCHAR(32),
        Frame NVARCHAR(MAX),
        RunId NVARCHAR(50),
        Hate INT,
        SelfHarm INT,
        Violence INT,
        Sexual INT,
        RunDateTime DATETIME
    );
END
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FrameBase64' AND xtype='U')
BEGIN
    CREATE TABLE FrameBase64 (
        Id NVARCHAR(50) PRIMARY KEY,
        Frame NVARCHAR(MAX),
        ImageBase64 NVARCHAR(MAX),
        RunDateTime DATETIME,
        RunId NVARCHAR(50)
    );
END
GO