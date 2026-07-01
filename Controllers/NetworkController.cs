using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.Json;

using System.Text.Json.Serialization;

public class VisualizeRequest 
{ 
    [JsonPropertyName("SelectedIds")]
    public List<int> SelectedIds { get; set; } = new(); 
}

namespace RelationshipVisualizer.Controllers
{
    public class LookupRequest { public string Keyword { get; set; } = string.Empty; }
    public class VisualizeRequest { public List<int> SelectedIds { get; set; } = new(); }
    public class Node { public string Id { get; set; } = ""; public string Label { get; set; } = ""; public string Group { get; set; } = ""; public string Title { get; set; } = ""; public object CustomData { get; set; } = new(); }
    public class Edge { public string From { get; set; } = ""; public string To { get; set; } = ""; public string Label { get; set; } = ""; public string Title { get; set; } = ""; }
    public class GraphData { public List<Node> Nodes { get; set; } = new(); public List<Edge> Edges { get; set; } = new(); }
    
    public class CustomerLookupDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string CorporateName { get; set; } = string.Empty;
        public string RiskRating { get; set; } = string.Empty;
        public string WatchlistStatus { get; set; } = string.Empty;
        public bool IsPep { get; set; }
        public string MatchReason { get; set; } = string.Empty;
    }

    public class CustomerNodeDetails
    {
        public int Id { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CustomerRisk { get; set; } = string.Empty;
        public string WatchlistStatus { get; set; } = string.Empty;
        public bool IsPep { get; set; }
        public string FatherFirstName { get; set; } = string.Empty;
        public string FatherLastName { get; set; } = string.Empty;
        public string MotherFirstName { get; set; } = string.Empty;
        public string MotherLastName { get; set; } = string.Empty;
        public string Narrative { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class NetworkController : ControllerBase
    {
        private readonly string _connectionString;
        public NetworkController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration));
        }

        // ENDPOINT 1: OFAC-Style Multi-Layered Fuzzy Match Indexer
        [HttpPost("lookup-profiles")]
        public async Task<IActionResult> LookupProfiles([FromBody] LookupRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Keyword))
            {
                return BadRequest(new { message = "Search term cannot be empty." });
            }
            var cleanKeyword = request.Keyword.Trim();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            var query = @"
                SELECT TOP 50
                c.CustomerId AS Id,
                ISNULL(c.CustomerFirstName, '') AS FirstName,
                ISNULL(c.CustomerMiddleName, '') AS MiddleName,
                ISNULL(c.CustomerLastName, '') AS LastName,
                ISNULL(c.CorporateName, '') AS CorporateName,
                c.CustomerRisk AS RiskRating,
                c.WATCHLIST_STATUS AS WatchlistStatus,
                c.IsPep AS IsPep,
                CASE
                WHEN c.CorporateName LIKE '%' + @Keyword + '%' THEN 'Corporate Name Match'
                WHEN (c.CustomerFirstName + ' ' + c.CustomerLastName) LIKE '%' + @Keyword + '%' THEN 'Primary Name Alignment'
                WHEN (c.FatherFirstName + ' ' + c.FatherLastName) LIKE '%' + @Keyword + '%' THEN 'Father Name Vector Match (' + ISNULL(c.FatherFirstName,'') + ' ' + ISNULL(c.FatherLastName,'') + ')'
                WHEN (c.MotherFirstName + ' ' + c.MotherLastName) LIKE '%' + @Keyword + '%' THEN 'Mother Name Vector Match (' + ISNULL(c.MotherFirstName,'') + ' ' + ISNULL(c.MotherLastName,'') + ')'
                ELSE 'Fuzzy Substring Match'
                END AS MatchReason
                FROM Customers c
                WHERE c.IsActive = 1 AND (
                c.CustomerFirstName LIKE '%' + @Keyword + '%'
                OR c.CustomerLastName LIKE '%' + @Keyword + '%'
                OR c.CorporateName LIKE '%' + @Keyword + '%'
                OR c.FatherFirstName LIKE '%' + @Keyword + '%'
                OR c.FatherLastName LIKE '%' + @Keyword + '%'
                OR c.MotherFirstName LIKE '%' + @Keyword + '%'
                OR c.MotherLastName LIKE '%' + @Keyword + '%'
                OR SOUNDEX(c.CustomerLastName) = SOUNDEX(@Keyword)
                )
                ORDER BY c.CustomerRisk DESC, c.CustomerLastName ASC";
            
            var results = await connection.QueryAsync<CustomerLookupDto>(query, new { Keyword = cleanKeyword });
            if (!results.Any())
            {
                return NotFound(new { message = "No system profile records found matching that context criteria." });
            }
            return Ok(results);
        }

        // ENDPOINT 2: Graph Mapping Engine via Shared Lines & Shared Joint Accounts
        [HttpPost("visualize-selected")]
        public async Task<IActionResult> VisualizeSelected([FromBody] VisualizeRequest request)
        {
            if (request?.SelectedIds == null || !request.SelectedIds.Any())
            {
                return BadRequest(new { message = "No system profile records target nodes allocated." });
            }
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            try
            {
                // 1. Fetch complete metadata records for all identities requested in the workspace payload pool
                var profiles = (await connection.QueryAsync<CustomerNodeDetails>(@"
                    SELECT
                    CustomerId AS Id,
                    CustomerNumber,
                    UPPER(TRIM(ISNULL(CustomerLastName, ''))) AS LastName,
                    CASE
                    WHEN TRIM(ISNULL(CorporateName, '')) <> '' THEN CorporateName
                    ELSE CONCAT(CustomerLastName, ', ', CustomerFirstName, ' ', CustomerMiddleName)
                    END AS FullName,
                    ISNULL(CustomerRisk, 'LOW') AS CustomerRisk,
                    ISNULL(WATCHLIST_STATUS, 'CLEARED') AS WatchlistStatus,
                    ISNULL(IsPep, 0) AS IsPep,
                    ISNULL(Narrative, '') AS Narrative,
                    UPPER(TRIM(ISNULL(FatherFirstName, ''))) AS FatherFirstName,
                    UPPER(TRIM(ISNULL(FatherLastName, ''))) AS FatherLastName,
                    UPPER(TRIM(ISNULL(MotherFirstName, ''))) AS MotherFirstName,
                    UPPER(TRIM(ISNULL(MotherLastName, ''))) AS MotherLastName
                    FROM Customers
                    WHERE CustomerId IN @Ids AND IsActive = 1", new { Ids = request.SelectedIds })).ToList();

                // 2. Fetch all matching contextual relationship mappings from Joint Account configurations
                var jointAccounts = (await connection.QueryAsync<dynamic>(@"
                    SELECT CustomerId, CustomerAccountId
                    FROM JointAccounts
                    WHERE CustomerId IN @Ids AND IsActive = 1", new { Ids = request.SelectedIds })).ToList();
                
                var graphData = new GraphData();
                var uniqueNodes = new HashSet<string>();

                // Build Individual Customer Nodes
                foreach (var p in profiles)
                {
                    string pid = p.Id.ToString();
                    if (uniqueNodes.Add(pid))
                    {
                        string risk = p.CustomerRisk.ToUpper();
                        bool isThreat = p.WatchlistStatus.ToUpper() == "MATCHED" || p.IsPep;
                        string nodeGroup = isThreat ? "SanctionRisk" : (risk == "HIGH" ? "HighRisk" : "Standard");
                        string tooltipHtml = $@"
                            <div>
                            <strong style='color:#06b6d4; font-size:0.85rem;'>{p.FullName}</strong><br/>
                            <span style='color:#94a3b8;'>ID:</span> {pid} | <span style='color:#94a3b8;'>Ref:</span> {p.CustomerNumber}<br/>
                            <span style='color:#94a3b8;'>Risk:</span> {risk}<br/>
                            <span style='color:#94a3b8;'>Watchlist:</span> {p.WatchlistStatus}<br/>
                            <span style='color:#94a3b8;'>PEP Status:</span> {(p.IsPep ? "ALERT MATCH" : "No")}
                            </div>";
                        
                        graphData.Nodes.Add(new Node {
                            Id = pid,
                            Label = p.FullName,
                            Group = nodeGroup,
                            Title = tooltipHtml,
                            CustomData = p
                        });
                    }
                }

                // 3. Close-Proximity Lineage & Finance Vector Loop (Excludes basic shared surnames)
                for (int i = 0; i < profiles.Count; i++)
                {
                    for (int j = i + 1; j < profiles.Count; j++)
                    {
                        var targetA = profiles[i];
                        var targetB = profiles[j];

                        // Check Lineage Vector 1: Shared Father
                        bool sharedFather = !string.IsNullOrEmpty(targetA.FatherLastName) &&
                            targetA.FatherLastName == targetB.FatherLastName &&
                            targetA.FatherFirstName == targetB.FatherFirstName;

                        // Check Lineage Vector 2: Shared Mother
                        bool sharedMother = !string.IsNullOrEmpty(targetA.MotherLastName) &&
                            targetA.MotherLastName == targetB.MotherLastName &&
                            targetA.MotherFirstName == targetB.MotherFirstName;

                        // Check Vector 3: Shared Banking Entities (Joint Account Links)
                        var aAccounts = jointAccounts.Where(ja => ja.CustomerId == targetA.Id).Select(ja => ja.CustomerAccountId).ToList();
                        var bAccounts = jointAccounts.Where(ja => ja.CustomerId == targetB.Id).Select(ja => ja.CustomerAccountId).ToList();
                        var sharedAccts = aAccounts.Intersect(bAccounts).ToList();

                        if (sharedFather && sharedMother)
                        {
                            graphData.Edges.Add(new Edge {
                                From = targetA.Id.ToString(), To = targetB.Id.ToString(),
                                Label = "Sibling Link", Title = "Confirmed Maternal & Paternal Alignment Paths"
                            });
                        }
                        else if (sharedFather || sharedMother)
                        {
                            graphData.Edges.Add(new Edge {
                                From = targetA.Id.ToString(), To = targetB.Id.ToString(),
                                Label = "Immediate Family", Title = $"Shared Parent: {(sharedFather ? "Father Vector" : "Mother Vector")}"
                            });
                        }
                        else if (sharedAccts.Any())
                        {
                            graphData.Edges.Add(new Edge {
                                From = targetA.Id.ToString(), To = targetB.Id.ToString(),
                                Label = "Joint Account Link", Title = $"Shared Joint Account Reference ID: {sharedAccts.First()}"
                            });
                        }
                    }
                }

                // 4. Dynamic Relational Hub Node Construction for Shared Surnames
                var surnameGroups = profiles
                    .Where(p => !string.IsNullOrWhiteSpace(p.LastName))
                    .GroupBy(p => p.LastName)
                    .Where(g => g.Count() > 1); // Only convert to hubs if 2 or more profiles share it

                foreach (var group in surnameGroups)
                {
                    string surname = group.Key;
                    string hubNodeId = $"HUB_SURNAME_{surname}";

                    // Inject the central Relational Hub Node explicitly into the node array
                    if (uniqueNodes.Add(hubNodeId))
                    {
                        string hubTooltipHtml = $@"
                            <div style='padding: 4px;'>
                            <strong style='color:#8b5cf6; font-size:0.85rem;'>Surname Cluster Hub: {surname}</strong><br/>
                            <span style='color:#94a3b8;'>Total Intersecting Nodes:</span> {group.Count()}
                            </div>";
                        
                        graphData.Nodes.Add(new Node
                        {
                            Id = hubNodeId,
                            Label = $"{surname} (Hub)",
                            Group = "RelationalHub",
                            Title = hubTooltipHtml,
                            CustomData = new { Type = "SurnameHub", Surname = surname }
                        });
                    }

                    // Tie all members matching this group to the central hub point
                    foreach (var profile in group)
                    {
                        graphData.Edges.Add(new Edge
                        {
                            From = profile.Id.ToString(),
                            To = hubNodeId,
                            Label = "Shared Surname",
                            Title = $"Lineage match verified on surname index: {surname}"
                        });
                    }
                }

                return Ok(graphData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Graph Vector Failure: {ex.Message}");
                return StatusCode(500, new { message = $"Graph vector generation failure: {ex.Message}" });
            }
        }
    }
}