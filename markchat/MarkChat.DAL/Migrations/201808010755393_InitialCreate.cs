namespace MarkChat.DAL.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AttachmentMsgs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FileName = c.String(),
                        Message_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Messages", t => t.Message_Id)
                .Index(t => t.Message_Id);
            
            CreateTable(
                "dbo.Messages",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Body = c.String(),
                        DateTime = c.DateTime(),
                        ChatRoomMember_Id = c.Int(),
                        ChatRoom_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ChatRoomMembers", t => t.ChatRoomMember_Id)
                .ForeignKey("dbo.ChatRooms", t => t.ChatRoom_Id)
                .Index(t => t.ChatRoomMember_Id)
                .Index(t => t.ChatRoom_Id);
            
            CreateTable(
                "dbo.ChatRooms",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        CreationTime = c.DateTime(),
                        TypeChat_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.TypeChats", t => t.TypeChat_Id)
                .Index(t => t.TypeChat_Id);
            
            CreateTable(
                "dbo.ChatRoomMembers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DateTimeConnected = c.DateTime(),
                        ChatRoom_Id = c.Int(),
                        User_Id = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ChatRooms", t => t.ChatRoom_Id)
                .ForeignKey("dbo.AspNetUsers", t => t.User_Id)
                .Index(t => t.ChatRoom_Id)
                .Index(t => t.User_Id);
            
            CreateTable(
                "dbo.ReadedMsgs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        RDateTime = c.DateTime(),
                        Readed = c.Boolean(),
                        ChatRoomMember_Id = c.Int(),
                        Message_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ChatRoomMembers", t => t.ChatRoomMember_Id)
                .ForeignKey("dbo.Messages", t => t.Message_Id)
                .Index(t => t.ChatRoomMember_Id)
                .Index(t => t.Message_Id);
            
            CreateTable(
                "dbo.AspNetUsers",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        FullName = c.String(),
                        PhotoName = c.String(),
                        BirthDate = c.DateTime(),
                        SecurityCode = c.String(),
                        SecurityCodeEnterCount = c.Int(nullable: false),
                        LastSecurityCodeSendDate = c.DateTime(),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "dbo.AspNetUserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.InvRequestToUsers",
                c => new
                    {
                        InvRequestToUserId = c.Int(nullable: false),
                        TagChat_Id = c.Int(),
                        User_Id = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.InvRequestToUserId)
                .ForeignKey("dbo.InvRequests", t => t.InvRequestToUserId)
                .ForeignKey("dbo.TagChats", t => t.TagChat_Id)
                .ForeignKey("dbo.AspNetUsers", t => t.User_Id)
                .Index(t => t.InvRequestToUserId)
                .Index(t => t.TagChat_Id)
                .Index(t => t.User_Id);
            
            CreateTable(
                "dbo.InvRequests",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        IsWatched = c.Boolean(nullable: false),
                        RequestDateTime = c.DateTime(),
                        Confirmed = c.Boolean(nullable: false),
                        Denied = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.TagChats",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        RootCategoryId = c.Int(nullable: false),
                        OwnerUser_Id = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Categories", t => t.RootCategoryId)
                .ForeignKey("dbo.AspNetUsers", t => t.OwnerUser_Id)
                .Index(t => t.RootCategoryId, unique: true, name: "IX_FirstNameLastName")
                .Index(t => t.OwnerUser_Id);
            
            CreateTable(
                "dbo.InvRequestToChats",
                c => new
                    {
                        InvRequestToChatId = c.Int(nullable: false),
                        TagChat_Id = c.Int(),
                        User_Id = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.InvRequestToChatId)
                .ForeignKey("dbo.InvRequests", t => t.InvRequestToChatId)
                .ForeignKey("dbo.TagChats", t => t.TagChat_Id)
                .ForeignKey("dbo.AspNetUsers", t => t.User_Id)
                .Index(t => t.InvRequestToChatId)
                .Index(t => t.TagChat_Id)
                .Index(t => t.User_Id);
            
            CreateTable(
                "dbo.Notifications",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Price = c.Int(nullable: false),
                        PublicationDate = c.DateTime(nullable: false),
                        Description = c.String(nullable: false, maxLength: 90),
                        Author_Id = c.String(maxLength: 128),
                        Category_Id = c.Int(),
                        Currency_Id = c.Int(),
                        TagChat_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.Author_Id)
                .ForeignKey("dbo.Categories", t => t.Category_Id)
                .ForeignKey("dbo.Currencies", t => t.Currency_Id)
                .ForeignKey("dbo.TagChats", t => t.TagChat_Id)
                .Index(t => t.Author_Id)
                .Index(t => t.Category_Id)
                .Index(t => t.Currency_Id)
                .Index(t => t.TagChat_Id);
            
            CreateTable(
                "dbo.Categories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                        Title = c.String(),
                        ParentCategory_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Categories", t => t.ParentCategory_Id)
                .Index(t => t.ParentCategory_Id);
            
            CreateTable(
                "dbo.Currencies",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.AspNetUserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserRoles",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        RoleId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "dbo.TypeChats",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Confirmations",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        PhoneNumber = c.String(),
                        Code = c.String(),
                        Date = c.DateTime(nullable: false),
                        Token = c.String(),
                        Confirmed = c.Boolean(nullable: false),
                        TryCount = c.Int(nullable: false),
                        LastTry = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.AspNetRoles",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Name = c.String(nullable: false, maxLength: 256),
                        Description = c.String(),
                        Discriminator = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
            CreateTable(
                "dbo.Templates",
                c => new
                    {
                        IdTemplate = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        Root_Id = c.Int(),
                    })
                .PrimaryKey(t => t.IdTemplate)
                .ForeignKey("dbo.Categories", t => t.Root_Id)
                .Index(t => t.Root_Id);
            
            CreateTable(
                "dbo.ChatUsers",
                c => new
                    {
                        TagChatId = c.Int(nullable: false),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.TagChatId, t.UserId })
                .ForeignKey("dbo.TagChats", t => t.TagChatId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.TagChatId)
                .Index(t => t.UserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Templates", "Root_Id", "dbo.Categories");
            DropForeignKey("dbo.AspNetUserRoles", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.ChatRooms", "TypeChat_Id", "dbo.TypeChats");
            DropForeignKey("dbo.Messages", "ChatRoom_Id", "dbo.ChatRooms");
            DropForeignKey("dbo.AspNetUserRoles", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserLogins", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.InvRequestToUsers", "User_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.InvRequestToUsers", "TagChat_Id", "dbo.TagChats");
            DropForeignKey("dbo.ChatUsers", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ChatUsers", "TagChatId", "dbo.TagChats");
            DropForeignKey("dbo.TagChats", "OwnerUser_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.Notifications", "TagChat_Id", "dbo.TagChats");
            DropForeignKey("dbo.Notifications", "Currency_Id", "dbo.Currencies");
            DropForeignKey("dbo.Notifications", "Category_Id", "dbo.Categories");
            DropForeignKey("dbo.Categories", "ParentCategory_Id", "dbo.Categories");
            DropForeignKey("dbo.TagChats", "RootCategoryId", "dbo.Categories");
            DropForeignKey("dbo.Notifications", "Author_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.InvRequestToChats", "User_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.InvRequestToChats", "TagChat_Id", "dbo.TagChats");
            DropForeignKey("dbo.InvRequestToChats", "InvRequestToChatId", "dbo.InvRequests");
            DropForeignKey("dbo.InvRequestToUsers", "InvRequestToUserId", "dbo.InvRequests");
            DropForeignKey("dbo.AspNetUserClaims", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ChatRoomMembers", "User_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.ReadedMsgs", "Message_Id", "dbo.Messages");
            DropForeignKey("dbo.ReadedMsgs", "ChatRoomMember_Id", "dbo.ChatRoomMembers");
            DropForeignKey("dbo.Messages", "ChatRoomMember_Id", "dbo.ChatRoomMembers");
            DropForeignKey("dbo.ChatRoomMembers", "ChatRoom_Id", "dbo.ChatRooms");
            DropForeignKey("dbo.AttachmentMsgs", "Message_Id", "dbo.Messages");
            DropIndex("dbo.ChatUsers", new[] { "UserId" });
            DropIndex("dbo.ChatUsers", new[] { "TagChatId" });
            DropIndex("dbo.Templates", new[] { "Root_Id" });
            DropIndex("dbo.AspNetRoles", "RoleNameIndex");
            DropIndex("dbo.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "UserId" });
            DropIndex("dbo.AspNetUserLogins", new[] { "UserId" });
            DropIndex("dbo.Categories", new[] { "ParentCategory_Id" });
            DropIndex("dbo.Notifications", new[] { "TagChat_Id" });
            DropIndex("dbo.Notifications", new[] { "Currency_Id" });
            DropIndex("dbo.Notifications", new[] { "Category_Id" });
            DropIndex("dbo.Notifications", new[] { "Author_Id" });
            DropIndex("dbo.InvRequestToChats", new[] { "User_Id" });
            DropIndex("dbo.InvRequestToChats", new[] { "TagChat_Id" });
            DropIndex("dbo.InvRequestToChats", new[] { "InvRequestToChatId" });
            DropIndex("dbo.TagChats", new[] { "OwnerUser_Id" });
            DropIndex("dbo.TagChats", "IX_FirstNameLastName");
            DropIndex("dbo.InvRequestToUsers", new[] { "User_Id" });
            DropIndex("dbo.InvRequestToUsers", new[] { "TagChat_Id" });
            DropIndex("dbo.InvRequestToUsers", new[] { "InvRequestToUserId" });
            DropIndex("dbo.AspNetUserClaims", new[] { "UserId" });
            DropIndex("dbo.AspNetUsers", "UserNameIndex");
            DropIndex("dbo.ReadedMsgs", new[] { "Message_Id" });
            DropIndex("dbo.ReadedMsgs", new[] { "ChatRoomMember_Id" });
            DropIndex("dbo.ChatRoomMembers", new[] { "User_Id" });
            DropIndex("dbo.ChatRoomMembers", new[] { "ChatRoom_Id" });
            DropIndex("dbo.ChatRooms", new[] { "TypeChat_Id" });
            DropIndex("dbo.Messages", new[] { "ChatRoom_Id" });
            DropIndex("dbo.Messages", new[] { "ChatRoomMember_Id" });
            DropIndex("dbo.AttachmentMsgs", new[] { "Message_Id" });
            DropTable("dbo.ChatUsers");
            DropTable("dbo.Templates");
            DropTable("dbo.AspNetRoles");
            DropTable("dbo.Confirmations");
            DropTable("dbo.TypeChats");
            DropTable("dbo.AspNetUserRoles");
            DropTable("dbo.AspNetUserLogins");
            DropTable("dbo.Currencies");
            DropTable("dbo.Categories");
            DropTable("dbo.Notifications");
            DropTable("dbo.InvRequestToChats");
            DropTable("dbo.TagChats");
            DropTable("dbo.InvRequests");
            DropTable("dbo.InvRequestToUsers");
            DropTable("dbo.AspNetUserClaims");
            DropTable("dbo.AspNetUsers");
            DropTable("dbo.ReadedMsgs");
            DropTable("dbo.ChatRoomMembers");
            DropTable("dbo.ChatRooms");
            DropTable("dbo.Messages");
            DropTable("dbo.AttachmentMsgs");
        }
    }
}
