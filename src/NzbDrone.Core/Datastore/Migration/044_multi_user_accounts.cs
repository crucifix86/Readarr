using System;
using System.Data;
using Dapper;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(044)]
    public class multi_user_accounts : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Users").AddColumn("Role").AsInt32().NotNullable().WithDefaultValue(0);
            Alter.Table("Users").AddColumn("ApiKey").AsString().Nullable();
            Alter.Table("Users").AddColumn("Email").AsString().Nullable();
            Alter.Table("Users").AddColumn("CreatedAt").AsDateTime().Nullable();

            Execute.WithConnection(SeedExistingUser);

            Create.Index("IX_Users_ApiKey").OnTable("Users").OnColumn("ApiKey").Ascending();

            Create.Table("UserBookProgress")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("UserId").AsInt32().NotNullable()
                .WithColumn("BookFileId").AsInt32().NotNullable()
                .WithColumn("Location").AsString().Nullable()
                .WithColumn("Percent").AsDouble().Nullable()
                .WithColumn("UpdatedAt").AsDateTime().NotNullable();

            Create.Index("IX_UserBookProgress_User_BookFile")
                .OnTable("UserBookProgress")
                .OnColumn("UserId").Ascending()
                .OnColumn("BookFileId").Ascending()
                .WithOptions().Unique();

            Create.Table("UserBookmarks")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("UserId").AsInt32().NotNullable()
                .WithColumn("BookFileId").AsInt32().NotNullable()
                .WithColumn("Location").AsString().NotNullable()
                .WithColumn("Note").AsString().Nullable()
                .WithColumn("CreatedAt").AsDateTime().NotNullable();

            Create.Index("IX_UserBookmarks_User_BookFile")
                .OnTable("UserBookmarks")
                .OnColumn("UserId").Ascending()
                .OnColumn("BookFileId").Ascending();

            Create.Table("UserFavorites")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("UserId").AsInt32().NotNullable()
                .WithColumn("BookId").AsInt32().NotNullable()
                .WithColumn("CreatedAt").AsDateTime().NotNullable();

            Create.Index("IX_UserFavorites_User_Book")
                .OnTable("UserFavorites")
                .OnColumn("UserId").Ascending()
                .OnColumn("BookId").Ascending()
                .WithOptions().Unique();
        }

        private void SeedExistingUser(IDbConnection conn, IDbTransaction tran)
        {
            var sql = "UPDATE \"Users\" SET \"Role\" = 1, \"ApiKey\" = @ApiKey, \"CreatedAt\" = @CreatedAt WHERE \"ApiKey\" IS NULL";
            conn.Execute(sql, new { ApiKey = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow }, transaction: tran);
        }
    }
}
