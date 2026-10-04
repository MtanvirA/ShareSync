// =========================================
// SHARESYNC
// Main JavaScript file
// =========================================

let portfolioChart;

const API_BASE_URL = window.location.origin.includes(":5000")
  ? "/api"
  : "http://localhost:5000/api";

function escapeHtml(str) {
  if (str === null || str === undefined) return "";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

async function ensureAuthenticated() {
  let token = localStorage.getItem("sharesync_token");
  if (token) return token;

  try {
    const res = await fetch(`${API_BASE_URL}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email: "tanvir@sharesync.com",
        password: "Password123#"
      })
    });
    const json = await res.json();
    if (json.success && json.data?.token) {
      localStorage.setItem("sharesync_token", json.data.token);
      localStorage.setItem("sharesync_user", JSON.stringify(json.data));
      return json.data.token;
    }
  } catch (err) {
    console.error("Auto-authentication notice:", err);
  }
  return null;
}

async function apiRequest(endpoint, options = {}) {
  const token = await ensureAuthenticated();
  const headers = {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options.headers || {})
  };

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    headers
  });

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    const errorMsg = data?.message || (data?.errors ? data.errors.join(", ") : "An error occurred.");
    const error = new Error(errorMsg);
    error.status = response.status;
    error.data = data;
    throw error;
  }

  return data;
}

function showToast(message, tone = "success") {
  let toast = document.querySelector(".app-toast");

  if (!toast) {
    toast = document.createElement("div");
    toast.className = "app-toast";
    toast.setAttribute("role", "status");
    document.body.appendChild(toast);
  }

  toast.className = `app-toast ${tone}`;
  toast.textContent = message;
  toast.classList.add("is-visible");

  window.clearTimeout(toast.dismissTimer);
  toast.dismissTimer = window.setTimeout(() => {
    toast.classList.remove("is-visible");
  }, 3600);
}

function setFieldError(field, message) {
  const wrapper = field.closest(".form-field");

  if (!wrapper) {
    return;
  }

  let error = wrapper.querySelector(".field-error");

  if (!error) {
    error = document.createElement("small");
    error.className = "field-error";
    wrapper.appendChild(error);
  }

  error.textContent = message;
  field.setAttribute("aria-invalid", "true");
  wrapper.classList.add("has-error");
}

function clearFieldError(field) {
  const wrapper = field.closest(".form-field");

  if (!wrapper) {
    return;
  }

  const error = wrapper.querySelector(".field-error");

  if (error) {
    error.remove();
  }

  field.removeAttribute("aria-invalid");
  wrapper.classList.remove("has-error");
}

function validateRequiredFields(form) {
  let firstInvalidField;

  form.querySelectorAll("[required]").forEach((field) => {
    clearFieldError(field);

    if (!field.value.trim()) {
      setFieldError(field, "This field is required.");

      if (!firstInvalidField) {
        firstInvalidField = field;
      }
    }
  });

  if (firstInvalidField) {
    firstInvalidField.focus();
    return false;
  }

  return true;
}

function setupInlineValidation(form) {
  form.querySelectorAll("[required]").forEach((field) => {
    field.addEventListener("input", () => clearFieldError(field));
    field.addEventListener("change", () => clearFieldError(field));
  });
}

function setupLastUpdated() {
  const pageHeader = document.querySelector(".page-header > div");

  if (!pageHeader || pageHeader.querySelector(".last-updated")) {
    return;
  }

  const updated = document.createElement("span");
  updated.className = "last-updated";
  updated.textContent = "Updated just now";
  pageHeader.appendChild(updated);
}

function setupMobileNavigation() {
  const links = [...document.querySelectorAll(".sidebar-section:first-child .sidebar-link")];

  if (!links.length || document.querySelector(".mobile-nav")) {
    return;
  }

  const mobileNav = document.createElement("nav");
  mobileNav.className = "mobile-nav";
  mobileNav.setAttribute("aria-label", "Primary navigation");

  links.slice(0, 4).forEach((link) => {
    const mobileLink = link.cloneNode(true);
    mobileLink.classList.add("mobile-nav-link");
    mobileNav.appendChild(mobileLink);
  });

  document.body.appendChild(mobileNav);
}

function setupTableHints() {
  document.querySelectorAll(".table-responsive").forEach((wrapper) => {
    if (wrapper.querySelector(".table-scroll-hint")) {
      return;
    }

    const hint = document.createElement("small");
    hint.className = "table-scroll-hint";
    hint.textContent = "Swipe horizontally to view all columns";
    wrapper.appendChild(hint);
  });
}

document.addEventListener("DOMContentLoaded", () => {
  console.log("ShareSync dashboard loaded.");

  setupSidebarNavigation();

  setupMobileNavigation();

  setupLastUpdated();

  setupTableHints();

  createPortfolioChart();

  setupChartPeriod();

  setupTransactionForm();

  setupWatchlistForm();

  setupDividendForm();

  setupReports();

  setupTransactionFilters();

  setupWatchlistFilter();

  setupDividendFilter();

  setupPortfolioManagement();
});

// =========================================
// DIVIDEND FORM
// =========================================

function setupDividendForm() {
  const form = document.getElementById("dividendForm");

  if (!form) {
    return;
  }

  const formCard = document.getElementById("dividendFormCard");

  const openButton = document.getElementById("openDividendForm");

  const closeButton = document.getElementById("closeDividendForm");

  const cancelButton = document.getElementById("cancelDividend");

  setupInlineValidation(form);

  openButton.addEventListener("click", () => {
    formCard.classList.remove("form-hidden");

    formCard.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  });

  function closeForm() {
    formCard.classList.add("form-hidden");
  }

  closeButton.addEventListener("click", closeForm);

  cancelButton.addEventListener("click", closeForm);

  form.addEventListener("submit", (event) => {
    event.preventDefault();

    if (!validateRequiredFields(form)) {
      return;
    }

    const company = document.getElementById("dividendCompany").value;

    const amount = document.getElementById("dividendPerShare").value;

    const declaration = document.getElementById("declarationDate").value;

    const payment = document.getElementById("paymentDate").value;

    if (payment < declaration) {
      setFieldError(document.getElementById("paymentDate"), "Payment date must be after declaration date.");

      return;
    }

    showToast(`${company} dividend recorded successfully.`);

    form.reset();

    closeForm();
  });
}

// =========================================
// WATCHLIST MANAGEMENT
// =========================================

let userWatchlistsCache = [];
let currentWatchlistId = null;
let currentWatchlistData = null;

async function setupWatchlistForm() {
  const watchlistTable = document.querySelector(".watchlist-table");
  const watchlistForm = document.getElementById("watchlistForm");
  const watchlistManageForm = document.getElementById("watchlistManageForm");

  // Only run if on watchlist page
  if (!watchlistTable && !watchlistForm && !watchlistManageForm) {
    return;
  }

  const openAddCompBtn = document.getElementById("openWatchlistForm");
  const closeAddCompBtn = document.getElementById("closeWatchlistForm");
  const cancelAddCompBtn = document.getElementById("cancelWatchlist");
  const addCompCard = document.getElementById("watchlistFormCard");
  const companySelect = document.getElementById("watchlistCompany");
  const targetPriceInput = document.getElementById("targetPrice");
  const targetWatchlistSelect = document.getElementById("targetWatchlistSelect");

  const openCreateWatchlistBtn = document.getElementById("openCreateWatchlistBtn");
  const openEditWatchlistBtn = document.getElementById("openEditWatchlistBtn");
  const deleteWatchlistBtn = document.getElementById("deleteWatchlistBtn");
  const watchlistManageCard = document.getElementById("watchlistManageCard");
  const closeWatchlistManage = document.getElementById("closeWatchlistManage");
  const cancelWatchlistManage = document.getElementById("cancelWatchlistManage");
  const watchlistManageTitle = document.getElementById("watchlistManageTitle");
  const watchlistManageSubtitle = document.getElementById("watchlistManageSubtitle");
  const watchlistManageId = document.getElementById("watchlistManageId");
  const watchlistManageName = document.getElementById("watchlistManageName");
  const watchlistManageDesc = document.getElementById("watchlistManageDesc");

  const watchlistSelect = document.getElementById("watchlistSelect");

  if (watchlistForm) setupInlineValidation(watchlistForm);
  if (watchlistManageForm) setupInlineValidation(watchlistManageForm);

  // Load companies for the Add Company dropdown
  try {
    const res = await apiRequest("/companies");
    const companies = res.data || [];
    if (companySelect) {
      companySelect.innerHTML = '<option value="">Select company</option>' +
        companies.map(c => `<option value="${c.companyId}">${escapeHtml(c.tickerSymbol)} - ${escapeHtml(c.companyName)}</option>`).join("");
    }
  } catch (err) {
    console.error("Failed to load companies for watchlist:", err);
  }

  // Load user watchlists
  await loadUserWatchlists();

  // Watchlist switcher dropdown change
  if (watchlistSelect) {
    watchlistSelect.addEventListener("change", async (e) => {
      const selectedId = parseInt(e.target.value, 10);
      if (selectedId) {
        currentWatchlistId = selectedId;
        if (targetWatchlistSelect) targetWatchlistSelect.value = selectedId;
        await loadWatchlistDetail(currentWatchlistId);
      }
    });
  }

  // Open/Close Add Company form
  if (openAddCompBtn) {
    openAddCompBtn.addEventListener("click", () => {
      if (watchlistForm) watchlistForm.reset();
      if (targetWatchlistSelect && currentWatchlistId) {
        targetWatchlistSelect.value = currentWatchlistId;
      }
      if (addCompCard) {
        addCompCard.classList.remove("form-hidden");
        addCompCard.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    });
  }

  function closeAddCompForm() {
    if (addCompCard) addCompCard.classList.add("form-hidden");
    if (watchlistForm) watchlistForm.reset();
  }

  if (closeAddCompBtn) closeAddCompBtn.addEventListener("click", closeAddCompForm);
  if (cancelAddCompBtn) cancelAddCompBtn.addEventListener("click", closeAddCompForm);

  // Submit Add Company form
  if (watchlistForm) {
    watchlistForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      if (!validateRequiredFields(watchlistForm)) return;

      const compId = parseInt(companySelect?.value, 10);
      const targetPriceVal = targetPriceInput?.value ? parseFloat(targetPriceInput.value) : null;
      const targetWId = parseInt(targetWatchlistSelect?.value, 10) || currentWatchlistId;

      if (!compId) {
        setFieldError(companySelect, "Please select a company.");
        return;
      }

      if (!targetWId) {
        showToast("Please create or select a watchlist first.", "danger");
        return;
      }

      if (targetPriceVal !== null && (isNaN(targetPriceVal) || targetPriceVal <= 0)) {
        setFieldError(targetPriceInput, "Target price must be greater than zero.");
        return;
      }

      try {
        const payload = {
          companyId: compId,
          targetPrice: targetPriceVal
        };

        const res = await apiRequest(`/watchlists/${targetWId}/items`, {
          method: "POST",
          body: JSON.stringify(payload)
        });

        showToast(res.message || "Company added to watchlist.", "success");
        closeAddCompForm();
        currentWatchlistId = targetWId;
        await loadWatchlistDetail(currentWatchlistId);
      } catch (err) {
        console.error("Failed to add company to watchlist:", err);
        showToast(err.message, "danger");
      }
    });
  }

  // Watchlist Manage Form (Create / Edit)
  if (openCreateWatchlistBtn) {
    openCreateWatchlistBtn.addEventListener("click", () => {
      if (watchlistManageForm) watchlistManageForm.reset();
      if (watchlistManageId) watchlistManageId.value = "";
      if (watchlistManageTitle) watchlistManageTitle.textContent = "Create watchlist";
      if (watchlistManageSubtitle) watchlistManageSubtitle.textContent = "Add a new watchlist to your account";
      if (watchlistManageCard) {
        watchlistManageCard.classList.remove("form-hidden");
        watchlistManageCard.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    });
  }

  if (openEditWatchlistBtn) {
    openEditWatchlistBtn.addEventListener("click", () => {
      if (!currentWatchlistId) {
        showToast("No watchlist selected to edit.", "warning");
        return;
      }
      if (watchlistManageId) watchlistManageId.value = currentWatchlistId;
      if (watchlistManageTitle) watchlistManageTitle.textContent = "Edit watchlist";
      if (watchlistManageSubtitle) watchlistManageSubtitle.textContent = "Update watchlist name or description";
      if (watchlistManageName && currentWatchlistData) watchlistManageName.value = currentWatchlistData.watchlistName || "";
      if (watchlistManageDesc && currentWatchlistData) watchlistManageDesc.value = currentWatchlistData.description || "";
      if (watchlistManageCard) {
        watchlistManageCard.classList.remove("form-hidden");
        watchlistManageCard.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    });
  }

  function closeWatchlistManageForm() {
    if (watchlistManageCard) watchlistManageCard.classList.add("form-hidden");
    if (watchlistManageForm) watchlistManageForm.reset();
  }

  if (closeWatchlistManage) closeWatchlistManage.addEventListener("click", closeWatchlistManageForm);
  if (cancelWatchlistManage) cancelWatchlistManage.addEventListener("click", closeWatchlistManageForm);

  if (watchlistManageForm) {
    watchlistManageForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      if (!validateRequiredFields(watchlistManageForm)) return;

      const editId = watchlistManageId?.value;
      const name = watchlistManageName?.value?.trim();
      const desc = watchlistManageDesc?.value?.trim() || null;

      try {
        if (editId) {
          const res = await apiRequest(`/watchlists/${editId}`, {
            method: "PUT",
            body: JSON.stringify({ watchlistName: name, description: desc })
          });
          showToast(res.message || "Watchlist updated.", "success");
        } else {
          const res = await apiRequest("/watchlists", {
            method: "POST",
            body: JSON.stringify({ watchlistName: name, description: desc })
          });
          showToast(res.message || "Watchlist created.", "success");
          if (res.data?.watchlistId) currentWatchlistId = res.data.watchlistId;
        }

        closeWatchlistManageForm();
        await loadUserWatchlists();
      } catch (err) {
        console.error("Watchlist save failed:", err);
        showToast(err.message, "danger");
      }
    });
  }

  // Delete current watchlist
  if (deleteWatchlistBtn) {
    deleteWatchlistBtn.addEventListener("click", async () => {
      if (!currentWatchlistId) {
        showToast("No watchlist selected to delete.", "warning");
        return;
      }

      const confirmed = confirm(`Are you sure you want to delete '${currentWatchlistData?.watchlistName || "this watchlist"}'?`);
      if (!confirmed) return;

      try {
        const res = await apiRequest(`/watchlists/${currentWatchlistId}`, { method: "DELETE" });
        showToast(res.message || "Watchlist deleted.", "success");
        currentWatchlistId = null;
        await loadUserWatchlists();
      } catch (err) {
        console.error("Delete watchlist failed:", err);
        showToast("Cannot delete watchlist: " + err.message, "danger");
      }
    });
  }
}

async function loadUserWatchlists() {
  const watchlistSelect = document.getElementById("watchlistSelect");
  const targetWatchlistSelect = document.getElementById("targetWatchlistSelect");

  try {
    const res = await apiRequest("/watchlists");
    userWatchlistsCache = res.data || [];

    if (userWatchlistsCache.length === 0) {
      currentWatchlistId = null;
      currentWatchlistData = null;
      if (watchlistSelect) {
        watchlistSelect.classList.add("d-none");
        watchlistSelect.innerHTML = "";
      }
      if (targetWatchlistSelect) {
        targetWatchlistSelect.innerHTML = '<option value="">No watchlists</option>';
      }
      renderEmptyWatchlistState();
      return;
    }

    if (watchlistSelect) {
      watchlistSelect.innerHTML = userWatchlistsCache
        .map(w => `<option value="${w.watchlistId}">${escapeHtml(w.watchlistName)}</option>`)
        .join("");
      watchlistSelect.classList.remove("d-none");
    }

    if (targetWatchlistSelect) {
      targetWatchlistSelect.innerHTML = userWatchlistsCache
        .map(w => `<option value="${w.watchlistId}">${escapeHtml(w.watchlistName)}</option>`)
        .join("");
    }

    if (!currentWatchlistId || !userWatchlistsCache.some(w => w.watchlistId === currentWatchlistId)) {
      currentWatchlistId = userWatchlistsCache[0].watchlistId;
    }

    if (watchlistSelect) watchlistSelect.value = currentWatchlistId;
    if (targetWatchlistSelect) targetWatchlistSelect.value = currentWatchlistId;

    await loadWatchlistDetail(currentWatchlistId);
  } catch (err) {
    console.error("Failed to load user watchlists:", err);
    showToast("Could not load watchlists: " + err.message, "danger");
  }
}

async function loadWatchlistDetail(watchlistId) {
  const titleEl = document.getElementById("watchlistTitle");
  const descEl = document.getElementById("watchlistDescription");
  const headingEl = document.getElementById("currentWatchlistHeading");
  const currentDescEl = document.getElementById("currentWatchlistDesc");

  const summaryWatching = document.getElementById("summaryWatching");
  const summaryAboveTarget = document.getElementById("summaryAboveTarget");
  const summaryNearTarget = document.getElementById("summaryNearTarget");
  const summaryAvgChange = document.getElementById("summaryAvgChange");

  const tbody = document.getElementById("watchlistTableBody");

  try {
    const res = await apiRequest(`/watchlists/${watchlistId}`);
    currentWatchlistData = res.data;
    const d = currentWatchlistData;

    if (titleEl) titleEl.textContent = d.watchlistName;
    if (descEl) descEl.textContent = d.description || "Keep track of companies you're interested in.";
    if (headingEl) headingEl.textContent = d.watchlistName;
    if (currentDescEl) currentDescEl.textContent = d.description || "Companies you're currently monitoring";

    if (summaryWatching) summaryWatching.textContent = d.totalWatching;
    if (summaryAboveTarget) summaryAboveTarget.textContent = d.aboveTargetCount;
    if (summaryNearTarget) summaryNearTarget.textContent = d.nearTargetCount;
    if (summaryAvgChange) summaryAvgChange.textContent = "+0.00%";

    if (tbody) {
      if (!d.items || d.items.length === 0) {
        tbody.innerHTML = `
          <tr>
            <td colspan="7" class="text-center py-4 text-muted">
              <i class="bi bi-inbox fs-4 d-block mb-1"></i>
              No companies in this watchlist yet. Click "Add company" above to begin tracking.
            </td>
          </tr>
        `;
      } else {
        tbody.innerHTML = d.items.map(item => {
          const logoText = escapeHtml((item.tickerSymbol || "SS").slice(0, 2).toUpperCase());
          const currentPriceFormatted = "৳" + item.currentPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
          const targetPriceFormatted = item.targetPrice
            ? "৳" + item.targetPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
            : '<span class="text-muted">Not set</span>';

          let distanceHtml = '<span class="text-muted">-</span>';
          if (item.isTargetReached) {
            distanceHtml = '<span class="target-distance target-reached">Target reached</span>';
          } else if (item.targetDistancePercentage !== null && item.targetDistancePercentage !== undefined) {
            const isNear = item.targetDistancePercentage <= 5.0;
            const badgeClass = isNear ? "target-distance near-target" : "target-distance";
            distanceHtml = `<span class="${badgeClass}">${item.targetDistancePercentage.toFixed(2)}%</span>`;
          }

          const addedFormatted = new Date(item.addedAt).toLocaleDateString("en-US", { month: "short", day: "2-digit", year: "numeric" });

          return `
            <tr>
              <td>
                <div class="company-cell">
                  <span class="company-logo">${logoText}</span>
                  <div class="company-details">
                    <strong>${escapeHtml(item.tickerSymbol)}</strong>
                    <small>${escapeHtml(item.companyName)}</small>
                  </div>
                </div>
              </td>
              <td>${currentPriceFormatted}</td>
              <td>
                ${targetPriceFormatted}
                <button type="button" class="btn btn-sm btn-link p-0 ms-1 text-primary text-decoration-none" onclick="editWatchlistTargetPrice(${item.watchlistId}, ${item.companyId}, ${item.targetPrice || 0})" title="Edit target price">
                  <i class="bi bi-pencil-square"></i>
                </button>
              </td>
              <td>${distanceHtml}</td>
              <td><span class="positive-value">+0.00%</span></td>
              <td>${addedFormatted}</td>
              <td>
                <button type="button" class="table-action-button text-danger" onclick="removeWatchlistCompany(${item.watchlistId}, ${item.companyId}, '${escapeHtml(item.tickerSymbol)}')" title="Remove">
                  <i class="bi bi-trash3"></i>
                </button>
              </td>
            </tr>
          `;
        }).join("");
      }
    }
  } catch (err) {
    console.error("Failed to load watchlist detail:", err);
    showToast("Error loading watchlist: " + err.message, "danger");
  }
}

function renderEmptyWatchlistState() {
  const titleEl = document.getElementById("watchlistTitle");
  const headingEl = document.getElementById("currentWatchlistHeading");
  const tbody = document.getElementById("watchlistTableBody");

  if (titleEl) titleEl.textContent = "No Watchlists";
  if (headingEl) headingEl.textContent = "No Watchlists";

  const summaryWatching = document.getElementById("summaryWatching");
  const summaryAboveTarget = document.getElementById("summaryAboveTarget");
  const summaryNearTarget = document.getElementById("summaryNearTarget");

  if (summaryWatching) summaryWatching.textContent = "0";
  if (summaryAboveTarget) summaryAboveTarget.textContent = "0";
  if (summaryNearTarget) summaryNearTarget.textContent = "0";

  if (tbody) {
    tbody.innerHTML = `
      <tr>
        <td colspan="7" class="text-center py-4 text-muted">
          <i class="bi bi-folder-plus fs-4 d-block mb-2"></i>
          You have no watchlists yet. Click "New watchlist" above to get started.
        </td>
      </tr>
    `;
  }
}

window.editWatchlistTargetPrice = async function(watchlistId, companyId, currentTarget) {
  const currentVal = currentTarget && currentTarget > 0 ? currentTarget : "";
  const input = prompt("Enter new target price (৳) (or leave blank to remove target):", currentVal);
  if (input === null) return;

  const trimmed = input.trim();
  let targetPrice = null;
  if (trimmed) {
    targetPrice = parseFloat(trimmed);
    if (isNaN(targetPrice) || targetPrice <= 0) {
      showToast("Target price must be greater than zero.", "danger");
      return;
    }
  }

  try {
    const res = await apiRequest(`/watchlists/${watchlistId}/items/${companyId}`, {
      method: "PUT",
      body: JSON.stringify({ targetPrice })
    });
    showToast(res.message || "Target price updated.", "success");
    await loadWatchlistDetail(watchlistId);
  } catch (err) {
    console.error("Failed to update target price:", err);
    showToast("Cannot update target price: " + err.message, "danger");
  }
};

window.removeWatchlistCompany = async function(watchlistId, companyId, ticker) {
  const confirmed = confirm(`Remove ${ticker} from this watchlist?`);
  if (!confirmed) return;

  try {
    const res = await apiRequest(`/watchlists/${watchlistId}/items/${companyId}`, { method: "DELETE" });
    showToast(res.message || `${ticker} removed from watchlist.`, "success");
    await loadWatchlistDetail(watchlistId);
  } catch (err) {
    console.error("Failed to remove company from watchlist:", err);
    showToast("Cannot remove company: " + err.message, "danger");
  }
};

// =========================================
// TRANSACTION FORM
// =========================================

let userPortfoliosCache = [];
let allCompaniesCache = [];

async function updateAvailableShares() {
  const typeInput = document.getElementById("transactionType");
  const portfolioSelect = document.getElementById("portfolio");
  const companySelect = document.getElementById("company");
  const availableHint = document.getElementById("availableSharesHint");
  const availableVal = document.getElementById("availableSharesVal");

  if (!availableHint || !availableVal) return;

  if (typeInput?.value === "SELL" && portfolioSelect?.value && companySelect?.value) {
    try {
      const res = await apiRequest(`/transactions/available-shares?portfolioId=${portfolioSelect.value}&companyId=${companySelect.value}`);
      const shares = res.data?.availableShares || 0;
      availableVal.textContent = shares.toLocaleString();
      availableHint.classList.remove("d-none");
    } catch {
      availableHint.classList.add("d-none");
    }
  } else {
    availableHint.classList.add("d-none");
  }
}

async function setupTransactionForm() {
  const form = document.getElementById("transactionForm");
  if (!form) return;

  const formCard = document.getElementById("transactionFormCard");
  const openButton = document.getElementById("openTransactionForm");
  const closeButton = document.getElementById("closeTransactionForm");
  const cancelButton = document.getElementById("cancelTransaction");
  const titleEl = document.getElementById("transactionFormTitle");
  const subtitleEl = document.getElementById("transactionFormSubtitle");
  const idInput = document.getElementById("transactionFormId");
  const typeInput = document.getElementById("transactionType");
  const typeButtons = document.querySelectorAll(".type-button");
  const portfolioSelect = document.getElementById("portfolio");
  const companySelect = document.getElementById("company");
  const quantityInput = document.getElementById("quantity");
  const priceInput = document.getElementById("price");
  const dateInput = document.getElementById("transactionDate");
  const notesInput = document.getElementById("notes");

  setupInlineValidation(form);

  // Set default date to today
  if (dateInput && !dateInput.value) {
    dateInput.value = new Date().toISOString().split("T")[0];
  }

  // Load portfolios for dropdowns
  try {
    const res = await apiRequest("/portfolios");
    userPortfoliosCache = res.data || [];
    if (portfolioSelect) {
      portfolioSelect.innerHTML = '<option value="">Select portfolio</option>' +
        userPortfoliosCache.map(p => `<option value="${p.portfolioId}">${escapeHtml(p.portfolioName)}</option>`).join("");
    }

    const portfolioFilter = document.getElementById("transactionPortfolioFilter");
    if (portfolioFilter) {
      portfolioFilter.innerHTML = '<option value="all">All portfolios</option>' +
        userPortfoliosCache.map(p => `<option value="${p.portfolioId}">${escapeHtml(p.portfolioName)}</option>`).join("");
    }
  } catch (err) {
    console.error("Failed to load portfolios for transactions:", err);
  }

  // Load companies for dropdowns
  try {
    const res = await apiRequest("/companies");
    allCompaniesCache = res.data || [];
    if (companySelect) {
      companySelect.innerHTML = '<option value="">Select company</option>' +
        allCompaniesCache.map(c => `<option value="${c.companyId}" data-price="${c.currentPrice}">${escapeHtml(c.tickerSymbol)} - ${escapeHtml(c.companyName)}</option>`).join("");
    }

    const companyFilter = document.getElementById("transactionCompanyFilter");
    if (companyFilter) {
      companyFilter.innerHTML = '<option value="all">All companies</option>' +
        allCompaniesCache.map(c => `<option value="${c.companyId}">${escapeHtml(c.tickerSymbol)}</option>`).join("");
    }
  } catch (err) {
    console.error("Failed to load companies for transactions:", err);
  }

  // Auto-fill price & check available shares on company change
  if (companySelect) {
    companySelect.addEventListener("change", () => {
      const selected = companySelect.options[companySelect.selectedIndex];
      if (selected && selected.dataset.price && (!idInput.value || !priceInput.value)) {
        priceInput.value = selected.dataset.price;
      }
      updateAvailableShares();
    });
  }

  if (portfolioSelect) {
    portfolioSelect.addEventListener("change", () => {
      updateAvailableShares();
    });
  }

  // Toggle BUY / SELL buttons
  typeButtons.forEach((button) => {
    button.addEventListener("click", () => {
      typeButtons.forEach((item) => {
        item.classList.remove("active");
        item.setAttribute("aria-pressed", "false");
      });

      button.classList.add("active");
      button.setAttribute("aria-pressed", "true");
      typeInput.value = button.dataset.type;

      updateAvailableShares();
    });
  });

  function resetTransactionForm() {
    form.reset();
    if (idInput) idInput.value = "";
    if (titleEl) titleEl.textContent = "Add transaction";
    if (subtitleEl) subtitleEl.textContent = "Record a BUY or SELL transaction.";
    if (typeInput) typeInput.value = "BUY";

    typeButtons.forEach((button) => {
      const isBuy = button.dataset.type === "BUY";
      button.classList.toggle("active", isBuy);
      button.setAttribute("aria-pressed", isBuy ? "true" : "false");
    });

    if (dateInput) {
      dateInput.value = new Date().toISOString().split("T")[0];
    }

    const hint = document.getElementById("availableSharesHint");
    if (hint) hint.classList.add("d-none");
  }

  function closeForm() {
    if (formCard) formCard.classList.add("form-hidden");
    resetTransactionForm();
  }

  if (openButton) {
    openButton.addEventListener("click", () => {
      resetTransactionForm();
      if (formCard) {
        formCard.classList.remove("form-hidden");
        formCard.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    });
  }

  if (closeButton) closeButton.addEventListener("click", closeForm);
  if (cancelButton) cancelButton.addEventListener("click", closeForm);

  // Form submission
  form.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!validateRequiredFields(form)) {
      return;
    }

    const transactionId = idInput?.value;
    const portfolioId = parseInt(portfolioSelect.value, 10);
    const companyId = parseInt(companySelect.value, 10);
    const transactionType = typeInput.value;
    const quantity = parseFloat(quantityInput.value);
    const price = parseFloat(priceInput.value);
    const transactionDate = dateInput.value;
    const notes = notesInput?.value ? notesInput.value.trim() : null;

    if (!portfolioId || isNaN(portfolioId)) {
      setFieldError(portfolioSelect, "Please select a valid portfolio.");
      return;
    }

    if (!companyId || isNaN(companyId)) {
      setFieldError(companySelect, "Please select a valid company.");
      return;
    }

    if (isNaN(quantity) || quantity <= 0) {
      setFieldError(quantityInput, "Quantity must be greater than zero.");
      return;
    }

    if (isNaN(price) || price <= 0) {
      setFieldError(priceInput, "Price must be greater than zero.");
      return;
    }

    const submitBtn = form.querySelector("button[type='submit']");
    const originalBtnText = submitBtn ? submitBtn.innerHTML : "Save transaction";
    if (submitBtn) {
      submitBtn.disabled = true;
      submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span> Saving...';
    }

    try {
      const payload = {
        portfolioId,
        companyId,
        transactionType,
        quantity,
        price,
        transactionDate,
        notes
      };

      if (transactionId) {
        await apiRequest(`/transactions/${transactionId}`, {
          method: "PUT",
          body: JSON.stringify(payload)
        });
        showToast("Transaction updated successfully.", "success");
      } else {
        await apiRequest("/transactions", {
          method: "POST",
          body: JSON.stringify(payload)
        });
        showToast(`${transactionType} transaction for ${quantity.toLocaleString()} shares saved.`, "success");
      }

      closeForm();
      if (typeof loadTransactionHistory === "function") {
        await loadTransactionHistory();
      }
    } catch (err) {
      console.error("Transaction save failed:", err);
      showToast(err.message, "danger");
    } finally {
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = originalBtnText;
      }
    }
  });
}

// =========================================
// CHART PERIOD SELECTOR
// =========================================

function setupChartPeriod() {
  const selector = document.getElementById("chartPeriod");

  if (!selector || !portfolioChart) {
    return;
  }

  selector.addEventListener("change", () => {
    const periods = {
      "1 month": {
        labels: ["Aug 29", "Sep 05", "Sep 12", "Sep 19", "Sep 26"],
        data: [116200, 117900, 120400, 123100, 125400],
      },
      "6 months": {
        labels: ["Apr", "May", "Jun", "Jul", "Aug", "Sep"],
        data: [82000, 91000, 88000, 104000, 116000, 125400],
      },
      "1 year": {
        labels: ["Oct", "Dec", "Feb", "Apr", "Jun", "Aug", "Sep"],
        data: [72000, 76000, 80000, 82000, 88000, 116000, 125400],
      },
      "All time": {
        labels: ["2022", "2023", "2024", "2025", "2026"],
        data: [42000, 59000, 71000, 94000, 125400],
      },
    };

    const period = periods[selector.value] || periods["6 months"];
    portfolioChart.data.labels = period.labels;
    portfolioChart.data.datasets[0].data = period.data;
    portfolioChart.update();
  });
}

function exportTableCsv(table, filename = "sharesync-transactions.csv") {
  if (!table) return;
  const rows = [...table.querySelectorAll("tr")];
  const csvContent = rows
    .map((row) => {
      const cells = [...row.querySelectorAll("th, td")];
      const exportCells = cells.slice(0, cells.length > 7 ? 7 : cells.length);
      return exportCells
        .map((cell) => {
          let text = cell.innerText.replace(/"/g, '""').trim();
          return `"${text}"`;
        })
        .join(",");
    })
    .join("\r\n");

  const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.setAttribute("href", url);
  link.setAttribute("download", filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

async function loadTransactionHistory() {
  const table = document.querySelector(".transaction-table");
  const tbody = document.getElementById("transactionTableBody");
  if (!table || !tbody) return;

  const typeFilter = document.getElementById("transactionTypeFilter");
  const companyFilter = document.getElementById("transactionCompanyFilter");
  const portfolioFilter = document.getElementById("transactionPortfolioFilter");
  const countLabel = document.getElementById("transactionCountSubtitle");
  const paginationText = document.querySelector(".pagination-row span");

  const params = new URLSearchParams();
  if (portfolioFilter && portfolioFilter.value && portfolioFilter.value !== "all") {
    params.append("portfolioId", portfolioFilter.value);
  }
  if (typeFilter && typeFilter.value && typeFilter.value !== "all") {
    params.append("transactionType", typeFilter.value);
  }
  if (companyFilter && companyFilter.value && companyFilter.value !== "all") {
    params.append("companyId", companyFilter.value);
  }

  tbody.innerHTML = `
    <tr>
      <td colspan="8" class="text-center py-4 text-muted">
        <div class="spinner-border spinner-border-sm me-2" role="status"></div>
        Loading transaction records...
      </td>
    </tr>
  `;

  try {
    const queryString = params.toString() ? `?${params.toString()}` : "";
    const res = await apiRequest(`/transactions${queryString}`);
    const transactions = res.data || [];

    if (!transactions.length) {
      tbody.innerHTML = `
        <tr>
          <td colspan="8" class="text-center py-4 text-muted">
            <i class="bi bi-inbox fs-4 d-block mb-1"></i>
            No transactions found matching your criteria.
          </td>
        </tr>
      `;
      if (countLabel) countLabel.textContent = "0 transactions recorded";
      if (paginationText) paginationText.textContent = "Showing 0 of 0 transactions";
      return;
    }

    tbody.innerHTML = transactions.map((t) => {
      const isBuy = t.transactionType === "BUY";
      const badgeClass = isBuy ? "buy-badge" : "sell-badge";
      const logoText = escapeHtml((t.tickerSymbol || "SS").slice(0, 2).toUpperCase());
      const d = new Date(t.transactionDate);
      const dateFormatted = isNaN(d.getTime())
        ? escapeHtml(t.transactionDate)
        : d.toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
      const priceFormatted = "৳" + t.price.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      const totalFormatted = "৳" + (t.totalAmount || (t.quantity * t.price)).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

      return `
        <tr>
          <td>${dateFormatted}</td>
          <td>
            <div class="company-cell">
              <span class="company-logo">${logoText}</span>
              <div>
                <strong>${escapeHtml(t.tickerSymbol)}</strong>
                <small>${escapeHtml(t.companyName)}</small>
              </div>
            </div>
          </td>
          <td>
            <span class="transaction-badge ${badgeClass}">${escapeHtml(t.transactionType)}</span>
          </td>
          <td>${t.quantity.toLocaleString()}</td>
          <td>${priceFormatted}</td>
          <td>${totalFormatted}</td>
          <td>${escapeHtml(t.portfolioName || "-")}</td>
          <td>
            <button type="button" class="btn btn-sm btn-link p-0 text-primary text-decoration-none me-2" onclick="editTransaction(${t.transactionId})" title="Edit transaction">
              <i class="bi bi-pencil"></i> Edit
            </button>
            <button type="button" class="btn btn-sm btn-link p-0 text-danger text-decoration-none" onclick="deleteTransaction(${t.transactionId})" title="Delete transaction">
              <i class="bi bi-trash"></i> Delete
            </button>
          </td>
        </tr>
      `;
    }).join("");

    if (countLabel) {
      countLabel.textContent = `${transactions.length} transaction${transactions.length === 1 ? "" : "s"} recorded`;
    }
    if (paginationText) {
      paginationText.textContent = `Showing 1–${transactions.length} of ${transactions.length} transactions`;
    }
  } catch (err) {
    console.error("Failed to load transactions:", err);
    tbody.innerHTML = `
      <tr>
        <td colspan="8" class="text-center py-4 text-danger">
          <i class="bi bi-exclamation-triangle fs-4 d-block mb-1"></i>
          Error loading transactions: ${escapeHtml(err.message)}
        </td>
      </tr>
    `;
    if (countLabel) countLabel.textContent = "Error loading records";
  }
}

window.editTransaction = async function(transactionId) {
  try {
    const res = await apiRequest(`/transactions/${transactionId}`);
    const t = res.data;
    if (!t) return;

    const formCard = document.getElementById("transactionFormCard");
    const titleEl = document.getElementById("transactionFormTitle");
    const subtitleEl = document.getElementById("transactionFormSubtitle");
    const idInput = document.getElementById("transactionFormId");
    const typeInput = document.getElementById("transactionType");
    const typeButtons = document.querySelectorAll(".type-button");
    const portfolioSelect = document.getElementById("portfolio");
    const companySelect = document.getElementById("company");
    const quantityInput = document.getElementById("quantity");
    const priceInput = document.getElementById("price");
    const dateInput = document.getElementById("transactionDate");
    const notesInput = document.getElementById("notes");

    if (idInput) idInput.value = t.transactionId;
    if (titleEl) titleEl.textContent = "Edit transaction";
    if (subtitleEl) subtitleEl.textContent = `Update transaction #${t.transactionId}`;
    if (portfolioSelect) portfolioSelect.value = t.portfolioId;
    if (companySelect) companySelect.value = t.companyId;
    if (quantityInput) quantityInput.value = t.quantity;
    if (priceInput) priceInput.value = t.price;
    if (dateInput && t.transactionDate) dateInput.value = t.transactionDate.split("T")[0];
    if (notesInput) notesInput.value = t.notes || "";

    if (typeInput) typeInput.value = t.transactionType;
    typeButtons.forEach((button) => {
      const isType = button.dataset.type === t.transactionType;
      button.classList.toggle("active", isType);
      button.setAttribute("aria-pressed", isType ? "true" : "false");
    });

    if (typeof updateAvailableShares === "function") {
      updateAvailableShares();
    }

    if (formCard) {
      formCard.classList.remove("form-hidden");
      formCard.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  } catch (err) {
    console.error("Failed to load transaction for edit:", err);
    showToast("Could not load transaction details: " + err.message, "danger");
  }
};

window.deleteTransaction = async function(transactionId) {
  const confirmed = confirm("Are you sure you want to delete this transaction? This action will adjust portfolio holdings.");
  if (!confirmed) return;

  try {
    const res = await apiRequest(`/transactions/${transactionId}`, { method: "DELETE" });
    showToast(res.message || "Transaction deleted successfully.", "success");
    await loadTransactionHistory();
  } catch (err) {
    console.error("Failed to delete transaction:", err);
    showToast("Cannot delete transaction: " + err.message, "danger");
  }
};

function setupTransactionFilters() {
  const table = document.querySelector(".transaction-table");
  if (!table) {
    return;
  }

  const typeFilter = document.getElementById("transactionTypeFilter");
  const companyFilter = document.getElementById("transactionCompanyFilter");
  const portfolioFilter = document.getElementById("transactionPortfolioFilter");
  const exportButton = document.getElementById("exportTransactions");

  typeFilter?.addEventListener("change", loadTransactionHistory);
  companyFilter?.addEventListener("change", loadTransactionHistory);
  portfolioFilter?.addEventListener("change", loadTransactionHistory);
  exportButton?.addEventListener("click", () => exportTableCsv(table, "sharesync-transactions.csv"));

  // Initial load
  loadTransactionHistory();
}

function setupWatchlistFilter() {
  const filter = document.getElementById("watchlistFilter");
  const table = document.querySelector(".watchlist-table");

  if (!filter || !table) {
    return;
  }

  filter.addEventListener("change", () => {
    const rows = table.querySelectorAll("tbody tr");

    rows.forEach((row) => {
      const distance = row.querySelector(".target-distance");
      const mode = filter.value;
      const isNear = distance?.classList.contains("near-target");
      const isAbove = distance?.classList.contains("target-reached");
      const visible = mode === "all" || (mode === "near" && isNear) || (mode === "above" && isAbove);
      row.hidden = !visible;
    });
  });
}

function setupDividendFilter() {
  const filter = document.getElementById("dividendFilter");
  const table = document.querySelector(".dividend-table");

  if (!filter || !table) {
    return;
  }

  filter.addEventListener("change", () => {
    const rows = table.querySelectorAll("tbody tr");
    const currentYear = new Date().getFullYear();

    rows.forEach((row) => {
      const company = row.querySelector(".company-details strong")?.textContent.trim().toLowerCase();
      const paymentDate = row.querySelectorAll("td")[3]?.textContent.trim();
      const paymentYear = new Date(paymentDate).getFullYear();
      const mode = filter.value;
      const visible = mode === "all"
        || (mode === "company" && company)
        || (mode === "current" && paymentYear === currentYear)
        || (mode === "previous" && paymentYear === currentYear - 1);

      row.hidden = !visible;
    });
  });
}

function exportTableCsv(table, filename) {
  const headers = [...table.querySelectorAll("thead th")].map((cell) => cell.textContent.trim());
  const rows = [...table.querySelectorAll("tbody tr:not([hidden])")].map((row) =>
    [...row.querySelectorAll("td")].map((cell) => `"${cell.textContent.trim().replaceAll('"', '""')}"`),
  );
  const csv = [headers, ...rows].map((row) => row.join(",")).join("\n");
  const link = document.createElement("a");
  link.href = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
  link.download = filename;
  link.click();
  URL.revokeObjectURL(link.href);
  showToast("Transaction export downloaded.");
}

// =========================================
// SIDEBAR NAVIGATION
// =========================================

// function setupSidebarNavigation() {

//     const links = document.querySelectorAll(".sidebar-link");

//     links.forEach((link) => {

//         link.addEventListener("click", (event) => {

//             event.preventDefault();

//             links.forEach((item) => {
//                 item.classList.remove("active");
//             });

//             link.classList.add("active");

//         });

//     });

// }

function setupSidebarNavigation() {
  const sidebarLinks = document.querySelectorAll(".sidebar-link");

  sidebarLinks.forEach((link) => {
    link.addEventListener("click", () => {
      sidebarLinks.forEach((item) => {
        item.classList.remove("active");
      });

      link.classList.add("active");
    });
  });
}

// =========================================
// PORTFOLIO PERFORMANCE CHART
// =========================================

function createPortfolioChart() {
  const canvas = document.getElementById("portfolioChart");

  if (!canvas) {
    return;
  }

  const ctx = canvas.getContext("2d");

  Chart.defaults.font.family = "Inter, Segoe UI, sans-serif";

  portfolioChart = new Chart(ctx, {
    type: "line",

    data: {
      labels: ["Apr", "May", "Jun", "Jul", "Aug", "Sep"],

      datasets: [
        {
          label: "Portfolio Value",

          data: [82000, 91000, 88000, 104000, 116000, 125400],

          borderColor: "#0F172A",
          backgroundColor: "rgba(15, 23, 42, 0.035)",
          borderWidth: 2,
          pointRadius: 0,
          pointHoverRadius: 3,
          pointBackgroundColor: "#0F172A",
          pointBorderColor: "#0F172A",
          pointBorderWidth: 1,
          tension: 0.28,
          fill: true,
        },
      ],
    },

    options: {
      responsive: true,

      maintainAspectRatio: false,

      interaction: {
        intersect: false,
        mode: "index",
      },

      plugins: {
        legend: {
          display: false,
        },

        tooltip: {
          backgroundColor: "rgba(15, 23, 42, 0.96)",
          titleColor: "#F8FAFC",
          bodyColor: "#F8FAFC",
          padding: 10,
          displayColors: false,
          borderWidth: 0,
          callbacks: {
            label: function (context) {
              return "৳" + context.raw.toLocaleString();
            },
          },
        },
      },

      scales: {
        x: {
          grid: {
            display: false,
          },
          border: {
            display: false,
          },
          ticks: {
            color: "#64748B",
            font: {
              size: 10,
            },
          },
        },

        y: {
          beginAtZero: false,
          border: {
            display: false,
          },
          grid: {
            color: "rgba(148, 163, 184, 0.18)",
          },
          ticks: {
            color: "#64748B",
            font: {
              size: 10,
            },
            callback: function (value) {
              return "৳" + Number(value / 1000).toFixed(0) + "k";
            },
          },
        },
      },
    },
  });
}

// =========================================
// REPORTS & ANALYTICS
// =========================================

function setupReports() {
  const portfolioChart = document.getElementById("portfolioValueReport");

  if (!portfolioChart) {
    return;
  }

  // -----------------------------------------
  // Portfolio value chart
  // -----------------------------------------

  Chart.defaults.font.family = "Inter, Segoe UI, sans-serif";

  new Chart(portfolioChart, {
    type: "line",

    data: {
      labels: ["Apr", "May", "Jun", "Jul", "Aug", "Sep"],

      datasets: [
        {
          label: "Portfolio Value",

          data: [132000, 141500, 149800, 158600, 171200, 186450],

          borderColor: "#0F172A",
          backgroundColor: "rgba(15, 23, 42, 0.035)",

          borderWidth: 2,
          pointRadius: 0,
          pointHoverRadius: 3,
          tension: 0.35,
          fill: true,
        },
      ],
    },

    options: {
      responsive: true,

      maintainAspectRatio: false,

      interaction: {
        mode: "index",
        intersect: false,
      },

      plugins: {
        legend: {
          display: false,
        },
      },

      scales: {
        y: {
          beginAtZero: false,
          border: {
            display: false,
          },
          grid: {
            color: "rgba(148, 163, 184, 0.18)",
          },
          ticks: {
            callback: function (value) {
              return "৳" + Number(value / 1000).toFixed(0) + "k";
            },
            color: "#64748B",
            font: {
              size: 10,
            },
          },
        },

        x: {
          grid: {
            display: false,
          },
          border: {
            display: false,
          },
          ticks: {
            color: "#64748B",
            font: {
              size: 10,
            },
          },
        },
      },
    },
  });

  // -----------------------------------------
  // Asset allocation
  // -----------------------------------------

  const allocationChart = document.getElementById("allocationChart");

  if (allocationChart) {
    new Chart(allocationChart, {
      type: "doughnut",

      data: {
        labels: ["Grameenphone", "BEXIMCO", "BAT Bangladesh", "Square Pharma"],

        datasets: [
          {
            data: [32, 26, 24, 18],
            backgroundColor: ["#0F172A", "#64748B", "#94A3B8", "#DDE5EE"],
            borderWidth: 0,
            hoverOffset: 2,
          },
        ],
      },

      options: {
        responsive: true,

        maintainAspectRatio: false,

        cutout: "68%",

        plugins: {
          legend: {
            display: false,
          },
          tooltip: {
            backgroundColor: "rgba(15, 23, 42, 0.96)",
            titleColor: "#F8FAFC",
            bodyColor: "#F8FAFC",
            displayColors: false,
            padding: 10,
          },
        },
      },
    });
  }

  // -----------------------------------------
  // Transaction activity
  // -----------------------------------------

  const transactionChart = document.getElementById("transactionActivityChart");

  if (transactionChart) {
    new Chart(transactionChart, {
      type: "bar",

      data: {
        labels: ["Apr", "May", "Jun", "Jul", "Aug", "Sep"],

        datasets: [
          {
            label: "BUY",

            data: [4, 3, 5, 2, 6, 4],
            backgroundColor: "#0F172A",
            borderRadius: 3,
            borderSkipped: false,
            borderWidth: 0,
          },

          {
            label: "SELL",

            data: [1, 2, 1, 3, 1, 2],
            backgroundColor: "#DDE5EE",
            borderRadius: 3,
            borderSkipped: false,
            borderWidth: 0,
          },
        ],
      },

      options: {
        responsive: true,

        maintainAspectRatio: false,

        plugins: {
          legend: {
            position: "bottom",
            labels: {
              usePointStyle: true,
              pointStyle: "circle",
              boxWidth: 8,
            },
          },
        },

        scales: {
          y: {
            beginAtZero: true,
            border: {
              display: false,
            },
            grid: {
              color: "rgba(148, 163, 184, 0.18)",
            },
            ticks: {
              stepSize: 1,
              color: "#64748B",
            },
          },

          x: {
            grid: {
              display: false,
            },
            border: {
              display: false,
            },
            ticks: {
              color: "#64748B",
            },
          },
        },
      },
    });
  }
}

// =========================================
// PORTFOLIO MANAGEMENT
// =========================================

let currentPortfolioId = null;
let userPortfolios = [];

async function setupPortfolioManagement() {
  const holdingsBody = document.getElementById("portfolioHoldingsBody");
  if (!holdingsBody) {
    // Not on portfolio page
    return;
  }

  const portfolioSelect = document.getElementById("portfolioSelect");
  const openCreateBtn = document.getElementById("openCreatePortfolioForm");
  const openEditBtn = document.getElementById("openEditPortfolioForm");
  const deleteBtn = document.getElementById("deletePortfolioBtn");
  const formCard = document.getElementById("portfolioFormCard");
  const closeFormBtn = document.getElementById("closePortfolioForm");
  const cancelFormBtn = document.getElementById("cancelPortfolioForm");
  const form = document.getElementById("portfolioForm");
  const formTitle = document.getElementById("portfolioFormTitle");
  const formSubtitle = document.getElementById("portfolioFormSubtitle");
  const formId = document.getElementById("portfolioFormId");
  const formName = document.getElementById("portfolioFormName");
  const formDesc = document.getElementById("portfolioFormDesc");

  setupInlineValidation(form);

  function openForm(isEdit = false) {
    clearFieldError(formName);
    if (isEdit) {
      const active = userPortfolios.find(p => p.portfolioId === currentPortfolioId);
      formTitle.textContent = "Edit portfolio";
      formSubtitle.textContent = "Update portfolio name and description";
      formId.value = currentPortfolioId;
      formName.value = active ? active.portfolioName : "";
      formDesc.value = active ? (active.description || "") : "";
    } else {
      formTitle.textContent = "Create portfolio";
      formSubtitle.textContent = "Add a new portfolio to your account";
      formId.value = "";
      formName.value = "";
      formDesc.value = "";
    }
    formCard.classList.remove("form-hidden");
    formCard.scrollIntoView({ behavior: "smooth", block: "start" });
    formName.focus();
  }

  function closeForm() {
    formCard.classList.add("form-hidden");
    form.reset();
  }

  if (openCreateBtn) openCreateBtn.addEventListener("click", () => openForm(false));
  if (openEditBtn) openEditBtn.addEventListener("click", () => openForm(true));
  if (closeFormBtn) closeFormBtn.addEventListener("click", closeForm);
  if (cancelFormBtn) cancelFormBtn.addEventListener("click", closeForm);

  if (portfolioSelect) {
    portfolioSelect.addEventListener("change", (e) => {
      const selectedId = parseInt(e.target.value, 10);
      if (selectedId && selectedId !== currentPortfolioId) {
        currentPortfolioId = selectedId;
        loadPortfolioDetail(currentPortfolioId);
      }
    });
  }

  if (deleteBtn) {
    deleteBtn.addEventListener("click", async () => {
      if (!currentPortfolioId) return;
      const active = userPortfolios.find(p => p.portfolioId === currentPortfolioId);
      const name = active ? active.portfolioName : "this portfolio";

      if (!confirm(`Are you sure you want to delete portfolio "${name}"?`)) {
        return;
      }

      try {
        await apiRequest(`/portfolios/${currentPortfolioId}`, { method: "DELETE" });
        showToast(`Portfolio "${name}" deleted successfully.`, "success");
        currentPortfolioId = null;
        await loadUserPortfolios();
      } catch (err) {
        showToast(err.message, "danger");
      }
    });
  }

  if (form) {
    form.addEventListener("submit", async (e) => {
      e.preventDefault();
      if (!validateRequiredFields(form)) return;

      const idVal = formId.value;
      const payload = {
        portfolioName: formName.value.trim(),
        description: formDesc.value.trim() || null
      };

      try {
        if (idVal) {
          // Update
          const res = await apiRequest(`/portfolios/${idVal}`, {
            method: "PUT",
            body: JSON.stringify(payload)
          });
          showToast(`Portfolio "${res.data.portfolioName}" updated successfully.`, "success");
          closeForm();
          await loadUserPortfolios(parseInt(idVal, 10));
        } else {
          // Create
          const res = await apiRequest("/portfolios", {
            method: "POST",
            body: JSON.stringify(payload)
          });
          showToast(`Portfolio "${res.data.portfolioName}" created successfully.`, "success");
          closeForm();
          await loadUserPortfolios(res.data.portfolioId);
        }
      } catch (err) {
        showToast(err.message, "danger");
      }
    });
  }

  await loadUserPortfolios();
}

async function loadUserPortfolios(selectId = null) {
  const portfolioSelect = document.getElementById("portfolioSelect");
  try {
    const res = await apiRequest("/portfolios");
    userPortfolios = res.data || [];

    if (portfolioSelect) {
      portfolioSelect.innerHTML = "";
      userPortfolios.forEach(p => {
        const opt = document.createElement("option");
        opt.value = p.portfolioId;
        opt.textContent = p.portfolioName;
        portfolioSelect.appendChild(opt);
      });

      if (userPortfolios.length > 1) {
        portfolioSelect.classList.remove("d-none");
      } else {
        portfolioSelect.classList.add("d-none");
      }
    }

    if (userPortfolios.length > 0) {
      if (selectId && userPortfolios.some(p => p.portfolioId === selectId)) {
        currentPortfolioId = selectId;
      } else if (!currentPortfolioId || !userPortfolios.some(p => p.portfolioId === currentPortfolioId)) {
        currentPortfolioId = userPortfolios[0].portfolioId;
      }
      if (portfolioSelect) portfolioSelect.value = currentPortfolioId;
      await loadPortfolioDetail(currentPortfolioId);
    } else {
      renderEmptyPortfolioState();
    }
  } catch (err) {
    console.error("Failed to load user portfolios:", err);
    showToast("Could not load portfolios: " + err.message, "danger");
  }
}

async function loadPortfolioDetail(portfolioId) {
  const titleEl = document.getElementById("portfolioTitle");
  const descEl = document.getElementById("portfolioDescription");
  const totalValEl = document.getElementById("portfolioTotalValue");
  const profitLossEl = document.getElementById("portfolioProfitLoss");
  const profitLossPctEl = document.getElementById("portfolioProfitLossPct");
  const totalInvestedEl = document.getElementById("portfolioTotalInvested");
  const holdingsCountEl = document.getElementById("portfolioHoldingsCount");
  const holdingsBody = document.getElementById("portfolioHoldingsBody");

  const metricTotalBuyCost = document.getElementById("metricTotalBuyCost");
  const metricMarketValue = document.getElementById("metricMarketValue");
  const metricUnrealizedProfit = document.getElementById("metricUnrealizedProfit");
  const metricReturnPercentage = document.getElementById("metricReturnPercentage");

  try {
    const res = await apiRequest(`/portfolios/${portfolioId}`);
    const d = res.data;

    if (titleEl) titleEl.textContent = d.portfolioName;
    if (descEl) descEl.textContent = d.description || "Track your holdings and investment performance.";

    const formattedValue = "৳" + d.totalValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const formattedInvested = "৳" + d.totalInvested.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const isProfitable = d.unrealizedProfitLoss >= 0;
    const sign = isProfitable ? "+" : "-";
    const absPL = Math.abs(d.unrealizedProfitLoss).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const formattedPL = `${sign}৳${absPL}`;
    const formattedPLPct = `${sign}${Math.abs(d.unrealizedProfitLossPercentage).toFixed(1)}%`;

    if (totalValEl) totalValEl.textContent = formattedValue;
    if (profitLossEl) {
      profitLossEl.textContent = formattedPL;
      profitLossEl.className = isProfitable ? "positive-text" : "negative-text";
    }
    if (profitLossPctEl) {
      profitLossPctEl.textContent = formattedPLPct;
      profitLossPctEl.className = isProfitable ? "positive-text" : "negative-text";
    }
    if (totalInvestedEl) totalInvestedEl.textContent = formattedInvested;
    if (holdingsCountEl) holdingsCountEl.textContent = d.holdingsCount;

    // Bottom metrics
    if (metricTotalBuyCost) metricTotalBuyCost.textContent = formattedInvested;
    if (metricMarketValue) metricMarketValue.textContent = formattedValue;
    if (metricUnrealizedProfit) {
      metricUnrealizedProfit.textContent = formattedPL;
      metricUnrealizedProfit.className = isProfitable ? "positive-text" : "negative-text";
    }
    if (metricReturnPercentage) {
      metricReturnPercentage.textContent = formattedPLPct;
      metricReturnPercentage.className = isProfitable ? "positive-text" : "negative-text";
    }

    // Holdings Table
    if (holdingsBody) {
      if (!d.holdings || d.holdings.length === 0) {
        holdingsBody.innerHTML = `
          <tr>
            <td colspan="6" class="text-center py-4 text-muted">
              <i class="bi bi-inbox fs-4 d-block mb-1"></i>
              No stock holdings in this portfolio yet.
            </td>
          </tr>
        `;
      } else {
        holdingsBody.innerHTML = d.holdings.map(h => {
          const hProfitable = h.unrealizedProfitLoss >= 0;
          const hSign = hProfitable ? "+" : "-";
          const hReturnStr = `${hSign}${Math.abs(h.returnPercentage).toFixed(1)}%`;
          const logoText = escapeHtml(h.tickerSymbol.slice(0, 2).toUpperCase());

          return `
            <tr>
              <td>
                <div class="company-cell">
                  <span class="company-logo">${logoText}</span>
                  <div>
                    <strong>${escapeHtml(h.tickerSymbol)}</strong>
                    <small>${escapeHtml(h.companyName)}</small>
                  </div>
                </div>
              </td>
              <td>${h.shares.toLocaleString()}</td>
              <td>৳${h.averageBuyPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
              <td>৳${h.currentPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
              <td>৳${h.marketValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
              <td class="${hProfitable ? 'positive-text' : 'negative-text'}">${hReturnStr}</td>
            </tr>
          `;
        }).join("");
      }
    }
  } catch (err) {
    console.error("Failed to load portfolio detail:", err);
    showToast("Error loading portfolio details: " + err.message, "danger");
  }
}

function renderEmptyPortfolioState() {
  const titleEl = document.getElementById("portfolioTitle");
  const totalValEl = document.getElementById("portfolioTotalValue");
  const holdingsBody = document.getElementById("portfolioHoldingsBody");
  if (titleEl) titleEl.textContent = "No Portfolios";
  if (totalValEl) totalValEl.textContent = "৳0.00";
  if (holdingsBody) {
    holdingsBody.innerHTML = `
      <tr>
        <td colspan="6" class="text-center py-4 text-muted">
          <i class="bi bi-folder-plus fs-4 d-block mb-2"></i>
          You have no portfolios yet. Click "New portfolio" above to get started.
        </td>
      </tr>
    `;
  }
}

