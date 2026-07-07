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
                
                // Split components to support accurate matching across spaces
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

                // FIXED: Explicit unrolled conditions instead of .All() to avoid EF core database translation exceptions
                if (nameTokens.Length >= 2)
                {
                    string firstToken = nameTokens[0];
                    string lastToken = nameTokens[nameTokens.Length - 1];

                    queryable = queryable.Where(x =>
                        // Exact combinations: First Name matches token 1 AND Last Name matches token 2 (e.g., "Anita Yap")
                        (EF.Functions.Like(x.c.CustomerFirstName ?? "", $"%{firstToken}%") && EF.Functions.Like(x.c.CustomerLastName ?? "", $"%{lastToken}%")) ||
                        // Inverse variations just in case (e.g., "Yap Anita")
                        (EF.Functions.Like(x.c.CustomerLastName ?? "", $"%{firstToken}%") && EF.Functions.Like(x.c.CustomerFirstName ?? "", $"%{lastToken}%")) ||
                        // Fallback fallback global matching
                        EF.Functions.Like(x.c.CustomerFirstName ?? "", wildCardSearch) ||
                        EF.Functions.Like(x.c.CustomerLastName ?? "", wildCardSearch)
                    );
                }
                else
                {
                    // Regular baseline matching for single terms
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
                    return Ok(new { 
                        nodes = graphData.Nodes, 
                        edges = graphData.Edges, 
                        tableReportData = new List<object>(),
                        activeBranches = new List<object>(),
                        activeTransactionTypes = new List<object>()
                    });
                }

                var query = _context.CustomerTransactions.Where(t => t.IsActive == true).AsQueryable();

                if (startDate.HasValue) query = query.Where(t => t.TransactionDate >= startDate.Value);
                if (endDate.HasValue) query = query.Where(t => t.TransactionDate <= endDate.Value);
                if (minAmount.HasValue) query = query.Where(t => t.TransactionAmount >= minAmount.Value);
                if (maxAmount.HasValue) query = query.Where(t => t.TransactionAmount <= maxAmount.Value);

                if (!string.IsNullOrWhiteSpace(branchId))
                {
                    query = query.Where(t => t.BranchId.ToString() == branchId);
                }

                if (!string.IsNullOrWhiteSpace(transactionType))
                {
                    string targetFilter = transactionType.Trim();
                    
                    var matchedTypes = await _context.TransactionTypes
                        .Where(tt => tt.Code == targetFilter || tt.TransactionTypeId.ToString() == targetFilter)
                        .Select(tt => new { Code = tt.Code, IdString = tt.TransactionTypeId.ToString() })
                        .ToListAsync();

                    var validFilterKeys = matchedTypes.Select(m => m.IdString)
                        .Concat(matchedTypes.Select(m => m.Code?.Trim()))
                        .Where(val => !string.IsNullOrEmpty(val))
                        .Distinct()
                        .ToList();

                    if (!validFilterKeys.Contains(targetFilter))
                    {
                        validFilterKeys.Add(targetFilter);
                    }

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
                        .OrderByDescending(t => t.TransactionDate.GetValueOrDefault())
                        .GroupBy(t => t.CustomerAccountId)
                        .Select(grouping => grouping.First())
                        .ToList();
                }

                var activeBranchIdsInCanvas = rawTransactions.Where(t => t.BranchId != null).Select(t => t.BranchId).Distinct().ToList();
                var activeBranchesFilter = await _context.Branches
                    .Where(b => activeBranchIdsInCanvas.Contains(b.BranchId))
                    .Select(b => new { branchId = b.BranchId.ToString(), name = b.Name.Trim() })
                    .Distinct().ToListAsync();

                var branchDictionaryLookup = activeBranchesFilter.ToDictionary(b => b.branchId, b => b.name, StringComparer.OrdinalIgnoreCase);

                var activeTxnTypesInCanvas = rawTransactions.Where(t => !string.IsNullOrWhiteSpace(t.TransactionType)).Select(t => t.TransactionType.Trim()).Distinct().ToList();
                var allTransactionTypes = await _context.TransactionTypes.ToListAsync();

                var txnTypeDictionaryLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var tt in allTransactionTypes)
                {
                    string nameLabel = tt.Name?.Trim() ?? "Unknown";
                    string idKey = tt.TransactionTypeId.ToString();
                    if (!txnTypeDictionaryLookup.ContainsKey(idKey)) txnTypeDictionaryLookup[idKey] = nameLabel;
                    if (!string.IsNullOrWhiteSpace(tt.Code))
                    {
                        string codeKey = tt.Code.Trim();
                        if (!txnTypeDictionaryLookup.ContainsKey(codeKey)) txnTypeDictionaryLookup[codeKey] = nameLabel;
                    }
                }

                var activeTransactionTypesFilter = allTransactionTypes
                    .Where(tt => activeTxnTypesInCanvas.Contains(tt.TransactionTypeId.ToString()) || 
                                 (!string.IsNullOrEmpty(tt.Code) && activeTxnTypesInCanvas.Contains(tt.Code.Trim())))
                    .Select(tt => new { code = !string.IsNullOrEmpty(tt.Code) ? tt.Code.Trim() : tt.TransactionTypeId.ToString(), name = tt.Name.Trim() })
                    .Distinct().ToList();

                var currentAccountIds = rawTransactions.Where(t => t.CustomerAccountId.HasValue).Select(t => t.CustomerAccountId.Value).Distinct().ToList();
                var transactionRefNumbers = rawTransactions.Select(t => t.TransactionReferenceNumber).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();

                var accountIdentityList = await (from acc in _context.CustomerAccounts
                                                 join ja in _context.JointAccounts on acc.CustomerAccountId equals ja.CustomerAccountId into jaGroup
                                                 from ja in jaGroup.DefaultIfEmpty()
                                                 join cust in _context.Customers on ja.CustomerId equals cust.CustomerId into custGroup
                                                 from cust in custGroup.DefaultIfEmpty()
                                                 where currentAccountIds.Contains(acc.CustomerAccountId) || transactionRefNumbers.Contains(acc.AccountNumber)
                                                 select new
                                                 {
                                                     acc.CustomerAccountId,
                                                     acc.AccountNumber,
                                                     CustomerId = cust != null ? cust.CustomerId : 0,
                                                     FullName = cust != null ? (cust.CustomerFirstName + " " + (string.IsNullOrEmpty(cust.CustomerMiddleName) ? "" : cust.CustomerMiddleName + " ") + cust.CustomerLastName).Trim() : "Unknown Holder"
                                                 }).ToListAsync();

                var accountIdentityMap = accountIdentityList.Where(x => x.CustomerAccountId != 0).GroupBy(x => x.CustomerAccountId).ToDictionary(g => g.Key, g => g.First());

                var transactionIds = rawTransactions.Select(t => t.CustomerTransactionId).ToList();
                var holders = await _context.AccountHolders.Where(h => h.CustomerTransactionId.HasValue && transactionIds.Contains(h.CustomerTransactionId.Value)).ToListAsync();
                var beneficiaries = await _context.Beneficiaries.Where(b => b.CustomerTransactionId.HasValue && transactionIds.Contains(b.CustomerTransactionId.Value)).ToListAsync();

                var phCulture = new CultureInfo("en-PH");
                var renderedAccountLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var tableReportData = new List<object>();

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

                    var beneficiaryAccountIdentity = accountIdentityList.FirstOrDefault(a => string.Equals(a.AccountNumber, beneficiaryId, StringComparison.OrdinalIgnoreCase));
                    if (beneficiaryAccountIdentity != null)
                    {
                        beneficiaryName = beneficiaryAccountIdentity.FullName;
                    }
                    else
                    {
                        var bene = beneficiaries.FirstOrDefault(b => b.CustomerTransactionId == txn.CustomerTransactionId);
                        if (bene != null)
                        {
                            if (!string.IsNullOrEmpty(bene.BeneficiaryCorporateName) || !string.IsNullOrEmpty(bene.BeneficiaryFirstName))
                            {
                                beneficiaryName = !string.IsNullOrEmpty(bene.BeneficiaryCorporateName) ? bene.BeneficiaryCorporateName : $"{bene.BeneficiaryFirstName} {bene.BeneficiaryLastName}".Trim();
                            }
                        }
                    }

                    decimal displayAmount = txn.TransactionAmount ?? 0;
                    string txnNodeId = $"TXN_NODE_{txn.CustomerTransactionId}";

                    string targetRawBranchId = txn.BranchId?.ToString() ?? "";
                    string resolvedBranchName = "N/A";
                    if (!string.IsNullOrEmpty(targetRawBranchId) && branchDictionaryLookup.TryGetValue(targetRawBranchId, out var foundBranchName)) resolvedBranchName = foundBranchName;

                    string targetRawTxnTypeCode = txn.TransactionType?.Trim() ?? "";
                    string resolvedTxnTypeName = "Transfer"; 
                    if (!string.IsNullOrEmpty(targetRawTxnTypeCode) && txnTypeDictionaryLookup.TryGetValue(targetRawTxnTypeCode, out var foundTxnTypeName)) resolvedTxnTypeName = foundTxnTypeName;

                    tableReportData.Add(new
                    {
                        referenceNumber = refNo,
                        date = txn.TransactionDate?.ToString("yyyy-MM-dd") ?? "N/A",
                        type = resolvedTxnTypeName, 
                        senderName = senderName,
                        senderAccount = senderId,
                        beneficiaryName = beneficiaryName,
                        beneficiaryAccount = beneficiaryId,
                        branchId = resolvedBranchName, 
                        amount = displayAmount.ToString("C", phCulture)
                    });

                    string senderTableTooltip = $@"<div style='padding:6px;min-width:260px;'><table style='width:100%;border-collapse:collapse;font-family:sans-serif;font-size:11px;'><tr style='background-color:#f8fafc;border-bottom:2px solid #e2e8f0;'><th colspan='2' style='padding:6px;text-align:left;color:#4f46e5;'>ORIGINATING SENDER</th></tr><tr style='border-bottom:1px solid #f1f5f9;'><td style='padding:6px;color:#64748b;'>Account Number</td><td style='padding:6px;font-weight:bold;font-family:monospace;'>{senderId}</td></tr><tr><td style='padding:6px;color:#64748b;'>Customer Name</td><td style='padding:6px;font-weight:600;'>{senderName}</td></tr></table></div>";
                    string beneficiaryTableTooltip = $@"<div style='padding:6px;min-width:260px;'><table style='width:100%;border-collapse:collapse;font-family:sans-serif;font-size:11px;'><tr style='background-color:#f8fafc;border-bottom:2px solid #e2e8f0;'><th colspan='2' style='padding:6px;text-align:left;color:#10b981;'>BENEFICIARY TARGET</th></tr><tr style='border-bottom:1px solid #f1f5f9;'><td style='padding:6px;color:#64748b;'>Account Number</td><td style='padding:6px;font-weight:bold;font-family:monospace;'>{beneficiaryId}</td></tr><tr><td style='padding:6px;color:#64748b;'>Customer Name</td><td style='padding:6px;font-weight:600;'>{beneficiaryName}</td></tr></table></div>";
                    string transactionNodeTooltip = $@"<div style='padding:6px;min-width:260px;'><table style='width:100%;border-collapse:collapse;font-family:sans-serif;font-size:11px;'><tr style='background-color:#f8fafc;border-bottom:2px solid #e2e8f0;'><th colspan='2' style='padding:6px;text-align:left;color:#d97706;'>TRANSACTION LEDGER LOG</th></tr><tr style='border-bottom:1px solid #f1f5f9;'><td style='padding:6px;color:#64748b;'>Reference ID</td><td style='padding:6px;font-weight:bold;font-family:monospace;color:#b45309;'>{refNo}</td></tr><tr style='border-bottom:1px solid #f1f5f9;'><td style='padding:6px;color:#64748b;'>Execution Date</td><td style='padding:6px;'>{txn.TransactionDate?.ToString("yyyy-MM-dd") ?? "N/A"}</td></tr><tr style='border-bottom:1px solid #f1f5f9;'><td style='padding:6px;color:#64748b;'>Type</td><td style='padding:6px;font-weight:600;'>{resolvedTxnTypeName}</td></tr><tr><td style='padding:6px;color:#64748b;'>Amount Settled</td><td style='padding:6px;font-weight:bold;color:#16a34a;'>{displayAmount.ToString("C", phCulture)}</td></tr></table></div>";

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

                    bool isSelfTransfer = !string.Equals(senderId, "N/A") && string.Equals(senderId, beneficiaryId, StringComparison.OrdinalIgnoreCase);

                    if (isSelfTransfer)
                    {
                        string clusterHubNodeId = $"HUB_SELF_{senderId}";

                        if (!graphData.Nodes.Any(n => n.Id == clusterHubNodeId))
                        {
                            string hubTooltip = $@"<div style='padding:6px;min-width:200px;'><b style='color:#6366f1;'>Self-Transfer Hub</b><br/><span style='font-size:11px;color:#64748b;'>Account: {senderId}</span></div>";
                            
                            graphData.Nodes.Add(new TransactionGraphNode 
                            { 
                                Id = clusterHubNodeId, 
                                Label = "Self-Transfer Cluster", 
                                Group = "SelfTransferHub", 
                                Title = hubTooltip
                            });
                        }

                        graphData.Nodes.Add(new TransactionGraphNode
                        {
                            Id = txnNodeId,
                            Label = displayAmount.ToString("C", phCulture),
                            Group = "Transaction",
                            Title = transactionNodeTooltip,
                            TransactionType = resolvedTxnTypeName 
                        });

                        graphData.Edges.Add(new TransactionGraphEdge { Id = $"{txn.CustomerTransactionId}_Self_Src", From = senderId, To = txnNodeId, Label = "" });
                        graphData.Edges.Add(new TransactionGraphEdge { Id = $"{txn.CustomerTransactionId}_Self_Hub", From = txnNodeId, To = clusterHubNodeId, Label = "Internal Loop" });
                    }
                    else
                    {
                        graphData.Nodes.Add(new TransactionGraphNode
                        {
                            Id = txnNodeId,
                            Label = displayAmount.ToString("C", phCulture),
                            Group = "Transaction",
                            Title = transactionNodeTooltip,
                            TransactionType = resolvedTxnTypeName 
                        });

                        graphData.Edges.Add(new TransactionGraphEdge { Id = $"{txn.CustomerTransactionId}_Src", From = senderId, To = txnNodeId, Label = "" });
                        graphData.Edges.Add(new TransactionGraphEdge { Id = $"{txn.CustomerTransactionId}_Dst", From = txnNodeId, To = beneficiaryId, Label = "" });
                    }
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
                            string customerTooltip = $@"<div style='padding:6px;min-width:200px;'><table style='width:100%;border-collapse:collapse;font-family:sans-serif;font-size:11px;'><tr style='background-color:#f8fafc;border-bottom:2px solid #e2e8f0;'><th style='padding:6px;text-align:left;color:#3b82f6;'>JOINT ACCOUNT OWNER</th></tr><tr><td style='padding:6px;font-weight:600;'>{customerName}</td></tr></table></div>";

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
                    tableReportData = tableReportData,
                    activeBranches = activeBranchesFilter,               
                    activeTransactionTypes = activeTransactionTypesFilter 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Graph Mapping Runtime Error Exception", message = ex.Message, trace = ex.StackTrace });
            }
        }
    }
}