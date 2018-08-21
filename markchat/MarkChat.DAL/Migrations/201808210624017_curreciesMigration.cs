namespace MarkChat.DAL.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class curreciesMigration : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Currencies", "Symbol", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Currencies", "Symbol");
        }
    }
}
