using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RelationshipVisualizer.Data;
using RelationshipVisualizer.Models;
using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
            // QuestPDF requires license setting initialization in community mode
            QuestPDF.Settings.License = LicenseType.Community;
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
                string[] nameTokens = searchTrim.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                var queryable = (from ct in _context.CustomerTransactions
                                 join ca in _context.CustomerAccounts
                                     on ct.CustomerAccountId equals ca.CustomerAccountId into caGroup
                                 from ca in caGroup.DefaultIfEmpty()
                                 join ja in _context.JointAccounts
                                     on ca.CustomerAccountId equals ja.CustomerAccountId into jaGroup
                                 from ja in jaGroup.DefaultIfEmpty()
                                 join c in _context.Customers
                                     on ja.CustomerId equals c.CustomerId into cGroup
                                 from c in cGroup.DefaultIfEmpty()
                                 where ct.IsActive == true && c != null && c.IsActive == true
                                 select new { ca, c });

                if (nameTokens.Length >= 2)
                {
                    string firstToken = nameTokens[0];
                    string lastToken = nameTokens[nameTokens.Length - 1];

                    queryable = queryable.Where(x =>
                        (EF.Functions.Like(x.c.CustomerFirstName ?? "", $"%{firstToken}%") && EF.Functions.Like(x.c.CustomerLastName ?? "", $"%{lastToken}%")) ||
                        (EF.Functions.Like(x.c.CustomerLastName ?? "", $"%{firstToken}%") && EF.Functions.Like(x.c.CustomerFirstName ?? "", $"%{lastToken}%")) ||
                        EF.Functions.Like(x.c.CustomerFirstName ?? "", wildCardSearch) ||
                        EF.Functions.Like(x.c.CustomerLastName ?? "", wildCardSearch)
                    );
                }
                else
                {
                    queryable = queryable.Where(x =>
                        x.c.CustomerFirstName == searchTrim ||
                        x.c.CustomerLastName == searchTrim ||
                        EF.Functions.Like(x.c.CustomerFirstName ?? "", wildCardSearch) ||
                        EF.Functions.Like(x.c.CustomerMiddleName ?? "", wildCardSearch) ||
                        EF.Functions.Like(x.c.CustomerLastName ?? "", wildCardSearch)
                    );
                }

                var databaseMatches = await queryable
                    .Select(x => new
                    {
                        AccountNumber = x.ca != null ? x.ca.AccountNumber : "N/A",
                        FirstName = x.c.CustomerFirstName ?? "",
                        MiddleName = x.c.CustomerMiddleName ?? "",
                        LastName = x.c.CustomerLastName ?? ""
                    })
                    .Distinct()
                    .ToListAsync();

                var uniqueResults = new List<object>();
                var seenFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var match in databaseMatches)
                {
                    string fullName = $"{match.FirstName} {(string.IsNullOrWhiteSpace(match.MiddleName) ? "" : match.MiddleName + " ")}{match.LastName}".Trim();
                    if (string.IsNullOrEmpty(fullName)) fullName = "Unknown Holder";
                    if (seenFullNames.Contains(fullName)) continue;

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
                var reportContext = await FetchReportDataAsync(accounts, startDate, endDate, minAmount, maxAmount, branchId, transactionType, latestOnly);
                if (reportContext == null)
                {
                    return Ok(new { nodes = new List<object>(), edges = new List<object>(), tableReportData = new List<object>() });
                }

                var graphData = new TransactionGraphData();
                var renderedAccountLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var phCulture = new CultureInfo("en-PH");

                foreach (var item in reportContext.ReportRows)
                {
                    string senderTableTooltip = $"<div style='padding:6px;min-width:260px;'><table style='width:100%;border-collapse:collapse;font-size:11px;'><tr style='background-color:#f8fafc;border-bottom:2px solid #e2e8f0;'><th colspan='2' style='padding:6px;text-align:left;color:#4f46e5;'>ORIGINATING SENDER</th></tr><tr><td style='padding:6px;color:#64748b;'>Account</td><td>{item.SenderAccount}</td></tr><tr><td style='padding:6px;color:#64748b;'>Name</td><td>{item.SenderName}</td></tr></table></div>";
                    string beneficiaryTableTooltip = $"<div style='padding:6px;min-width:260px;'><table style='width:100%;border-collapse:collapse;font-size:11px;'><tr style='background-color:#f8fafc;border-bottom:2px solid #e2e8f0;'><th colspan='2' style='padding:6px;text-align:left;color:#10b981;'>BENEFICIARY TARGET</th></tr><tr><td style='padding:6px;color:#64748b;'>Account</td><td>{item.BeneficiaryAccount}</td></tr><tr><td style='padding:6px;color:#64748b;'>Name</td><td>{item.BeneficiaryName}</td></tr></table></div>";
                    string transactionNodeTooltip = $"<div style='padding:6px;min-width:260px;'><table style='width:100%;border-collapse:collapse;font-size:11px;'><tr style='background-color:#f8fafc;border-bottom:2px solid #e2e8f0;'><th colspan='2' style='padding:6px;text-align:left;color:#d97706;'>TRANSACTION LOG</th></tr><tr><td style='padding:6px;color:#64748b;'>ID</td><td>{item.ReferenceNumber}</td></tr><tr><td style='padding:6px;color:#64748b;'>Amount</td><td>{item.RawAmount.ToString("C", phCulture)}</td></tr></table></div>";

                    if (!graphData.Nodes.Any(n => n.Id == item.SenderAccount))
                    {
                        graphData.Nodes.Add(new TransactionGraphNode { Id = item.SenderAccount, Label = item.SenderName, Group = "Sender", Title = senderTableTooltip });
                        renderedAccountLabels[item.SenderAccount] = item.SenderName;
                    }
                    if (!graphData.Nodes.Any(n => n.Id == item.BeneficiaryAccount))
                    {
                        graphData.Nodes.Add(new TransactionGraphNode { Id = item.BeneficiaryAccount, Label = item.BeneficiaryName, Group = "Beneficiary", Title = beneficiaryTableTooltip });
                        renderedAccountLabels[item.BeneficiaryAccount] = item.BeneficiaryName;
                    }

                    string txnNodeId = $"TXN_NODE_{item.InternalTxnId}";
                    bool isSelfTransfer = !string.Equals(item.SenderAccount, "N/A") && string.Equals(item.SenderAccount, item.BeneficiaryAccount, StringComparison.OrdinalIgnoreCase);

                    if (isSelfTransfer)
                    {
                        string clusterHubNodeId = $"HUB_SELF_{item.SenderAccount}";
                        if (!graphData.Nodes.Any(n => n.Id == clusterHubNodeId))
                        {
                            graphData.Nodes.Add(new TransactionGraphNode { Id = clusterHubNodeId, Label = "Self-Transfer Cluster", Group = "SelfTransferHub", Title = "Self Hub" });
                        }
                        graphData.Nodes.Add(new TransactionGraphNode { Id = txnNodeId, Label = item.Amount, Group = "Transaction", Title = transactionNodeTooltip, TransactionType = item.Type });
                        graphData.Edges.Add(new TransactionGraphEdge { Id = $"{item.InternalTxnId}_Self_Src", From = item.SenderAccount, To = txnNodeId, Label = "" });
                        graphData.Edges.Add(new TransactionGraphEdge { Id = $"{item.InternalTxnId}_Self_Hub", From = txnNodeId, To = clusterHubNodeId, Label = "Internal Loop" });
                    }
                    else
                    {
                        graphData.Nodes.Add(new TransactionGraphNode { Id = txnNodeId, Label = item.Amount, Group = "Transaction", Title = transactionNodeTooltip, TransactionType = item.Type });
                        graphData.Edges.Add(new TransactionGraphEdge { Id = $"{item.InternalTxnId}_Src", From = item.SenderAccount, To = txnNodeId, Label = "" });
                        graphData.Edges.Add(new TransactionGraphEdge { Id = $"{item.InternalTxnId}_Dst", From = txnNodeId, To = item.BeneficiaryAccount, Label = "" });
                    }
                }

                // Append joint ownership rules mapping loops
                var activeCanvasAccountNumbers = new HashSet<string>(renderedAccountLabels.Keys, StringComparer.OrdinalIgnoreCase);
                var customerGroupings = reportContext.AccountIdentityList
                    .Where(identity => identity.CustomerId != 0 && activeCanvasAccountNumbers.Contains(identity.AccountNumber))
                    .GroupBy(identity => identity.CustomerId);

                foreach (var customerGroup in customerGroupings)
                {
                    var firstIdentity = customerGroup.First();
                    string customerNodeId = $"CUST_{firstIdentity.CustomerId}";

                    if (customerGroup.Select(x => x.AccountNumber).Distinct().Count() > 1)
                    {
                        if (!graphData.Nodes.Any(n => n.Id == customerNodeId))
                        {
                            graphData.Nodes.Add(new TransactionGraphNode { Id = customerNodeId, Label = firstIdentity.FullName, Group = "Customer", Shape = "dot", Title = "Joint Owner" });
                        }
                        foreach (var identity in customerGroup)
                        {
                            string jointEdgeId = $"JOINT_LINK_{identity.CustomerId}_{identity.AccountNumber}";
                            if (!graphData.Edges.Any(e => e.Id == jointEdgeId))
                            {
                                graphData.Edges.Add(new TransactionGraphEdge { Id = jointEdgeId, From = customerNodeId, To = identity.AccountNumber, Label = "Joint Owner" });
                            }
                        }
                    }
                }

                return Ok(new 
                { 
                    nodes = graphData.Nodes, 
                    edges = graphData.Edges, 
                    tableReportData = reportContext.ReportRows.Select(r => new {
                        r.ReferenceNumber, r.Date, r.Type, r.SenderName, r.SenderAccount, r.BeneficiaryName, r.BeneficiaryAccount, r.BranchId, r.Amount
                    }),
                    activeBranches = reportContext.ActiveBranches,               
                    activeTransactionTypes = reportContext.ActiveTransactionTypes 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Graph Mapping Runtime Error", message = ex.Message });
            }
        }

        /* =========================================================================
           NEW DOWNLOAD EXPORT ENGINES
           ========================================================================= */

        [HttpGet("DownloadCsv")]
        public async Task<IActionResult> DownloadCsv(
            [FromQuery] string[] accounts, DateTime? startDate, DateTime? endDate,
            decimal? minAmount, decimal? maxAmount, [FromQuery] string? branchId = null,
            [FromQuery] string? transactionType = null, [FromQuery] bool latestOnly = false)
        {
            var dataContext = await FetchReportDataAsync(accounts, startDate, endDate, minAmount, maxAmount, branchId, transactionType, latestOnly);
            if (dataContext == null || !dataContext.ReportRows.Any())
                return BadRequest("No report records available to export.");

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Reference Number,Date,Type,Sender Name,Sender Account,Beneficiary Name,Beneficiary Account,Branch,Amount");

            foreach (var row in dataContext.ReportRows)
            {
                csvBuilder.AppendLine($"\"{EscapeCsv(row.ReferenceNumber)}\",\"{row.Date}\",\"{EscapeCsv(row.Type)}\",\"{EscapeCsv(row.SenderName)}\",\"{row.SenderAccount}\",\"{EscapeCsv(row.BeneficiaryName)}\",\"{row.BeneficiaryAccount}\",\"{EscapeCsv(row.BranchId)}\",\"{EscapeCsv(row.Amount)}\"");
            }

            // 1. Convert your string builder content to regular UTF-8 bytes
            var csvBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());

            // 2. Get the 3-byte UTF-8 BOM preamble (0xEF, 0xBB, 0xBF)
            var bomBytes = Encoding.UTF8.GetPreamble();

            // 3. Combine the BOM bytes and the CSV data bytes into a single array
            var combinedBytes = new byte[bomBytes.Length + csvBytes.Length];
            Buffer.BlockCopy(bomBytes, 0, combinedBytes, 0, bomBytes.Length);
            Buffer.BlockCopy(csvBytes, 0, combinedBytes, bomBytes.Length, csvBytes.Length);

            // 4. Return the combined bytes file with the correct MIME type
            return File(combinedBytes, "text/csv", $"TransactionReport_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        [HttpGet("DownloadPdf")]
        public async Task<IActionResult> DownloadPdf(
            [FromQuery] string[] accounts, DateTime? startDate, DateTime? endDate,
            decimal? minAmount, decimal? maxAmount, [FromQuery] string? branchId = null,
            [FromQuery] string? transactionType = null, [FromQuery] bool latestOnly = false)
        {
            var dataContext = await FetchReportDataAsync(accounts, startDate, endDate, minAmount, maxAmount, branchId, transactionType, latestOnly);
            if (dataContext == null || !dataContext.ReportRows.Any())
                return BadRequest("No report records available to export.");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape()); // Wide mode matches dense tabular views
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Transaction Report Analysis").FontSize(18).Bold().FontColor("#0f172a");
                        col.Item().PaddingBottom(10).Text($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(9).FontColor(Colors.Grey.Lighten1);
                    });

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        // Match structure columns standard blueprints layout sizing
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(75);  // Ref Num
                            columns.ConstantColumn(65);  // Date
                            columns.ConstantColumn(65);  // Type
                            columns.RelativeColumn(1);   // Sender Name
                            columns.ConstantColumn(75);  // Sender Acc
                            columns.RelativeColumn(1);   // Beneficiary Name
                            columns.ConstantColumn(75);  // Beneficiary Acc
                            columns.RelativeColumn(0.8f); // Branch
                            columns.ConstantColumn(85);  // Amount
                        });

                        // Styled Table Headers matching layout colors
                        table.Header(header =>
                        {
                            string[] titles = { "Ref Number", "Date", "Type", "Sender Name", "Sender Acc", "Beneficiary Name", "Beneficiary Acc", "Branch", "Amount" };
                            foreach (var title in titles)
                            {
                                header.Cell().Background("#111827").Padding(5).Text(title).Bold().FontColor("#94a3b8").FontSize(8);
                            }
                        });

                        // Data rows mapping context content loops
                        bool alternateRow = false;
                        foreach (var row in dataContext.ReportRows)
                        {
                            string bg = alternateRow ? "#f8fafc" : "#ffffff";
                            
                            table.Cell().Background(bg).Padding(5).Text(row.ReferenceNumber).FontFamily(Fonts.CourierNew);
                            table.Cell().Background(bg).Padding(5).Text(row.Date);
                            table.Cell().Background(bg).Padding(5).Text(row.Type);
                            table.Cell().Background(bg).Padding(5).Text(row.SenderName);
                            table.Cell().Background(bg).Padding(5).Text(row.SenderAccount).FontFamily(Fonts.CourierNew);
                            table.Cell().Background(bg).Padding(5).Text(row.BeneficiaryName);
                            table.Cell().Background(bg).Padding(5).Text(row.BeneficiaryAccount).FontFamily(Fonts.CourierNew);
                            table.Cell().Background(bg).Padding(5).Text(row.BranchId);
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(row.Amount).Bold();

                            alternateRow = !alternateRow;
                        }
                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                    });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return File(stream.ToArray(), "application/pdf", $"TransactionReport_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        }

        /* =========================================================================
           CORE REUSABLE EXTRACTOR INTERACTION METHODS
           ========================================================================= */
        private async Task<ReportExtractionContext?> FetchReportDataAsync(
            string[] accounts, DateTime? startDate, DateTime? endDate,
            decimal? minAmount, decimal? maxAmount, string? branchId,
            string? transactionType, bool latestOnly)
        {
            if (accounts == null || accounts.Length == 0) return null;

            var query = _context.CustomerTransactions.Where(t => t.IsActive == true).AsQueryable();

            if (startDate.HasValue) query = query.Where(t => t.TransactionDate >= startDate.Value);
            if (endDate.HasValue) query = query.Where(t => t.TransactionDate <= endDate.Value);
            if (minAmount.HasValue) query = query.Where(t => t.TransactionAmount >= minAmount.Value);
            if (maxAmount.HasValue) query = query.Where(t => t.TransactionAmount <= maxAmount.Value);

            if (!string.IsNullOrWhiteSpace(branchId))
            {
                if (int.TryParse(branchId, out int branchIntId)) query = query.Where(t => t.BranchId == branchIntId);
                else query = query.Where(t => t.BranchId.ToString() == branchId);
            }

            if (!string.IsNullOrWhiteSpace(transactionType))
            {
                string targetFilter = transactionType.Trim();
                var matchedTypes = await _context.TransactionTypes
                    .Where(tt => tt.Code == targetFilter || tt.TransactionTypeId.ToString() == targetFilter)
                    .Select(tt => new { Code = tt.Code, IdString = tt.TransactionTypeId.ToString() }).ToListAsync();

                var validFilterKeys = matchedTypes.Select(m => m.IdString).Concat(matchedTypes.Select(m => m.Code!.Trim())).Distinct().ToList();
                if (!validFilterKeys.Contains(targetFilter)) validFilterKeys.Add(targetFilter);

                query = query.Where(t => validFilterKeys.Contains(t.TransactionType.Trim()));
            }

            query = from txn in query
                    join acc in _context.CustomerAccounts on txn.CustomerAccountId equals acc.CustomerAccountId into accGroup
                    from acc in accGroup.DefaultIfEmpty()
                    where (acc != null && accounts.Contains(acc.AccountNumber)) || accounts.Contains(txn.TransactionReferenceNumber)
                    select txn;

            var rawTransactions = await query.Distinct().ToListAsync();

            if (latestOnly)
            {
                rawTransactions = rawTransactions
                    .Where(t => t.TransactionDate != null)
                    .OrderByDescending(t => t.TransactionDate!.Value)
                    .GroupBy(t => t.CustomerAccountId)
                    .Select(g => g.First()).ToList();
            }

            var activeBranchIds = rawTransactions.Where(t => t.BranchId != null).Select(t => t.BranchId).Distinct().ToList();
            var activeBranchesFilter = await _context.Branches
                .Where(b => activeBranchIds.Contains(b.BranchId))
                .Select(b => new { branchId = b.BranchId.ToString(), name = b.Name.Trim() }).Distinct().ToListAsync();
            var branchLookup = activeBranchesFilter.ToDictionary(b => b.branchId, b => b.name, StringComparer.OrdinalIgnoreCase);

            var activeTxnTypes = rawTransactions.Where(t => !string.IsNullOrWhiteSpace(t.TransactionType)).Select(t => t.TransactionType.Trim()).Distinct().ToList();
            var allTransactionTypes = await _context.TransactionTypes.ToListAsync();
            var txnTypeLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var tt in allTransactionTypes)
            {
                string nameLabel = tt.Name?.Trim() ?? "Unknown";
                txnTypeLookup[tt.TransactionTypeId.ToString()] = nameLabel;
                if (!string.IsNullOrWhiteSpace(tt.Code)) txnTypeLookup[tt.Code.Trim()] = nameLabel;
            }

            var activeTransactionTypesFilter = allTransactionTypes
                .Where(tt => activeTxnTypes.Contains(tt.TransactionTypeId.ToString()) || (!string.IsNullOrEmpty(tt.Code) && activeTxnTypes.Contains(tt.Code.Trim())))
                .Select(tt => new { code = !string.IsNullOrEmpty(tt.Code) ? tt.Code.Trim() : tt.TransactionTypeId.ToString(), name = tt.Name.Trim() }).Distinct().ToList();

            var currentAccountIds = rawTransactions.Where(t => t.CustomerAccountId.HasValue).Select(t => t.CustomerAccountId!.Value).Distinct().ToList();
            var transactionRefNumbers = rawTransactions.Select(t => t.TransactionReferenceNumber).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();

            var accountIdentityList = await (from acc in _context.CustomerAccounts
                                             join ja in _context.JointAccounts on acc.CustomerAccountId equals ja.CustomerAccountId into jaGroup
                                             from ja in jaGroup.DefaultIfEmpty()
                                             join cust in _context.Customers on ja.CustomerId equals cust.CustomerId into custGroup
                                             from cust in custGroup.DefaultIfEmpty()
                                             where currentAccountIds.Contains(acc.CustomerAccountId) || transactionRefNumbers.Contains(acc.AccountNumber)
                                             select new AccountIdentityDto
                                             {
                                                 CustomerAccountId = acc.CustomerAccountId,
                                                 AccountNumber = acc.AccountNumber,
                                                 CustomerId = cust != null ? cust.CustomerId : 0,
                                                 FullName = cust != null ? (cust.CustomerFirstName + " " + (string.IsNullOrEmpty(cust.CustomerMiddleName) ? "" : cust.CustomerMiddleName + " ") + cust.CustomerLastName).Trim() : "Unknown Holder"
                                             }).ToListAsync();

            var accountIdentityMap = accountIdentityList.Where(x => x.CustomerAccountId != 0).GroupBy(x => x.CustomerAccountId).ToDictionary(g => g.Key, g => g.First());
            var transactionIds = rawTransactions.Select(t => t.CustomerTransactionId).ToList();
            var holders = await _context.AccountHolders.Where(h => h.CustomerTransactionId.HasValue && transactionIds.Contains(h.CustomerTransactionId!.Value)).ToListAsync();
            var beneficiaries = await _context.Beneficiaries.Where(b => b.CustomerTransactionId.HasValue && transactionIds.Contains(b.CustomerTransactionId!.Value)).ToListAsync();

            var phCulture = new CultureInfo("en-PH");
            var rows = new List<ReportRowDto>();

            foreach (var txn in rawTransactions)
            {
                string refNo = txn.TransactionReferenceNumber ?? $"TXN_{txn.CustomerTransactionId}";
                string senderId = "N/A";
                string senderName = "Unknown Sender Account";
                
                if (txn.CustomerAccountId.HasValue && accountIdentityMap.TryGetValue(txn.CustomerAccountId.Value, out var senderIdentity))
                {
                    senderId = senderIdentity.AccountNumber;
                    senderName = senderIdentity.FullName;
                }
                else
                {
                    var holder = holders.FirstOrDefault(h => h.CustomerTransactionId == txn.CustomerTransactionId);
                    if (holder != null) senderName = !string.IsNullOrEmpty(holder.AccountHolderCorporateName) ? holder.AccountHolderCorporateName : $"{holder.AccountHolderFirstName} {holder.AccountHolderLastName}".Trim();
                }

                string beneficiaryId = !string.IsNullOrWhiteSpace(txn.TransactionReferenceNumber) ? txn.TransactionReferenceNumber.Trim() : "N/A";
                string beneficiaryName = "Unknown Beneficiary Target";

                var beneIdentity = accountIdentityList.FirstOrDefault(a => string.Equals(a.AccountNumber, beneficiaryId, StringComparison.OrdinalIgnoreCase));
                if (beneIdentity != null) beneficiaryName = beneIdentity.FullName;
                else
                {
                    var bene = beneficiaries.FirstOrDefault(b => b.CustomerTransactionId == txn.CustomerTransactionId);
                    if (bene != null && (!string.IsNullOrEmpty(bene.BeneficiaryCorporateName) || !string.IsNullOrEmpty(bene.BeneficiaryFirstName)))
                    {
                        beneficiaryName = !string.IsNullOrEmpty(bene.BeneficiaryCorporateName) ? bene.BeneficiaryCorporateName : $"{bene.BeneficiaryFirstName} {bene.BeneficiaryLastName}".Trim();
                    }
                }

                string resolvedBranchName = txn.BranchId.HasValue && branchLookup.TryGetValue(txn.BranchId.Value.ToString(), out var bName) ? bName : "N/A";
                string resolvedTxnTypeName = !string.IsNullOrEmpty(txn.TransactionType) && txnTypeLookup.TryGetValue(txn.TransactionType.Trim(), out var tName) ? tName : "Transfer";

                rows.Add(new ReportRowDto
                {
                    InternalTxnId = txn.CustomerTransactionId,
                    ReferenceNumber = refNo,
                    Date = txn.TransactionDate?.ToString("yyyy-MM-dd") ?? "N/A",
                    Type = resolvedTxnTypeName,
                    SenderName = senderName,
                    SenderAccount = senderId,
                    BeneficiaryName = beneficiaryName,
                    BeneficiaryAccount = beneficiaryId,
                    BranchId = resolvedBranchName,
                    RawAmount = txn.TransactionAmount ?? 0,
                    Amount = (txn.TransactionAmount ?? 0).ToString("C", phCulture)
                });
            }

            return new ReportExtractionContext
            {
                ReportRows = rows,
                AccountIdentityList = accountIdentityList,
                ActiveBranches = activeBranchesFilter,
                ActiveTransactionTypes = activeTransactionTypesFilter
            };
        }

        private string EscapeCsv(string val) => (val ?? "").Replace("\"", "\"\"");
    }

    /* Helper context transfer components */
    public class ReportExtractionContext
    {
        public List<ReportRowDto> ReportRows { get; set; } = new();
        public List<AccountIdentityDto> AccountIdentityList { get; set; } = new();
        public object ActiveBranches { get; set; } = new();
        public object ActiveTransactionTypes { get; set; } = new();
    }

    public class ReportRowDto
    {
        public int InternalTxnId { get; set; }
        public string ReferenceNumber { get; set; } = "";
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string SenderAccount { get; set; } = "";
        public string BeneficiaryName { get; set; } = "";
        public string BeneficiaryAccount { get; set; } = "";
        public string BranchId { get; set; } = "";
        public decimal RawAmount { get; set; }
        public string Amount { get; set; } = "";
    }

    public class AccountIdentityDto
    {
        public int CustomerAccountId { get; set; }
        public string AccountNumber { get; set; } = "";
        public int CustomerId { get; set; }
        public string FullName { get; set; } = "";
    }
}