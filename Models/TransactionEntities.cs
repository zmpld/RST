using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RelationshipVisualizer.Models
{
    [Table("Customers")]
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }
        public string? CustomerFirstName { get; set; }
        public string? CustomerMiddleName { get; set; }
        public string? CustomerLastName { get; set; }
        public bool? IsActive { get; set; }
    }

    [Table("CustomerAccounts")]
    public class CustomerAccount
    {
        [Key]
        public int CustomerAccountId { get; set; }
        public string? AccountNumber { get; set; }
        public bool? IsActive { get; set; }
    }

    [Table("CustomerTransactions")]
    public class CustomerTransaction
    {
        [Key]
        public int CustomerTransactionId { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string? TransactionReferenceNumber { get; set; }
        public decimal? TransactionAmount { get; set; }
        public int? CustomerAccountId { get; set; }
        public bool? IsActive { get; set; }
        public int? BranchId { get; set; }
        public string? TransactionType { get; set; }
    }

    [Table("AccountHolders")]
    public class AccountHolder
    {
        [Key]
        public int AccountHolderId { get; set; }
        public string? AccountHolderFirstName { get; set; }
        public string? AccountHolderLastName { get; set; }
        public string? AccountHolderCorporateName { get; set; }
        public int? CustomerTransactionId { get; set; }
        public bool? IsActive { get; set; }
    }

    [Table("Beneficiaries")]
    public class Beneficiary
    {
        [Key]
        public int BeneficiaryId { get; set; }
        public string? BeneficiaryFirstName { get; set; }
        public string? BeneficiaryLastName { get; set; }
        public string? BeneficiaryCorporateName { get; set; }
        public string? BeneficiaryAccountNumber { get; set; }
        public int? CustomerTransactionId { get; set; }
        public bool? IsActive { get; set; }
    }

    [Table("JointAccounts")]
    public class JointAccount
    {
        [Key]
        public int JointAccountId { get; set; }
        public int CustomerAccountId { get; set; }
        public int CustomerId { get; set; }
        public bool? IsActive { get; set; }
    }

    [Table("TransactionTypes")]
    public class TransactionType
    {
        [Key]
        public int TransactionTypeId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }

    [Table("Branches")]
    public class Branch
    {
        [Key]
        public int BranchId { get; set; }
        public string? InstitutionCode { get; set; }
        public string? Name { get; set; }
        public string? BranchAddress { get; set; }
        public string? Description { get; set; }
        public string? BranchCode { get; set; }
        public string? BranchArea { get; set; }
        public bool? IsActive { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? RestoredByUserId { get; set; }
        public DateTime? RestoredDate { get; set; }
        public int? DeletedByUserId { get; set; }
        public DateTime? DeletedDate { get; set; }
        public string? PermanentDeleteBy { get; set; }
        public bool? PermanentDelete { get; set; }
        public string? PermanentDeleteReason { get; set; }
        public DateTime? PermanentDeleteDate { get; set; }
        public int? BankId { get; set; }
        public string? BranchAddress1 { get; set; }
        public string? BranchAddress2 { get; set; }
        public string? BranchAddress3 { get; set; }
        public decimal? BranchLatitude { get; set; }
        public decimal? BranchLongitude { get; set; }
    }
}
