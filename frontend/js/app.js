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

function getAuthToken() {
  return localStorage.getItem("sharesync_token");
}

function getCurrentUser() {
  try {
    const raw = localStorage.getItem("sharesync_user");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function isAuthPage() {
  const path = (window.location.pathname || "").toLowerCase();
  return path.endsWith("login.html") || path.endsWith("register.html");
}

function checkAuthProtection() {
  const token = getAuthToken();
  if (isAuthPage()) {
    if (token) {
      window.location.href = "index.html";
    }
  } else {
    if (!token) {
      window.location.href = "login.html";
    }
  }
}

async function ensureAuthenticated() {
  const token = getAuthToken();
  if (token) return token;
  if (!isAuthPage()) {
    window.location.href = "login.html";
  }
  return null;
}

function sanitizeErrorMessage(msg, status) {
  if (!msg || typeof msg !== "string") {
    if (status === 401) return "Session expired or invalid. Please sign in again.";
    if (status === 403) return "You do not have permission to access this resource.";
    if (status === 404) return "The requested record was not found.";
    if (status === 409) return "A record with this identifier already exists.";
    return "An unexpected error occurred. Please try again.";
  }
  const lower = msg.toLowerCase();
  if (
    lower.includes("ora-") ||
    lower.includes("oracle") ||
    lower.includes("exception") ||
    lower.includes("stack trace") ||
    lower.includes("connection string") ||
    lower.includes("data source") ||
    lower.includes("internal server error")
  ) {
    return "A server processing error occurred. Please try again later.";
  }
  return msg;
}

async function apiRequest(endpoint, options = {}) {
  const token = getAuthToken();
  const isAuthReq = endpoint.startsWith("/auth/login") || endpoint.startsWith("/auth/register");
  if (!token && !isAuthReq && !isAuthPage()) {
    window.location.href = "login.html";
    throw new Error("Authentication required.");
  }

  const headers = {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options.headers || {})
  };

  let response;
  try {
    response = await fetch(`${API_BASE_URL}${endpoint}`, {
      ...options,
      headers
    });
  } catch (netErr) {
    const error = new Error("Network error. Unable to reach ShareSync API server. Ensure the backend is active.");
    error.status = 0;
    throw error;
  }

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    if ((response.status === 401 || response.status === 403) && !isAuthReq) {
      localStorage.removeItem("sharesync_token");
      localStorage.removeItem("sharesync_user");
      if (!isAuthPage()) {
        window.location.href = "login.html";
      }
    }
    const rawMsg = data?.message || (data?.errors ? (Array.isArray(data.errors) ? data.errors.join(", ") : String(data.errors)) : "An error occurred.");
    const safeMsg = sanitizeErrorMessage(rawMsg, response.status);
    const error = new Error(safeMsg);
    error.status = response.status;
    error.data = data;
    throw error;
  }

  return data;
}

function setupUserHeader() {
  const user = getCurrentUser();
  if (!user) return;

  const headerUserName = document.getElementById("headerUserName");
  const headerAvatar = document.getElementById("headerAvatar");

  const displayName = user.name || user.email || "Investor";
  const firstName = displayName.trim().split(" ")[0];

  if (headerUserName) headerUserName.textContent = firstName;
  if (headerAvatar) headerAvatar.textContent = firstName.charAt(0).toUpperCase();
}

function setupLoginForm() {
  const form = document.getElementById("loginForm");
  if (!form) return;

  const emailInput = document.getElementById("loginEmail");
  const passwordInput = document.getElementById("loginPassword");
  const submitBtn = document.getElementById("loginBtn");
  const alertEl = document.getElementById("authAlert");
  const alertText = document.getElementById("authAlertText");

  const showAlert = (message, tone = "danger") => {
    if (!alertEl || !alertText) return;
    alertEl.className = `auth-alert auth-alert-${tone} show`;
    alertText.textContent = message;
  };

  const hideAlert = () => {
    if (!alertEl) return;
    alertEl.className = "auth-alert";
  };

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    hideAlert();

    const email = (emailInput?.value || "").trim();
    const password = (passwordInput?.value || "");

    if (!email) {
      showAlert("Please enter your email address.");
      emailInput?.focus();
      return;
    }
    if (!password) {
      showAlert("Please enter your password.");
      passwordInput?.focus();
      return;
    }

    const originalBtnHtml = submitBtn.innerHTML;
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Signing in...';

    try {
      const res = await fetch(`${API_BASE_URL}/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password })
      });
      const data = await res.json().catch(() => null);

      if (!res.ok || !data?.success) {
        const errorMsg = data?.message || (data?.errors ? (Array.isArray(data.errors) ? data.errors.join(", ") : String(data.errors)) : "Invalid email or password.");
        showAlert(sanitizeErrorMessage(errorMsg, res.status), "danger");
        submitBtn.disabled = false;
        submitBtn.innerHTML = originalBtnHtml;
        return;
      }

      localStorage.setItem("sharesync_token", data.data.token);
      localStorage.setItem("sharesync_user", JSON.stringify(data.data));

      showAlert("Sign in successful! Redirecting...", "success");
      setTimeout(() => {
        window.location.href = "index.html";
      }, 300);
    } catch (err) {
      showAlert("Unable to connect to the backend server. Please check your connection.", "danger");
      submitBtn.disabled = false;
      submitBtn.innerHTML = originalBtnHtml;
    }
  });
}

function setupRegisterForm() {
  const form = document.getElementById("registerForm");
  if (!form) return;

  const nameInput = document.getElementById("registerName");
  const emailInput = document.getElementById("registerEmail");
  const passwordInput = document.getElementById("registerPassword");
  const confirmPasswordInput = document.getElementById("registerConfirmPassword");
  const submitBtn = document.getElementById("registerBtn");
  const alertEl = document.getElementById("authAlert");
  const alertText = document.getElementById("authAlertText");

  const showAlert = (message, tone = "danger") => {
    if (!alertEl || !alertText) return;
    alertEl.className = `auth-alert auth-alert-${tone} show`;
    alertText.textContent = message;
  };

  const hideAlert = () => {
    if (!alertEl) return;
    alertEl.className = "auth-alert";
  };

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    hideAlert();

    const name = (nameInput?.value || "").trim();
    const email = (emailInput?.value || "").trim();
    const password = (passwordInput?.value || "");
    const confirmPassword = (confirmPasswordInput?.value || "");

    if (!name || name.length < 2) {
      showAlert("Full name must be at least 2 characters.");
      nameInput?.focus();
      return;
    }
    if (!email || !email.includes("@")) {
      showAlert("Please enter a valid email address.");
      emailInput?.focus();
      return;
    }
    if (!password || password.length < 6) {
      showAlert("Password must be at least 6 characters long.");
      passwordInput?.focus();
      return;
    }
    if (password !== confirmPassword) {
      showAlert("Passwords do not match.");
      confirmPasswordInput?.focus();
      return;
    }

    const originalBtnHtml = submitBtn.innerHTML;
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Creating account...';

    try {
      const res = await fetch(`${API_BASE_URL}/auth/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, email, password })
      });
      const data = await res.json().catch(() => null);

      if (!res.ok || !data?.success) {
        const errorMsg = data?.message || (data?.errors ? (Array.isArray(data.errors) ? data.errors.join(", ") : String(data.errors)) : "Registration failed.");
        showAlert(sanitizeErrorMessage(errorMsg, res.status), "danger");
        submitBtn.disabled = false;
        submitBtn.innerHTML = originalBtnHtml;
        return;
      }

      localStorage.setItem("sharesync_token", data.data.token);
      localStorage.setItem("sharesync_user", JSON.stringify(data.data));

      showAlert("Account created successfully! Redirecting...", "success");
      setTimeout(() => {
        window.location.href = "index.html";
      }, 500);
    } catch (err) {
      showAlert("Unable to connect to the backend server. Please check your connection.", "danger");
      submitBtn.disabled = false;
      submitBtn.innerHTML = originalBtnHtml;
    }
  });
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

function setupSidebarNavigation() {
  const sidebarLinks = document.querySelectorAll(".sidebar-link");
  const currentPath = (window.location.pathname || "").toLowerCase().split("/").pop() || "index.html";

  sidebarLinks.forEach((link) => {
    const href = (link.getAttribute("href") || "").toLowerCase().split("/").pop();
    const text = (link.textContent || "").trim().toLowerCase();
    const hasLogoutIcon = !!link.querySelector(".bi-box-arrow-right");

    if (href && (href === currentPath || (currentPath === "" && href === "index.html"))) {
      link.classList.add("active");
    } else if (href && href !== "#") {
      link.classList.remove("active");
    }

    if (text === "log out" || hasLogoutIcon) {
      link.addEventListener("click", async (e) => {
        e.preventDefault();
        try {
          await apiRequest("/auth/logout", { method: "POST" });
        } catch {
          // ignore logout network errors
        } finally {
          localStorage.removeItem("sharesync_token");
          localStorage.removeItem("sharesync_user");
          window.location.href = "login.html";
        }
      });
    }
  });
}

// =========================================
// DASHBOARD MANAGEMENT
// =========================================

function formatBDT(amount) {
  const num = Number(amount) || 0;
  return "৳" + num.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function renderDashboardError(message) {
  const valueEl = document.getElementById("dashboardPortfolioValue");
  if (valueEl) valueEl.textContent = "৳0.00";
  const bodyEl = document.getElementById("dashboardHoldingsBody");
  if (bodyEl) {
    bodyEl.innerHTML = `
      <tr>
        <td colspan="5" class="text-center py-4 text-danger">
          <i class="bi bi-exclamation-triangle fs-4 d-block mb-1"></i>
          ${escapeHtml(message)}
        </td>
      </tr>
    `;
  }
  const activityEl = document.getElementById("dashboardActivityList");
  if (activityEl) {
    activityEl.innerHTML = `
      <div class="text-center py-4 text-danger">
        <i class="bi bi-exclamation-triangle fs-4 d-block mb-1"></i>
        ${escapeHtml(message)}
      </div>
    `;
  }
}

async function setupDashboard() {
  const dashboardValueEl = document.getElementById("dashboardPortfolioValue");
  if (!dashboardValueEl) return;

  // Personalized greeting based on time of day
  const userStr = localStorage.getItem("sharesync_user");
  let cachedUser = null;
  if (userStr) {
    try { cachedUser = JSON.parse(userStr); } catch (_) {}
  }
  let firstName = cachedUser?.firstName || "Investor";
  const nowHour = new Date().getHours();
  let greetingPrefix = "Good morning";
  if (nowHour >= 12 && nowHour < 18) greetingPrefix = "Good afternoon";
  else if (nowHour >= 18) greetingPrefix = "Good evening";

  const greetingEl = document.getElementById("dashboardGreeting");
  if (greetingEl) greetingEl.textContent = `${greetingPrefix}, ${firstName}.`;

  const headerAvatar = document.getElementById("headerAvatar");
  if (headerAvatar) headerAvatar.textContent = firstName.charAt(0).toUpperCase();

  const headerUserName = document.getElementById("headerUserName");
  if (headerUserName) headerUserName.textContent = firstName;

  // Chart period selector handling
  const periodSelect = document.getElementById("chartPeriod");
  if (periodSelect && !periodSelect.dataset.initialized) {
    periodSelect.dataset.initialized = "true";
    periodSelect.addEventListener("change", (e) => {
      const period = e.target.value;
      const subTitle = document.getElementById("dashboardChartSubtitle");
      if (subTitle) {
        const labels = {
          "1m": "Portfolio value over the last 1 month",
          "6m": "Portfolio value over the last 6 months",
          "1y": "Portfolio value over the last 1 year",
          "all": "Portfolio value all time"
        };
        subTitle.textContent = labels[period] || `Portfolio value (${period})`;
      }
      loadDashboardData(period);
    });
  }

  async function loadDashboardData(period = "6m") {
    try {
      const res = await apiRequest(`/dashboard?period=${encodeURIComponent(period)}`);
      if (!res || !res.success || !res.data) {
        throw new Error(res?.message || "Failed to load dashboard data");
      }
      const d = res.data;

      // Update name if returned from backend
      if (d.userFullName && d.userFullName.trim()) {
        const parts = d.userFullName.trim().split(" ");
        firstName = parts[0];
        if (greetingEl) greetingEl.textContent = `${greetingPrefix}, ${firstName}.`;
        if (headerAvatar) headerAvatar.textContent = firstName.charAt(0).toUpperCase();
        if (headerUserName) headerUserName.textContent = firstName;
      }

      // 1. Summary Cards
      const summary = d.summary || {};
      dashboardValueEl.textContent = formatBDT(summary.totalPortfolioValue);

      const totalInvestedEl = document.getElementById("dashboardTotalInvested");
      if (totalInvestedEl) totalInvestedEl.textContent = formatBDT(summary.totalInvested);

      const holdingsMetaEl = document.getElementById("dashboardHoldingsMeta");
      if (holdingsMetaEl) {
        const count = summary.holdingsCount || 0;
        holdingsMetaEl.textContent = `Across ${count} ${count === 1 ? 'holding' : 'holdings'}`;
      }

      const unrealizedPLEl = document.getElementById("dashboardUnrealizedPL");
      if (unrealizedPLEl) {
        const pl = Number(summary.totalUnrealizedProfitLoss) || 0;
        const isUp = pl >= 0;
        unrealizedPLEl.textContent = (isUp ? "+" : "-") + formatBDT(Math.abs(pl));
        unrealizedPLEl.className = `summary-value ${isUp ? 'positive-text' : 'negative-text'}`;
      }

      const returnMetaEl = document.getElementById("dashboardReturnMeta");
      if (returnMetaEl) {
        const retPct = Number(summary.profitLossPercentage) || 0;
        const isUp = retPct >= 0;
        const icon = retPct > 0 ? "bi-arrow-up" : (retPct < 0 ? "bi-arrow-down" : "bi-dash");
        returnMetaEl.className = `summary-meta ${isUp ? 'positive' : 'negative'}`;
        returnMetaEl.innerHTML = `<i class="bi ${icon}"></i> <span>${isUp ? '+' : ''}${retPct.toFixed(1)}% return</span>`;
      }

      const dividendIncomeEl = document.getElementById("dashboardDividendIncome");
      if (dividendIncomeEl) dividendIncomeEl.textContent = formatBDT(summary.totalDividendIncome);

      const portfolioMetaEl = document.getElementById("dashboardPortfolioMeta");
      if (portfolioMetaEl) {
        const perf = d.performance || {};
        if (perf.startingValue > 0) {
          const perfPct = Number(perf.netChangePercentage) || 0;
          const isUp = perfPct >= 0;
          portfolioMetaEl.className = `summary-meta ${isUp ? 'positive' : 'negative'}`;
          portfolioMetaEl.innerHTML = `<i class="bi bi-arrow-${isUp ? 'up' : 'down'}"></i> <span>${isUp ? '+' : ''}${perfPct.toFixed(1)}% this period</span>`;
        } else {
          portfolioMetaEl.className = "summary-meta neutral";
          const pCount = summary.portfoliosCount || 0;
          portfolioMetaEl.innerHTML = `<span>${pCount} ${pCount === 1 ? 'portfolio' : 'portfolios'}</span>`;
        }
      }

      // 2. Chart.js Line Chart
      const canvas = document.getElementById("portfolioChart");
      if (canvas && typeof Chart !== "undefined") {
        if (portfolioChart) {
          portfolioChart.destroy();
          portfolioChart = null;
        }

        const perf = d.performance || {};
        const labels = perf.labels && perf.labels.length > 0 ? perf.labels : ["Current"];
        const values = perf.values && perf.values.length > 0 ? perf.values : [summary.totalPortfolioValue || 0];

        const isUp = (perf.netChange || 0) >= 0;
        const strokeColor = isUp ? "#10b981" : "#ef4444";
        const bgGradColor = isUp ? "rgba(16, 185, 129, 0.15)" : "rgba(239, 68, 68, 0.15)";

        const ctx = canvas.getContext("2d");
        const gradient = ctx.createLinearGradient(0, 0, 0, 200);
        gradient.addColorStop(0, bgGradColor);
        gradient.addColorStop(1, "rgba(255, 255, 255, 0)");

        portfolioChart = new Chart(ctx, {
          type: "line",
          data: {
            labels: labels,
            datasets: [{
              label: "Portfolio Value",
              data: values,
              borderColor: strokeColor,
              borderWidth: 2.5,
              backgroundColor: gradient,
              fill: true,
              tension: 0.35,
              pointRadius: values.length > 30 ? 0 : 3,
              pointHoverRadius: 6,
              pointBackgroundColor: strokeColor,
              pointBorderColor: "#ffffff",
              pointBorderWidth: 2
            }]
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
              intersect: false,
              mode: "index"
            },
            plugins: {
              legend: { display: false },
              tooltip: {
                backgroundColor: "#0f172a",
                titleFont: { size: 12 },
                bodyFont: { size: 12 },
                padding: 10,
                cornerRadius: 6,
                displayColors: false,
                callbacks: {
                  label: context => "Value: ৳" + Number(context.parsed.y).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                }
              }
            },
            scales: {
              x: {
                grid: { display: false },
                ticks: {
                  font: { size: 11 },
                  color: "#64748b",
                  maxTicksLimit: 7
                }
              },
              y: {
                grid: { color: "rgba(226, 232, 240, 0.7)" },
                ticks: {
                  font: { size: 11 },
                  color: "#64748b",
                  callback: value => "৳" + (value >= 1000 ? (value / 1000).toFixed(0) + "k" : value)
                }
              }
            }
          }
        });
      }

      // 3. Sector Allocation Donut
      const donut = document.getElementById("dashboardDonut");
      const holdingsCountEl = document.getElementById("dashboardHoldingsCount");
      const holdingsLabelEl = document.getElementById("dashboardHoldingsLabel");
      const allocListEl = document.getElementById("dashboardAllocationList");

      if (holdingsCountEl) holdingsCountEl.textContent = summary.holdingsCount || 0;
      if (holdingsLabelEl) holdingsLabelEl.textContent = summary.holdingsCount === 1 ? "holding" : "holdings";

      const sectors = d.sectorAllocation || [];
      const palette = ["#2563eb", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899", "#06b6d4", "#6366f1", "#14b8a6", "#64748b"];

      if (allocListEl) {
        if (sectors.length === 0) {
          if (donut) donut.style.background = "#e2e8f0";
          allocListEl.innerHTML = `<div class="text-center text-muted py-3">No sector allocations yet.</div>`;
        } else {
          let gradientParts = [];
          let currentPct = 0;
          let rowsHtml = "";

          sectors.forEach((sec, idx) => {
            const color = palette[idx % palette.length];
            const nextPct = currentPct + Number(sec.allocationPercentage || 0);
            gradientParts.push(`${color} ${currentPct.toFixed(1)}% ${nextPct.toFixed(1)}%`);
            currentPct = nextPct;

            rowsHtml += `
              <div class="allocation-row">
                <span class="allocation-company">
                  <span class="allocation-dot" style="background-color: ${color};"></span>
                  ${escapeHtml(sec.sectorName)}
                </span>
                <strong>${Number(sec.allocationPercentage || 0).toFixed(1)}%</strong>
              </div>
            `;
          });

          if (donut) {
            donut.style.background = `conic-gradient(${gradientParts.join(", ")})`;
          }
          allocListEl.innerHTML = rowsHtml;
        }
      }

      // 4. Top Holdings Table
      const holdingsBody = document.getElementById("dashboardHoldingsBody");
      if (holdingsBody) {
        const topHoldings = d.topHoldings || [];
        if (topHoldings.length === 0) {
          holdingsBody.innerHTML = `
            <tr>
              <td colspan="5" class="text-center py-4 text-muted">
                <i class="bi bi-inbox fs-4 d-block mb-1"></i>
                No holdings in portfolio yet. Click "Add transaction" above to begin.
              </td>
            </tr>
          `;
        } else {
          holdingsBody.innerHTML = topHoldings.map(h => {
            const isUp = (h.unrealizedProfitLoss || 0) >= 0;
            const logo = escapeHtml((h.tickerSymbol || "SH").slice(0, 2).toUpperCase());
            const sign = isUp ? "+" : "";
            return `
              <tr>
                <td>
                  <div class="company-cell">
                    <span class="company-logo">${logo}</span>
                    <div>
                      <strong>${escapeHtml(h.tickerSymbol)}</strong>
                      <small>${escapeHtml(h.companyName)}</small>
                    </div>
                  </div>
                </td>
                <td>${Number(h.shares).toLocaleString()}</td>
                <td>${formatBDT(h.averageBuyPrice)}</td>
                <td>${formatBDT(h.currentPrice)}</td>
                <td class="${isUp ? 'positive-text' : 'negative-text'}">${sign}${Number(h.returnPercentage || 0).toFixed(1)}%</td>
              </tr>
            `;
          }).join("");
        }
      }

      // 5. Recent Activity List
      const activityList = document.getElementById("dashboardActivityList");
      if (activityList) {
        const txs = d.recentTransactions || [];
        if (txs.length === 0) {
          activityList.innerHTML = `
            <div class="text-center py-4 text-muted">
              <i class="bi bi-clock-history fs-4 d-block mb-1"></i>
              No recent transaction activity.
            </div>
          `;
        } else {
          activityList.innerHTML = txs.map(tx => {
            const isBuy = (tx.transactionType || "").toUpperCase() === "BUY";
            const icon = isBuy ? "bi-arrow-down-left" : "bi-arrow-up-right";
            const iconClass = isBuy ? "activity-icon buy" : "activity-icon sell";
            const amountClass = isBuy ? "activity-amount" : "activity-amount positive-text";
            const sign = isBuy ? "-৳" : "+৳";
            const dateStr = new Date(tx.transactionDate).toLocaleDateString("en-US", {
              month: "short",
              day: "numeric",
              year: "numeric"
            });

            return `
              <div class="activity-item">
                <div class="${iconClass}">
                  <i class="bi ${icon}"></i>
                </div>
                <div class="activity-info">
                  <strong>${isBuy ? 'Bought' : 'Sold'} ${escapeHtml(tx.tickerSymbol)}</strong>
                  <span>${Number(tx.quantity).toLocaleString()} shares · ${dateStr}</span>
                </div>
                <strong class="${amountClass}">${sign}${Number(tx.totalValue).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong>
              </div>
            `;
          }).join("");
        }
      }

    } catch (err) {
      console.error("Failed to load dashboard data:", err);
      renderDashboardError("Unable to load dashboard data. Please verify your connection.");
    }
  }

  window.refreshDashboard = loadDashboardData;
  const initialPeriod = periodSelect ? periodSelect.value || "6m" : "6m";
  await loadDashboardData(initialPeriod);
}

// =========================================
// DSE LIVE PRICE SYNCHRONIZATION
// =========================================

function setupDseSync() {
  const syncBtns = document.querySelectorAll("#syncDseBtn");
  if (!syncBtns.length) return;

  syncBtns.forEach((btn) => {
    btn.addEventListener("click", async (e) => {
      e.preventDefault();
      const originalHtml = btn.innerHTML;
      btn.disabled = true;
      btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span> Syncing DSE...';
      try {
        const res = await apiRequest("/companies/sync-prices", { method: "POST" });
        showToast(res?.message || "DSE live prices synchronized successfully!", "success");

        const updatedEl = document.querySelector(".last-updated");
        if (updatedEl) {
          const nowTime = new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
          updatedEl.textContent = `DSE Live · ${nowTime}`;
        }

        // 1. Refresh Dashboard if on dashboard
        if (typeof window.refreshDashboard === "function") {
          const periodSelect = document.getElementById("chartPeriod");
          await window.refreshDashboard(periodSelect ? periodSelect.value || "6m" : "6m");
        }

        // 2. Refresh Portfolio if on portfolio page
        if (typeof loadUserPortfolios === "function" && document.getElementById("portfolioTotalValue")) {
          const activeId = typeof currentPortfolioId !== "undefined" ? currentPortfolioId : null;
          await loadUserPortfolios(activeId);
        }

        // 3. Refresh Watchlist if on watchlist page
        if (typeof loadWatchlistDetail === "function" && document.getElementById("watchlistTableBody")) {
          const wlSelect = document.getElementById("watchlistSelect");
          if (wlSelect && wlSelect.value) {
            await loadWatchlistDetail(parseInt(wlSelect.value, 10));
          }
        }
      } catch (err) {
        console.error("DSE price sync error:", err);
        showToast("DSE sync: " + (err.message || "Unable to sync prices"), "danger");
      } finally {
        btn.disabled = false;
        btn.innerHTML = originalHtml;
      }
    });
  });
}

// =========================================
// THEME MANAGEMENT (LIGHT / DARK / SYSTEM)
// =========================================

function getSystemTheme() {
  return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function getStoredThemePreference() {
  return localStorage.getItem("sharesync_theme") || "system";
}

function updateChartsForTheme(theme) {
  const isDark = theme === "dark";
  const gridColor = isDark ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.06)";
  const textColor = isDark ? "#94A3B8" : "#6B7280";

  if (typeof Chart !== "undefined") {
    Chart.defaults.color = textColor;
    Chart.defaults.borderColor = gridColor;
  }

  const chartsToUpdate = [
    typeof portfolioChart !== "undefined" ? portfolioChart : null,
    typeof reportValueChartInstance !== "undefined" ? reportValueChartInstance : null,
    typeof reportActivityChartInstance !== "undefined" ? reportActivityChartInstance : null
  ].filter(Boolean);

  chartsToUpdate.forEach((chart) => {
    try {
      if (chart.options?.scales?.x) {
        if (chart.options.scales.x.ticks) chart.options.scales.x.ticks.color = textColor;
        if (chart.options.scales.x.grid) chart.options.scales.x.grid.color = gridColor;
      }
      if (chart.options?.scales?.y) {
        if (chart.options.scales.y.ticks) chart.options.scales.y.ticks.color = textColor;
        if (chart.options.scales.y.grid) chart.options.scales.y.grid.color = gridColor;
      }
      chart.update();
    } catch (_) {}
  });
}

function applyTheme(themeChoice) {
  const effectiveTheme = themeChoice === "system" ? getSystemTheme() : themeChoice;
  document.documentElement.setAttribute("data-theme", effectiveTheme);

  const toggleBtns = document.querySelectorAll(".theme-toggle-btn, #themeToggleBtn");
  toggleBtns.forEach((btn) => {
    const icon = btn.querySelector("i");
    if (icon) {
      if (effectiveTheme === "dark") {
        icon.className = "bi bi-sun";
        btn.setAttribute("title", "Switch to Light Mode");
      } else {
        icon.className = "bi bi-moon-stars";
        btn.setAttribute("title", "Switch to Dark Mode");
      }
    }
  });

  const themeCards = document.querySelectorAll(".theme-card");
  themeCards.forEach((card) => {
    const cardTheme = card.dataset.theme;
    const badge = card.querySelector(".theme-card-badge");
    if (cardTheme === themeChoice) {
      card.classList.add("active");
      if (badge) badge.textContent = "Active";
    } else {
      card.classList.remove("active");
      if (badge) badge.textContent = "Select";
    }
  });

  updateChartsForTheme(effectiveTheme);
}

function setThemePreference(newChoice) {
  localStorage.setItem("sharesync_theme", newChoice);
  applyTheme(newChoice);
}

function initThemeSystem() {
  const currentPref = getStoredThemePreference();
  applyTheme(currentPref);

  if (window.matchMedia) {
    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => {
      if (getStoredThemePreference() === "system") {
        applyTheme("system");
      }
    });
  }

  document.querySelectorAll(".theme-toggle-btn, #themeToggleBtn").forEach((btn) => {
    if (btn.dataset.initialized) return;
    btn.dataset.initialized = "true";
    btn.addEventListener("click", (e) => {
      e.preventDefault();
      const current = document.documentElement.getAttribute("data-theme") || "light";
      const next = current === "dark" ? "light" : "dark";
      setThemePreference(next);
      showToast(`Switched to ${next === "dark" ? "Dark" : "Light"} mode`, "info");
    });
  });
}

// =========================================
// SETTINGS PAGE
// =========================================

async function setupSettingsPage() {
  const settingsContainer = document.getElementById("settingsTabs");
  if (!settingsContainer) {
    return;
  }

  // 1. Theme Selection Cards
  const themeCards = document.querySelectorAll(".theme-card");
  const storedTheme = getStoredThemePreference();

  themeCards.forEach((card) => {
    const theme = card.dataset.theme;
    if (theme === storedTheme) {
      card.classList.add("active");
      const badge = card.querySelector(".theme-card-badge");
      if (badge) badge.textContent = "Active";
    }

    card.addEventListener("click", () => {
      setThemePreference(theme);
      showToast(`Theme changed to ${theme.toUpperCase()}`, "success");
    });
  });

  // 2. Load User Profile from Oracle via /api/auth/me
  try {
    const meRes = await apiRequest("/auth/me");
    if (meRes?.data) {
      const u = meRes.data;
      const fullNameInput = document.getElementById("profileFullName");
      const emailInput = document.getElementById("profileEmail");
      const roleInput = document.getElementById("profileRole");
      const memberSinceInput = document.getElementById("profileMemberSince");

      if (fullNameInput) fullNameInput.value = u.name || "";
      if (emailInput) emailInput.value = u.email || "";
      if (roleInput) roleInput.value = u.role || "INVESTOR";
      if (memberSinceInput && u.createdAt) {
        const date = new Date(u.createdAt);
        memberSinceInput.value = date.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
      }
    }
  } catch (err) {
    console.warn("Could not load /api/auth/me:", err);
  }

  // 3. Profile Form Submit (PUT /api/auth/profile)
  const profileForm = document.getElementById("profileForm");
  if (profileForm) {
    profileForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      const saveBtn = document.getElementById("saveProfileBtn");
      const originalText = saveBtn.innerHTML;
      saveBtn.disabled = true;
      saveBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span> Saving...';

      try {
        const nameVal = document.getElementById("profileFullName")?.value?.trim();
        if (!nameVal || nameVal.length < 2) {
          throw new Error("Please enter a valid full name (minimum 2 characters).");
        }

        const res = await apiRequest("/auth/profile", {
          method: "PUT",
          body: JSON.stringify({ name: nameVal })
        });

        const cachedUser = getCurrentUser() || {};
        cachedUser.name = nameVal;
        localStorage.setItem("sharesync_user", JSON.stringify(cachedUser));
        setupUserHeader();

        showToast(res?.message || "Profile updated successfully.", "success");
      } catch (err) {
        showToast(err.message || "Failed to update profile.", "danger");
      } finally {
        saveBtn.disabled = false;
        saveBtn.innerHTML = originalText;
      }
    });
  }

  // 4. Market & Currency Preferences Form
  const marketForm = document.getElementById("marketPreferencesForm");
  if (marketForm) {
    const curSelect = document.getElementById("prefCurrency");
    const numSelect = document.getElementById("prefNumberFormat");
    const refSelect = document.getElementById("prefDseRefresh");
    const alertSelect = document.getElementById("prefAlertThreshold");

    if (curSelect) curSelect.value = localStorage.getItem("sharesync_currency") || "BDT";
    if (numSelect) numSelect.value = localStorage.getItem("sharesync_number_format") || "BD";
    if (refSelect) refSelect.value = localStorage.getItem("sharesync_dse_refresh") || "10";
    if (alertSelect) alertSelect.value = localStorage.getItem("sharesync_alert_buffer") || "5";

    marketForm.addEventListener("submit", (e) => {
      e.preventDefault();
      if (curSelect) localStorage.setItem("sharesync_currency", curSelect.value);
      if (numSelect) localStorage.setItem("sharesync_number_format", numSelect.value);
      if (refSelect) localStorage.setItem("sharesync_dse_refresh", refSelect.value);
      if (alertSelect) localStorage.setItem("sharesync_alert_buffer", alertSelect.value);

      showToast("Market and currency preferences saved successfully.", "success");
    });
  }

  // 5. Change Password Form (PUT /api/auth/change-password)
  const passwordForm = document.getElementById("changePasswordForm");
  if (passwordForm) {
    passwordForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      const currentPw = document.getElementById("currentPassword")?.value || "";
      const newPw = document.getElementById("newPassword")?.value || "";
      const confirmPw = document.getElementById("confirmNewPassword")?.value || "";

      if (newPw.length < 6) {
        showToast("New password must be at least 6 characters.", "danger");
        return;
      }
      if (newPw !== confirmPw) {
        showToast("New password and confirm password do not match.", "danger");
        return;
      }

      const updateBtn = document.getElementById("updatePasswordBtn");
      const originalText = updateBtn.innerHTML;
      updateBtn.disabled = true;
      updateBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span> Updating...';

      try {
        const res = await apiRequest("/auth/change-password", {
          method: "PUT",
          body: JSON.stringify({
            currentPassword: currentPw,
            newPassword: newPw
          })
        });

        showToast(res?.message || "Password changed successfully!", "success");
        passwordForm.reset();
      } catch (err) {
        showToast(err.message || "Failed to change password.", "danger");
      } finally {
        updateBtn.disabled = false;
        updateBtn.innerHTML = originalText;
      }
    });
  }

  // 6. Data & Export Handlers
  const exportCsvBtn = document.getElementById("exportTransactionsCsvBtn");
  if (exportCsvBtn) {
    exportCsvBtn.addEventListener("click", async (e) => {
      e.preventDefault();
      try {
        const res = await apiRequest("/transactions");
        const txs = res?.data || [];
        if (!txs.length) {
          showToast("No transactions found to export.", "info");
          return;
        }

        const headers = ["TransactionId", "PortfolioId", "Ticker", "Type", "Quantity", "Price", "TotalAmount", "Date"];
        const rows = txs.map(t => [
          t.transactionId,
          t.portfolioId,
          `"${t.tickerSymbol || ""}"`,
          t.transactionType,
          t.quantity,
          t.pricePerShare,
          t.totalAmount,
          `"${new Date(t.transactionDate).toISOString()}"`
        ]);

        const csvContent = "data:text/csv;charset=utf-8," + [headers.join(","), ...rows.map(r => r.join(","))].join("\n");
        const encodedUri = encodeURI(csvContent);
        const link = document.createElement("a");
        link.setAttribute("href", encodedUri);
        link.setAttribute("download", `ShareSync_Transactions_${new Date().toISOString().slice(0, 10)}.csv`);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        showToast("Transactions exported successfully as CSV.", "success");
      } catch (err) {
        showToast("Export failed: " + err.message, "danger");
      }
    });
  }

  const exportJsonBtn = document.getElementById("exportPortfolioJsonBtn");
  if (exportJsonBtn) {
    exportJsonBtn.addEventListener("click", async (e) => {
      e.preventDefault();
      try {
        const res = await apiRequest("/portfolios");
        const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(res?.data || [], null, 2));
        const link = document.createElement("a");
        link.setAttribute("href", dataStr);
        link.setAttribute("download", `ShareSync_Portfolios_${new Date().toISOString().slice(0, 10)}.json`);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        showToast("Portfolio snapshot exported successfully as JSON.", "success");
      } catch (err) {
        showToast("Export failed: " + err.message, "danger");
      }
    });
  }

  const clearCacheBtn = document.getElementById("clearCacheBtn");
  if (clearCacheBtn) {
    clearCacheBtn.addEventListener("click", (e) => {
      e.preventDefault();
      if (confirm("Reset local preferences and cache? (Your account and database transactions are safe)")) {
        localStorage.removeItem("sharesync_theme");
        localStorage.removeItem("sharesync_currency");
        localStorage.removeItem("sharesync_number_format");
        localStorage.removeItem("sharesync_dse_refresh");
        localStorage.removeItem("sharesync_alert_buffer");
        showToast("Preferences reset. Reloading...", "info");
        setTimeout(() => window.location.reload(), 800);
      }
    });
  }
}

document.addEventListener("DOMContentLoaded", () => {
  initThemeSystem();
  checkAuthProtection();
  console.log("ShareSync application loaded.");

  setupSidebarNavigation();
  setupMobileNavigation();
  setupLastUpdated();
  setupTableHints();
  setupUserHeader();

  if (typeof setupLoginForm === "function") setupLoginForm();
  if (typeof setupRegisterForm === "function") setupRegisterForm();
  if (typeof setupDashboard === "function") setupDashboard();
  if (typeof setupTransactionForm === "function") setupTransactionForm();
  if (typeof setupWatchlistForm === "function") setupWatchlistForm();
  if (typeof setupDividendForm === "function") setupDividendForm();
  if (typeof setupReports === "function") setupReports();
  if (typeof setupTransactionFilters === "function") setupTransactionFilters();
  if (typeof setupWatchlistFilter === "function") setupWatchlistFilter();
  if (typeof setupDividendFilter === "function") setupDividendFilter();
  if (typeof setupPortfolioManagement === "function") setupPortfolioManagement();
  if (typeof setupDseSync === "function") setupDseSync();
  if (typeof setupSettingsPage === "function") setupSettingsPage();
});

// =========================================
// DIVIDEND FORM
// =========================================

// =========================================
// DIVIDEND MANAGEMENT
// =========================================

let dividendCompaniesCache = [];

async function setupDividendForm() {
  const form = document.getElementById("dividendForm");
  const tableBody = document.getElementById("dividendTableBody");

  if (!form && !tableBody) {
    return;
  }

  const formCard = document.getElementById("dividendFormCard");
  const openButton = document.getElementById("openDividendForm");
  const closeButton = document.getElementById("closeDividendForm");
  const cancelButton = document.getElementById("cancelDividend");
  const formTitle = document.getElementById("dividendFormTitle");
  const formSubtitle = document.getElementById("dividendFormSubtitle");
  const formId = document.getElementById("dividendFormId");
  const companySelect = document.getElementById("dividendCompany");
  const perShareInput = document.getElementById("dividendPerShare");
  const declDateInput = document.getElementById("declarationDate");
  const payDateInput = document.getElementById("paymentDate");
  const companyFilter = document.getElementById("dividendCompanyFilter");
  const periodFilter = document.getElementById("dividendFilter");

  if (form) setupInlineValidation(form);

  // Load companies for the dropdowns
  try {
    const res = await apiRequest("/companies");
    dividendCompaniesCache = res.data || [];

    if (companySelect) {
      companySelect.innerHTML = '<option value="">Select company</option>' +
        dividendCompaniesCache.map(c => `<option value="${c.companyId}">${escapeHtml(c.tickerSymbol)} - ${escapeHtml(c.companyName)}</option>`).join("");
    }

    if (companyFilter) {
      companyFilter.innerHTML = '<option value="all">All companies</option>' +
        dividendCompaniesCache.map(c => `<option value="${c.companyId}">${escapeHtml(c.tickerSymbol)} - ${escapeHtml(c.companyName)}</option>`).join("");
    }
  } catch (err) {
    console.error("Failed to load companies for dividends:", err);
  }

  function openForm(isEdit = false, item = null) {
    if (!formCard) return;
    clearFieldError(companySelect);
    clearFieldError(perShareInput);
    clearFieldError(declDateInput);
    clearFieldError(payDateInput);

    if (isEdit && item) {
      formTitle.textContent = "Edit dividend record";
      formSubtitle.textContent = `Update dividend for ${item.companyName}`;
      formId.value = item.dividendId;
      companySelect.value = item.companyId;
      perShareInput.value = item.dividendPerShare;
      declDateInput.value = item.declarationDate ? item.declarationDate.split("T")[0] : "";
      payDateInput.value = item.paymentDate ? item.paymentDate.split("T")[0] : "";
    } else {
      formTitle.textContent = "Add dividend record";
      formSubtitle.textContent = "Record a dividend declaration or payment.";
      formId.value = "";
      form.reset();
      const today = new Date().toISOString().split("T")[0];
      declDateInput.value = today;
      // Default payment date 14 days later
      const future = new Date(Date.now() + 14 * 86400000).toISOString().split("T")[0];
      payDateInput.value = future;
    }

    formCard.classList.remove("form-hidden");
    formCard.scrollIntoView({ behavior: "smooth", block: "start" });
    companySelect.focus();
  }

  function closeForm() {
    if (formCard) formCard.classList.add("form-hidden");
    if (form) form.reset();
    if (formId) formId.value = "";
  }

  if (openButton) openButton.addEventListener("click", () => openForm(false));
  if (closeButton) closeButton.addEventListener("click", closeForm);
  if (cancelButton) cancelButton.addEventListener("click", closeForm);

  if (form) {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();

      if (!validateRequiredFields(form)) {
        return;
      }

      const compId = parseInt(companySelect.value, 10);
      const amount = parseFloat(perShareInput.value);
      const declaration = declDateInput.value;
      const payment = payDateInput.value;

      if (!compId) {
        setFieldError(companySelect, "Please select a company.");
        return;
      }

      if (isNaN(amount) || amount <= 0) {
        setFieldError(perShareInput, "Dividend per share must be greater than zero.");
        return;
      }

      if (payment < declaration) {
        setFieldError(payDateInput, "Payment date must not be earlier than declaration date.");
        return;
      }

      const payload = {
        companyId: compId,
        dividendPerShare: amount,
        declarationDate: declaration,
        paymentDate: payment
      };

      const editId = formId.value ? parseInt(formId.value, 10) : null;

      try {
        if (editId) {
          await apiRequest(`/dividends/${editId}`, {
            method: "PUT",
            body: JSON.stringify(payload)
          });
          showToast("Dividend record updated successfully.", "success");
        } else {
          await apiRequest("/dividends", {
            method: "POST",
            body: JSON.stringify(payload)
          });
          showToast("Dividend record created successfully.", "success");
        }

        closeForm();
        await loadDividendSummary();
        await loadDividendHistory();
      } catch (err) {
        showToast("Error saving dividend: " + err.message, "danger");
      }
    });
  }

  window.editDividend = async function(dividendId) {
    try {
      const res = await apiRequest(`/dividends/${dividendId}`);
      openForm(true, res.data);
    } catch (err) {
      console.error("Failed to load dividend for edit:", err);
      showToast("Could not load dividend details: " + err.message, "danger");
    }
  };

  window.deleteDividend = async function(dividendId) {
    if (!confirm("Are you sure you want to delete this dividend record?")) {
      return;
    }

    try {
      await apiRequest(`/dividends/${dividendId}`, { method: "DELETE" });
      showToast("Dividend record deleted successfully.", "success");
      await loadDividendSummary();
      await loadDividendHistory();
    } catch (err) {
      console.error("Failed to delete dividend:", err);
      showToast("Cannot delete dividend: " + err.message, "danger");
    }
  };

  // Filter change events
  if (companyFilter) companyFilter.addEventListener("change", loadDividendHistory);
  if (periodFilter) periodFilter.addEventListener("change", loadDividendHistory);

  // Initial loads
  await loadDividendSummary();
  await loadDividendHistory();
}

async function loadDividendSummary() {
  const totalEl = document.getElementById("summaryTotalIncome");
  const yearEl = document.getElementById("summaryThisYearIncome");
  const upcomingEl = document.getElementById("summaryUpcomingIncome");
  const countEl = document.getElementById("summaryCompaniesCount");

  try {
    const res = await apiRequest("/dividends/summary");
    const d = res.data;

    if (totalEl) totalEl.textContent = "৳" + Number(d.totalIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (yearEl) yearEl.textContent = "৳" + Number(d.thisYearIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (upcomingEl) upcomingEl.textContent = "৳" + Number(d.upcomingIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (countEl) countEl.textContent = d.companiesCount || 0;
  } catch (err) {
    console.error("Failed to load dividend summary:", err);
  }
}

async function loadDividendHistory() {
  const tableBody = document.getElementById("dividendTableBody");
  const countSubtitle = document.getElementById("dividendCountSubtitle");
  const companyFilter = document.getElementById("dividendCompanyFilter");
  const periodFilter = document.getElementById("dividendFilter");

  if (!tableBody) return;

  const params = new URLSearchParams();
  if (companyFilter && companyFilter.value !== "all") {
    params.append("companyId", companyFilter.value);
  }
  if (periodFilter && periodFilter.value === "current") {
    params.append("year", new Date().getFullYear());
  } else if (periodFilter && periodFilter.value === "previous") {
    params.append("year", new Date().getFullYear() - 1);
  }

  const queryString = params.toString() ? `?${params.toString()}` : "";

  try {
    const res = await apiRequest(`/dividends${queryString}`);
    const items = res.data || [];

    if (countSubtitle) {
      countSubtitle.textContent = `${items.length} dividend ${items.length === 1 ? 'record' : 'records'} found`;
    }

    if (items.length === 0) {
      tableBody.innerHTML = `
        <tr>
          <td colspan="7" class="text-center py-4 text-muted">
            <i class="bi bi-inbox fs-4 d-block mb-1"></i>
            No dividend records found. Click "Add dividend" to record one.
          </td>
        </tr>
      `;
      return;
    }

    tableBody.innerHTML = items.map(d => {
      const isPaid = d.status === "Paid";
      const statusClass = isPaid ? "status-paid" : "status-upcoming";
      const logoText = escapeHtml(d.tickerSymbol.slice(0, 2).toUpperCase());
      const declDateStr = d.declarationDate ? new Date(d.declarationDate).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" }) : "-";
      const payDateStr = d.paymentDate ? new Date(d.paymentDate).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" }) : "-";
      const formattedPerShare = "৳" + Number(d.dividendPerShare || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      const formattedEstimated = "৳" + Number(d.estimatedIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      const sharesNote = (d.userSharesHeld || 0) > 0 
        ? `<small class="text-muted d-block font-monospace">${Number(d.userSharesHeld).toLocaleString()} shares</small>`
        : `<small class="text-muted d-block">0 shares held</small>`;

      return `
        <tr>
          <td>
            <div class="company-cell">
              <span class="company-logo">${logoText}</span>
              <div>
                <strong>${escapeHtml(d.companyName)}</strong>
                <small>${escapeHtml(d.tickerSymbol)}</small>
              </div>
            </div>
          </td>
          <td>${formattedPerShare}</td>
          <td>${declDateStr}</td>
          <td>${payDateStr}</td>
          <td>
            <span class="status-badge ${statusClass}">${escapeHtml(d.status)}</span>
          </td>
          <td>
            <strong>${formattedEstimated}</strong>
            ${sharesNote}
          </td>
          <td>
            <div class="d-flex align-items-center gap-1">
              <button class="btn btn-sm btn-outline-secondary p-1 px-2" onclick="editDividend(${d.dividendId})" title="Edit dividend">
                <i class="bi bi-pencil"></i>
              </button>
              <button class="btn btn-sm btn-outline-danger p-1 px-2" onclick="deleteDividend(${d.dividendId})" title="Delete dividend">
                <i class="bi bi-trash"></i>
              </button>
            </div>
          </td>
        </tr>
      `;
    }).join("");
  } catch (err) {
    console.error("Failed to load dividend history:", err);
    tableBody.innerHTML = `
      <tr>
        <td colspan="7" class="text-center py-4 text-danger">
          Error loading dividend history: ${escapeHtml(err.message)}
        </td>
      </tr>
    `;
  }
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
          const currentPriceFormatted = "৳" + Number(item.currentPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
          const targetPriceFormatted = (item.targetPrice !== null && item.targetPrice !== undefined)
            ? "৳" + Number(item.targetPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
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
        pricePerShare: price,
        price: price,
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

  if (!selector) {
    return;
  }

  selector.addEventListener("change", () => {
    if (typeof loadPortfolioPerformanceChart === "function") {
      loadPortfolioPerformanceChart(selector.value);
    }
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
      const priceNum = Number(t.pricePerShare ?? t.price ?? 0);
      const qtyNum = Number(t.quantity ?? 0);
      const totalNum = Number(t.totalAmount ?? (qtyNum * priceNum));
      const priceFormatted = "৳" + priceNum.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      const totalFormatted = "৳" + totalNum.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

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
          <td>${qtyNum.toLocaleString()}</td>
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
    if (priceInput) priceInput.value = t.pricePerShare ?? t.price ?? "";
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
  // Handled dynamically via API in setupDividendForm()
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

// Sidebar navigation active state and logout are handled in primary setupSidebarNavigation

// =========================================
// PORTFOLIO PERFORMANCE CHART
// =========================================

async function loadPortfolioPerformanceChart(period = "6 months") {
  const canvas = document.getElementById("portfolioChart");
  if (!canvas || !portfolioChart) return;

  try {
    let pId = currentPortfolioId;
    if (!pId) {
      const res = await apiRequest("/portfolios");
      if (res.data && res.data.length > 0) {
        pId = res.data[0].portfolioId;
        currentPortfolioId = pId;
      }
    }

    if (!pId) {
      portfolioChart.data.labels = ["No Data"];
      portfolioChart.data.datasets[0].data = [0];
      portfolioChart.update();
      return;
    }

    const perfRes = await apiRequest(`/portfolios/${pId}/snapshots/performance?period=${encodeURIComponent(period)}`);
    const perfData = perfRes.data;

    if (perfData && perfData.labels && perfData.labels.length > 0) {
      portfolioChart.data.labels = perfData.labels;
      portfolioChart.data.datasets[0].data = perfData.values;
      portfolioChart.update();

      const datesEl = document.querySelector(".chart-dates");
      if (datesEl) {
        datesEl.innerHTML = perfData.labels.map(l => `<span>${escapeHtml(l)}</span>`).join("");
      }
    } else {
      portfolioChart.data.labels = ["No Data"];
      portfolioChart.data.datasets[0].data = [0];
      portfolioChart.update();
    }
  } catch (err) {
    console.error("Failed to load portfolio performance chart:", err);
  }
}

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
      labels: [],

      datasets: [
        {
          label: "Portfolio Value",

          data: [],

          borderColor: "#0F172A",
          backgroundColor: "rgba(15, 23, 42, 0.035)",
          borderWidth: 2,
          pointRadius: 2,
          pointHoverRadius: 4,
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
              return "৳" + Number(context.raw).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
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

  const selector = document.getElementById("chartPeriod");
  const initialPeriod = selector ? selector.value : "6 months";
  loadPortfolioPerformanceChart(initialPeriod);
}

// =========================================
// REPORTS & ANALYTICS
// =========================================

let reportValueChartInstance = null;
let reportAllocationChartInstance = null;
let reportActivityChartInstance = null;

async function setupReports() {
  const portfolioCanvas = document.getElementById("portfolioValueReport");
  if (!portfolioCanvas) {
    return;
  }

  const portfolioSelect = document.getElementById("reportPortfolioSelect");
  const reportTypeSelect = document.getElementById("reportTypeSelect");
  const periodSelect = document.getElementById("reportPeriod");

  // Load portfolios into filter dropdown
  try {
    const res = await apiRequest("/portfolios");
    const portfolios = res.data || [];
    if (portfolioSelect) {
      portfolioSelect.innerHTML = '<option value="">All Portfolios</option>' +
        portfolios.map(p => `<option value="${p.portfolioId}">${escapeHtml(p.portfolioName)}</option>`).join("");
    }
  } catch (err) {
    console.error("Failed to load portfolios for reports:", err);
  }

  async function refreshReports() {
    const portfolioId = portfolioSelect ? portfolioSelect.value : "";
    const period = periodSelect ? periodSelect.value : "6";
    const reportType = reportTypeSelect ? reportTypeSelect.value : "holdings";

    await Promise.allSettled([
      loadReportSummary(portfolioId),
      loadReportCharts(portfolioId, period),
      loadActiveReportTable(reportType, portfolioId, period)
    ]);
  }

  if (portfolioSelect) {
    portfolioSelect.addEventListener("change", refreshReports);
  }
  if (periodSelect) {
    periodSelect.addEventListener("change", refreshReports);
  }
  if (reportTypeSelect) {
    reportTypeSelect.addEventListener("change", () => {
      const portfolioId = portfolioSelect ? portfolioSelect.value : "";
      const period = periodSelect ? periodSelect.value : "6";
      const reportType = reportTypeSelect.value;
      loadActiveReportTable(reportType, portfolioId, period);
    });
  }

  await refreshReports();
}

async function loadReportSummary(portfolioId) {
  const valEl = document.getElementById("reportSummaryPortfolioValue");
  const plEl = document.getElementById("reportSummaryUnrealizedPL");
  const divEl = document.getElementById("reportSummaryDividendIncome");
  const txEl = document.getElementById("reportSummaryTransactions");

  try {
    const url = portfolioId ? `/reports/summary?portfolioId=${portfolioId}` : "/reports/summary";
    const res = await apiRequest(url);
    const d = res.data;

    if (valEl) {
      valEl.textContent = "৳" + Number(d.totalPortfolioValue).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    if (plEl) {
      const isPos = d.totalUnrealizedProfitLoss >= 0;
      const sign = isPos ? "+" : "-";
      plEl.textContent = `${sign}৳${Math.abs(d.totalUnrealizedProfitLoss).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      plEl.className = isPos ? "summary-value positive-text" : "summary-value negative-text";
    }
    if (divEl) {
      divEl.textContent = "৳" + Number(d.totalDividendIncome).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    if (txEl) {
      txEl.textContent = Number(d.totalTransactionsCount).toLocaleString();
    }
  } catch (err) {
    console.error("Failed to load report summary:", err);
  }
}

async function loadReportCharts(portfolioId, period) {
  Chart.defaults.font.family = "Inter, Segoe UI, sans-serif";

  // 1. Performance Chart
  const perfCanvas = document.getElementById("portfolioValueReport");
  if (perfCanvas) {
    try {
      const params = new URLSearchParams();
      if (portfolioId) params.append("portfolioId", portfolioId);
      if (period) params.append("period", period);
      const res = await apiRequest(`/reports/performance?${params.toString()}`);
      const snapshots = res.data?.snapshots || [];
      const labels = res.data?.chartLabels?.length ? res.data.chartLabels : snapshots.map(s => {
        const d = new Date(s.snapshotDate);
        return d.toLocaleDateString(undefined, { month: "short", day: "numeric" });
      });
      const data = res.data?.chartValues?.length ? res.data.chartValues : snapshots.map(s => s.portfolioValue);

      if (reportValueChartInstance) {
        reportValueChartInstance.destroy();
      }

      reportValueChartInstance = new Chart(perfCanvas, {
        type: "line",
        data: {
          labels: labels.length ? labels : ["No data"],
          datasets: [{
            label: "Portfolio Value",
            data: data.length ? data : [0],
            borderColor: "#0F172A",
            backgroundColor: "rgba(15, 23, 42, 0.04)",
            borderWidth: 2,
            pointRadius: data.length > 20 ? 1 : 3,
            pointHoverRadius: 5,
            tension: 0.25,
            fill: true
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          interaction: { mode: "index", intersect: false },
          plugins: {
            legend: { display: false },
            tooltip: {
              backgroundColor: "rgba(15, 23, 42, 0.95)",
              titleColor: "#F8FAFC",
              bodyColor: "#F8FAFC",
              callbacks: {
                label: (ctx) => "৳" + Number(ctx.raw).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
              }
            }
          },
          scales: {
            y: {
              beginAtZero: false,
              grid: { color: "rgba(148, 163, 184, 0.15)" },
              ticks: {
                color: "#64748B",
                callback: (val) => "৳" + (val >= 1000 ? (val / 1000).toFixed(0) + "k" : val)
              }
            },
            x: {
              grid: { display: false },
              ticks: { color: "#64748B" }
            }
          }
        }
      });
    } catch (err) {
      console.error("Failed to load performance chart:", err);
    }
  }

  // 2. Allocation Chart & List
  const allocCanvas = document.getElementById("allocationChart");
  const allocList = document.getElementById("reportAllocationList");
  if (allocCanvas) {
    try {
      const url = portfolioId ? `/reports/company-sector?portfolioId=${portfolioId}` : "/reports/company-sector";
      const res = await apiRequest(url);
      const sectors = res.data?.sectors || [];

      const colors = ["#0F172A", "#334155", "#475569", "#64748B", "#94A3B8", "#CBD5E1", "#E2E8F0"];
      const labels = sectors.map(s => s.sectorName);
      const data = sectors.map(s => s.currentMarketValue);
      const bgColors = sectors.map((_, i) => colors[i % colors.length]);

      if (reportAllocationChartInstance) {
        reportAllocationChartInstance.destroy();
      }

      reportAllocationChartInstance = new Chart(allocCanvas, {
        type: "doughnut",
        data: {
          labels: labels.length ? labels : ["No Holdings"],
          datasets: [{
            data: data.length ? data : [1],
            backgroundColor: data.length ? bgColors : ["#E2E8F0"],
            borderWidth: 0,
            hoverOffset: 3
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: "68%",
          plugins: {
            legend: { display: false },
            tooltip: {
              backgroundColor: "rgba(15, 23, 42, 0.95)",
              callbacks: {
                label: (ctx) => {
                  const val = Number(ctx.raw);
                  return " ৳" + val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
                }
              }
            }
          }
        }
      });

      if (allocList) {
        if (!sectors.length) {
          allocList.innerHTML = `<div class="text-muted text-center py-3">No holdings found for allocation.</div>`;
        } else {
          allocList.innerHTML = sectors.map((s, idx) => `
            <div class="allocation-item">
              <div class="allocation-label">
                <span class="allocation-dot" style="background-color: ${bgColors[idx]};"></span>
                <span>${escapeHtml(s.sectorName)}</span>
              </div>
              <strong>${s.allocationPercentage.toFixed(1)}%</strong>
            </div>
          `).join("");
        }
      }
    } catch (err) {
      console.error("Failed to load allocation chart:", err);
    }
  }

  // 3. Transaction Activity Chart
  const txCanvas = document.getElementById("transactionActivityChart");
  if (txCanvas) {
    try {
      const params = new URLSearchParams();
      if (portfolioId) params.append("portfolioId", portfolioId);
      if (period) params.append("period", period);
      const res = await apiRequest(`/reports/transactions?${params.toString()}`);
      const txs = res.data?.transactions || [];

      // Group transactions by month
      const monthlyGroups = {};
      txs.forEach(t => {
        const d = new Date(t.transactionDate);
        const monthKey = d.toLocaleDateString(undefined, { month: "short", year: "2-digit" });
        if (!monthlyGroups[monthKey]) {
          monthlyGroups[monthKey] = { BUY: 0, SELL: 0 };
        }
        if (t.transactionType === "BUY") {
          monthlyGroups[monthKey].BUY++;
        } else {
          monthlyGroups[monthKey].SELL++;
        }
      });

      const months = Object.keys(monthlyGroups);
      const buyCounts = months.map(m => monthlyGroups[m].BUY);
      const sellCounts = months.map(m => monthlyGroups[m].SELL);

      if (reportActivityChartInstance) {
        reportActivityChartInstance.destroy();
      }

      reportActivityChartInstance = new Chart(txCanvas, {
        type: "bar",
        data: {
          labels: months.length ? months : ["No activity"],
          datasets: [
            {
              label: "BUY",
              data: buyCounts.length ? buyCounts : [0],
              backgroundColor: "#0F172A",
              borderRadius: 3,
              borderSkipped: false
            },
            {
              label: "SELL",
              data: sellCounts.length ? sellCounts : [0],
              backgroundColor: "#CBD5E1",
              borderRadius: 3,
              borderSkipped: false
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: {
              position: "bottom",
              labels: { usePointStyle: true, pointStyle: "circle", boxWidth: 8 }
            }
          },
          scales: {
            y: {
              beginAtZero: true,
              grid: { color: "rgba(148, 163, 184, 0.15)" },
              ticks: { stepSize: 1, color: "#64748B" }
            },
            x: {
              grid: { display: false },
              ticks: { color: "#64748B" }
            }
          }
        }
      });
    } catch (err) {
      console.error("Failed to load transaction activity chart:", err);
    }
  }
}

async function loadActiveReportTable(reportType, portfolioId, period) {
  const titleEl = document.getElementById("reportSectionTitle");
  const subEl = document.getElementById("reportSectionSubtitle");
  const headEl = document.getElementById("reportTableHead");
  const bodyEl = document.getElementById("reportTableBody");

  if (!headEl || !bodyEl) return;

  bodyEl.innerHTML = `<tr><td colspan="10" class="text-center py-4 text-muted"><div class="spinner-border spinner-border-sm me-2" role="status"></div>Loading report data...</td></tr>`;

  try {
    switch (reportType) {
      case "holdings":
        if (titleEl) titleEl.textContent = "1. Portfolio Holdings Report";
        if (subEl) subEl.textContent = "Real-time holdings calculated from normalized transactions with weighted average cost, market prices, and unrealized profit/loss.";
        await renderHoldingsReport(portfolioId, headEl, bodyEl);
        break;

      case "performance":
        if (titleEl) titleEl.textContent = "2. Portfolio Performance / Profit-Loss Report";
        if (subEl) subEl.textContent = "Chronological valuation snapshots tracking capital progression, profit/loss margins, and period-over-period variations.";
        await renderPerformanceReport(portfolioId, period, headEl, bodyEl);
        break;

      case "transactions":
        if (titleEl) titleEl.textContent = "3. Transaction History Report";
        if (subEl) subEl.textContent = "Comprehensive audit log of all investment operations with unit prices, quantity balances, and transaction totals.";
        await renderTransactionsReport(portfolioId, period, headEl, bodyEl);
        break;

      case "company-sector":
        if (titleEl) titleEl.textContent = "4. Company & Sector Investment Report";
        if (subEl) subEl.textContent = "Meaningful aggregation of invested capital, market valuation, and portfolio diversification by industry sector.";
        await renderCompanySectorReport(portfolioId, headEl, bodyEl);
        break;

      case "dividends":
        if (titleEl) titleEl.textContent = "5. Dividend Income Report";
        if (subEl) subEl.textContent = "Historical corporate dividend distributions with entitlement yield calculations based on active holdings.";
        await renderDividendsReport(portfolioId, period, headEl, bodyEl);
        break;

      case "watchlist":
        if (titleEl) titleEl.textContent = "6. Watchlist / Target Price Report";
        if (subEl) subEl.textContent = "Tracked securities across personal watchlists showing distance to target price and target proximity indicators.";
        await renderWatchlistReport(headEl, bodyEl);
        break;

      default:
        await renderHoldingsReport(portfolioId, headEl, bodyEl);
        break;
    }
  } catch (err) {
    console.error("Error loading report table:", err);
    bodyEl.innerHTML = `<tr><td colspan="10" class="text-center text-danger py-4">Failed to load report: ${escapeHtml(err.message)}</td></tr>`;
  }
}

async function renderHoldingsReport(portfolioId, headEl, bodyEl) {
  headEl.innerHTML = `
    <tr>
      <th>Company</th>
      <th>Ticker</th>
      <th>Quantity</th>
      <th>Avg. Cost</th>
      <th>Market Price</th>
      <th>Invested Value</th>
      <th>Market Value</th>
      <th>Unrealized P/L</th>
      <th>Return %</th>
    </tr>
  `;

  const url = portfolioId ? `/reports/holdings?portfolioId=${portfolioId}` : "/reports/holdings";
  const res = await apiRequest(url);
  const rows = res.data?.holdings || [];

  if (!rows.length) {
    bodyEl.innerHTML = `<tr><td colspan="9" class="text-center py-4 text-muted">No holdings found for the selected portfolio.</td></tr>`;
    return;
  }

  bodyEl.innerHTML = rows.map(r => {
    const isPos = r.unrealizedProfitLoss >= 0;
    const sign = isPos ? "+" : "-";
    const plClass = isPos ? "positive-text" : "negative-text";
    const logo = escapeHtml(r.tickerSymbol.slice(0, 2).toUpperCase());
    const avgCost = r.weightedAverageBuyPrice ?? r.weightedAveragePurchasePrice ?? 0;

    return `
      <tr>
        <td>
          <div class="company-cell">
            <span class="company-logo">${logo}</span>
            <div class="company-details">
              <strong>${escapeHtml(r.companyName)}</strong>
              <small>${escapeHtml(r.sectorName || "Equity")}</small>
            </div>
          </div>
        </td>
        <td><strong>${escapeHtml(r.tickerSymbol)}</strong></td>
        <td>${Number(r.currentQuantity || 0).toLocaleString()}</td>
        <td>৳${Number(avgCost || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td>৳${Number(r.currentMarketPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td>৳${Number(r.investedValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td>৳${Number(r.currentMarketValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td class="${plClass}"><strong>${sign}৳${Number(Math.abs(r.unrealizedProfitLoss || 0)).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong></td>
        <td class="${plClass}">${sign}${Number(Math.abs(r.profitLossPercentage || 0)).toFixed(2)}%</td>
      </tr>
    `;
  }).join("");
}

async function renderPerformanceReport(portfolioId, period, headEl, bodyEl) {
  headEl.innerHTML = `
    <tr>
      <th>Snapshot Date</th>
      <th>Portfolio</th>
      <th>Portfolio Value</th>
      <th>Change from Prev</th>
      <th>% Change</th>
    </tr>
  `;

  const params = new URLSearchParams();
  if (portfolioId) params.append("portfolioId", portfolioId);
  if (period) params.append("period", period);
  const res = await apiRequest(`/reports/performance?${params.toString()}`);
  const rows = res.data?.snapshots || [];

  if (!rows.length) {
    bodyEl.innerHTML = `<tr><td colspan="5" class="text-center py-4 text-muted">No snapshot records found for the selected criteria.</td></tr>`;
    return;
  }

  const pName = res.data?.portfolioName || "Portfolio";

  bodyEl.innerHTML = rows.map(r => {
    let changeText = "-";
    let changePctText = "-";
    let changeClass = "text-muted";

    if (r.changeFromPrevious !== null && r.changeFromPrevious !== undefined) {
      const isChgPos = r.changeFromPrevious >= 0;
      const chgSign = isChgPos ? "+" : "-";
      changeClass = isChgPos ? "positive-text" : "negative-text";
      changeText = `${chgSign}৳${Math.abs(r.changeFromPrevious).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      changePctText = `${chgSign}${Math.abs(r.percentageChange || 0).toFixed(2)}%`;
    }

    const dateStr = new Date(r.snapshotDate).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });

    return `
      <tr>
        <td><strong>${escapeHtml(dateStr)}</strong></td>
        <td>${escapeHtml(pName)}</td>
        <td><strong>৳${r.portfolioValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong></td>
        <td class="${changeClass}">${changeText}</td>
        <td class="${changeClass}">${changePctText}</td>
      </tr>
    `;
  }).join("");
}

async function renderTransactionsReport(portfolioId, period, headEl, bodyEl) {
  headEl.innerHTML = `
    <tr>
      <th>Tx ID</th>
      <th>Date</th>
      <th>Portfolio</th>
      <th>Company</th>
      <th>Ticker</th>
      <th>Type</th>
      <th>Quantity</th>
      <th>Price / Share</th>
      <th>Total Value</th>
    </tr>
  `;

  const params = new URLSearchParams();
  if (portfolioId) params.append("portfolioId", portfolioId);
  if (period) params.append("period", period);
  const res = await apiRequest(`/reports/transactions?${params.toString()}`);
  const rows = res.data?.transactions || [];

  if (!rows.length) {
    bodyEl.innerHTML = `<tr><td colspan="9" class="text-center py-4 text-muted">No transactions found for the selected period.</td></tr>`;
    return;
  }

  bodyEl.innerHTML = rows.map(r => {
    const isBuy = r.transactionType === "BUY";
    const badgeClass = isBuy ? "buy-badge" : "sell-badge";
    const dateStr = new Date(r.transactionDate).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });

    return `
      <tr>
        <td>#${r.transactionId}</td>
        <td>${escapeHtml(dateStr)}</td>
        <td>${escapeHtml(r.portfolioName)}</td>
        <td>${escapeHtml(r.companyName)}</td>
        <td><strong>${escapeHtml(r.tickerSymbol)}</strong></td>
        <td><span class="transaction-badge ${badgeClass}">${escapeHtml(r.transactionType)}</span></td>
        <td>${Number(r.quantity || 0).toLocaleString()}</td>
        <td>৳${Number(r.pricePerShare || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td><strong>৳${Number(r.totalTransactionValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong></td>
      </tr>
    `;
  }).join("");
}

async function renderCompanySectorReport(portfolioId, headEl, bodyEl) {
  headEl.innerHTML = `
    <tr>
      <th>Sector Name</th>
      <th>Holdings Count</th>
      <th>Total Invested</th>
      <th>Market Value</th>
      <th>Unrealized P/L</th>
      <th>Sector Allocation</th>
    </tr>
  `;

  const url = portfolioId ? `/reports/company-sector?portfolioId=${portfolioId}` : "/reports/company-sector";
  const res = await apiRequest(url);
  const rows = res.data?.sectors || [];

  if (!rows.length) {
    bodyEl.innerHTML = `<tr><td colspan="6" class="text-center py-4 text-muted">No sector investment data found.</td></tr>`;
    return;
  }

  bodyEl.innerHTML = rows.map(r => {
    const isPos = r.unrealizedProfitLoss >= 0;
    const sign = isPos ? "+" : "-";
    const plClass = isPos ? "positive-text" : "negative-text";

    return `
      <tr>
        <td><strong>${escapeHtml(r.sectorName)}</strong></td>
        <td>${r.holdingsCount}</td>
        <td>৳${r.totalInvested.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td>৳${r.currentMarketValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td class="${plClass}">${sign}৳${Math.abs(r.unrealizedProfitLoss).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td>
          <div class="d-flex align-items-center gap-2">
            <div class="progress flex-grow-1" style="height: 6px;">
              <div class="progress-bar bg-dark" style="width: ${Math.min(r.allocationPercentage, 100)}%;"></div>
            </div>
            <strong style="min-width: 45px;">${r.allocationPercentage.toFixed(1)}%</strong>
          </div>
        </td>
      </tr>
    `;
  }).join("");
}

async function renderDividendsReport(portfolioId, period, headEl, bodyEl) {
  headEl.innerHTML = `
    <tr>
      <th>Company</th>
      <th>Ticker</th>
      <th>Dividend / Share</th>
      <th>Declaration Date</th>
      <th>Payment Date</th>
      <th>Shares Held</th>
      <th>Est. Total Dividend</th>
      <th>Status</th>
    </tr>
  `;

  const params = new URLSearchParams();
  if (portfolioId) params.append("portfolioId", portfolioId);
  if (period) params.append("period", period);
  const res = await apiRequest(`/reports/dividends?${params.toString()}`);
  const rows = res.data?.dividends || [];

  if (!rows.length) {
    bodyEl.innerHTML = `<tr><td colspan="8" class="text-center py-4 text-muted">No dividend records found.</td></tr>`;
    return;
  }

  bodyEl.innerHTML = rows.map(r => {
    const declStr = r.declarationDate ? new Date(r.declarationDate).toLocaleDateString() : "-";
    const payStr = r.paymentDate ? new Date(r.paymentDate).toLocaleDateString() : "-";
    const isPaid = new Date(r.paymentDate) <= new Date();
    const statusClass = isPaid ? "status-badge status-paid" : "status-badge status-declared";
    const statusText = isPaid ? "Paid" : "Declared";

    return `
      <tr>
        <td><strong>${escapeHtml(r.companyName)}</strong></td>
        <td>${escapeHtml(r.tickerSymbol)}</td>
        <td>৳${r.dividendPerShare.toFixed(2)}</td>
        <td>${escapeHtml(declStr)}</td>
        <td>${escapeHtml(payStr)}</td>
        <td>${r.userSharesHeld.toLocaleString()}</td>
        <td><strong>৳${r.estimatedIncome.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong></td>
        <td><span class="${statusClass}">${statusText}</span></td>
      </tr>
    `;
  }).join("");
}

async function renderWatchlistReport(headEl, bodyEl) {
  headEl.innerHTML = `
    <tr>
      <th>Watchlist</th>
      <th>Company</th>
      <th>Ticker</th>
      <th>Current Price</th>
      <th>Target Price</th>
      <th>Price Difference</th>
      <th>Difference %</th>
      <th>Proximity</th>
    </tr>
  `;

  const res = await apiRequest("/reports/watchlist");
  const rows = res.data?.items || [];

  if (!rows.length) {
    bodyEl.innerHTML = `<tr><td colspan="8" class="text-center py-4 text-muted">No watchlist items with target prices found.</td></tr>`;
    return;
  }

  bodyEl.innerHTML = rows.map(r => {
    const isNear = Math.abs(r.percentageDifference) <= 5.0;
    const badgeClass = isNear ? "target-distance near-target" : "target-distance";
    const targetStatus = r.status || (isNear ? "Near Target (±5%)" : (r.percentageDifference > 0 ? "Above Target" : "Below Target"));
    const diffSign = r.priceDifference >= 0 ? "+" : "-";

    return `
      <tr>
        <td><strong>${escapeHtml(r.watchlistName)}</strong></td>
        <td>${escapeHtml(r.companyName)}</td>
        <td><strong>${escapeHtml(r.tickerSymbol)}</strong></td>
        <td>৳${r.currentPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
        <td>${r.targetPrice !== null ? `৳${r.targetPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : "-"}</td>
        <td>${r.priceDifference !== null ? `${diffSign}৳${Math.abs(r.priceDifference).toFixed(2)}` : "-"}</td>
        <td>${r.percentageDifference !== null ? `${diffSign}${Math.abs(r.percentageDifference).toFixed(2)}%` : "-"}</td>
        <td><span class="${badgeClass}">${targetStatus}</span></td>
      </tr>
    `;
  }).join("");
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

    const formattedValue = "৳" + Number(d.totalValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const formattedInvested = "৳" + Number(d.totalInvested || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const isProfitable = d.unrealizedProfitLoss >= 0;
    const sign = isProfitable ? "+" : "-";
    const absPL = Number(Math.abs(d.unrealizedProfitLoss || 0)).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
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
              <td>${Number(h.shares || 0).toLocaleString()}</td>
              <td>৳${Number(h.averageBuyPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
              <td>৳${Number(h.currentPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
              <td>৳${Number(h.marketValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
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

  // Populate recent portfolio transactions (activity list)
  const activityList = document.getElementById("portfolioActivityList");
  if (activityList) {
    try {
      const txRes = await apiRequest(`/transactions?portfolioId=${portfolioId}`);
      const txs = (txRes.data || []).slice(0, 5);
      if (txs.length === 0) {
        activityList.innerHTML = `
          <div class="text-center py-4 text-muted">
            <i class="bi bi-clock-history fs-4 d-block mb-1"></i>
            No transactions in this portfolio yet.
          </div>
        `;
      } else {
        activityList.innerHTML = txs.map(tx => {
          const isBuy = (tx.transactionType || "").toUpperCase() === "BUY";
          const icon = isBuy ? "bi-arrow-down-left" : "bi-arrow-up-right";
          const iconClass = isBuy ? "activity-icon buy" : "activity-icon sell";
          const amountClass = isBuy ? "activity-amount" : "activity-amount positive-text";
          const sign = isBuy ? "-৳" : "+৳";
          const dateStr = new Date(tx.transactionDate).toLocaleDateString("en-US", {
            month: "short", day: "numeric", year: "numeric"
          });
          const total = tx.totalAmount || (Number(tx.quantity ?? 0) * Number(tx.pricePerShare ?? tx.price ?? 0));
          return `
            <div class="activity-item">
              <div class="${iconClass}">
                <i class="bi ${icon}"></i>
              </div>
              <div class="activity-info">
                <strong>${isBuy ? 'Bought' : 'Sold'} ${escapeHtml(tx.tickerSymbol)}</strong>
                <span>${Number(tx.quantity).toLocaleString()} shares · ${dateStr}</span>
              </div>
              <strong class="${amountClass}">${sign}${Number(total).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong>
            </div>
          `;
        }).join("");
      }
    } catch (err) {
      console.error("Failed to load portfolio activity:", err);
      activityList.innerHTML = `<div class="text-center py-3 text-muted">Could not load recent transactions.</div>`;
    }
  }

  // Populate dividend income for the portfolio
  const divIncomeEl = document.getElementById("portfolioDividendIncome");
  if (divIncomeEl) {
    try {
      const divRes = await apiRequest("/dividends/summary");
      const divIncome = divRes.data?.totalIncome || 0;
      divIncomeEl.textContent = "৳" + Number(divIncome).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    } catch (err) {
      console.warn("Could not load dividend income for portfolio page:", err);
      divIncomeEl.textContent = "৳0.00";
    }
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

