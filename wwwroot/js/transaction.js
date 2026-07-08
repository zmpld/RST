// ============================================================================
// PART 1: CORE APPLICATION STATE & VIS.JS IMPLEMENTATION LAYER
// ============================================================================
let networkInstance = null;
let physicsEnabled = true;
let filtersExpanded = true;
let tablePanelExpanded = false;
let nodeTooltipMap = {};
let currentRawTableData = [];        // Cached array for structural data binding lookups
let globalCachedEdges = [];          // Master backup list of all loaded graph edges
let toastTimer = null;
let currentActiveAccountsCache = []; // Global tracker variable for account caching

const GROUP_COLORS = {
    Sender: '#4f46e5', 
    SelfTransferHub: '#d946ef', 
    Beneficiary: '#10b981', 
    Transaction: '#f59e0b', 
    Customer: '#0ea5e9', 
    UnknownBeneficiary: '#0f172a'
};

// ==========================================
// INTERACTIVE WINDOW PANELS & OVERLAYS
// ==========================================
function toggleFiltersPanel() {
    filtersExpanded = !filtersExpanded;
    const body = document.getElementById('filtersBody');
    const chevron = document.getElementById('filtersChevron');
    if (body) body.style.display = filtersExpanded ? '' : 'none';
    if (chevron) chevron.style.transform = filtersExpanded ? '' : 'rotate(-90deg)';
}

function toggleReportTablePanel() {
    tablePanelExpanded = !tablePanelExpanded;
    const panel = document.getElementById('tableReportPanel');
    const btn = document.getElementById('btnToggleTable');
    if (!panel || !btn) return;

    if (tablePanelExpanded) {
        panel.classList.add('active');
        btn.textContent = 'Hide Data Table';
    } else {
        panel.classList.remove('active');
        btn.textContent = 'Show Data Table';
    }
}

function getSelectedAccountsArray() {
    return Array.isArray(currentActiveAccountsCache) ? currentActiveAccountsCache : [];
}

// ==========================================
// EXPORT PIPELINE MANAGEMENT
// ==========================================
function exportReport(format) {
    const activeAccounts = getSelectedAccountsArray(); 
    const startDate = document.getElementById('startDate')?.value || '';
    const endDate = document.getElementById('endDate')?.value || '';
    const minAmount = document.getElementById('minAmount')?.value || '';
    const maxAmount = document.getElementById('maxAmount')?.value || '';
    const branchId = document.getElementById('branchFilter')?.value || '';
    const transactionType = document.getElementById('typeFilter')?.value || '';
    const latestOnly = document.getElementById('latestOnlyToggle')?.checked || false;

    if (!activeAccounts || activeAccounts.length === 0) {
        alert("No active accounts selected to export.");
        return;
    }

    const endpoint = format === 'pdf' ? '/Transaction/DownloadPdf' : '/Transaction/DownloadCsv';
    const params = new URLSearchParams();
    
    activeAccounts.forEach(acc => params.append('accounts', acc));
    
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    if (minAmount) params.append('minAmount', minAmount);
    if (maxAmount) params.append('maxAmount', maxAmount);
    if (branchId) params.append('branchId', branchId);
    if (transactionType) params.append('transactionType', transactionType);
    if (latestOnly) params.append('latestOnly', latestOnly);

    window.location.href = `${endpoint}?${params.toString()}`;
}

// ==========================================
// GRAPH THEME ENGINE (HIGH-CONTRAST / LIGHT)
// ==========================================
function toggleCanvasTheme(toggleInput) {
    const frame = document.getElementById('canvasViewportFrame');
    const iconContainer = document.getElementById('themeToggleIcon');
    const titleText = document.getElementById('tablePanelMainTitle');
    const isLightMode = toggleInput.checked;
    
    if (isLightMode) {
        frame?.classList.add('light-mode');
        if (titleText) titleText.style.color = '#475569';
        if (iconContainer) {
            iconContainer.innerHTML = `
                <circle cx="12" cy="12" r="5"></circle>
                <line x1="12" y1="1" x2="12" y2="3.5"></line>
                <line x1="12" y1="20.5" x2="12" y2="23"></line>
                <line x1="4.22" y1="4.22" x2="6" y2="6"></line>
                <line x1="18" y1="18" x2="19.78" y2="19.78"></line>
                <line x1="1" y1="12" x2="3.5" y2="12"></line>
                <line x1="20.5" y1="12" x2="23" y2="12"></line>
                <line x1="4.22" y1="19.78" x2="6" y2="18"></line>
                <line x1="18" y1="6" x2="19.78" y2="4.22"></line>
            `;
        }
    } else {
        frame?.classList.remove('light-mode');
        if (titleText) titleText.style.color = '#94a3b8';
        if (iconContainer) {
            iconContainer.innerHTML = `<path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>`;
        }
    }

    if (networkInstance) {
        const labelColor = isLightMode ? '#1e293b' : '#94a3b8';
        const edgeBgColor = isLightMode ? '#f1f5f9' : '#080b11';
        const edgeLineColor = isLightMode ? '#94a3b8' : '#26334d';

        networkInstance.setOptions({
            nodes: { font: { color: labelColor } },
            edges: {
                color: { color: edgeLineColor },
                font: { background: edgeBgColor }
            }
        });
    }
}

// ==========================================
// FORM FILTERS UI UPDATES & RESET POPULATION
// ==========================================
function onFilterChange() {
    const values = [
        document.getElementById('startDate')?.value,
        document.getElementById('endDate')?.value,
        document.getElementById('minAmount')?.value,
        document.getElementById('maxAmount')?.value,
        document.getElementById('branchFilter')?.value,
        document.getElementById('typeFilter')?.value,
        document.getElementById('latestOnlyToggle')?.checked
    ];
    const activeCount = values.filter(Boolean).length;
    const badge = document.getElementById('activeFilterBadge');
    if (badge) {
        badge.textContent = `${activeCount} active`;
        badge.classList.toggle('hidden', activeCount === 0);
    }
}

// Helper to keep manual toggles synced across re-renders
function getUnlinkedRefsRegistry() {
    if (typeof window.globalUnlinkedRefs === 'undefined') {
        window.globalUnlinkedRefs = new Set();
    }
    return window.globalUnlinkedRefs;
}

function clearAllFilters() {
    ['startDate','endDate','minAmount','maxAmount','branchFilter','typeFilter'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
    });
    const latestToggle = document.getElementById('latestOnlyToggle');
    if (latestToggle) latestToggle.checked = false;
    onFilterChange();
}

function populateBranchFilterOptions(branches) {
    const select = document.getElementById('branchFilter');
    if (!select) return;
    const previousValue = select.value;
    const list = Array.isArray(branches) ? branches : [];

    select.innerHTML = '<option value="">All Branches</option>';

    const seenNames = new Set();
    const uniqueSorted = list
        .filter(b => {
            const name = b.name ?? b.Name;
            if (!name || seenNames.has(name)) return false;
            seenNames.add(name);
            return true;
        })
        .sort((a, b) => (a.name ?? a.Name).localeCompare(b.name ?? b.Name));

    uniqueSorted.forEach(b => {
        const id = b.branchId ?? b.BranchId;
        const name = b.name ?? b.Name;
        const opt = document.createElement('option');
        opt.value = id;
        opt.textContent = name;
        select.appendChild(opt);
    });

    select.value = Array.from(select.options).some(o => o.value === previousValue) ? previousValue : '';
}

function populateTransactionTypeFilterOptions(types) {
    const select = document.getElementById('typeFilter');
    if (!select) return;
    const previousValue = select.value;
    const list = Array.isArray(types) ? types : [];

    select.innerHTML = '<option value="">All Types</option>';

    const sorted = list
        .slice()
        .sort((a, b) => (a.name ?? a.Name).localeCompare(b.name ?? b.Name));

    sorted.forEach(t => {
        const code = t.code ?? t.Code;
        const name = t.name ?? t.Name;
        const opt = document.createElement('option');
        opt.value = code;
        opt.textContent = name;
        select.appendChild(opt);
    });

    select.value = Array.from(select.options).some(o => o.value === previousValue) ? previousValue : '';
}

// ==========================================
// ASYNCHRONOUS CUSTOMER PROFILE LOOKUP ENGINE
// ==========================================
async function executeAccountLookup() {
    const query = document.getElementById('searchQuery')?.value.trim();
    const container = document.getElementById('searchResultsContainer');
    const selectAll = document.getElementById('selectAllCheckbox');
    const badge = document.getElementById('resultCountBadge');

    if (!container) return;
    if (!query) { renderEmptyState(container, 'Please enter a customer name to search.'); return; }

    container.innerHTML = '<div class="empty-state">Searching accounts…</div>';

    try {
        const response = await fetch(`/Transaction/SearchAccounts?query=${encodeURIComponent(query)}`);
        const foundAccountsList = await response.json();

        if (!foundAccountsList || foundAccountsList.length === 0) {
            if (selectAll) { selectAll.checked = false; selectAll.disabled = true; }
            if (badge) badge.classList.add('hidden');
            renderEmptyState(container, 'No accounts matched that search term.');
            updateVisualizeButtonState();
            return;
        }

        if (selectAll) { selectAll.disabled = false; selectAll.checked = false; }
        if (badge) {
            badge.textContent = foundAccountsList.length;
            badge.classList.remove('hidden');
        }

        let html = '<div class="results-inner">';
        foundAccountsList.forEach(acc => {
            const num = acc.accountNumber || acc.AccountNumber;
            const name = acc.accountName || acc.AccountName;
            const role = acc.role || acc.Role || 'Account';
            html += `<label class="account-card">
                <input type="checkbox" value="${num}" onchange="updateVisualizeButtonState()" class="account-item-checkbox">
                <div class="meta-stack">
                  <span class="entity-title">${name}</span>
                  <div class="title-row" style="margin-top:4px;">
                    <span class="mono-text">${num}</span>
                    <span class="pill role">${role}</span>
                  </div>
                </div>
              </label>`;
        });
        html += '</div>';
        container.innerHTML = html;
        updateVisualizeButtonState();
    } catch (err) {
        container.innerHTML = '<div class="empty-state error">Search failed — check the console.</div>';
        console.error(err);
    }
}

function renderEmptyState(container, msg) {
    if (container) container.innerHTML = `<div class="empty-state">${msg}</div>`;
}

// ==========================================
// LOCAL STATE MANAGERS
// ==========================================
function toggleSelectAllMatches(cb) {
    document.querySelectorAll('.account-item-checkbox').forEach(b => b.checked = cb.checked);
    updateVisualizeButtonState();
}

function clearSelections() {
    document.querySelectorAll('.account-item-checkbox').forEach(b => b.checked = false);
    const sa = document.getElementById('selectAllCheckbox');
    if (sa) { sa.checked = false; sa.indeterminate = false; }
    updateVisualizeButtonState();
}

function updateVisualizeButtonState() {
    const checked = document.querySelectorAll('.account-item-checkbox:checked');
    const total = document.querySelectorAll('.account-item-checkbox');
    const btn = document.getElementById('btnVisualize');
    const sa = document.getElementById('selectAllCheckbox');
    const selectedCountEl = document.getElementById('selectedCount');
    const clearSelBtn = document.getElementById('btnClearSel');

    currentActiveAccountsCache = Array.from(checked).map(input => input.value);

    if (selectedCountEl) selectedCountEl.textContent = checked.length;
    if (clearSelBtn) clearSelBtn.classList.toggle('hidden', checked.length === 0);

    if (total.length > 0 && sa) {
        sa.indeterminate = checked.length > 0 && checked.length < total.length;
        if (!sa.indeterminate) sa.checked = checked.length === total.length && total.length > 0;
    }

    const active = checked.length > 0;
    if (btn) {
        btn.disabled = !active;
        btn.className = active ? 'btn-full' : 'btn-secondary btn-full';
    }
}

function handleToggleShortcutRefresh() {
    if (document.querySelectorAll('.account-item-checkbox:checked').length > 0) {
        loadTransactionNetworkGraph();
    }
}

// ============================================================================
// PIPELINE DATA LAYER: GRAPH LOADING AND INSTANTIATION
// ============================================================================
async function loadTransactionNetworkGraph() {
    const checkedBoxes = document.querySelectorAll('.account-item-checkbox:checked');
    if (!checkedBoxes.length) return;

    const typeFilterVal = document.getElementById('typeFilter')?.value || '';

    const params = new URLSearchParams({
        startDate: document.getElementById('startDate')?.value || '',
        endDate: document.getElementById('endDate')?.value || '',
        minAmount: document.getElementById('minAmount')?.value || '',
        maxAmount: document.getElementById('maxAmount')?.value || '',
        branchId: document.getElementById('branchFilter')?.value || '',
        transactionType: typeFilterVal,
        latestOnly: document.getElementById('latestOnlyToggle')?.checked || false
    });
    checkedBoxes.forEach(b => params.append('accounts', b.value));

    const overlay = document.getElementById('canvasLoadingOverlay');
    if (overlay) overlay.style.display = 'flex';
    
    if (typeof resetTableFilters === 'function') resetTableFilters();
    if (typeof hideAccordionInspector === 'function') hideAccordionInspector();

    try {
        const response = await fetch(`/Transaction/GetGraphData?${params}`);
        const rawData = await response.json();

        if (!response.ok) {
            showToast(rawData.message || 'Graph generation error');
            return;
        }

        currentRawTableData = rawData.reportRows || rawData.ReportRows || rawData.tableReportData || rawData.TableReportData || [];
        if (typeof populateReportTable === 'function') populateReportTable(currentRawTableData);

        const activeBranches = rawData.activeBranches || rawData.ActiveBranches || [];
        const activeTypes = rawData.activeTransactionTypes || rawData.ActiveTransactionTypes || [];
        populateBranchFilterOptions(activeBranches);
        populateTransactionTypeFilterOptions(activeTypes);

        const canvasEmptyState = document.getElementById('canvasEmptyState');
        if (!rawData.nodes?.length) {
            if (canvasEmptyState) canvasEmptyState.style.display = 'flex';
            showToast('No graph data for the selected filters.');
            return;
        }

        if (canvasEmptyState) canvasEmptyState.style.display = 'none';
        nodeTooltipMap = {};
        
        const visNodes = []; 
        const seenNodes = new Set(); 
        const unlinkedRefs = getUnlinkedRefsRegistry();
        
        let txnCount = 0;
        let nodesToRender = rawData.nodes || [];
        let edgesToRender = rawData.edges || [];

        nodesToRender.forEach(node => {
            const id = node.id ?? node.Id;
            const label = node.label ?? node.Label ?? '';
            let group = node.group ?? node.Group;
            const title = node.title ?? node.Title ?? '';
            
            if (seenNodes.has(id)) return;
            
            // DYNAMIC PURGE: Completely skips and drops transaction nodes if manually unlinked by the user
            if (group === 'Transaction') {
                const nodeRefToken = String(node.referenceNumber || id).trim().toLowerCase();
                if (unlinkedRefs.has(nodeRefToken)) return;
            }

            seenNodes.add(id);
            
            if (group === 'Beneficiary' && label.toLowerCase().includes('unknown')) {
                group = 'UnknownBeneficiary';
            }
            if (group === 'Transaction') txnCount++;
            
            nodeTooltipMap[id] = { html: title, group, label };
            let titleEl = title;
            if (title && title.includes('<div')) { 
                const w = document.createElement('div'); 
                w.innerHTML = title; 
                titleEl = w; 
            }
            visNodes.push({ id, label, title: titleEl, group, shape: node.shape ?? node.Shape ?? 'dot' });
        });

        globalCachedEdges = [];
        const finalVisEdges = [];

        edgesToRender.forEach((edge, i) => {
            let ef = String(edge.from ?? edge.From);
            let et = String(edge.to ?? edge.To);
            const edgeId = edge.id ?? edge.Id;
            const finalId = `edge_instance_${edgeId}_${i}`;
            const edgeRefToken = String(edgeId).trim().toLowerCase();

            const edgeBlueprint = {
                id: finalId, 
                from: ef, 
                to: et,
                label: edge.label ?? edge.Label ?? '',
                associatedRef: String(edgeId)
            };

            globalCachedEdges.push(edgeBlueprint);

            // COMPILING CONNECTION ROUTING: Drops edges connecting to unlinked elements to ensure broken connections
            if (!unlinkedRefs.has(edgeRefToken)) {
                if (seenNodes.has(ef) && seenNodes.has(et)) {
                    finalVisEdges.push(edgeBlueprint);
                }
            }
        });

        const isLightMode = document.getElementById('canvasThemeToggle')?.checked || false;
        const initialLabelColor = isLightMode ? '#1e293b' : '#94a3b8';
        const initialEdgeBgColor = isLightMode ? '#f1f5f9' : '#080b11';
        const initialEdgeLineColor = isLightMode ? '#94a3b8' : '#26334d';

        const options = {
            nodes: { borderWidth: 2, font: { size: 11, color: initialLabelColor, face: 'Segoe UI', vadjust: 26 } },
            edges: {
                arrows: { to: { enabled: true, scaleFactor: 0.75 } },
                color: { color: initialEdgeLineColor, highlight: '#06b6d4', hover: '#3b82f6' },
                smooth: { enabled: true, type: 'cubicBezier', roundness: 0.4 },
                width: 1.5,
                font: { align: 'horizontal', size: 10, color: '#64748b', background: initialEdgeBgColor }
            },
            groups: {
                Sender: { shape: 'dot', size: 20, color: { background: '#4f46e5', border: '#4338ca', highlight: { background: '#818cf8', border: '#4338ca' } } },
                SelfTransferHub: { shape: 'dot', size: 24, color: { background: '#d946ef', border: '#a21caf', highlight: { background: '#f5d0fe', border: '#a21caf' } } },
                Beneficiary: { shape: 'dot', size: 20, color: { background: '#10b981', border: '#047857', highlight: { background: '#34d399', border: '#047857' } } },
                UnknownBeneficiary: { shape: 'dot', size: 20, color: { background: '#0f172a', border: '#020617', highlight: { background: '#334155', border: '#020617' } } },
                Transaction: { shape: 'diamond', size: 14, color: { background: '#f59e0b', border: '#b45309', highlight: { background: '#fbbf24', border: '#b45309' } }, font: { vadjust: 22, size: 10, color: '#f59e0b', face: 'monospace' } },
                Customer: { shape: 'dot', size: 20, color: { background: '#0ea5e9', border: '#0369a1', highlight: { background: '#38bdf8', border: '#0369a1' } } }
            },
            physics: { enabled: true, barnesHut: { gravitationalConstant: -2500, centralGravity: 0.2, springLength: 140, springConstant: 0.04 }, stabilization: { iterations: 200, updateInterval: 25 } },
            interaction: { hover: true, tooltipDelay: 150, navigationButtons: false }
        };

        if (networkInstance) networkInstance.destroy();
        
        networkInstance = new vis.Network(
            document.getElementById('visualNetworkCanvas'),
            { nodes: new vis.DataSet(visNodes), edges: new vis.DataSet(finalVisEdges) },
            options
        );
        physicsEnabled = true;
        if (typeof syncPhysicsButton === 'function') syncPhysicsButton();

        networkInstance.on('stabilizationIterationsDone', () => {
            networkInstance.fit({ animation: { duration: 600, easingFunction: 'easeInOutQuad' } });
            if (typeof setGraphStats === 'function') setGraphStats(visNodes.length, finalVisEdges.length, txnCount);
        });
        
        networkInstance.on('click', params => {
            if (params.nodes.length > 0) {
                const selectedNodeId = params.nodes[0];
                if (typeof filterReportTableByNode === 'function') filterReportTableByNode(selectedNodeId);
                generateAccordionInspector(selectedNodeId);
            } else {
                if (typeof resetTableFilters === 'function') resetTableFilters();
                if (typeof hideAccordionInspector === 'function') hideAccordionInspector();
            }
        });
        
        networkInstance.on('doubleClick', params => {
            if (params.nodes.length > 0) {
                networkInstance.focus(params.nodes[0], { scale: 2, animation: { duration: 400 } });
            }
        });

        if (typeof setToolbarVisible === 'function') setToolbarVisible(true);
        showToast(`Loaded ${visNodes.length} nodes · ${txnCount} transactions`);
    } catch (err) {
        console.error(err);
        showToast('Failed to render graph.');
    } finally {
        if (overlay) overlay.style.display = 'none';
    }
}

// ============================================================================
// ACCORDION SYNC, CONNECTION HOOKS & DYNAMIC INSPECTOR GENERATION
// ============================================================================
function handleAccordionEdgeToggle(checkbox, transactionRef) {
    if (!networkInstance) return;
    
    const refToken = String(transactionRef).trim().toLowerCase();
    const unlinkedRefs = getUnlinkedRefsRegistry();

    // STEP 1: Commit visibility modification variables directly to master data cache
    if (!checkbox.checked) {
        unlinkedRefs.add(refToken);
    } else {
        unlinkedRefs.delete(refToken);
    }

    // STEP 2: Re-trigger clean canvas generation loop to break rendering bridges
    loadTransactionNetworkGraph();
}
    
function generateAccordionInspector(nodeId) {
    const nodeMeta = nodeTooltipMap[nodeId];
    const container = document.getElementById('transactionAccordionContainer');
    const section = document.getElementById('accordionInspectionSection');
    const title = document.getElementById('accordionSectionTitle');
    
    if (!nodeMeta) { 
        if (typeof hideAccordionInspector === 'function') hideAccordionInspector(); 
        return; 
    }
    
    if (container) container.innerHTML = '';
    if (section) section.style.display = 'flex'; 
    
    let searchToken = String(nodeId).trim().toLowerCase();
    if (nodeMeta.group === 'SelfTransferHub' && searchToken.startsWith('hub_self_')) {
        searchToken = searchToken.replace('hub_self_', '');
    }
    
    const matchedRecords = (currentRawTableData || []).filter(row => {
        const ref = String(row.referenceNumber || row.id || '').toLowerCase();
        const sender = String(row.senderAccount || '').toLowerCase();
        const beneficiary = String(row.beneficiaryAccount || '').toLowerCase();
        
        if (nodeMeta.group === 'Transaction') {
            return (ref === searchToken || searchToken.includes(ref) || ref.includes(searchToken));
        } else if (nodeMeta.group === 'SelfTransferHub') {
            return (sender === searchToken && beneficiary === searchToken);
        } else {
            return (sender === searchToken || beneficiary === searchToken);
        }
    });
    
    if (title) title.textContent = `${nodeMeta.group}: ${nodeMeta.label || nodeId}`;
    
    if (matchedRecords.length === 0 && container) {
        container.innerHTML = '<div style="color:#64748b; font-size:11px; padding:0.5rem; text-align:center;">No matching transaction logs.</div>';
        return;
    }

    // ============================================================================
    // PART 2: ACCORDION ITERATION, TABULAR RENDERING, & UI RESIZE ENGINES
    // ============================================================================
    const unlinkedRefs = getUnlinkedRefsRegistry();

    matchedRecords.forEach((record, index) => {
        const refNum = record.referenceNumber || 'N/A';
        const date = record.date || 'N/A';
        const type = record.type || 'N/A';
        const amount = record.amount || '0.00';
        const sName = record.senderName || 'N/A';
        const sAcc = record.senderAccount || 'N/A';
        const bName = record.beneficiaryName || 'N/A';
        const bAcc = record.beneficiaryAccount || 'N/A';
        const branch = record.branchId || 'N/A';
        
        // Calculate checkbox checked/unchecked status strictly based on tracking registry
        const isCurrentlyLinked = !unlinkedRefs.has(String(refNum).trim().toLowerCase());

        const card = document.createElement('div');
        card.className = `txn-accordion-card ${index === 0 ? 'expanded' : ''}`;
        
        card.innerHTML = `
            <div class="txn-accordion-trigger" style="display: flex; justify-content: space-between; align-items: center;">
              <div class="accordion-header-left">
                <input type="checkbox" class="accordion-link-checkbox" 
                       ${isCurrentlyLinked ? 'checked' : ''} 
                       onclick="event.stopPropagation(); handleAccordionEdgeToggle(this, '${escapeHtml(refNum)}')">
                <span style="font-weight:600; font-family:monospace; color:#fbbf24; cursor:pointer;" 
                      onclick="this.closest('.txn-accordion-card').classList.toggle('expanded')">
                  ${escapeHtml(refNum)}
                </span>
              </div>
              <div style="display:flex; align-items:center; gap:0.5rem; cursor:pointer;" 
                   onclick="this.closest('.txn-accordion-card').classList.toggle('expanded')">
                <span style="color:#10b981; font-weight:600;">${escapeHtml(amount)}</span>
                <svg class="txn-accordion-chevron" fill="none" stroke="currentColor" viewBox="0 0 24 24" style="width:16px; height:16px;">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 9l-7 7-7-7"/>
                </svg>
              </div>
            </div>
            <div class="txn-accordion-content">
              <div class="txn-details-grid" style="padding: 0.5rem 0; font-size: 11px;">
                <span class="txn-details-label">Date:</span><span class="txn-details-value">${escapeHtml(date)}</span>
                <span class="txn-details-label">Type:</span><span class="txn-details-value">${escapeHtml(type)}</span>
                <span class="txn-details-label">Sender:</span><span class="txn-details-value" title="${escapeHtml(sName)}">${escapeHtml(sName)}</span>
                <span class="txn-details-label">Snd Acc:</span><span class="txn-details-value">${escapeHtml(sAcc)}</span>
                <span class="txn-details-label">Target:</span><span class="txn-details-value" title="${escapeHtml(bName)}">${escapeHtml(bName)}</span>
                <span class="txn-details-label">Bnf Acc:</span><span class="txn-details-value">${escapeHtml(bAcc)}</span>
                <span class="txn-details-label">Branch:</span><span class="txn-details-value">${escapeHtml(branch)}</span>
              </div>
            </div>
        `;
        if (container) container.appendChild(card);
    });
}

function hideAccordionInspector() {
    const section = document.getElementById('accordionInspectionSection');
    if (section) section.style.display = 'none';
    const container = document.getElementById('transactionAccordionContainer');
    if (container) container.innerHTML = '';
}

function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, ch => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[ch]));
}

// ==========================================
// GRID DATA REPRESENTATION & REPORT POPULATION
// ==========================================
function populateReportTable(dataArray) {
    const tbody = document.getElementById('tableReportRows');
    if (!tbody) return;
    if (!dataArray || dataArray.length === 0) {
        tbody.innerHTML = `<tr><td colspan="9" style="text-align:center;color:#64748b;">No results match the specified criteria.</td></tr>`;
        return;
    }

    let rowsHtml = '';
    dataArray.forEach(row => {
        const refNumber = row.referenceNumber || 'N/A';
        const txnDate = row.date || 'N/A';
        const txnType = row.type || 'N/A';
        const senderName = row.senderName || 'N/A';
        const senderAcc = row.senderAccount || 'N/A';
        const beneficiaryName = row.beneficiaryName || 'N/A';
        const beneficiaryAcc = row.beneficiaryAccount || 'N/A';
        const branch = row.branchId || 'N/A';
        const amount = row.amount || '0.00';

        rowsHtml += `<tr data-ref="${escapeHtml(refNumber)}" data-sender="${escapeHtml(senderAcc)}" data-beneficiary="${escapeHtml(beneficiaryAcc)}">
            <td style="font-family:monospace;color:#fbbf24;" title="${escapeHtml(refNumber)}">${escapeHtml(refNumber)}</td>
            <td title="${escapeHtml(txnDate)}">${escapeHtml(txnDate)}</td>
            <td><span class="pill role" style="background:#1e293b;color:#cbd5e1;" title="${escapeHtml(txnType)}">${escapeHtml(txnType)}</span></td>
            <td title="${escapeHtml(senderName)}">${escapeHtml(senderName)}</td>
            <td style="font-family:monospace;" title="${escapeHtml(senderAcc)}">${escapeHtml(senderAcc)}</td>
            <td title="${escapeHtml(beneficiaryName)}">${escapeHtml(beneficiaryName)}</td>
            <td style="font-family:monospace;" title="${escapeHtml(beneficiaryAcc)}">${escapeHtml(beneficiaryAcc)}</td>
            <td title="${escapeHtml(branch)}">${escapeHtml(branch)}</td>
            <td style="text-align:right;font-weight:600;color:#10b981;" title="${escapeHtml(amount)}">${escapeHtml(amount)}</td>
        </tr>`;
    });
    tbody.innerHTML = rowsHtml;
}

// ==========================================
// TRANSFORMATION & FILTERS BY CLICK EVENT
// ==========================================
function filterReportTableByNode(nodeId) {
    const nodeMeta = nodeTooltipMap[nodeId];
    if (!nodeMeta) return;

    const rows = document.querySelectorAll('#tableReportRows tr');
    const indicator = document.getElementById('tableFilterIndicator');
    
    let matchCount = 0;
    let searchToken = String(nodeId).trim().toLowerCase();

    if (nodeMeta.group === 'SelfTransferHub' && searchToken.startsWith('hub_self_')) {
        searchToken = searchToken.replace('hub_self_', '');
    }

    rows.forEach(row => {
        if (row.cells.length < 2) return;

        const ref = (row.getAttribute('data-ref') || '').toLowerCase();
        const sender = (row.getAttribute('data-sender') || '').toLowerCase();
        const beneficiary = (row.getAttribute('data-beneficiary') || '').toLowerCase();

        let isMatch = false;
        if (nodeMeta.group === 'Transaction') {
            isMatch = (ref === searchToken || searchToken.includes(ref) || ref.includes(searchToken));
        } else if (nodeMeta.group === 'SelfTransferHub') {
            isMatch = (sender === searchToken && beneficiary === searchToken);
        } else {
            isMatch = (sender === searchToken || beneficiary === searchToken);
        }

        if (isMatch) {
            row.classList.remove('hidden-row');
            matchCount++;
        } else {
            row.classList.add('hidden-row');
        }
    });

    if (indicator) {
        indicator.textContent = `Filtered by: ${nodeMeta.label || nodeId} (${matchCount} records)`;
        indicator.style.display = 'inline-block';
    }
    
    if (!tablePanelExpanded) {
        toggleReportTablePanel();
    }
}

function resetTableFilters() {
    const rows = document.querySelectorAll('#tableReportRows tr');
    rows.forEach(row => row.classList.remove('hidden-row'));
    const indicator = document.getElementById('tableFilterIndicator');
    if (indicator) {
        indicator.style.display = 'none';
        indicator.textContent = '';
    }
}

// ==========================================
// GLOBAL MISC ENGINE WRAPPERS
// ==========================================
function fitNetworkView() {
    networkInstance?.fit({ animation: { duration: 500, easingFunction: 'easeInOutQuad' } });
}

function togglePhysics() {
    if (!networkInstance) return;
    physicsEnabled = !physicsEnabled;
    networkInstance.setOptions({ physics: { enabled: physicsEnabled } });
    syncPhysicsButton();
}

function syncPhysicsButton() {
    const lbl = document.getElementById('physicsLabel');
    if (lbl) lbl.textContent = physicsEnabled ? 'Physics' : 'Frozen';
}

function clearNetworkCanvas() {
    if (networkInstance) { networkInstance.destroy(); networkInstance = null; }
    nodeTooltipMap = {};
    currentRawTableData = [];
    globalCachedEdges = [];
    
    if (typeof window.globalUnlinkedRefs !== 'undefined') {
        window.globalUnlinkedRefs.clear();
    }
    
    hideAccordionInspector();
    
    const emptyState = document.getElementById('canvasEmptyState');
    if (emptyState) emptyState.style.display = 'flex';
    
    setToolbarVisible(false);
    
    const statsBar = document.getElementById('graphStatsBar');
    if (statsBar) statsBar.classList.add('hidden');
    
    const tableRows = document.getElementById('tableReportRows');
    if (tableRows) tableRows.innerHTML = `<tr><td colspan="9" style="text-align:center;color:#64748b;">No dataset queried. Run visualization logic.</td></tr>`;
    
    resetTableFilters();
    populateBranchFilterOptions([]);
    populateTransactionTypeFilterOptions([]);
    showToast('Canvas cleared.');
}

function setToolbarVisible(visible) {
    const toolbar = document.getElementById('canvasToolbar');
    if (toolbar) toolbar.style.display = visible ? 'flex' : 'none';
}

// Sync counts to the UI display elements
function setGraphStats(nodes, edges, txns) {
    const sNodes = document.getElementById('statNodes');
    const sEdges = document.getElementById('statEdges');
    const sTxns = document.getElementById('statTxns');
    const bar = document.getElementById('graphStatsBar');

    if (sNodes) sNodes.textContent = nodes;
    if (sEdges) sEdges.textContent = edges;
    if (sTxns) sTxns.textContent = txns;
    if (bar) bar.classList.remove('hidden');
}

function showToast(message) {
    const toast = document.getElementById('toastNotification');
    if (!toast) return;
    toast.textContent = message;
    toast.classList.add('visible');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => {
        toast.classList.remove('visible');
    }, 3000);
}

// =========================================================================
// INITIALIZATION LOGIC FOR INTERACTIVE DRAGGABLE RESIZE ARRAYS
// =========================================================================
document.addEventListener('DOMContentLoaded', () => {
    const panel = document.getElementById('tableReportPanel');
    const handle = document.getElementById('tableReportHeaderHandle');

    if (handle && panel) {
        let isDraggingPanel = false;
        let startPanelY, startPanelHeight;

        handle.addEventListener('mousedown', (e) => {
            if (e.target.tagName === 'BUTTON' || e.target.classList.contains('close-inspector')) return;
            isDraggingPanel = true;
            startPanelY = e.clientY;
            startPanelHeight = parseInt(document.defaultView.getComputedStyle(panel).height, 10);
            document.addEventListener('mousemove', handlePanelResize);
            document.addEventListener('mouseup', stopPanelInteraction);
            e.preventDefault();
        });

        function handlePanelResize(e) {
            if (!isDraggingPanel) return;
            const deltaY = startPanelY - e.clientY; 
            panel.style.height = `${startPanelHeight + deltaY}px`;
        }

        function stopPanelInteraction() {
            isDraggingPanel = false;
            document.removeEventListener('mousemove', handlePanelResize);
            document.removeEventListener('mouseup', stopPanelInteraction);
        }
    }

    const table = document.getElementById('resizableReportTable');
    if (table) {
        const cols = table.querySelectorAll('thead th');

        cols.forEach((col) => {
            const resizer = document.createElement('div');
            resizer.classList.add('col-resize-handle');
            col.appendChild(resizer);

            let isResizingCol = false;
            let startX, startWidth;

            resizer.addEventListener('mousedown', (e) => {
                isResizingCol = true;
                resizer.classList.add('resizing');
                startX = e.clientX;
                startWidth = col.offsetWidth;

                e.stopPropagation();
                e.preventDefault();

                const mouseMoveHandler = (moveEvent) => {
                    if (!isResizingCol) return;
                    const currentDeltaX = moveEvent.clientX - startX;
                    const updatedWidth = Math.max(45, startWidth + currentDeltaX);
                    col.style.width = `${updatedWidth}px`;
                };

                const mouseUpHandler = () => {
                    isResizingCol = false;
                    resizer.classList.remove('resizing');
                    document.removeEventListener('mousemove', mouseMoveHandler);
                    document.removeEventListener('mouseup', mouseUpHandler);
                };

                document.addEventListener('mousemove', mouseMoveHandler);
                document.addEventListener('mouseup', mouseUpHandler);
            });
        });
    }
});