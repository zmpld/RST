using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RelationshipVisualizer.Data;
using RelationshipVisualizer.Models;
using System.Globalization;

namespace RelationshipVisualizer.Controllers
{
    [ApiController]
    [Route("Transaction")]
    public class TransactionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TransactionController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("SearchAccounts")]
        public async Task<IActionResult> SearchAccounts([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Ok(new List<object>());
                }

                string searchTrim = query.Trim();
                string wildCardSearch = $"%{searchTrim}%";

                var databaseMatches = await (from ct in _context.CustomerTransactions
                                             join ca in _context.CustomerAccounts
                                                 on ct.CustomerAccountId equals ca.CustomerAccountId into caGroup
                                             from ca in caGroup.DefaultIfEmpty()
                                             join ja in _context.JointAccounts
                                                 on ca.CustomerAccountId equals ja.CustomerAccountId into jaGroup
                                             from ja in jaGroup.DefaultIfEmpty()
                                             join c in _context.Customers
                                                 on ja.CustomerId equals c.CustomerId into cGroup
                                             from c in cGroup.DefaultIfEmpty()
                                             where ct.IsActive == true && c != null && c.IsActive == true && (
                                                 EF.Functions.Like(c.CustomerFirstName, wildCardSearch) ||
                                                 EF.Functions.Like(c.CustomerMiddleName, wildCardSearch) ||
                                                 EF.Functions.Like(c.CustomerLastName, wildCardSearch)
                                             )
                                             select new
                                             {
                                                 AccountNumber = ca != null ? ca.AccountNumber : "N/A",
                                                 FirstName = c.CustomerFirstName ?? "",
                                                 MiddleName = c.CustomerMiddleName ?? "",
                                                 LastName = c.CustomerLastName ?? ""
                                             }).Distinct().ToListAsync();

                var uniqueResults = new List<object>();
                var seenFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var match in databaseMatches)
                {
                    string fullName = $"{match.FirstName} {(string.IsNullOrWhiteSpace(match.MiddleName) ? "" : match.MiddleName + " ")}{match.LastName}".Trim();

                    if (string.IsNullOrEmpty(fullName)) fullName = "Unknown Holder";

                    if (seenFullNames.Contains(fullName))
                    {
                        continue;
                    }

                    seenFullNames.Add(fullName);

                    uniqueResults.Add(new
                    {
                        accountNumber = match.AccountNumber,
                        accountName = fullName,
                        role = "Customer"
                    });
                }

                return Ok(uniqueResults);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Search Execution Failure", message = ex.Message });
            }
        }

        [HttpGet("GetBranches")]
        public async Task<IActionResult> GetBranches()
        {
            try
            {
                // Prepopulated lookup matching your specified data string items exactly
                var exactBranches = new List<string>
                {
                    "HEAD OFFICE BRANCH", "MAIN OFFICE BRANCH", "DAVAO CITY BRANCH", "CEBU CITY BRANCH", 
                    "MANDAUE NORTH, CEBU BRANCH", "TALISAY CEBU BRANCH", "DUMAGUETE CITY BRANCH", "VALENCIA CITY BRANCH", 
                    "AKLAN BRANCH", "TACLOBAN, LEYTE BRANCH", "ILOILO BRANCH", "DIPOLOG CITY BRANCH", "ROXAS CITY BRANCH", 
                    "CALBAYOG SAMAR BRANCH", "ILIGAN CITY BRANCH", "LAPU-LAPU, CEBU BRANCH", "MABOLO CEBU BRANCH", 
                    "KIDAPAWAN CITY BRANCH", "ZAMBOANGA CITY BRANCH", "GENERAL SANTOS BRANCH", "TAGBILARAN CITY BRANCH", 
                    "CAGAYAN DE ORO BRANCH", "BUTUAN CITY BRANCH", "MANDAUE SOUTH, CEBU BRANCH", "MATINA DAVAO BRANCH", 
                    "TAGUM CITY BRANCH", "NEGROS OCCIDENTAL BRANCH", "BATANGAS BRANCH", "BACOOR, CAVITE BRANCH", 
                    "SANTA ROSA LAGUNA BRANCH", "PUERTO PRINCESA CITY BRANCH", "TAYTAY, RIZAL BRANCH", "LUCENA CITY BRANCH", 
                    "ALBAY BRANCH", "CALAMBA, LAGUNA BRANCH", "SILANG CAVITE, BRANCH", "CAMARINES SUR BRANCH", 
                    "SAN PABLO LAGUNA BRANCH", "CALAPAN CITY BRANCH", "DASMARINAS-CAVITE BRANCH", "LIPA, BATANGAS BRANCH", 
                    "ALABANG BRANCH", "GLOBAL CITY BRANCH", "PASIG BRANCH", "COMMONWEALTH BRANCH", "QUEZON AVENUE BRANCH", 
                    "NORTH EDSA BRANCH", "PASONG TAMO BRANCH", "ISABELA BRANCH", "SAN JOSE DEL MONTE, BULACAN BRANCH", 
                    "TARLAC CITY BRANCH", "ANGELES, PAMPANGA BRANCH", "LA UNION BRANCH", "BAGUIO BRANCH", "SAN FERNANDO BRANCH", 
                    "DAGUPAN BRANCH", "BATAAN BRANCH", "TUGUEGARAO CITY BRANCH", "PLARIDEL, BULACAN BRANCH", "MARILAO, BULACAN BRANCH", 
                    "ILOCOS NORTE BRANCH", "SUBIC BRANCH", "NUEVA ECIJA BRANCH", "OTIS BRANCH", "MAKATI BRANCH", "BICUTAN PARANAQUE BRANCH", 
                    "FAIRVIEW BRANCH", "BALINTAWAK MAIN BRANCH", "VALENZUELA BRANCH", "MANILA BAY BRANCH", "CUBAO BRANCH", 
                    "MARIKINA BRANCH", "SHAW BRANCH", "ABAD SANTOS, MANILA BRANCH", "CAGAYAN DE ORO (AUTOCENTRAL BRANCH)", 
                    "CEBU NORTH (CEBU AUTOCENTRALE BRANCH)", "CEBU SOUTH (SAKURA AUTOWORLD BRANCH)", "COMMONWEALTH (ETNA MOTORS BRANCH)", 
                    "DAVAO (GRAND CANYON MULTI HOLDINGS BRANCH)", "KALOOKAN (MATRIX MOTOR BRANCH)", "MAKATI (PEER MOTORTEK SALES BRANCH)", 
                    "MATINA (CEBU AUTOCENTRALE BRANCH)", "PASIG (MT. SINAI MOTORS BRANCH)", "SAN FERNANDO (FAMILY CARS BRANCH)", 
                    "SUCAT (SAKURA AUTOWORLD BRANCH)", "TARLAC (GRAND CANYON MULTI HOLDINGS BRANCH)", "ABAD SANTOS, MANILA (SUBIC GS AUTO BRANCH)", 
                    "BALINTAWAK BRANCH", "BATANGAS (LOVI MOTORS BRANCH)", "BUTUAN CITY BRANCH", "CAGAYAN DE ORO (BIGTRUCKS BRANCH)", 
                    "CARMONA (LOVI MOTORS BRANCH)", "CEBU (PASAJERO MOTORS BRANCH)", "DAVAO (MOTORMALL DAVAO BRANCH)", "ISABELA BRANCH", 
                    "LAGUNA (KAIHATSU MOTORS BRANCH)", "LEYTE BRANCH", "MANDAUE, CEBU (SUBIC GS AUTO BRANCH)", "NAGA (LASS AUTOMOTIVE BRANCH)", 
                    "NEGROS (TRUCKMAX SALES AND SERVICES BRANCH)", "NUEVA ECIJA BRANCH", "PAMPANGA BRANCH", "PANAY ( F & E FLEET SALES AND SERVICES BRANCH)", 
                    "PARANAQUE METRO MANILA (SUBIC GS AUTO BRANCH)", "U.N. AVENUE (SUPERB MOTORS BRANCH)", "MOTORMALL NCR BRANCH", 
                    "Executive Office  BRANCH", "Comptrollership BRANCH", "Business Services Group  BRANCH", "AMBIT_ROPA BRANCH", 
                    "ORMOC LEYTE BRANCH", "SANTIAGO ISABELA BRANCH", "UN AVENUE (ONE ALLIANCE MOTOR SOURCE BRANCH)", "MANILA BRANCH", 
                    "OZAMIZ (DES STRONG MOTORS BRANCH)", "PASIG ( MT. SINAI MOTORS BRANCH)", "TAGUIG (ZOOMHUB BRANCH)", "restore test", 
                    "BOHOL (DES STRONG MOTORS BRANCH", "testing branch"
                };

                // Query DB to extract structural mapping associations dynamically
                var activeDatabaseBranches = await _context.Branches
                    .Select(b => new { b.BranchId, Name = b.Name.Trim() })
                    .Distinct()
                    .ToListAsync();

                // Build a response listing the specified data elements alongside mapping structures safely
                var payloadList = exactBranches.Select((name, index) => 
                {
                    var foundDbMatch = activeDatabaseBranches.FirstOrDefault(dbb => string.Equals(dbb.Name, name, StringComparison.OrdinalIgnoreCase));
                    return new 
                    {
                        branchId = foundDbMatch != null ? foundDbMatch.BranchId.ToString() : $"STATIC_REF_{index}",
                        name = name
                    };
                }).ToList();

                return Ok(payloadList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load branches", message = ex.Message });
            }
        }

        [HttpGet("GetTransactionTypes")]
        public async Task<IActionResult> GetTransactionTypes()
        {
            try
            {
                var definedTypes = new List<string>
                {
                    "Bills Purchase/Discounting - OC", "Bills Purchase/Discounting - MC/CC", "Bills Purchase/Discounting - Cash", 
                    "Bills Purchase/Discounting - Credit Memo", "Bills Payment - Cash", "Bills Payment - Debit Memo", 
                    "Bills Payment - MC/CC/OC", "Clean Bills for Collection (Export)", "Credit Bills - Import", "Check Clearing", 
                    "Cancelled/Stale MC/CC/DD/TC", "Collection", "Payroll Account - Debit", "Deposit - Cash", "Deposit - Check", 
                    "Salaries/Pension- Credit", "Electronic Cash Card - Loading", "Electronic Cash Card - Purchase", 
                    "Electronic Cash Card - Withdrawal", "Encashment", "Prepaid Card reversal", "Prepaid Card Loading", 
                    "Prepaid Card Purchase", "Purchase of MC/CC/DD/TC - Cash", "Purchase of MC/CC/DD/TC - Debit Memo", 
                    "Payment to Govt Agencies", "Returned Check", "Inter-Account Transfers (same bank)", "Withdrawals - ATM", 
                    "Withdrawals - OTC", "Interest/Income Earned- Cash", "Interest/Income Earned- Credit to Account", 
                    "Interest/Income Earned- MC/CC/OC", "Time Deposit Placement - Cash", "Time Deposit Placement - Debit Memo", 
                    "Time Deposit Placement - On-Us/OC", "Time Deposit Placement - MC/CC", "Time Deposit Placement - Wire", 
                    "Time Deposit Pretermination - Cash", "Time Deposit Pretermination - Credit Memo", "Time Deposit Pretermination - MC/CC", 
                    "Time Deposit Pretermination - Wire", "Time Deposit Payment - Cash", "Time Deposit Payment - Credit Memo", 
                    "Time Deposit Payment - MC/CC/OC", "Time Deposit Payment - Wire", "Buy Foreign Exchange - Cash", 
                    "Buy Foreign Exchange - Debit Memo", "Buy Foreign Exchange - MC/CC/OC", "Buy Foreign Exchange - Wire", 
                    "Sell FX - Cash", "Sell FX through Debit Memo - Credit to Account", "Sell FX - Credit Memo", "Sell FX - MC/CC/OC", 
                    "Sell FX - Wire", "Buy Foreign Exchange using other currencies - Cash", "Buy Foreign Exchange using other currencies - Debit Memo", 
                    "Buy Foreign Exchange using other currencies - Check", "Buy Foreign Exchange using other currencies - Wire", 
                    "Sell FX settled using other currencies - Cash", "Sell FX settled using other currencies - Credit Memo", 
                    "Sell FX settled using other currencies - check", "Sell FX settled using other currencies - Wire", 
                    "Cancelled Documentary Collection - Credit", "Cancelled Outward Bills - Debit", "Documentary Collection with LC (Buyer) Domestic- Cash", 
                    "Documentary Collection with LC (Buyer) Domestic- Debit Memo", "Documentary Collection with LC (Buyer) Domestic- MC/CC/OC", 
                    "Documentary Collection with LC (Import) Cash - Foreign", "Documentary Collection with LC (Import) Debit Memo - Foreign", 
                    "Documentary Collection with LC (Import) MC/CC/OC - Foreign", "Documentary Collection with LC (Seller) Domestic", 
                    "Documentary Collection Non LC (Import) Cash - Foreign", "Documentary Collection Non LC (Import) Debit Memo - Foreign", 
                    "Documentary Collection Non LC (Import)MC/CC/OC- Foreign", "Interest/Income Payment- Cash", "Interest/Income Payment- Debit Memo", 
                    "Interest/Income Payment- MC/CC/OC", "Letter of Credit Cancellation", "Outward Bills For Collection with LC (Export) Cash- Foreign", 
                    "Outward Bills For Collection with LC (Export) Credit Memo - Foreign", "Outward Bills For Collection with LC (Export) MC/CC/OC- Foreign", 
                    "Outward Bills For Collection Non LC (Export) Cash- Foreign", "Outward Bills For Collection Non LC (Export) Credit Memo - Foreign", 
                    "Outward Bills For Collection Non LC (Export) MC/CC/OC- Foreign", "Trust Receipt Availment", "Trust Receipt  Payment- Cash", 
                    "Trust Receipt  Payment- Debit Memo", "Trust Receipt Payment- MC/CC/OC", "Trust Receipt Payment- Wire", "Credit Card Cash Advance", 
                    "Credit Card Adjustment", "Credit Card Purchases/Availments", "Credit Card Purchase (Purchase of Credit Card Receivable)", 
                    "Credit Card Payment - Cash", "Credit Card Payment - EP (Electronic Payment)", "Credit Card Payment - Check", 
                    "Disposition of bank assets and ROPA through donation", "Cancellation of Contract to Sell of ROPA", "Execution of the CTS of ROPA", 
                    "Foreclosed/Acquired Asset/ROPA", "Lease Payment on Asset and ROPA", "Refund of Lease Payment on Asset and ROPA", 
                    "Refund of Sale Payment on Asset and ROPA", "Sale Payment of Asset & ROPA", "Interbank Borrowing (Regular/Foreign Currency Denominated Unit)", 
                    "Interbank Lending (Regular/Foreign Currency Denominated Unit)", "Loan Interest Payment - Cash", "Loan Interest Payment - Debit Memo", 
                    "Loan Interest Payment - MC/CC/OC", "Loan Interest Payment -Wire", "Loan Availment (Regular/Foreign Currency Denominated Unit) - Cash", 
                    "Loan Availment (Regular/Foreign Currency Denominated Unit) - Credit Memo", "Loan Availment (Regular/Foreign Currency Denominated Unit) - MC/CC/OC", 
                    "Loan Availment (Regular/Foreign Currency Denominated Unit) - Mixed Payment", "Loan Availment (Regular/Foreign Currency Denominated Unit) - Wire", 
                    "Loan Payment (Regular/Foreign Currency Denominated Unit) - Cash", "Loan Payment (Regular/Foreign Currency Denominated Unit) - Debit Memo", 
                    "Loan Payment (Regular/Foreign Currency Denominated Unit) - MC/CC/OC", "Loan Payment (Regular/Foreign Currency Denominated Unit) - Mixed Payment", 
                    "Loan Payment (Regular/Foreign Currency Denominated Unit) - Wire", "Loan Restructuring(Regular/Foreign Currency Denominated Unit)", 
                    "Loan Renewal/Repricing", "Loan Pretermination (Regular/Foreign Currency Denominated Unit) - Cash", 
                    "Loan Pretermination (Regular/Foreign Currency Denominated Unit) - Debit Memo", "Loan Pretermination (Regular/Foreign Currency Denominated Unit) - MC/CC/OC", 
                    "Loan Pretermination (Regular/Foreign Currency Denominated Unit) - Mixed Payment", "Loan Pretermination (Regular/Foreign Currency Denominated Unit) - Wire", 
                    "Pledge Loan Release - Cash", "Pledge Loan Release - Credit Memo", "Pledge Loan Release - MC/CC/OC", "Pledge Loan Release - Wire", 
                    "Loan Rebates", "Redemption", "Sale of Loan Receivable", "Returned Inward Remittance (International)", "Returned Inward Remittance (Domestic)", 
                    "Inward Remittance(Domestic) - For Further Credit to Another Account", "Inward Remittance (Domestic)- Credit to Beneficiary's Account", 
                    "Inward Remittance (Domestic)- Advise and Pay Baneficiary", "Inward Remittance (International)- For Further Credit to Another Account", 
                    "Inward Remittance (International)- Credit to Beneficiary's  Account", "Inward Remittance (International)- Advise and Pay Beneficiary", 
                    "Returned Outward Remittance (International)", "Returned Outward Remittance/TT (Domestic)", "Outward Remittance/TT (Domestic) - For Further Credit to another acct.", 
                    "Outward Remittance/TT (Domestic) - Credit to Beneficiary's  Account", "Outward Remittance/TT (Domestic) - Advise and Pay Baneficiary", 
                    "Outward Remittance/TT (International) - For Further Credit to another acct.", "Outward Remittance/TT (International) - Credit to Beneficiary's Account", 
                    "Outward Remittance/TT (International) - Advise and Pay Baneficiary", "Buy Call Option", "Buy Securities", "Contribution/Subscription", 
                    "External Transfer of Investment Holding/s - IN", "External Transfer of Investment Holding/s - OUT", "Internal Transfer of Investment Holding/s - IN", 
                    "Internal Transfer of Investment Holding/s - OUT", "Securities Pretermination", "Securities Cash Account - Deposit", 
                    "Securities Cash account Withdrawal", "Sell Call Option", "Sell Securities", "Underwrite Debt Issues", "Underwrite Equity Issues", 
                    "Buy Bonds - CASH", "Buy Bonds - Debit Memo", "Buy Bonds - MC/CC/OC", "Buy Bonds - Wire", "Bond Pretermination - Cash", 
                    "Bond Pretermination - Credit Memo", "Bond Pretermination - MC/CC/OC", "Bond Pretermination - wire", "Bond Payment - Cash", 
                    "Bond Payment - Credit Memo", "Bond Payment - MC/CC/OC", "Bond Payment - wire", "Sell Bonds - CASH", "Sell Bonds - Credit Memo", 
                    "Sell Bonds - MC/CC/OC", "Sell Bonds - Wire", "Buy Corporate Bonds - CASH", "Buy Corporate Bonds - Debit Memo", 
                    "Buy Corporate Bonds - MC/CC/OC", "Buy Corporate Bonds - Wire", "Corporate Bond Pretermination - Cash", 
                    "Corporate Bond Pretermination - Credit Memo", "Corporate Bond Pretermination - MC/CC/OC", "Corporate Bond Pretermination - wire", 
                    "Sell Corporate Bonds - CASH", "Sell Corporate Bonds - Credit Memo", "Sell Corporate Bonds - MC/CC/OC", "Sell Corporate Bonds - Wire", 
                    "Corporate Bond Payment - Cash", "Corporate Bond Payment - Credit Memo", "Corporate Bond Payment - MC/CC/OC", "Corporate Bond Payment - wire", 
                    "Cross Currency Swap", "Buy Currency Futures - CASH", "Buy Currency Futures - Debit Memo", "Buy Currency Futures - MC/CC/OC", 
                    "Buy Currency Futures - Wire", "Currency Futures Pretermination - Cash", "Currency Futures Pretermination - Credit Memo", 
                    "Currency Futures Pretermination - MC/CC/OC", "Currency Futures Pretermination - Wire", "Sell Currency Futures - CASH", 
                    "Sell Currency Futures - Credit Memo", "Sell Currency Futures - MC/CC/OC", "Sell Currency Futures - Wire", "Currency Futures Payment - Cash", 
                    "Currency Futures Payment - Credit Memo", "Currency Futures Payment - MC/CC/OC", "Currency Futures Payment - Wire", 
                    "Buy Currency Option  - CASH", "Buy Currency Option - Debit Memo", "Buy Currency Option - MC/CC/OC", "Buy Currency Option - Wire", 
                    "Sell Currency Option - CASH", "Sell Currency Option - Credit Memo", "Sell Currency Option - MC/CC/OC", "Sell Currency Option - Wire", 
                    "Buy Contracts Receivable with Recourse - CASH", "Buy Contracts Receivable with Recourse - Debit Memo", 
                    "Buy Contracts Receivable with Recourse - MC/CC/OC", "Buy Contracts Receivable with Recourse - Wire", "Cancel Contracts", 
                    "Interest/Income Earned - Cash", "Interest/Income Earned - Credit to Account", "Interest/Income Earned - MC/CC/OC", 
                    "Contracts Receivables with recourse Pretermination - Cash", "Contracts Receivables with recourse Pretermination - Credit Memo", 
                    "Contracts Receivables with recourse Pretermination - MC/CC/OC", "Contracts Receivables with recourse Pretermination - Wire", 
                    "Sell Contracts Receivable with recourse - Cash", "Sell Contracts Receivable with recourse - Credit Memo", 
                    "Sell Contracts Receivable with recourse - MC/CC/OC", "Sell Contracts Receivable with recourse - Wire", 
                    "Contracts Receivables with recourse Payment - Cash", "Contracts Receivables with recourse Payment - Credit Memo", 
                    "Contracts Receivables with recourse Payment - MC/CC/OC", "Contracts Receivables with recourse Payment - Wire", 
                    "Buy Common Stocks - CASH", "Buy Common Stocks - Debit Memo", "Buy Common Stocks - MC/CC/OC", "Buy Common Stocks - Wire", 
                    "Common Stocks Pretermination - Cash", "Common Stocks Pretermination - Credit Memo", "Common Stocks Pretermination - MC/CC/OC", 
                    "Common Stocks Pretermination - Wire", "Sell Common Stocks - CASH", "Sell Common Stocks - Credit Memo", "Sell Common Stocks - MC/CC/OC", 
                    "Sell Common Stocks - Wire", "Common Stocks Payment - Cash", "Common Stocks Payment - Credit Memo", "Common Stocks Payment - MC/CC/OC", 
                    "Common Stocks Payment - Wire", "Buy Contracts Receivable without Recourse - CASH", "Buy Contracts Receivable without Recourse - Debit Memo", 
                    "Buy Contracts Receivable without Recourse - MC/CC/OC", "Buy Contracts Receivable without Recourse - Wire", 
                    "Contracts Receivables (w/o recourse) Pretermination - Cash", "Contracts Receivables (w/o recourse) Pretermination - Credit Memo", 
                    "Contracts Receivables (w/o recourse) Pretermination - MC/CC/OC", "Contracts Receivables (w/o recourse) Pretermination - Wire", 
                    "Sell Contracts Receivables (w/o recourse) - Cash", "Sell Contracts Receivables (w/o recourse) - Credit Memo", 
                    "Sell Contracts Receivables (w/o recourse) - MC/CC/OC", "Sell Contracts Receivables (w/o recourse) - Wire", 
                    "Contracts Receivables (w/o recourse) Payment - Cash", "Contracts Receivables (w/o recourse) Payment - Credit Memo", 
                    "Contracts Receivables (w/o recourse) Payment - MC/CC/OC", "Contracts Receivables (w/o recourse) Payment - Wire", 
                    "Buy Derivative Securities - CASH", "Buy Derivative Securities - Debit Memo", "Buy Derivative Securities - MC/CC/OC", 
                    "Buy Derivative Securities - Wire", "Derivative Securities Pretermination - Cash", "Derivative Securities Pretermination - Credit Memo", 
                    "Derivative Securities Pretermination - MC/CC/OC", "Derivative Securities Pretermination - Wire", "Sell Derivative Securities - Cash", 
                    "Sell Derivative Securities - Credit Memo", "Sell Derivative Securities - MC/CC/OC", "Sell Derivative Securities - Wire", 
                    "Derivative Securities Payment - Cash", "Derivative Securities Payment - Credit Memo", "Derivative Securities Payment - MC/CC/OC", 
                    "Derivative Securities Payment - wire", "Buy Currency Forward  - CASH", "Buy Currency Forward - Debit Memo", 
                    "Buy Currency Forward - MC/CC/OC", "Buy Currency Forward - Wire", "Sell Currency Forward - CASH", "Sell Currency Forward - Credit Memo", 
                    "Sell Currency Forward - MC/CC/OC", "Sell Currency Forward - Wire", "Interest Rate Swap", "Buy Mutual Fund Investments/Shares - CASH", 
                    "Buy Mutual Fund Investments/Shares - Debit Memo", "Buy Mutual Fund Investments/Shares - MC/CC/OC", "Buy Mutual Fund Investments/Shares - Wire", 
                    "Mutual Fund Investments/Shares Pretermination - Cash", "Mutual Fund Investments/Shares Pretermination - Credit Memo", 
                    "Mutual Fund Investments/Shares Pretermination - MC/CC/OC", "Mutual Fund Investments/Shares Pretermination - Wire", 
                    "Sell Mutual Fund Investments/Shares - CASH", "Sell Mutual Fund Investments/Shares - Credit Memo", "Sell Mutual Fund Investments/Shares - MC/CC/OC", 
                    "Sell Mutual Fund Investments/Shares - Wire", "Mutual Fund Investments/Shares Payment - Cash", "Mutual Fund Investments/Shares Payment - Credit Memo", 
                    "Mutual Fund Investments/Shares Payment - MC/CC/OC", "Mutual Fund Investments/Shares Payment - Wire", "Money Market Instrument Interest Payment - Cash", 
                    "Money Market Instrument Interest Payment - Credit Memo", "Money Market Instrument Interest Payment - MC/CC/OC", 
                    "Money Market Instrument Placement - CASH", "Money Market Instrument Placement - Debit Memo", "Money Market Instrument Pretermination - Credit Memo", 
                    "Money Market Instrument Placement - MC/CC/OC", "Money Market Instrument Placement - Wire", "Money Market Instrument Pretermination - Cash", 
                    "Money Market Instrument Pretermination - MC/CC/OC", "Money Market Instrument Pretermination - Wire", "Money Market Instrument Payment - Cash", 
                    "Money Market Instrument Payment - Credit Memo", "Money Market Instrument Payment - MC/CC/OC", "Money Market Instrument Payment - Wire", 
                    "Purchase of Precious Metals - Debit", "Buy Preferred Stocks - CASH", "Buy Preferred Stocks - Debit Memo", "Buy Preferred Stocks - MC/CC/OC", 
                    "Buy Preferred Stocks - Wire", "Preferred Stocks Pretermination - Cash", "Preferred Stocks Pretermination - Credit Memo", 
                    "Preferred Stocks Pretermination - MC/CC/OC", "Preferred Stocks Pretermination - Wire", "Sell Preferred Stocks - CASH", 
                    "Sell Preferred Stocks - Credit Memo", "Sell Preferred Stocks - MC/CC/OC", "Sell Preferred Stocks - Wire", "Preferred Stocks Payment - Cash", 
                    "Preferred Stocks Payment - Credit Memo", "Preferred Stocks Payment - MC/CC/OC", "Preferred Stocks Payment - Wire", 
                    "Buy Securities - REPO - Cash", "Buy Securities - REPO - Debit Memo", "Buy Securities - REPO - MC/CC/OC", "Buy Securities - REPO - Wire", 
                    "Sell Securities - REPO - Cash", "Sell Securities - REPO - Credit Memo", "Sell Securities - REPO - MC/CC/OC", "Sell Securities - REPO - Wire", 
                    "Buy Securitized Assets - CASH", "Buy Securitized Assets - Debit Memo", "Buy Securitized Assets - MC/CC/OC", "Buy Securitized Assets - Wire", 
                    "Securitized Assets Pretermination - Cash", "Securitized Assets Pretermination - Credit Memo", "Securitized Assets Pretermination - MC/CC/OC", 
                    "Securitized Assets Pretermination - Wire", "Sell Securitized Assets - CASH", "Sell Securitized Assets - Credit Memo", 
                    "Sell Securitized Assets - MC/CC/OC", "Sell Securitized Assets - Wire", "Securitized Assets Payment - Cash", "Securitized Assets Payment - Credit Memo", 
                    "Securitized Assets Payment - MC/CC/OC", "Securitized Assets Payment - Wire", "Buy Sovereign Bonds - CASH", "Buy Sovereign Bonds - Debit Memo", 
                    "Buy Sovereign Bonds - MC/CC/OC", "Buy Sovereign Bonds - Wire", "Sovereign Bond Pretermination - Cash", "Sovereign Bond Pretermination - Credit Memo", 
                    "Sovereign Bond Pretermination - MC/CC/OC", "Sovereign Bond Pretermination - wire", "Sell Sovereign Bonds - CASH", 
                    "Sell Sovereign Bonds - Credit Memo", "Sell Sovereign Bonds - MC/CC/OC", "Sell Sovereign Bonds - Wire", "Sovereign Bond Payment - Cash", 
                    "Sovereign Bond Payment - Credit Memo", "Sovereign Bond Payment - MC/CC/OC", "Sovereign Bond Payment - wire", "Securities Pretermination - Cash", 
                    "Securities Pretermination - Credit Memo", "Securities Pretermination - MC/CC/OC", "Securities Pretermination - wire", "Sell Securities - Cash", 
                    "Sell Securities - Credit Memo", "Sell Securities - MC/CC/OC", "Sell Securities - Wire", "Securities Payment - Cash", 
                    "Securities Payment - Credit Memo", "Securities Payment - MC/CC/OC", "Securities Payment - Wire", "Trust Fund Income", "Trust Fund Expense", 
                    "Buy Warrants - CASH", "Buy Warrants - Debit Memo", "Buy Warrants - MC/CC/OC", "Buy Warrants - Wire", "Warrants Pretermination - Cash", 
                    "Warrants Pretermination - Credit Memo", "Warrants Pretermination - MC/CC/OC", "Warrants Pretermination - Wire", "Sell Warrants - CASH", 
                    "Sell Warrants - Credit Memo", "Sell Warrants - MC/CC/OC", "Sell Warrants - Wire", "Warrants Payment - Cash", "Warrants Payment - Credit Memo", 
                    "Warrants Payment - MC/CC/OC", "Warrants Payment - Wire", "Agency Investment in Debt Instruments", "Agency Investment in Equities", 
                    "Agency Investment in Loans & Receivables", "Agency Investment in Other Assets", "Agency placement - cash", "Agency placement - debit memo", 
                    "Agency Payment of Loans & Receivables", "Agency placement - MC/CC/OC/DD", "Agency placement - wire", "Agency Sale of  Debt Instruments", 
                    "Agency Sale of  Equities", "Agency Sale of  Other Assets", "Agency withdrawal - Cash", "Agency withdrawal - credit memo", 
                    "Agency withdrawal - MC/CC/OC/DD", "Agency withdrawal - Wire", "Fiduciary-Administratorship  placement - cash", 
                    "Fiduciary-Administratorship  placement - debit memo", "Fiduciary-Administratorship  placement - MC/CC/OC/DD", 
                    "Fiduciary-Administratorship  placement - wire", "Fiduciary-Administratorship  withdrawal - cash", 
                    "Fiduciary-Administratorship  withdrawal - credit memo", "Fiduciary-Administratorship  withdrawal - MC/CC/OC", 
                    "Fiduciary-Administratorship  withdrawal - Wire", "Fiduciary-Custodianship placement - cash", "Fiduciary-Custodianship placement - debit memo", 
                    "Fiduciary-Custodianship placement - MC/CC/OC", "Fiduciary-Custodianship placement - wire", "Fiduciary-Custodianship withdrawal - cash", 
                    "Fiduciary-Custodianship withdrawal - credit memo", "Fiduciary-Custodianship withdrawal - MC/CC/OC", "Fiduciary-Custodianship withdrawal - Wire", 
                    "Fiduciary-Escrow placement - cash", "Fiduciary-Escrow placement - debit memo", "Fiduciary-Escrow placement - MC/CC/OC", 
                    "Fiduciary-Escrow placement - wire", "Fiduciary-Escrow withdrawal - cash", "Fiduciary-Escrow withdrawal - credit memo", 
                    "Fiduciary-Escrow withdrawal - MC/CC/OC", "Fiduciary-Escrow withdrawal - Wire", "Fiduciary-Guardianship placement - cash", 
                    "Fiduciary-Guardianship placement - debit memo", "Fiduciary-Guardianship placement - MC/CC/OC", "Fiduciary-Guardianship placement - wire", 
                    "Fiduciary-Guardianship withdrawal - cash", "Fiduciary-Guardianship withdrawal - credit memo", "Fiduciary-Guardianship withdrawal - MC/CC/OC", 
                    "Fiduciary-Guardianship withdrawal - wire", "Fiduciary-Life Insurance Trust placement - cash", "Fiduciary-Life Insurance Trust placement - debit memo", 
                    "Fiduciary-Life Insurance Trust placement - MC/CC/OC", "Fiduciary-Life Insurance Trust placement - wire", 
                    "Fiduciary-Life Insurance Trust withdrawal - cash", "Fiduciary-Life Insurance Trust withdrawal - credit memo", 
                    "Fiduciary-Life Insurance Trust withdrawal - MC/CC/OC", "Fiduciary-Life Insurance Trust withdrawal - wire", "Fiduciary-Safekeeping placement", 
                    "Fiduciary-Safekeeping placement - Cash", "Fiduciary-Safekeeping placement - Debit Memo", "Fiduciary-Safekeeping placement  - MC/CC/OC", 
                    "Fiduciary-Safekeeping placement - wire", "Fiduciary-Safekeeping withdrawal", "Fiduciary-Safekeeping withdrawal -Cash", 
                    "Fiduciary-Safekeeping withdrawal - Credit Memo", "Fiduciary-Safekeeping withdrawal - MC/CC/OC", "Fiduciary-Safekeeping withdrawal - wire", 
                    "Fiduciary-Executorship placement - cash", "Fiduciary-Executorship placement - debit memo", "Fiduciary-Executorship placement - MC/CC/OC", 
                    "Fiduciary-Executorship placement - wire", "Fiduciary-Executorship withdrawal - cash", "Fiduciary-Executorship withdrawal - credit memo", 
                    "Fiduciary-Executorship withdrawal - MC/CC/OC", "Fiduciary-Executorship withdrawal - wire", "Other Fiduciary Placement - Cash", 
                    "Other Fiduciary Placement - Debit Memo", "Other Fiduciary Placement - MC/CC/OC", "Other Fiduciary Placement - wire", 
                    "Other Fiduciary withdrawal - Cash", "Other Fiduciary withdrawal - credit memo", "Other Fiduciary withdrawal - MC/CC/OC", 
                    "Other Fiduciary withdrawal - wire", "Pre-Need Account placement - cash", "Pre-Need Account placement - debit memo", 
                    "Pre-Need Account placement - MC/CC/OC", "Pre-Need Account placement - wire", "Pre-Need Account  withdrawal - cash", 
                    "Pre-Need Account  withdrawal- credit memo", "Pre-Need Account  withdrawal - MC/CC/OC", "Pre-Need Account  withdrawal - wire", 
                    "Securities Delivered Free of Payment", "Securities Delivered vs. Payment", "Special Purpose Trust Placement", "Special Purpose Trust Placement-Cash", 
                    "Special Purpose Trust Placement-Debit Memo", "Special Purpose Trust Placement - MC/CC/OC", "Special Purpose Trust Placement - wire", 
                    "Securities Received Free of Payment", "Securities Received vs. Payment", "Special Purpose Trust Withdrawal - Cash", 
                    "Special Purpose Trust Withdrawal - Credit Memo", "Special Purpose Trust Withdrawal - MC/CC/OC", "Special Purpose Trust Withdrawal - wire", 
                    "Trust Fund Contribution/Placement/ Investment - Cash", "Trust Fund Contribution/Placement/ Investment - Debit Memo", 
                    "Trust Fund Contribution/Placement/ Investment - MC/CC/OC", "Trust Fund Contribution/Placement/ investment - wire", 
                    "Trust Fund Maturity/Withdrawal/ Redemption/Cancellation - Cash", "Trust Fund Maturity/Withdrawal/ Redemption/Cancellation - Credit Memo", 
                    "Trust Fund Maturity/Withdrawal/Redemption/Cancellation - MC/CC/OC", "Trust Fund Maturity/Withdrawal/Redemption/Cancellation - Wire", 
                    "Unit Investment Trust Fund Cont./Placement/Investment - Cash", "Unit Investment Trust Fund Cont./Placement/Investment  - Debit Memo", 
                    "Unit Investment Trust Fund Cont./Placement/Investment  - MC/CC/OC", "Unit Investment Trust Fund Cont./Placement/Investment - wire", 
                    "Unit Investment Trust Fund Maturity/Withdrawal/ Redemption/Cancellation/ Pretermination - Cash", 
                    "Unit Investment Trust Fund Maturity/Withdrawal/Redemption/Cancellation/Pretermination - Credit Memo", 
                    "Unit Investment Trust Fund Maturity/Withdrawal/Redemption/Cancellation/Pretermination - MC/CC/OC", 
                    "Unit Investment Trust Fund Maturity/Withdrawal/Redemption/Cancellation/Pretermination - Wire", "Deposit Interest Earned", 
                    "Claim bond portion of Land Transfer Payment - Agrarian Reform 10-yr bond", "Claim Cash portion of Land Transfer Payment - Cash", 
                    "Claim Cash portion of Land Transfer Payment - Credit memo", "Claim bond portion of Land Transfer Payment - LandBank 25-yr bond", 
                    "Claim Cash portion of Land Transfer Payment - MC", "Expense Payment", "STR transactions", "NOT AVAILABLE", "TEST", "WITCHREMITTANCE", 
                    "WITCH-LOADING", "Roll Over of Time Deposit", "Deposit – through other local bank ", "On-Us Check Deposit", "Check Cutting Services – Debit from Account", 
                    "Purchase of MC/CC/DD/TC – Mixed Payment", "Withdrawal - through issuance of check", "Withdrawal - through other local bank", 
                    "Deffered Transaction", "Trust Receipt Pre-termination", "Time Deposit Placement – Mixed Payment", "STR - per Account", 
                    "STR – Attempted transactions", "Loan Cancellation", "demos", "Try Testing", "try132", "demos1", "kangkong chips by josh mojica", 
                    "Time Deposit  Pretermination – Mixed  Payment", "Time Deposit Payment – Mixed Payment", "Buy Foreign Exchange – Mixed Payment", 
                    "Sell FX – Mixed Payment", "Payment to credit card  merchants – Credit to  Account", "Payment to credit card  merchants – Check", 
                    "Lease Contract  Agreement Cancellation", "Lease Contract  Agreemen", "Capital Infusion - Cash ", "Capital Infusion - MC/CC/OC ", 
                    "Capital Infusion - Wire ", "Capital Infusion - Debit ", "Credit Default Swap", "Deliverable/Currency  Forward Credit – Outward  Remittance", 
                    "Deliverable/Currency  Forward Credit – Credit to  Account/MC/CC/OC", "Deliverable/Currency  Forward Debit – Inward  Remittance", 
                    "Deliverable/Currency  Forward Debit – Debit to  Account/MC/CC/OC", "NDF Credit – Outward Remittance ", 
                    "NDF Credit – Credit to  Account/MC/CC/OC", "NDF Debit – Inward  Remittance", "NDF Debit – Debit to  Account/MC/CC/OC", 
                    "UAT TESTING", "Test ITD"
                };

                // Map static strings cleanly into code-name structures for UI dropdown list rendering
                var resultsList = definedTypes.Distinct().Select(name => new 
                {
                    code = name,
                    name = name
                }).ToList();

                return Ok(resultsList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load transaction types", message = ex.Message });
            }
        }

        [HttpGet("GetGraphData")]
        public async Task<IActionResult> GetGraphData(
            [FromQuery] string[] accounts,
            DateTime? startDate,
            DateTime? endDate,
            decimal? minAmount,
            decimal? maxAmount,
            [FromQuery] string? branchId = null,
            [FromQuery] string? transactionType = null,
            [FromQuery] bool latestOnly = false)
        {
            try
            {
                var graphData = new TransactionGraphData();
                if (accounts == null || accounts.Length == 0) 
                {
                    return Ok(new { nodes = graphData.Nodes, edges = graphData.Edges, tableReportData = new List<object>() });
                }

                var query = _context.CustomerTransactions.Where(t => t.IsActive == true).AsQueryable();

                if (startDate.HasValue) query = query.Where(t => t.TransactionDate >= startDate.Value);
                if (endDate.HasValue) query = query.Where(t => t.TransactionDate <= endDate.Value);
                if (minAmount.HasValue) query = query.Where(t => t.TransactionAmount >= minAmount.Value);
                if (maxAmount.HasValue) query = query.Where(t => t.TransactionAmount <= maxAmount.Value);

                // Branch integration supporting both explicit name matching and structural Database ID fields safely
                if (!string.IsNullOrWhiteSpace(branchId))
                {
                    if (branchId.StartsWith("STATIC_REF_"))
                    {
                        // Fallback logic handling items without direct active key records cleanly
                        query = query.Where(t => false); 
                    }
                    else
                    {
                        query = query.Where(t => t.BranchId.ToString() == branchId);
                    }
                }

                // Transaction type text-level constraint verification logic
                if (!string.IsNullOrWhiteSpace(transactionType))
                {
                    query = query.Where(t => t.TransactionType == transactionType);
                }

                query = from txn in query
                        join acc in _context.CustomerAccounts on txn.CustomerAccountId equals acc.CustomerAccountId into accGroup
                        from acc in accGroup.DefaultIfEmpty()
                        join bene in _context.Beneficiaries on txn.CustomerTransactionId equals bene.CustomerTransactionId into bGroup
                        from bene in bGroup.DefaultIfEmpty()
                        where (acc != null && accounts.Contains(acc.AccountNumber)) ||
                              (bene != null && accounts.Contains(bene.BeneficiaryAccountNumber))
                        select txn;

                var rawTransactions = await query.Distinct().ToListAsync();

                if (latestOnly)
                {
                    rawTransactions = rawTransactions
                        .Where(t => t.TransactionDate != null)
                        .OrderByDescending(t => t.TransactionDate.GetValueOrDefault())
                        .GroupBy(t => t.CustomerAccountId)
                        .Select(grouping => grouping.First())
                        .ToList();
                }

                var refNumbers = rawTransactions
                    .Select(t => t.TransactionReferenceNumber)
                    .Where(refNo => !string.IsNullOrEmpty(refNo))
                    .Distinct()
                    .ToList();

                var sharedRefTransactions = await _context.CustomerTransactions
                    .Where(t => t.IsActive == true && refNumbers.Contains(t.TransactionReferenceNumber))
                    .ToListAsync();

                var allInvolvedAccountIds = sharedRefTransactions
                    .Where(t => t.CustomerAccountId != null)
                    .Select(t => t.CustomerAccountId.GetValueOrDefault())
                    .Distinct()
                    .ToList();

                var accountIdentityList = await (from acc in _context.CustomerAccounts
                                                 join ja in _context.JointAccounts on acc.CustomerAccountId equals ja.CustomerAccountId into jaGroup
                                                 from ja in jaGroup.DefaultIfEmpty()
                                                 join cust in _context.Customers on ja.CustomerId equals cust.CustomerId into custGroup
                                                 from cust in custGroup.DefaultIfEmpty()
                                                 where allInvolvedAccountIds.Contains(acc.CustomerAccountId)
                                                 select new
                                                 {
                                                     acc.CustomerAccountId,
                                                     acc.AccountNumber,
                                                     CustomerId = cust != null ? cust.CustomerId : 0,
                                                     FullName = cust != null ? (cust.CustomerFirstName + " " + (string.IsNullOrEmpty(cust.CustomerMiddleName) ? "" : cust.CustomerMiddleName + " ") + cust.CustomerLastName).Trim() : "Unknown Holder"
                                                 }).ToListAsync();

                var accountIdentityMap = accountIdentityList
                    .GroupBy(x => x.CustomerAccountId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.First()
                    );

                var transactionIds = rawTransactions.Select(t => t.CustomerTransactionId).ToList();
                var holders = await _context.AccountHolders.Where(h => h.CustomerTransactionId.HasValue && transactionIds.Contains(h.CustomerTransactionId.Value)).ToListAsync();
                var beneficiaries = await _context.Beneficiaries.Where(b => b.CustomerTransactionId.HasValue && transactionIds.Contains(b.CustomerTransactionId.Value)).ToListAsync();

                var phCulture = new CultureInfo("en-PH");
                var renderedAccountLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var tableReportData = new List<object>();

                foreach (var txn in rawTransactions)
                {
                    string senderId = $"S_ACC_{txn.CustomerAccountId}";
                    string senderName = "Unknown Sender Account";

                    if (txn.CustomerAccountId.HasValue && accountIdentityMap.TryGetValue(txn.CustomerAccountId.Value, out var senderIdentity))
                    {
                        senderId = senderIdentity.AccountNumber;
                        senderName = senderIdentity.FullName;
                    }
                    else
                    {
                        var holder = holders.FirstOrDefault(h => h.CustomerTransactionId == txn.CustomerTransactionId);
                        if (holder != null)
                        {
                            senderName = !string.IsNullOrEmpty(holder.AccountHolderCorporateName) ? holder.AccountHolderCorporateName : $"{holder.AccountHolderFirstName} {holder.AccountHolderLastName}".Trim();
                        }
                    }

                    string beneficiaryId = $"B_ACC_{txn.CustomerTransactionId}";
                    string beneficiaryName = "Unknown Beneficiary Target";

                    var counterpartyTxn = sharedRefTransactions.FirstOrDefault(t =>
                        t.TransactionReferenceNumber == txn.TransactionReferenceNumber &&
                        t.CustomerAccountId != txn.CustomerAccountId);

                    if (counterpartyTxn != null && counterpartyTxn.CustomerAccountId.HasValue &&
                        accountIdentityMap.TryGetValue(counterpartyTxn.CustomerAccountId.Value, out var targetIdentity))
                    {
                        beneficiaryId = targetIdentity.AccountNumber;
                        beneficiaryName = targetIdentity.FullName;
                    }
                    else
                    {
                        var bene = beneficiaries.FirstOrDefault(b => b.CustomerTransactionId == txn.CustomerTransactionId);
                        if (bene != null)
                        {
                            beneficiaryId = !string.IsNullOrEmpty(bene.BeneficiaryAccountNumber) ? bene.BeneficiaryAccountNumber : beneficiaryId;
                            if (!string.IsNullOrEmpty(bene.BeneficiaryCorporateName) || !string.IsNullOrEmpty(bene.BeneficiaryFirstName))
                            {
                                beneficiaryName = !string.IsNullOrEmpty(bene.BeneficiaryCorporateName) ? bene.BeneficiaryCorporateName : $"{bene.BeneficiaryFirstName} {bene.BeneficiaryLastName}".Trim();
                            }
                        }
                    }

                    decimal displayAmount = txn.TransactionAmount ?? 0;
                    string refNo = txn.TransactionReferenceNumber ?? $"TXN_{txn.CustomerTransactionId}";
                    string txnNodeId = $"TXN_NODE_{txn.CustomerTransactionId}";

                    tableReportData.Add(new
                    {
                        referenceNumber = refNo,
                        date = txn.TransactionDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A",
                        type = txn.TransactionType ?? "TRFR",
                        senderName = senderName,
                        senderAccount = senderId,
                        beneficiaryName = beneficiaryName,
                        beneficiaryAccount = beneficiaryId,
                        branchId = txn.BranchId.ToString(),
                        amount = displayAmount.ToString("C", phCulture)
                    });

                    string senderTableTooltip = $@"
                        <div style='padding: 6px; min-width: 260px;'>
                            <table style='width: 100%; border-collapse: collapse; font-family: sans-serif; font-size: 11px;'>
                                <tr style='background-color: #f8fafc; border-bottom: 2px solid #e2e8f0;'><th colspan='2' style='padding: 6px; text-align: left; color: #4f46e5;'>ORIGINATING SENDER</th></tr>
                                <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 6px; color: #64748b;'>Account Number</td><td style='padding: 6px; font-weight: bold; font-family: monospace;'>{senderId}</td></tr>
                                <tr><td style='padding: 6px; color: #64748b;'>Customer Name</td><td style='padding: 6px; font-weight: 600;'>{senderName}</td></tr>
                            </table>
                        </div>";

                    string beneficiaryTableTooltip = $@"
                        <div style='padding: 6px; min-width: 260px;'>
                            <table style='width: 100%; border-collapse: collapse; font-family: sans-serif; font-size: 11px;'>
                                <tr style='background-color: #f8fafc; border-bottom: 2px solid #e2e8f0;'><th colspan='2' style='padding: 6px; text-align: left; color: #10b981;'>BENEFICIARY TARGET</th></tr>
                                <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 6px; color: #64748b;'>Account Number</td><td style='padding: 6px; font-weight: bold; font-family: monospace;'>{beneficiaryId}</td></tr>
                                <tr><td style='padding: 6px; color: #64748b;'>Customer Name</td><td style='padding: 6px; font-weight: 600;'>{beneficiaryName}</td></tr>
                            </table>
                        </div>";

                    string transactionNodeTooltip = $@"
                        <div style='padding: 6px; min-width: 260px;'>
                            <table style='width: 100%; border-collapse: collapse; font-family: sans-serif; font-size: 11px;'>
                                <tr style='background-color: #f8fafc; border-bottom: 2px solid #e2e8f0;'><th colspan='2' style='padding: 6px; text-align: left; color: #d97706;'>TRANSACTION LEDGER LOG</th></tr>
                                <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 6px; color: #64748b;'>Reference ID</td><td style='padding: 6px; font-weight: bold; font-family: monospace; color: #b45309;'>{refNo}</td></tr>
                                <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 6px; color: #64748b;'>Execution Date</td><td style='padding: 6px;'>{txn.TransactionDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}</td></tr>
                                <tr><td style='padding: 6px; color: #64748b;'>Amount Settled</td><td style='padding: 6px; font-weight: bold; color: #16a34a;'>{displayAmount.ToString("C", phCulture)}</td></tr>
                            </table>
                        </div>";

                    if (!graphData.Nodes.Any(n => n.Id == senderId))
                    {
                        graphData.Nodes.Add(new TransactionGraphNode { Id = senderId, Label = senderName, Group = "Sender", Title = senderTableTooltip });
                        renderedAccountLabels[senderId] = senderName;
                    }

                    if (!graphData.Nodes.Any(n => n.Id == beneficiaryId))
                    {
                        graphData.Nodes.Add(new TransactionGraphNode { Id = beneficiaryId, Label = beneficiaryName, Group = "Beneficiary", Title = beneficiaryTableTooltip });
                        renderedAccountLabels[beneficiaryId] = beneficiaryName;
                    }

                    graphData.Nodes.Add(new TransactionGraphNode
                    {
                        Id = txnNodeId,
                        Label = displayAmount.ToString("C", phCulture),
                        Group = "Transaction",
                        Title = transactionNodeTooltip
                    });

                    graphData.Edges.Add(new TransactionGraphEdge { Id = $"{refNo}_Src", From = senderId, To = txnNodeId, Label = "" });
                    graphData.Edges.Add(new TransactionGraphEdge { Id = $"{refNo}_Dst", From = txnNodeId, To = beneficiaryId, Label = "" });
                }

                var activeCanvasAccountNumbers = new HashSet<string>(renderedAccountLabels.Keys, StringComparer.OrdinalIgnoreCase);

                var customerGroupings = accountIdentityList
                    .Where(identity => identity.CustomerId != 0 && activeCanvasAccountNumbers.Contains(identity.AccountNumber))
                    .GroupBy(identity => identity.CustomerId)
                    .ToList();

                foreach (var customerGroup in customerGroupings)
                {
                    var firstIdentity = customerGroup.First();
                    string customerName = firstIdentity.FullName;
                    string customerNodeId = $"CUST_{firstIdentity.CustomerId}";

                    int totalConnectedAccounts = customerGroup.Select(x => x.AccountNumber).Distinct().Count();
                    bool nameMismatchDetected = false;

                    foreach (var relationship in customerGroup)
                    {
                        if (renderedAccountLabels.TryGetValue(relationship.AccountNumber, out var renderedLabel))
                        {
                            if (!string.Equals(customerName, renderedLabel, StringComparison.OrdinalIgnoreCase))
                            {
                                nameMismatchDetected = true;
                                break;
                            }
                        }
                    }

                    if (totalConnectedAccounts > 1 || nameMismatchDetected)
                    {
                        if (!graphData.Nodes.Any(n => n.Id == customerNodeId))
                        {
                            string customerTooltip = $@"
                                <div style='padding: 6px; min-width: 200px;'>
                                    <table style='width: 100%; border-collapse: collapse; font-family: sans-serif; font-size: 11px;'>
                                        <tr style='background-color: #f8fafc; border-bottom: 2px solid #e2e8f0;'><th style='padding: 6px; text-align: left; color: #3b82f6;'>JOINT ACCOUNT OWNER</th></tr>
                                        <tr><td style='padding: 6px; font-weight: 600;'>{customerName}</td></tr>
                                    </table>
                                </div>";

                            graphData.Nodes.Add(new TransactionGraphNode
                            {
                                Id = customerNodeId,
                                Label = customerName,
                                Group = "Customer",
                                Title = customerTooltip,
                                Shape = "dot"
                            });
                        }

                        foreach (var identity in customerGroup)
                        {
                            string jointEdgeId = $"JOINT_LINK_{identity.CustomerId}_{identity.AccountNumber}";
                            if (!graphData.Edges.Any(e => e.Id == jointEdgeId))
                            {
                                graphData.Edges.Add(new TransactionGraphEdge
                                {
                                    Id = jointEdgeId,
                                    From = customerNodeId,
                                    To = identity.AccountNumber,
                                    Label = "Joint Owner"
                                });
                            }
                        }
                    }
                }

                return Ok(new 
                { 
                    nodes = graphData.Nodes, 
                    edges = graphData.Edges, 
                    tableReportData = tableReportData 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Graph Mapping Runtime Error Exception", message = ex.Message, trace = ex.StackTrace });
            }
        }
    }
}