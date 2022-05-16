CREATE TABLE [dbo].[AspNetFavorites] (
    [Id]     INT            NOT NULL,
    [UserId] NVARCHAR (450) NOT NULL,
    [Place]  NVARCHAR (128) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

