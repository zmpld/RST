using Microsoft.EntityFrameworkCore;
using RelationshipVisualizer.Models;

namespace RelationshipVisualizer.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAccount> CustomerAccounts { get; set; }
        public DbSet<CustomerTransaction> CustomerTransactions { get; set; }
        public DbSet<TransactionType> TransactionTypes { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<AccountHolder> AccountHolders { get; set; }
        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<JointAccount> JointAccounts { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CustomerTransaction>()
                .Property(t => t.TransactionAmount)
                .HasColumnType("decimal(18,2)");
        }
    }
}
