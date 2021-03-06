﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using System.Data.Entity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Data.Entity.Infrastructure.Annotations;
using MarkChat.DAL.ChatEntities;

namespace MarkChat.DAL.Entities
{

    public class ApplicationRole : IdentityRole
    {
        public ApplicationRole() { }
        public string Description { get; set; }
    }
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit https://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        ////[Required]
        //public string FirstName { get; set; }
        ////[Required]
        //public string MiddleName { get; set; }
        ////[Required]
        //public string LastName { get; set; }

        public string FullName { get; set; }
        public string PhotoName { get; set; }
        public DateTime? BirthDate { get; set; }
        public string SecurityCode { get; set; }

        public int SecurityCodeEnterCount { get; set; }
        public DateTime? LastSecurityCodeSendDate { get; set; }
        public virtual List<ChatRoomMember> ChatRoomsMember { get; set; }

        public virtual List<Notification> Notifications { get; set; }

        public virtual List<TagChat> OwnerChats { get; set; }

        public virtual List<TagChat> TagChats { get; set; }

        public virtual List<InvRequestToUser> InvRequests { get; set; }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager, string authenticationType)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, authenticationType);
            // Add custom user claims here
            return userIdentity;
        }
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }


    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        static ApplicationDbContext()
        {
            Database.SetInitializer<ApplicationDbContext>(new ContextInitializer());
        }
        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TagChat>()
                .HasMany<ApplicationUser>(s => s.Users)
                .WithMany(c => c.TagChats)
                .Map(cs =>
                {
                    cs.MapLeftKey("TagChatId");
                    cs.MapRightKey("UserId");
                    cs.ToTable("ChatUsers");
                });


            modelBuilder.Entity<TagChat>()
          .HasRequired<ApplicationUser>(s => s.OwnerUser)
          .WithMany(g => g.OwnerChats).WillCascadeOnDelete(false);       

            modelBuilder.Entity<Category>().HasOptional(x => x.ChatRoot).WithRequired(x => x.RootCategory).Map(k => k.MapKey("RootCategoryId")
            .HasColumnAnnotation("RootCategoryId", IndexAnnotation.AnnotationName,
                new IndexAnnotation(
            new IndexAttribute("IX_FirstNameLastName", 1) { IsUnique = true })));
            
        }

        public DbSet<Category> Categoties { get; set; }
        //public DbSet<CategoryTag> CategoryTags { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<TagChat> TagChats { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<Confirmation> Confirmations { get; set; }

        public DbSet<Template> Templates { get; set; }
        public DbSet<InvRequestToChat> InvRequestToChats { get; set; }
        public DbSet<InvRequestToUser> InvRequestTousers { get; set; }
        public DbSet<InvRequest> InvRequests { get; set; }
        public DbSet<AttachmentMsg> AttachmentMsgs { get; set; }
        public DbSet<ChatRoom> ChatRooms { get; set; }
        public DbSet<ChatRoomMember> ChatRoomMember { get; set; }
        public DbSet<Message> Message { get; set; }
        public DbSet<ReadedMsg> ReadedMsgs { get; set; }
        public DbSet<TypeChat> TypesChats { get; set; }


    }




}
