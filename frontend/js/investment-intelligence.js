document.addEventListener('DOMContentLoaded', () => {
  const companySelect = document.getElementById('companySelect');
  const analyzeBtn = document.getElementById('analyzeBtn');
  const emptyState = document.getElementById('emptyState');
  const loadingState = document.getElementById('loadingState');
  const errorState = document.getElementById('errorState');
  const resultsContainer = document.getElementById('resultsContainer');
  
  let currentController = null;
  let priceChartInst = null;
  let maChartInst = null;
  let drawChartInst = null;

  const companySearchInput = document.getElementById('companySearchInput');
  const companySearchDropdown = document.getElementById('companySearchDropdown');
  const selectedCompanyId = document.getElementById('selectedCompanyId');

  let searchTimeout = null;

  companySearchInput.addEventListener('input', (e) => {
    const query = e.target.value.trim();
    selectedCompanyId.value = '';
    analyzeBtn.disabled = true;

    if (query.length < 2) {
      companySearchDropdown.style.display = 'none';
      return;
    }

    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => performSearch(query), 300);
  });

  const localCompanyMap = new Map();

  async function loadExistingCompanies() {
    try {
      const res = await fetch('/api/companies', {
        headers: { 'Authorization': `Bearer ${localStorage.getItem('sharesync_token')}` }
      });
      if (res.ok) {
        const json = await res.json();
        const list = json.data || [];
        list.forEach(c => {
          if (c.tickerSymbol) {
            localCompanyMap.set(c.tickerSymbol.toUpperCase(), c.companyId);
          }
        });
      }
    } catch(e) {
      console.warn("Could not pre-fetch company map", e);
    }
  }
  loadExistingCompanies();

  // Quick Select Buttons
  const quickChips = document.querySelectorAll('.quick-company-btn');
  quickChips.forEach(btn => {
    btn.addEventListener('click', () => {
      const sym = (btn.dataset.ticker || '').toUpperCase();
      const localId = localCompanyMap.get(sym);
      const c = {
        symbol: btn.dataset.ticker,
        name: btn.dataset.name,
        sector: btn.dataset.sector,
        isAdded: Boolean(localId),
        localCompanyId: localId
      };
      selectCompany(c);
      analyzeBtn.click();
    });
  });

  document.addEventListener('click', (e) => {
    if (!companySearchInput.contains(e.target) && !companySearchDropdown.contains(e.target)) {
      companySearchDropdown.style.display = 'none';
    }
  });

  async function performSearch(query) {
    try {
      const res = await fetch(`/api/companies/search?q=${encodeURIComponent(query)}&limit=10`, {
        headers: { 'Authorization': `Bearer ${localStorage.getItem('sharesync_token')}` }
      });
      if (res.ok) {
        const data = await res.json();
        if (data.success && Array.isArray(data.data)) {
          renderDropdown(data.data);
        } else {
          renderDropdown([]);
        }
      }
    } catch (e) {
      console.error("Search failed", e);
      renderDropdown([]);
    }
  }

  function renderDropdown(companies) {
    companySearchDropdown.innerHTML = '';
    if (companies.length === 0) {
      companySearchDropdown.innerHTML = `<div class="p-3 text-muted text-center">No companies found.</div>`;
    } else {
      companies.forEach(c => {
        const item = document.createElement('a');
        item.className = 'dropdown-item p-2 border-bottom d-flex align-items-center justify-content-between';
        item.href = '#';
        item.innerHTML = `
          <div style="min-width: 0;">
            <div class="fw-semibold text-truncate" style="font-size: 14px;">${c.symbol} - ${c.name}</div>
            <div class="text-muted text-truncate" style="font-size: 12px;">${c.sector}</div>
          </div>
        `;
        item.addEventListener('click', (e) => {
          e.preventDefault();
          selectCompany(c);
        });
        companySearchDropdown.appendChild(item);
      });
    }
    companySearchDropdown.style.display = 'block';
  }

  function selectCompany(c) {
    const sym = (c.symbol || '').toUpperCase();
    if (localCompanyMap.has(sym)) {
      c.localCompanyId = localCompanyMap.get(sym);
      c.isAdded = true;
    }
    companySearchInput.value = `${c.symbol} - ${c.name}`;
    selectedCompanyId.value = JSON.stringify(c);
    companySearchDropdown.style.display = 'none';
    analyzeBtn.disabled = false;
  }

  analyzeBtn.addEventListener('click', async () => {
    const val = selectedCompanyId.value;
    if (!val) return;
    const c = JSON.parse(val);
    let cid = c.localCompanyId;
    
    if (!c.isAdded || !cid) {
       analyzeBtn.disabled = true;
       analyzeBtn.innerHTML = '<i class="spinner-border spinner-border-sm me-2"></i> Importing...';
       try {
         const res = await fetch('/api/companies/add-from-dse', {
           method: 'POST',
           headers: { 
             'Authorization': `Bearer ${localStorage.getItem('sharesync_token')}`,
             'Content-Type': 'application/json'
           },
           body: JSON.stringify({ symbol: c.symbol, name: c.name, sector: c.sector })
         });
         const data = await res.json();
         if(data.success && data.data) {
           cid = data.data.companyId;
           // Update local cache state so we don't import again
           c.localCompanyId = cid;
           c.isAdded = true;
           selectedCompanyId.value = JSON.stringify(c);
         } else {
           alert(data.message || "Failed to import company.");
           analyzeBtn.disabled = false;
           analyzeBtn.innerHTML = '<i class="bi bi-search"></i> Analyze';
           return;
         }
       } catch(e) {
         console.error(e);
         alert("Error importing company.");
         analyzeBtn.disabled = false;
         analyzeBtn.innerHTML = '<i class="bi bi-search"></i> Analyze';
         return;
       }
       analyzeBtn.disabled = false;
       analyzeBtn.innerHTML = '<i class="bi bi-search"></i> Analyze';
    }
    
    fetchProfile(cid);
  });

  async function fetchProfile(cid) {
    if (currentController) currentController.abort();
    currentController = new AbortController();

    emptyState.classList.add('d-none');
    errorState.classList.add('d-none');
    resultsContainer.classList.add('d-none');
    loadingState.classList.remove('d-none');
    analyzeBtn.disabled = true;

    try {
      const res = await fetch(`/api/investment-analysis/company/${cid}`, {
        headers: { 'Authorization': `Bearer ${localStorage.getItem('sharesync_token')}` },
        signal: currentController.signal
      });
      const data = await res.json();
      if (!res.ok || !data.success) throw new Error(data.message || 'Failed to retrieve investment profile.');
      
      renderResults(data.data);
      
      loadingState.classList.add('d-none');
      resultsContainer.classList.remove('d-none');
    } catch(err) {
      if (err.name === 'AbortError') return;
      loadingState.classList.add('d-none');
      document.getElementById('errorMessage').textContent = "Analysis Failed";
      document.getElementById('errorSubMessage').textContent = err.message || "An unexpected error occurred.";
      errorState.classList.remove('d-none');
    } finally {
      analyzeBtn.disabled = false;
    }
  }

  const setText = (id, val) => {
    const el = document.getElementById(id);
    if (el) el.textContent = val ?? '';
  };

  function renderResults(d) {
    if (!d) return;

    // 1. Header
    setText('resName', d.company?.name);
    setText('resCode', d.company?.tradingCode);
    setText('resSector', d.company?.sector);
    setText('resDates', d.analysisPeriod ? `${d.analysisPeriod.startDate || ''} to ${d.analysisPeriod.endDate || ''}` : '');
    
    const covEl = document.getElementById('resDataCoverage');
    if (covEl) {
      if (d.dataQuality?.hasFullFiveYearHistory) {
        covEl.className = 'alert alert-success py-2 px-3 mb-0 d-inline-block text-start small';
        covEl.innerHTML = `<i class="bi bi-check-circle-fill"></i> 5 years of validated historical data`;
      } else {
        covEl.className = 'alert alert-warning py-2 px-3 mb-0 d-inline-block text-start small';
        covEl.innerHTML = `<i class="bi bi-exclamation-triangle-fill"></i> Limited historical data (${d.analysisPeriod?.years ?? 0} years)`;
      }
    }

    // 2. Profile Score
    const overallScore = d.score?.overallScore ?? 0;
    setText('resScore', Number(overallScore).toFixed(0));
    
    let scoreColor = '#ef4444';
    let badgeClass = 'bg-danger-subtle text-danger border border-danger-subtle';
    if (overallScore >= 75) {
      scoreColor = '#10b981';
      badgeClass = 'bg-success-subtle text-success border border-success-subtle';
    } else if (overallScore >= 60) {
      scoreColor = '#06b6d4';
      badgeClass = 'bg-info-subtle text-info border border-info-subtle';
    } else if (overallScore >= 45) {
      scoreColor = '#f59e0b';
      badgeClass = 'bg-warning-subtle text-warning border border-warning-subtle';
    } else if (overallScore >= 30) {
      scoreColor = '#f97316';
      badgeClass = 'bg-warning-subtle text-warning border border-warning-subtle';
    }

    const scoreCircle = document.getElementById('resScoreCircle');
    if (scoreCircle) {
      scoreCircle.style.setProperty('--score-deg', `${(overallScore / 100) * 360}deg`);
      scoreCircle.style.setProperty('--score-color', scoreColor);
      scoreCircle.style.boxShadow = `0 6px 25px ${scoreColor}25`;
    }
    const resClass = document.getElementById('resClass');
    if (resClass) {
      resClass.innerHTML = `<span class="badge ${badgeClass} fs-6 px-3 py-2 rounded-pill shadow-sm">${d.score?.classification || ''}</span>`;
    }

    // 3. Component Scores
    const comps = [
      { id: 'scReturn', v: d.score?.returnScore ?? 0 },
      { id: 'scRisk', v: d.score?.riskScore ?? 0 },
      { id: 'scDrawdown', v: d.score?.drawdownScore ?? 0 },
      { id: 'scConsistency', v: d.score?.consistencyScore ?? 0 },
      { id: 'scTrend', v: d.score?.trendScore ?? 0 }
    ];
    comps.forEach(c => {
      setText(c.id + 'Txt', `${Number(c.v).toFixed(0)} / 100`);
      const bar = document.getElementById(c.id + 'Bar');
      if (bar) bar.style.width = `${Math.min(100, Math.max(0, Number(c.v)))}%`;
    });

    // 4. Metrics
    const fmtPct = v => v != null && !isNaN(v) ? `${v > 0 ? '+' : ''}${Number(v).toFixed(2)}%` : 'N/A';
    const fmtCur = v => v != null && !isNaN(v) ? `৳${Number(v).toFixed(2)}` : 'N/A';

    setText('mReturn', fmtPct(d.summary?.historicalReturnPercentage));
    setText('mCAGR', fmtPct(d.summary?.cagrPercentage));
    setText('mVol', fmtPct(d.risk?.annualizedVolatilityPercentage));
    setText('mDraw', fmtPct(d.risk?.maximumDrawdownPercentage));
    setText('mPos', d.consistency?.positiveDayPercentage != null ? `${Number(d.consistency.positiveDayPercentage).toFixed(1)}%` : 'N/A');
    setText('mPrice', fmtCur(d.trend?.latestClose));

    // 5. Trend Class
    const tcMap = {
      'STRONGER_UPTREND': 'Stronger Historical Uptrend',
      'UPTREND': 'Uptrend',
      'WEAKENING_OR_MIXED': 'Weakening or Mixed',
      'DOWNTREND': 'Downtrend',
      'TREND_INSUFFICIENT_DATA': 'Insufficient Data'
    };
    setText('trendClassText', tcMap[d.trend?.classification] || d.trend?.classification || 'N/A');
    setText('tMa50', fmtCur(d.trend?.latestMA50));
    setText('tMa200', fmtCur(d.trend?.latestMA200));

    // 6. Explanations
    const mkList = (arr, elId, emptyMsg, iconClass) => {
      const el = document.getElementById(elId);
      if (!el) return;
      el.innerHTML = '';
      if (!arr || arr.length === 0) {
        el.innerHTML = `<li class="text-muted">${emptyMsg}</li>`;
      } else {
        arr.forEach(txt => {
          const li = document.createElement('li');
          li.innerHTML = `<i class="${iconClass}"></i> ${txt}`;
          el.appendChild(li);
        });
      }
    };
    mkList(d.explanations?.positiveFactors, 'expPos', 'No positive factors found.', 'bi bi-check me-1');
    mkList(d.explanations?.riskFactors, 'expRisk', 'No significant risk factors found.', 'bi bi-exclamation-circle me-1');
    mkList(d.explanations?.trendFactors, 'expTrend', 'No clear trend factors.', 'bi bi-arrow-right-short me-1');

    // 7. Data Quality & Source
    setText('srcName', d.source?.name);
    setText('srcDataset', d.source?.dataset);
    setText('srcDoi', d.source?.doi);
    setText('disclaimerText', d.disclaimer);
    setText('footerDisclaimer', d.disclaimer);

    const dq = document.getElementById('dqList');
    if (dq && d.dataQuality) {
      dq.innerHTML = `
        <li>${(d.dataQuality.analysisObservationCount ?? 0).toLocaleString()} validated observations</li>
        <li>${d.dataQuality.firstValidDate || 'N/A'} to ${d.dataQuality.lastValidDate || 'N/A'}</li>
      `;
      if (d.dataQuality.excludedObservationCount > 0) {
        dq.innerHTML += `<li>${d.dataQuality.excludedObservationCount} observations excluded during validation</li>`;
      }
      if (d.dataQuality.dataQualityWarnings && Array.isArray(d.dataQuality.dataQualityWarnings)) {
        d.dataQuality.dataQualityWarnings.forEach(w => {
          dq.innerHTML += `<li class="text-warning"><i class="bi bi-exclamation-triangle"></i> ${w}</li>`;
        });
      }
    }

    // 8. Yearly Performance
    const yt = document.getElementById('yearlyTableBody');
    if (yt) {
      yt.innerHTML = '';
      if (d.yearlyPerformance && Array.isArray(d.yearlyPerformance)) {
        d.yearlyPerformance.forEach(y => {
          const c = (y.returnPercentage ?? 0) >= 0 ? 'text-success' : 'text-danger';
          yt.innerHTML += `<tr>
            <td>${y.year}</td>
            <td>${fmtCur(y.startPrice)}</td>
            <td>${fmtCur(y.endPrice)}</td>
            <td class="text-end fw-semibold ${c}">${fmtPct(y.returnPercentage)}</td>
          </tr>`;
        });
      }
    }

    // 9. Charts
    currentProfileData = d;
    resetTimeframeButtons();
    renderCharts(d.chartData || []);
  }

  let currentProfileData = null;

  function resetTimeframeButtons() {
    const tfButtons = document.querySelectorAll('.ii-tf-btn');
    tfButtons.forEach(b => {
      if (b.dataset.days === 'all') b.classList.add('active');
      else b.classList.remove('active');
    });
  }

  // Timeframe selector listeners
  const tfButtons = document.querySelectorAll('.ii-tf-btn');
  tfButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      tfButtons.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      const days = btn.dataset.days;
      filterAndRenderCharts(days);
    });
  });

  function filterAndRenderCharts(days) {
    if (!currentProfileData || !currentProfileData.chartData) return;
    let ch = [...currentProfileData.chartData];
    if (days !== 'all') {
      const dCount = parseInt(days, 10);
      if (!isNaN(dCount) && dCount > 0) {
        // Map calendar days to approximate trading days (~250/365)
        const tradingDays = Math.max(5, Math.round(dCount * (250 / 365)));
        ch = ch.slice(Math.max(0, ch.length - tradingDays));
      }
    }
    renderCharts(ch);
  }

  function renderCharts(chRaw) {
    if (priceChartInst) {
      priceChartInst.destroy();
      priceChartInst = null;
    }
    if (maChartInst) {
      maChartInst.destroy();
      maChartInst = null;
    }
    if (drawChartInst) {
      drawChartInst.destroy();
      drawChartInst = null;
    }

    let ch = chRaw || [];
    if (!Array.isArray(ch) || ch.length === 0) return;

    const max = 350;
    if (ch.length > max) {
      const step = Math.ceil(ch.length / max);
      ch = ch.filter((_, i) => i % step === 0);
    }
    const labels = ch.map(x => x.date);
    const close = ch.map(x => x.close);
    const ma50 = ch.map(x => x.ma50);
    const ma200 = ch.map(x => x.ma200);
    const draw = ch.map(x => x.drawdown);

    const isUp = close.length >= 2 ? (close[close.length - 1] >= close[0]) : true;
    const strokeColor = isUp ? '#10b981' : '#ef4444';
    const fillColor = isUp ? 'rgba(16, 185, 129, 0.12)' : 'rgba(239, 68, 68, 0.12)';

    if (window.Chart) {
      Chart.defaults.color = getComputedStyle(document.documentElement).getPropertyValue('--color-text').trim() || '#333';
      Chart.defaults.borderColor = 'rgba(150,150,150,0.1)';
      Chart.defaults.font.family = "'Inter', -apple-system, BlinkMacSystemFont, sans-serif";
    }

    const priceCanvas = document.getElementById('priceChart');
    if (priceCanvas && window.Chart) {
      const pCtx = priceCanvas.getContext('2d');
      priceChartInst = new Chart(pCtx, {
        type: 'line',
        data: {
          labels,
          datasets: [{
            label: 'Close Price (BDT)',
            data: close,
            borderColor: strokeColor,
            borderWidth: 2.2,
            pointRadius: ch.length <= 30 ? 3 : 0,
            pointHoverRadius: 5,
            pointBackgroundColor: strokeColor,
            fill: true,
            backgroundColor: fillColor,
            tension: 0.15
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          interaction: { mode: 'index', intersect: false },
          plugins: {
            legend: { display: false },
            tooltip: {
              backgroundColor: 'rgba(17, 24, 39, 0.95)',
              titleColor: '#f9fafb',
              bodyColor: '#f9fafb',
              padding: 10,
              cornerRadius: 8,
              callbacks: {
                label: ctx => ` Price: ৳${Number(ctx.parsed.y).toFixed(2)}`
              }
            }
          },
          scales: {
            x: {
              grid: { display: false },
              ticks: { maxTicksLimit: 8, font: { size: 11 } }
            },
            y: {
              grid: { color: 'rgba(150,150,150,0.08)' },
              ticks: {
                callback: v => `৳${v}`,
                font: { size: 11 }
              }
            }
          }
        }
      });
    }

    const maCanvas = document.getElementById('maChart');
    if (maCanvas && window.Chart) {
      const mCtx = maCanvas.getContext('2d');
      const hasMa = ma50.some(v => v != null) || ma200.some(v => v != null);
      const maWarning = document.getElementById('maWarning');
      if (hasMa) {
        if (maWarning) maWarning.classList.add('d-none');
        maChartInst = new Chart(mCtx, {
          type: 'line',
          data: {
            labels,
            datasets: [
              { label: 'Close', data: close, borderColor: 'rgba(148, 163, 184, 0.7)', borderWidth: 1.5, pointRadius: 0, tension: 0.15 },
              { label: 'MA 50', data: ma50, borderColor: '#3b82f6', borderWidth: 2, pointRadius: 0, tension: 0.15 },
              { label: 'MA 200', data: ma200, borderColor: '#f59e0b', borderWidth: 2, pointRadius: 0, tension: 0.15 }
            ]
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
              tooltip: {
                padding: 10,
                cornerRadius: 8
              }
            },
            scales: {
              x: { grid: { display: false }, ticks: { maxTicksLimit: 8, font: { size: 11 } } },
              y: { grid: { color: 'rgba(150,150,150,0.08)' }, ticks: { callback: v => `৳${v}`, font: { size: 11 } } }
            }
          }
        });
      } else {
        if (maWarning) maWarning.classList.remove('d-none');
      }
    }

    const drawCanvas = document.getElementById('drawdownChart');
    if (drawCanvas && window.Chart) {
      const dCtx = drawCanvas.getContext('2d');
      drawChartInst = new Chart(dCtx, {
        type: 'line',
        data: {
          labels,
          datasets: [{
            label: 'Drawdown (%)',
            data: draw,
            borderColor: '#ef4444',
            backgroundColor: 'rgba(239, 68, 68, 0.18)',
            borderWidth: 2,
            pointRadius: 0,
            fill: true,
            tension: 0.15
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          interaction: { mode: 'index', intersect: false },
          plugins: {
            legend: { display: false },
            tooltip: {
              padding: 10,
              cornerRadius: 8,
              callbacks: {
                label: ctx => ` Drawdown: ${Number(ctx.parsed.y).toFixed(2)}%`
              }
            }
          },
          scales: {
            x: { grid: { display: false }, ticks: { maxTicksLimit: 8, font: { size: 11 } } },
            y: {
              grid: { color: 'rgba(150,150,150,0.08)' },
              ticks: { callback: v => `${v}%`, font: { size: 11 } },
              suggestedMax: 0
            }
          }
        }
      });
    }
  }
});
