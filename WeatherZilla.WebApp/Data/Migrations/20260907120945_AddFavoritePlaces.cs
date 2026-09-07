using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherZilla.WebApp.Data.Migrations
{
    public partial class AddFavoritePlaces : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The SQL project already defines this table without generated IDs. Adopt it without deleting favorites.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[AspNetFavorites]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetFavorites] (
        [Id] int NOT NULL PRIMARY KEY,
        [UserId] nvarchar(450) NOT NULL,
        [Place] nvarchar(128) NOT NULL
    );
END;
DECLARE @nextId bigint = (SELECT ISNULL(MAX(CONVERT(bigint, [Id])), 0) + 1 FROM [dbo].[AspNetFavorites]);
IF @nextId > 2147483647 THROW 50000, 'Favorite identifiers exhausted.', 1;
IF OBJECT_ID(N'[dbo].[FavoriteIds]', N'SO') IS NULL
    EXEC(N'CREATE SEQUENCE [dbo].[FavoriteIds] AS int START WITH ' + @nextId + N' INCREMENT BY 1');
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[AspNetFavorites]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[AspNetFavorites]'), 'Id', 'ColumnId'))
    ALTER TABLE [dbo].[AspNetFavorites] ADD CONSTRAINT [DF_AspNetFavorites_Id] DEFAULT (NEXT VALUE FOR [dbo].[FavoriteIds]) FOR [Id];
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AspNetFavorites_AspNetUsers_UserId')
    ALTER TABLE [dbo].[AspNetFavorites] ADD CONSTRAINT [FK_AspNetFavorites_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AspNetFavorites]') AND name = 'IX_AspNetFavorites_UserId_Place')
    CREATE UNIQUE INDEX [IX_AspNetFavorites_UserId_Place] ON [dbo].[AspNetFavorites] ([UserId], [Place]);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetFavorites");

            migrationBuilder.DropSequence(
                name: "FavoriteIds");
        }
    }
}
