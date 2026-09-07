CREATE SEQUENCE [dbo].[FavoriteIds] AS INT START WITH 1 INCREMENT BY 1;
GO
CREATE TABLE [dbo].[AspNetFavorites] (
    [Id] INT NOT NULL DEFAULT (NEXT VALUE FOR [dbo].[FavoriteIds]),
    [UserId] NVARCHAR(450) NOT NULL,
    [Place] NVARCHAR(128) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AspNetFavorites_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO
CREATE UNIQUE INDEX [IX_AspNetFavorites_UserId_Place] ON [dbo].[AspNetFavorites] ([UserId], [Place]);
