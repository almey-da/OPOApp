using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace OPOService.Models
{
    public partial class OPOContext : DbContext
    {
        public OPOContext()
        {
        }

        public OPOContext(DbContextOptions<OPOContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Role> Roles { get; set; } = null!;
        public virtual DbSet<Saldo> Saldos { get; set; } = null!;
        public virtual DbSet<Transaction> Transactions { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;
        public virtual DbSet<UserRole> UserRoles { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=OPO;uid=tester;pwd=pass123;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Role");

                entity.Property(e => e.Name).HasMaxLength(50);
            });

            modelBuilder.Entity<Saldo>(entity =>
            {
                entity.ToTable("Saldo");

                entity.Property(e => e.Saldo1)
                    .HasMaxLength(50)
                    .HasColumnName("Saldo");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Saldos)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Saldo_User");
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("Transaction");

                entity.Property(e => e.Amount).HasMaxLength(50);

                entity.Property(e => e.Description).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.TransactionDate).HasColumnType("datetime");

                entity.Property(e => e.TransactionName).HasMaxLength(50);

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Transaction_User");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");

                entity.Property(e => e.Email).HasMaxLength(50);

                entity.Property(e => e.FullName).HasMaxLength(50);

                entity.Property(e => e.Password).HasColumnType("ntext");

                entity.Property(e => e.PhoneNumber).HasMaxLength(50);

                entity.Property(e => e.Username).HasMaxLength(50);
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("User Role");

                entity.HasOne(d => d.Role)
                    .WithMany(p => p.UserRoles)
                    .HasForeignKey(d => d.RoleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_User Role_Role");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.UserRoles)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_User Role_User");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
