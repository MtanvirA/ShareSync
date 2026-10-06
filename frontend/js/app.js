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

async function apiDownloadBlob(endpoint, options = {}) {
  const token = getAuthToken();
  if (!token && !isAuthPage()) {
    window.location.href = "login.html";
    throw new Error("Authentication required.");
  }

  const headers = {
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

  if (!response.ok) {
    if ((response.status === 401 || response.status === 403) && !isAuthPage()) {
      localStorage.removeItem("sharesync_token");
      localStorage.removeItem("sharesync_user");
      window.location.href = "login.html";
    }

    let errorMsg = "Export failed. Please try again.";
    try {
      const errJson = await response.json();
      if (errJson) {
        errorMsg = errJson.message || (errJson.errors ? (Array.isArray(errJson.errors) ? errJson.errors.join(", ") : String(errJson.errors)) : errorMsg);
      }
    } catch (_) {
      // Not JSON
    }

    const safeMsg = sanitizeErrorMessage(errorMsg, response.status);
    const error = new Error(safeMsg);
    error.status = response.status;
    throw error;
  }

  let filename = "";
  const disposition = response.headers.get("Content-Disposition");
  if (disposition && disposition.indexOf("filename=") !== -1) {
    const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
    if (match && match[1]) {
      filename = match[1].replace(/['"]/g, "").trim();
    }
  }

  const blob = await response.blob();
  return { blob, filename };
}

async function setupUserHeader() {
  let user = getCurrentUser();
  if (!user) return;

  const headerUserName = document.getElementById("headerUserName") || document.getElementById("userNameLabel");
  const headerAvatar = document.getElementById("headerAvatar") || document.getElementById("userAvatar");
  const dropdownUserName = document.getElementById("dropdownUserName");
  const dropdownUserEmail = document.getElementById("dropdownUserEmail") || document.getElementById("userEmailLabel");
  const dropdownAvatar = document.getElementById("dropdownAvatar") || document.getElementById("dropdownUserAvatar");
  const dropdownUserRole = document.getElementById("dropdownUserRole");

  const updateUI = (u) => {
    const displayName = (u.name || u.email || "Investor").trim();
    const firstName = displayName.split(" ")[0];

    if (headerUserName) headerUserName.textContent = firstName;
    if (headerAvatar) headerAvatar.textContent = firstName.charAt(0).toUpperCase();

    if (dropdownUserName) dropdownUserName.textContent = displayName;
    if (dropdownUserEmail) dropdownUserEmail.textContent = u.email || "investor@sharesync.com";
    if (dropdownAvatar) dropdownAvatar.textContent = firstName.charAt(0).toUpperCase();
    if (dropdownUserRole) dropdownUserRole.textContent = (u.role || "INVESTOR").toUpperCase();

    if ((u.role || "").toUpperCase() === "ADMIN") {
      const profileDropdown = document.getElementById("profileDropdown");
      if (profileDropdown && !document.getElementById("dropdownAdminLink")) {
        const py2 = profileDropdown.querySelector(".py-2.border-bottom");
        if (py2) {
          const adminA = document.createElement("a");
          adminA.id = "dropdownAdminLink";
          adminA.href = "admin.html";
          adminA.className = "dropdown-item-link px-3 py-2 d-flex align-items-center gap-2 text-danger fw-semibold";
          adminA.innerHTML = '<i class="bi bi-shield-lock text-danger"></i><span>Admin Control Panel</span>';
          py2.prepend(adminA);
        }
      }
      const sidebarNav = document.querySelector(".sidebar-nav");
      if (sidebarNav && !document.getElementById("sidebarAdminLink")) {
        const adminSideA = document.createElement("a");
        adminSideA.id = "sidebarAdminLink";
        adminSideA.href = "admin.html";
        adminSideA.className = "sidebar-link text-danger fw-semibold";
        adminSideA.innerHTML = '<i class="bi bi-shield-lock text-danger"></i><span>Admin Control Panel</span>';
        sidebarNav.appendChild(adminSideA);
      }
    }
  };

  updateUI(user);

  // If email or role is missing from local cache, fetch from /api/auth/me
  if (!user.email || !user.role) {
    try {
      const meRes = await apiRequest("/auth/me");
      if (meRes?.data) {
        user = { ...user, ...meRes.data };
        localStorage.setItem("sharesync_user", JSON.stringify(user));
        updateUI(user);
      }
    } catch (_) {}
  }
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

  const urlParams = new URLSearchParams(window.location.search);
  if (urlParams.get("msg") === "password_changed") {
    showAlert("Password changed successfully! Please sign in with your new password.", "success");
  }

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
      localStorage.setItem("sharesync_login_time", new Date().toISOString());

      showAlert("Sign in successful! Redirecting...", "success");
      setTimeout(() => {
        const role = (data?.data?.role || "").toUpperCase();
        window.location.href = role === "ADMIN" ? "admin.html" : "index.html";
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
      setTimeout(() => {
        try {
          link.scrollIntoView({ inline: "center", block: "nearest", behavior: "smooth" });
        } catch {}
      }, 80);
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

function timeAgo(dateInput) {
  if (!dateInput) return "";
  const date = new Date(dateInput);
  const diffMs = Date.now() - date.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  if (diffSec < 60) return "Just now";
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return `${diffHr}h ago`;
  const diffDays = Math.floor(diffHr / 24);
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString("en-US", { month: "short", day: "numeric" });
}

function setupHeaderDropdowns() {
  const profileBtn = document.getElementById("headerProfileBtn") || document.getElementById("userProfileBtn");
  const profileDropdown = document.getElementById("profileDropdown");
  const notificationBtn = document.getElementById("notificationBtn");
  const notificationDropdown = document.getElementById("notificationDropdown");
  const notificationBadge = document.getElementById("notificationBadge");
  const markAllReadBtn = document.getElementById("markAllNotificationsReadBtn");
  const notifCountBadge = document.getElementById("notificationCountBadge");
  const themeSwitch = document.getElementById("dropdownThemeSwitch");
  const logoutBtn = document.getElementById("dropdownLogoutBtn") || document.getElementById("logoutBtn");

  const closeAllDropdowns = () => {
    if (profileDropdown) profileDropdown.classList.remove("show");
    if (notificationDropdown) notificationDropdown.classList.remove("show");
    if (profileBtn) {
      profileBtn.classList.remove("active");
      profileBtn.setAttribute("aria-expanded", "false");
    }
    if (notificationBtn) {
      notificationBtn.classList.remove("active");
      notificationBtn.setAttribute("aria-expanded", "false");
    }
  };

  // Profile dropdown toggle
  if (profileBtn && profileDropdown) {
    profileBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      const isOpen = profileDropdown.classList.contains("show");
      closeAllDropdowns();
      if (!isOpen) {
        profileDropdown.classList.add("show");
        profileBtn.classList.add("active");
        profileBtn.setAttribute("aria-expanded", "true");
      }
    });

    profileBtn.addEventListener("keydown", (e) => {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        profileBtn.click();
      }
    });
  }

  // Notification dropdown toggle
  if (notificationBtn && notificationDropdown) {
    notificationBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      const isOpen = notificationDropdown.classList.contains("show");
      closeAllDropdowns();
      if (!isOpen) {
        notificationDropdown.classList.add("show");
        notificationBtn.classList.add("active");
        notificationBtn.setAttribute("aria-expanded", "true");
      }
    });
  }

  // Prevent dropdown interior clicks from closing unless clicking action links
  if (profileDropdown) {
    profileDropdown.addEventListener("click", (e) => {
      if (!e.target.closest("a, button")) {
        e.stopPropagation();
      }
    });
  }

  if (notificationDropdown) {
    notificationDropdown.addEventListener("click", (e) => {
      if (!e.target.closest("a, #markAllNotificationsReadBtn")) {
        e.stopPropagation();
      }
    });
  }

  // Close on outside click
  document.addEventListener("click", (e) => {
    if (!e.target.closest(".header-dropdown-wrapper")) {
      closeAllDropdowns();
    }
  });

  // Close on Escape key
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      closeAllDropdowns();
    }
  });

  // Notification read persistence & interactions
  const isRead = localStorage.getItem("sharesync_notifications_read") === "true";
  if (isRead) {
    if (notificationBadge) notificationBadge.classList.add("d-none");
    if (notifCountBadge) {
      notifCountBadge.textContent = "0 new";
      notifCountBadge.className = "badge bg-secondary rounded-pill px-2 py-1";
    }
    document.querySelectorAll(".notification-item.unread").forEach((item) => {
      item.classList.remove("unread");
      const dot = item.querySelector(".unread-dot");
      if (dot) dot.remove();
    });
  }

  if (markAllReadBtn) {
    markAllReadBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      localStorage.setItem("sharesync_notifications_read", "true");
      if (notificationBadge) notificationBadge.classList.add("d-none");
      if (notifCountBadge) {
        notifCountBadge.textContent = "0 new";
        notifCountBadge.className = "badge bg-secondary rounded-pill px-2 py-1";
      }
      document.querySelectorAll(".notification-item.unread").forEach((item) => {
        item.classList.remove("unread");
        const dot = item.querySelector(".unread-dot");
        if (dot) dot.remove();
      });
      if (typeof showToast === "function") {
        showToast("All notifications marked as read", "info");
      }
    });
  }

  // Individual notification item clicks
  document.querySelectorAll(".notification-item").forEach((item) => {
    item.addEventListener("click", (e) => {
      e.stopPropagation();
      if (item.classList.contains("unread")) {
        item.classList.remove("unread");
        const dot = item.querySelector(".unread-dot");
        if (dot) dot.remove();

        const remainingUnread = document.querySelectorAll(".notification-item.unread").length;
        if (remainingUnread === 0) {
          localStorage.setItem("sharesync_notifications_read", "true");
          if (notificationBadge) notificationBadge.classList.add("d-none");
          if (notifCountBadge) {
            notifCountBadge.textContent = "0 new";
            notifCountBadge.className = "badge bg-secondary rounded-pill px-2 py-1";
          }
        } else if (notifCountBadge) {
          notifCountBadge.textContent = `${remainingUnread} new`;
        }
      }
    });
  });

  // Dropdown Theme Switch
  if (themeSwitch) {
    const isDark = (document.documentElement.getAttribute("data-theme") || getStoredThemePreference()) === "dark";
    themeSwitch.checked = isDark;
    themeSwitch.addEventListener("change", () => {
      const next = themeSwitch.checked ? "dark" : "light";
      setThemePreference(next);
      if (typeof showToast === "function") {
        showToast(`Switched to ${next === "dark" ? "Dark" : "Light"} mode`, "info");
      }
    });
  }

  // Dropdown Sign Out button
  if (logoutBtn) {
    logoutBtn.addEventListener("click", async (e) => {
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

        const isDark = document.documentElement.getAttribute("data-theme") === "dark";
        const isUp = (perf.netChange || 0) >= 0;
        const strokeColor = isUp ? (isDark ? "#10B981" : "#16A34A") : (isDark ? "#F43F5E" : "#DC2626");
        const gradTop = isUp ? (isDark ? "rgba(16, 185, 129, 0.22)" : "rgba(22, 163, 74, 0.16)") : (isDark ? "rgba(244, 63, 94, 0.22)" : "rgba(220, 38, 38, 0.16)");
        const gradBottom = isUp ? "rgba(16, 185, 129, 0.0)" : "rgba(244, 63, 94, 0.0)";

        const ctx = canvas.getContext("2d");
        const gradient = ctx.createLinearGradient(0, 0, 0, 220);
        gradient.addColorStop(0, gradTop);
        gradient.addColorStop(1, gradBottom);

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
              pointRadius: values.length > 30 ? 0 : 3.5,
              pointHoverRadius: 6,
              pointBackgroundColor: strokeColor,
              pointBorderColor: isDark ? "#111726" : "#ffffff",
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
                backgroundColor: isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)",
                titleColor: isDark ? "#F8FAFC" : "#FFFFFF",
                bodyColor: isDark ? "#E2E8F0" : "#FFFFFF",
                borderColor: isDark ? "rgba(255, 255, 255, 0.12)" : "transparent",
                borderWidth: isDark ? 1 : 0,
                titleFont: { size: 12, weight: "600" },
                bodyFont: { size: 12 },
                padding: 10,
                cornerRadius: 8,
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
                  color: isDark ? "#94A3B8" : "#64748b",
                  maxTicksLimit: 7
                }
              },
              y: {
                grid: {
                  color: isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(226, 232, 240, 0.7)",
                  drawBorder: false
                },
                ticks: {
                  font: { size: 11 },
                  color: isDark ? "#94A3B8" : "#64748b",
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
                      <strong><a href="company.html?id=${h.companyId}" class="text-decoration-none text-body fw-bold">${escapeHtml(h.tickerSymbol)}</a></strong>
                      <small><a href="company.html?id=${h.companyId}" class="text-decoration-none text-muted">${escapeHtml(h.companyName)}</a></small>
                    </div>
                  </div>
                </td>
                <td>${Number(h.shares).toLocaleString()}</td>
                <td>${formatBDT(h.averageBuyPrice)}</td>
                <td>
                  ${formatBDT(h.currentPrice)}
                  <button type="button" class="price-history-btn ms-1" onclick="openPriceHistoryModal(${h.companyId}, '${escapeHtml(h.tickerSymbol)}', '${escapeHtml(h.companyName)}', ${h.currentPrice})" title="View Price Trend &amp; History">
                    <i class="bi bi-graph-up"></i>
                  </button>
                </td>
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
  const gridColor = isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(226, 232, 240, 0.7)";
  const textColor = isDark ? "#94A3B8" : "#64748B";

  if (typeof Chart !== "undefined") {
    Chart.defaults.color = textColor;
    Chart.defaults.borderColor = gridColor;
  }

  // 1. Dashboard Portfolio Chart
  if (typeof portfolioChart !== "undefined" && portfolioChart && portfolioChart.ctx) {
    try {
      const ds = portfolioChart.data.datasets[0];
      if (ds && ds.data && ds.data.length > 0) {
        const firstVal = ds.data[0] || 0;
        const lastVal = ds.data[ds.data.length - 1] || 0;
        const isUp = lastVal >= firstVal;

        const strokeColor = isUp ? (isDark ? "#10B981" : "#16A34A") : (isDark ? "#F43F5E" : "#DC2626");
        const gradTop = isUp ? (isDark ? "rgba(16, 185, 129, 0.22)" : "rgba(22, 163, 74, 0.16)") : (isDark ? "rgba(244, 63, 94, 0.22)" : "rgba(220, 38, 38, 0.16)");
        const gradBottom = isUp ? "rgba(16, 185, 129, 0.0)" : "rgba(244, 63, 94, 0.0)";

        const gradient = portfolioChart.ctx.createLinearGradient(0, 0, 0, 220);
        gradient.addColorStop(0, gradTop);
        gradient.addColorStop(1, gradBottom);

        ds.borderColor = strokeColor;
        ds.backgroundColor = gradient;
        ds.pointBackgroundColor = strokeColor;
        ds.pointBorderColor = isDark ? "#111726" : "#ffffff";
      }
      if (portfolioChart.options?.scales?.x) {
        if (portfolioChart.options.scales.x.ticks) portfolioChart.options.scales.x.ticks.color = textColor;
      }
      if (portfolioChart.options?.scales?.y) {
        if (portfolioChart.options.scales.y.ticks) portfolioChart.options.scales.y.ticks.color = textColor;
        if (portfolioChart.options.scales.y.grid) portfolioChart.options.scales.y.grid.color = gridColor;
      }
      if (portfolioChart.options?.plugins?.tooltip) {
        portfolioChart.options.plugins.tooltip.backgroundColor = isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)";
        portfolioChart.options.plugins.tooltip.borderColor = isDark ? "rgba(255, 255, 255, 0.12)" : "transparent";
        portfolioChart.options.plugins.tooltip.borderWidth = isDark ? 1 : 0;
      }
      portfolioChart.update();
    } catch (_) {}
  }

  // 2. Report Value Chart on Reports Page
  if (typeof reportValueChartInstance !== "undefined" && reportValueChartInstance && reportValueChartInstance.ctx) {
    try {
      const ds = reportValueChartInstance.data.datasets[0];
      if (ds) {
        const strokeColor = isDark ? "#38BDF8" : "#1D4ED8";
        const gradTop = isDark ? "rgba(56, 189, 248, 0.2)" : "rgba(29, 78, 216, 0.12)";
        const gradBottom = isDark ? "rgba(56, 189, 248, 0.0)" : "rgba(29, 78, 216, 0.0)";

        const gradient = reportValueChartInstance.ctx.createLinearGradient(0, 0, 0, 260);
        gradient.addColorStop(0, gradTop);
        gradient.addColorStop(1, gradBottom);

        ds.borderColor = strokeColor;
        ds.backgroundColor = gradient;
        ds.pointBackgroundColor = strokeColor;
        ds.pointBorderColor = isDark ? "#111726" : "#ffffff";
      }
      if (reportValueChartInstance.options?.scales?.x) {
        if (reportValueChartInstance.options.scales.x.ticks) reportValueChartInstance.options.scales.x.ticks.color = textColor;
      }
      if (reportValueChartInstance.options?.scales?.y) {
        if (reportValueChartInstance.options.scales.y.ticks) reportValueChartInstance.options.scales.y.ticks.color = textColor;
        if (reportValueChartInstance.options.scales.y.grid) reportValueChartInstance.options.scales.y.grid.color = gridColor;
      }
      if (reportValueChartInstance.options?.plugins?.tooltip) {
        reportValueChartInstance.options.plugins.tooltip.backgroundColor = isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)";
      }
      reportValueChartInstance.update();
    } catch (_) {}
  }

  // 3. Report Allocation Donut
  if (typeof reportAllocationChartInstance !== "undefined" && reportAllocationChartInstance) {
    try {
      const ds = reportAllocationChartInstance.data.datasets[0];
      if (ds && ds.data && ds.data.length > 0) {
        const lightPalette = ["#1D4ED8", "#10B981", "#F59E0B", "#8B5CF6", "#06B6D4", "#EC4899", "#64748B"];
        const darkPalette = ["#3B82F6", "#10B981", "#F59E0B", "#A855F7", "#22D3EE", "#F43F5E", "#94A3B8"];
        const colors = isDark ? darkPalette : lightPalette;
        ds.backgroundColor = ds.data.map((_, i) => colors[i % colors.length]);
        ds.borderColor = isDark ? "#111726" : "#ffffff";
        ds.borderWidth = 2;
      }
      reportAllocationChartInstance.update();
    } catch (_) {}
  }

  // 4. Report Activity Bar Chart
  if (typeof reportActivityChartInstance !== "undefined" && reportActivityChartInstance) {
    try {
      if (reportActivityChartInstance.data.datasets[0]) {
        reportActivityChartInstance.data.datasets[0].backgroundColor = isDark ? "#10B981" : "#059669";
      }
      if (reportActivityChartInstance.data.datasets[1]) {
        reportActivityChartInstance.data.datasets[1].backgroundColor = isDark ? "#F59E0B" : "#D97706";
      }
      if (reportActivityChartInstance.options?.scales?.x) {
        if (reportActivityChartInstance.options.scales.x.ticks) reportActivityChartInstance.options.scales.x.ticks.color = textColor;
      }
      if (reportActivityChartInstance.options?.scales?.y) {
        if (reportActivityChartInstance.options.scales.y.ticks) reportActivityChartInstance.options.scales.y.ticks.color = textColor;
        if (reportActivityChartInstance.options.scales.y.grid) reportActivityChartInstance.options.scales.y.grid.color = gridColor;
      }
      if (reportActivityChartInstance.options?.plugins?.legend?.labels) {
        reportActivityChartInstance.options.plugins.legend.labels.color = textColor;
      }
      reportActivityChartInstance.update();
    } catch (_) {}
  }
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

  const themeSwitch = document.getElementById("dropdownThemeSwitch");
  if (themeSwitch) {
    themeSwitch.checked = (effectiveTheme === "dark");
  }

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
  const profileAlert = document.getElementById("profileAlert");
  const showProfileAlert = (msg, tone = "success") => {
    if (!profileAlert) return;
    profileAlert.className = `alert alert-${tone} mb-3`;
    profileAlert.textContent = msg;
    profileAlert.classList.remove("d-none");
  };

  if (profileForm) {
    profileForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      if (profileAlert) profileAlert.classList.add("d-none");
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

        const successMsg = res?.message || "Profile updated successfully.";
        showProfileAlert(successMsg, "success");
        showToast(successMsg, "success");
      } catch (err) {
        showProfileAlert(err.message || "Failed to update profile.", "danger");
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

  // 5. Active Session & Security Details (Feature 9)
  const lastLoginEl = document.getElementById("securityLastLogin");
  if (lastLoginEl) {
    const loginTime = localStorage.getItem("sharesync_login_time");
    if (loginTime) {
      const d = new Date(loginTime);
      lastLoginEl.textContent = isNaN(d.getTime()) ? "Active (Current Session)" : d.toLocaleString("en-US", { month: "short", day: "numeric", year: "numeric", hour: "2-digit", minute: "2-digit" });
    } else {
      lastLoginEl.textContent = "Active (Current Session)";
    }
  }

  const expiryEl = document.getElementById("securitySessionExpiry");
  if (expiryEl) {
    const user = getCurrentUser();
    if (user?.expiresAt) {
      const d = new Date(user.expiresAt);
      expiryEl.textContent = isNaN(d.getTime()) ? "Bearer JWT (Active)" : `Valid until ${d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}`;
    } else {
      expiryEl.textContent = "Bearer JWT (7 Days)";
    }
  }

  const deviceEl = document.getElementById("securityClientDevice");
  if (deviceEl) {
    const ua = navigator.userAgent;
    let browser = "Web Browser";
    if (ua.includes("Edg/")) browser = "Microsoft Edge";
    else if (ua.includes("Chrome/")) browser = "Google Chrome";
    else if (ua.includes("Firefox/")) browser = "Mozilla Firefox";
    else if (ua.includes("Safari/")) browser = "Apple Safari";

    let os = "Desktop";
    if (ua.includes("Windows")) os = "Windows";
    else if (ua.includes("Macintosh")) os = "macOS";
    else if (ua.includes("Linux")) os = "Linux";
    else if (ua.includes("Android")) os = "Android";
    else if (ua.includes("iPhone") || ua.includes("iPad")) os = "iOS";

    deviceEl.textContent = `${browser} on ${os}`;
  }

  const secLogoutBtn = document.getElementById("securityLogoutBtn");
  if (secLogoutBtn) {
    secLogoutBtn.addEventListener("click", async () => {
      secLogoutBtn.disabled = true;
      secLogoutBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span> Signing out...';
      try {
        await apiRequest("/auth/logout", { method: "POST" });
      } catch {
        // ignore network error on logout
      }
      localStorage.removeItem("sharesync_token");
      localStorage.removeItem("sharesync_user");
      localStorage.removeItem("sharesync_login_time");
      window.location.href = "login.html";
    });
  }

  // 6. Change Password Form (PUT /api/auth/change-password)
  const passwordForm = document.getElementById("changePasswordForm");
  if (passwordForm) {
    const pwAlert = document.getElementById("changePasswordAlert");
    const showPwAlert = (msg, tone = "danger") => {
      if (!pwAlert) return;
      pwAlert.className = `alert alert-${tone} mb-3`;
      pwAlert.textContent = msg;
      pwAlert.classList.remove("d-none");
    };

    passwordForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      if (pwAlert) pwAlert.classList.add("d-none");

      const currentPw = document.getElementById("currentPassword")?.value || "";
      const newPw = document.getElementById("newPassword")?.value || "";
      const confirmPw = document.getElementById("confirmNewPassword")?.value || "";

      if (!currentPw) {
        showPwAlert("Current password is required.", "danger");
        showToast("Current password is required.", "danger");
        return;
      }

      if (newPw.length < 6) {
        showPwAlert("New password must be at least 6 characters long.", "danger");
        showToast("New password must be at least 6 characters.", "danger");
        return;
      }

      if (newPw !== confirmPw) {
        showPwAlert("New password and confirm password do not match.", "danger");
        showToast("New password and confirm password do not match.", "danger");
        return;
      }

      const updateBtn = document.getElementById("updatePasswordBtn");
      const originalText = updateBtn.innerHTML;
      updateBtn.disabled = true;
      updateBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span> Updating password...';

      try {
        const res = await apiRequest("/auth/change-password", {
          method: "PUT",
          body: JSON.stringify({
            currentPassword: currentPw,
            newPassword: newPw,
            confirmNewPassword: confirmPw
          })
        });

        const successMsg = res?.message || "Password changed successfully! Please log in with your new password.";
        showPwAlert(`${successMsg} Terminating session and redirecting to sign in...`, "success");
        showToast(successMsg, "success");
        passwordForm.reset();

        // Invalidate current session and require re-authentication
        localStorage.removeItem("sharesync_token");
        localStorage.removeItem("sharesync_user");
        localStorage.removeItem("sharesync_login_time");

        setTimeout(() => {
          window.location.href = "login.html?msg=password_changed";
        }, 1500);
      } catch (err) {
        showPwAlert(err.message || "Failed to change password. Please verify your current credentials.", "danger");
        showToast(err.message || "Failed to change password.", "danger");
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
        const txs = Array.isArray(res?.data) ? res.data : (res?.data?.items || []);
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
  setupHeaderDropdowns();

  if (typeof setupLoginForm === "function") setupLoginForm();
  if (typeof setupRegisterForm === "function") setupRegisterForm();
  if (typeof setupDashboard === "function") setupDashboard();
  if (typeof setupTransactionForm === "function") setupTransactionForm();
  if (typeof setupCsvTransactionImport === "function") setupCsvTransactionImport();
  if (typeof setupWatchlistForm === "function") setupWatchlistForm();
  if (typeof setupDividendForm === "function") setupDividendForm();
  if (typeof setupReports === "function") setupReports();
  if (typeof setupTransactionFilters === "function") setupTransactionFilters();
  if (typeof setupWatchlistFilter === "function") setupWatchlistFilter();
  if (typeof setupDividendFilter === "function") setupDividendFilter();
  if (typeof setupPortfolioManagement === "function") setupPortfolioManagement();
  if (typeof setupDseSync === "function") setupDseSync();
  if (typeof setupSettingsPage === "function") setupSettingsPage();
  if (typeof setupCompanyDetailPage === "function") setupCompanyDetailPage();
  if (typeof setupAnalyticsPage === "function") setupAnalyticsPage();
  if (typeof setupAlertsPage === "function") setupAlertsPage();
  if (typeof setupAdminPage === "function") setupAdminPage();
  if (typeof setupSimulatorPage === "function") setupSimulatorPage();
  if (typeof setupGoalsPage === "function") setupGoalsPage();
  if (typeof setupActivityTimeline === "function") setupActivityTimeline();
  if (typeof setupGlobalSearch === "function") setupGlobalSearch();
});

// =========================================
// DIVIDEND FORM
// =========================================

// =========================================
// DIVIDEND MANAGEMENT
// =========================================

let dividendCompaniesCache = [];
let dividendMonthlyChartInstance = null;
let dividendCompanyChartInstance = null;

async function setupDividendForm() {
  const form = document.getElementById("dividendForm");
  const tableBody = document.getElementById("dividendTableBody");
  const analyticsContainer = document.getElementById("dividendMonthlyChart");

  if (!form && !tableBody && !analyticsContainer) {
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

  // Filters
  const portfolioFilter = document.getElementById("dividendPortfolioFilter");
  const companyAnalyticsFilter = document.getElementById("dividendCompanyAnalyticsFilter");
  const yearFilter = document.getElementById("dividendYearFilter");
  const startDateFilter = document.getElementById("dividendStartDateFilter");
  const endDateFilter = document.getElementById("dividendEndDateFilter");
  const resetFiltersBtn = document.getElementById("dividendResetFiltersBtn");
  const companyFilter = document.getElementById("dividendCompanyFilter");
  const periodFilter = document.getElementById("dividendFilter");

  if (form) setupInlineValidation(form);

  // 1. Load portfolios into filter dropdown
  if (portfolioFilter) {
    try {
      const pRes = await apiRequest("/portfolios");
      const portfolios = pRes.data || [];
      portfolioFilter.innerHTML = '<option value="">Consolidated (All Portfolios)</option>' +
        portfolios.map(p => `<option value="${p.portfolioId}">${escapeHtml(p.name)}</option>`).join("");
    } catch (err) {
      console.error("Failed to load portfolios for dividend filter:", err);
    }
  }

  // 2. Load companies for the dropdowns
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

    if (companyAnalyticsFilter) {
      companyAnalyticsFilter.innerHTML = '<option value="">All Companies</option>' +
        dividendCompaniesCache.map(c => `<option value="${c.companyId}">${escapeHtml(c.tickerSymbol)} - ${escapeHtml(c.companyName)}</option>`).join("");
    }
  } catch (err) {
    console.error("Failed to load companies for dividends:", err);
  }

  // 3. Populate Year filter dropdown
  if (yearFilter) {
    const currentYear = new Date().getFullYear();
    let yearsHtml = '<option value="">All Years</option>';
    for (let y = currentYear + 1; y >= currentYear - 5; y--) {
      yearsHtml += `<option value="${y}" ${y === currentYear ? 'selected' : ''}>${y}</option>`;
    }
    yearFilter.innerHTML = yearsHtml;
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
        await loadDividendAnalytics();
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
      await loadDividendAnalytics();
      await loadDividendHistory();
    } catch (err) {
      console.error("Failed to delete dividend:", err);
      showToast("Cannot delete dividend: " + err.message, "danger");
    }
  };

  // Filter change events
  const onFilterChange = async () => {
    await loadDividendAnalytics();
    await loadDividendHistory();
  };

  if (portfolioFilter) portfolioFilter.addEventListener("change", onFilterChange);
  if (companyAnalyticsFilter) companyAnalyticsFilter.addEventListener("change", () => {
    if (companyFilter) {
      companyFilter.value = companyAnalyticsFilter.value ? companyAnalyticsFilter.value : "all";
    }
    onFilterChange();
  });
  if (yearFilter) yearFilter.addEventListener("change", onFilterChange);
  if (startDateFilter) startDateFilter.addEventListener("change", onFilterChange);
  if (endDateFilter) endDateFilter.addEventListener("change", onFilterChange);

  if (resetFiltersBtn) {
    resetFiltersBtn.addEventListener("click", () => {
      if (portfolioFilter) portfolioFilter.value = "";
      if (companyAnalyticsFilter) companyAnalyticsFilter.value = "";
      if (yearFilter) yearFilter.value = "";
      if (startDateFilter) startDateFilter.value = "";
      if (endDateFilter) endDateFilter.value = "";
      if (companyFilter) companyFilter.value = "all";
      if (periodFilter) periodFilter.value = "all";
      onFilterChange();
    });
  }

  if (companyFilter) companyFilter.addEventListener("change", () => {
    if (companyAnalyticsFilter) {
      companyAnalyticsFilter.value = companyFilter.value !== "all" ? companyFilter.value : "";
    }
    onFilterChange();
  });
  if (periodFilter) periodFilter.addEventListener("change", loadDividendHistory);

  // Initial loads
  await loadDividendAnalytics();
  await loadDividendHistory();
}

async function loadDividendSummary() {
  await loadDividendAnalytics();
}

async function loadDividendAnalytics() {
  const portfolioFilter = document.getElementById("dividendPortfolioFilter");
  const companyAnalyticsFilter = document.getElementById("dividendCompanyAnalyticsFilter");
  const yearFilter = document.getElementById("dividendYearFilter");
  const startDateFilter = document.getElementById("dividendStartDateFilter");
  const endDateFilter = document.getElementById("dividendEndDateFilter");

  const params = new URLSearchParams();
  if (portfolioFilter && portfolioFilter.value) {
    params.append("portfolioId", portfolioFilter.value);
  }
  if (companyAnalyticsFilter && companyAnalyticsFilter.value) {
    params.append("companyId", companyAnalyticsFilter.value);
  }
  if (yearFilter && yearFilter.value) {
    params.append("year", yearFilter.value);
  }
  if (startDateFilter && startDateFilter.value) {
    params.append("startDate", startDateFilter.value);
  }
  if (endDateFilter && endDateFilter.value) {
    params.append("endDate", endDateFilter.value);
  }

  const queryStr = params.toString() ? `?${params.toString()}` : "";

  try {
    const res = await apiRequest(`/dividends/analytics${queryStr}`);
    const data = res.data;
    if (!data) return;

    // 1. KPI Summary Cards
    const totalEl = document.getElementById("summaryTotalIncome");
    const totalEventsEl = document.getElementById("summaryTotalEvents");
    const yearEl = document.getElementById("summaryThisYearIncome");
    const yearLabelEl = document.getElementById("summaryThisYearLabel");
    const monthEl = document.getElementById("summaryThisMonthIncome");
    const yieldEl = document.getElementById("summaryDividendYield");
    const upcomingEl = document.getElementById("summaryUpcomingIncome");
    const countEl = document.getElementById("summaryCompaniesCount");

    if (totalEl) totalEl.textContent = "৳" + Number(data.totalDividendIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (totalEventsEl) totalEventsEl.textContent = `${data.totalEventsCount || 0} dividend ${data.totalEventsCount === 1 ? 'payout' : 'payouts'}`;
    if (yearEl) yearEl.textContent = "৳" + Number(data.thisYearIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (yearLabelEl) yearLabelEl.textContent = `Year ${data.filterYear || new Date().getFullYear()}`;
    if (monthEl) monthEl.textContent = "৳" + Number(data.thisMonthIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (yieldEl) yieldEl.textContent = Number(data.averageDividendYield || 0).toFixed(2) + "%";
    if (upcomingEl) upcomingEl.textContent = "Upcoming: ৳" + Number(data.upcomingIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (countEl) countEl.textContent = data.companyBreakdown ? data.companyBreakdown.length : 0;

    // 2. Monthly Chart
    renderDividendMonthlyChart(data.monthlyIncome || [], data.filterYear);

    // 3. Company Breakdown Chart
    renderDividendCompanyChart(data.companyBreakdown || []);

    // 4. Upcoming Dividends Calendar / List
    renderUpcomingDividendsList(data.upcomingDividends || []);

    // 5. Sector Breakdown
    renderDividendSectorBreakdown(data.sectorBreakdown || []);
  } catch (err) {
    console.error("Failed to load dividend analytics:", err);
  }
}

function renderDividendMonthlyChart(monthlyIncome, filterYear) {
  const canvas = document.getElementById("dividendMonthlyChart");
  const emptyState = document.getElementById("monthlyChartEmptyState");
  const yearBadge = document.getElementById("monthlyChartYearBadge");
  const subtitle = document.getElementById("monthlyChartSubtitle");

  if (!canvas) return;

  const currentYear = filterYear || new Date().getFullYear();
  if (yearBadge) yearBadge.textContent = currentYear;
  if (subtitle) subtitle.textContent = `Monthly cash distributions received in ${currentYear}`;

  if (dividendMonthlyChartInstance) {
    dividendMonthlyChartInstance.destroy();
    dividendMonthlyChartInstance = null;
  }

  const hasData = monthlyIncome.some(m => (m.income || 0) > 0);
  if (!hasData) {
    canvas.style.display = "none";
    if (emptyState) emptyState.classList.remove("d-none");
    return;
  }

  canvas.style.display = "block";
  if (emptyState) emptyState.classList.add("d-none");

  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const ctx = canvas.getContext("2d");

  dividendMonthlyChartInstance = new Chart(ctx, {
    type: "bar",
    data: {
      labels: monthlyIncome.map(m => m.monthName),
      datasets: [
        {
          label: "Dividend Income",
          data: monthlyIncome.map(m => m.income),
          backgroundColor: isDark ? "rgba(59, 130, 246, 0.75)" : "rgba(37, 99, 235, 0.82)",
          borderColor: isDark ? "#60A5FA" : "#1D4ED8",
          borderWidth: 1.5,
          borderRadius: 6,
          hoverBackgroundColor: isDark ? "#93C5FD" : "#1E40AF"
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label: context => {
              const val = context.raw;
              const item = monthlyIncome[context.dataIndex];
              const events = item ? item.eventCount : 0;
              return ` Income: ৳${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${events} ${events === 1 ? 'event' : 'events'})`;
            }
          }
        }
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: { color: isDark ? "#94A3B8" : "#64748B", font: { size: 11 } }
        },
        y: {
          beginAtZero: true,
          grid: { color: isDark ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.05)" },
          ticks: {
            color: isDark ? "#94A3B8" : "#64748B",
            font: { size: 11 },
            callback: value => "৳" + Number(value).toLocaleString()
          }
        }
      }
    }
  });
}

function renderDividendCompanyChart(companies) {
  const canvas = document.getElementById("dividendCompanyChart");
  const emptyState = document.getElementById("companyChartEmptyState");
  const countBadge = document.getElementById("companyChartCountBadge");

  if (!canvas) return;

  if (countBadge) countBadge.textContent = `${companies.length} ${companies.length === 1 ? 'Company' : 'Companies'}`;

  if (dividendCompanyChartInstance) {
    dividendCompanyChartInstance.destroy();
    dividendCompanyChartInstance = null;
  }

  if (!companies || companies.length === 0) {
    canvas.style.display = "none";
    if (emptyState) emptyState.classList.remove("d-none");
    return;
  }

  canvas.style.display = "block";
  if (emptyState) emptyState.classList.add("d-none");

  const palette = [
    "#2563EB", "#10B981", "#8B5CF6", "#F59E0B", "#06B6D4",
    "#EC4899", "#6366F1", "#14B8A6", "#F97316", "#64748B"
  ];
  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const ctx = canvas.getContext("2d");

  dividendCompanyChartInstance = new Chart(ctx, {
    type: "doughnut",
    data: {
      labels: companies.map(c => c.tickerSymbol),
      datasets: [
        {
          data: companies.map(c => c.totalIncome),
          backgroundColor: palette.slice(0, companies.length),
          borderWidth: 2,
          borderColor: isDark ? "#1E293B" : "#FFFFFF"
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: "bottom",
          labels: {
            boxWidth: 12,
            font: { size: 11 },
            color: isDark ? "#CBD5E1" : "#475569"
          }
        },
        tooltip: {
          callbacks: {
            label: context => {
              const val = context.raw;
              const c = companies[context.dataIndex];
              const pct = c?.incomePercentage || 0;
              const yieldStr = c?.dividendYield ? ` | Yield: ${c.dividendYield.toFixed(2)}%` : "";
              return ` ${context.label}: ৳${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${pct.toFixed(1)}%${yieldStr})`;
            }
          }
        }
      },
      cutout: "60%"
    }
  });
}

function renderUpcomingDividendsList(upcoming) {
  const container = document.getElementById("upcomingDividendsContainer");
  const badge = document.getElementById("upcomingBadge");
  if (!container) return;

  if (badge) badge.textContent = `${upcoming.length} Scheduled`;

  if (!upcoming || upcoming.length === 0) {
    container.innerHTML = `
      <div class="text-center py-4 text-muted">
        <i class="bi bi-calendar-check fs-2 d-block mb-1 text-secondary opacity-50"></i>
        <span class="small">No upcoming dividend events currently scheduled for your holdings.</span>
      </div>
    `;
    return;
  }

  container.innerHTML = upcoming.map(item => {
    const payDate = new Date(item.paymentDate);
    const dateStr = payDate.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
    const dpsStr = "৳" + Number(item.dividendPerShare || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const estStr = "৳" + Number(item.estimatedIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const days = item.daysUntilPayment;

    let dayBadge = "";
    if (days === 0) {
      dayBadge = '<span class="badge bg-danger">Payable Today</span>';
    } else if (days === 1) {
      dayBadge = '<span class="badge bg-warning text-dark">Tomorrow</span>';
    } else {
      dayBadge = `<span class="badge bg-primary-subtle text-primary border border-primary-subtle">In ${days} days</span>`;
    }

    return `
      <div class="p-3 mb-2 rounded-2 border d-flex flex-wrap align-items-center justify-content-between gap-2" style="background: var(--color-surface);">
        <div class="d-flex align-items-center gap-3">
          <div class="rounded-circle d-flex align-items-center justify-content-center fw-bold" style="width: 40px; height: 40px; background: rgba(37, 99, 235, 0.12); color: var(--color-accent); font-size: 13px;">
            ${escapeHtml(item.tickerSymbol.slice(0, 3))}
          </div>
          <div>
            <div class="d-flex align-items-center gap-2">
              <strong class="text-dark-emphasis">${escapeHtml(item.companyName)}</strong>
              <small class="badge bg-secondary-subtle text-secondary border px-1" style="font-size: 10px;">${escapeHtml(item.tickerSymbol)}</small>
            </div>
            <div class="small text-muted d-flex align-items-center gap-2 mt-1">
              <span>Payment: <strong>${dateStr}</strong></span>
              ${item.sectorName ? `<span>• ${escapeHtml(item.sectorName)}</span>` : ""}
            </div>
          </div>
        </div>
        <div class="text-end ms-auto">
          <div class="d-flex align-items-center justify-content-end gap-2 mb-1">
            ${dayBadge}
            <strong class="text-success" style="font-size: 15px;">${estStr}</strong>
          </div>
          <div class="small text-muted font-monospace">
            DPS: ${dpsStr} | ${Number(item.userSharesHeld || 0).toLocaleString()} shares held
          </div>
        </div>
      </div>
    `;
  }).join("");
}

function renderDividendSectorBreakdown(sectors) {
  const container = document.getElementById("dividendSectorContainer");
  if (!container) return;

  if (!sectors || sectors.length === 0) {
    container.innerHTML = `
      <div class="text-center py-4 text-muted">
        <i class="bi bi-pie-chart-fill fs-2 d-block mb-1 text-secondary opacity-50"></i>
        <span class="small">No sector breakdown data available.</span>
      </div>
    `;
    return;
  }

  container.innerHTML = sectors.map((s, idx) => {
    const palette = ["#2563EB", "#10B981", "#8B5CF6", "#F59E0B", "#06B6D4", "#EC4899"];
    const color = palette[idx % palette.length];
    const amountStr = "৳" + Number(s.totalIncome || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const pct = s.incomePercentage.toFixed(1);

    return `
      <div class="mb-3">
        <div class="d-flex align-items-center justify-content-between mb-1" style="font-size: 13px;">
          <span class="fw-semibold text-dark-emphasis">
            ${escapeHtml(s.sectorName)}
            <small class="text-muted fw-normal">(${s.companyCount} ${s.companyCount === 1 ? 'co.' : 'cos.'})</small>
          </span>
          <span class="font-monospace text-muted">${amountStr} <strong class="text-dark-emphasis">(${pct}%)</strong></span>
        </div>
        <div class="progress" style="height: 6px; background-color: rgba(148, 163, 184, 0.2);">
          <div class="progress-bar rounded" role="progressbar" style="width: ${pct}%; background-color: ${color};" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100"></div>
        </div>
      </div>
    `;
  }).join("");
}

async function loadDividendHistory() {
  const tableBody = document.getElementById("dividendTableBody");
  const countSubtitle = document.getElementById("dividendCountSubtitle");
  const portfolioFilter = document.getElementById("dividendPortfolioFilter");
  const companyAnalyticsFilter = document.getElementById("dividendCompanyAnalyticsFilter");
  const companyFilter = document.getElementById("dividendCompanyFilter");
  const periodFilter = document.getElementById("dividendFilter");
  const yearFilter = document.getElementById("dividendYearFilter");
  const startDateFilter = document.getElementById("dividendStartDateFilter");
  const endDateFilter = document.getElementById("dividendEndDateFilter");

  if (!tableBody) return;

  const params = new URLSearchParams();

  // Company filter
  if (companyAnalyticsFilter && companyAnalyticsFilter.value) {
    params.append("companyId", companyAnalyticsFilter.value);
  } else if (companyFilter && companyFilter.value !== "all") {
    params.append("companyId", companyFilter.value);
  }

  // Period / Year filter
  if (yearFilter && yearFilter.value) {
    params.append("year", yearFilter.value);
  } else if (periodFilter && periodFilter.value === "current") {
    params.append("year", new Date().getFullYear());
  } else if (periodFilter && periodFilter.value === "previous") {
    params.append("year", new Date().getFullYear() - 1);
  }

  // Dates
  if (startDateFilter && startDateFilter.value) {
    params.append("startDate", startDateFilter.value);
  }
  if (endDateFilter && endDateFilter.value) {
    params.append("endDate", endDateFilter.value);
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
                    <strong><a href="company.html?id=${item.companyId}" class="text-decoration-none text-body fw-bold">${escapeHtml(item.tickerSymbol)}</a></strong>
                    <small><a href="company.html?id=${item.companyId}" class="text-decoration-none text-muted">${escapeHtml(item.companyName)}</a></small>
                  </div>
                </div>
              <td>
                ${currentPriceFormatted}
                <button type="button" class="price-history-btn ms-1" onclick="openPriceHistoryModal(${item.companyId}, '${escapeHtml(item.tickerSymbol)}', '${escapeHtml(item.companyName)}', ${item.currentPrice})" title="View Price Trend &amp; History">
                  <i class="bi bi-graph-up"></i>
                </button>
              </td>
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
                <button type="button" class="table-action-button text-primary me-1" onclick="openPriceHistoryModal(${item.companyId}, '${escapeHtml(item.tickerSymbol)}', '${escapeHtml(item.companyName)}', ${item.currentPrice})" title="View Price History">
                  <i class="bi bi-graph-up"></i>
                </button>
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
      let shares = 0;
      if (typeof res?.data === "number") {
        shares = res.data;
      } else if (res?.data && typeof res.data.availableShares === "number") {
        shares = res.data.availableShares;
      } else if (res?.data && typeof res.data.availableQuantity === "number") {
        shares = res.data.availableQuantity;
      } else if (res && typeof res.availableShares === "number") {
        shares = res.availableShares;
      } else if (res?.data && !isNaN(Number(res.data))) {
        shares = Number(res.data);
      }
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

  const availableHintEl = document.getElementById("availableSharesHint");
  if (availableHintEl && quantityInput) {
    availableHintEl.style.cursor = "pointer";
    availableHintEl.title = "Click to set quantity to available shares";
    availableHintEl.addEventListener("click", () => {
      const availableValEl = document.getElementById("availableSharesVal");
      const num = parseFloat(availableValEl?.textContent?.replace(/,/g, "") || "0");
      if (num > 0) {
        quantityInput.value = num;
        quantityInput.dispatchEvent(new Event("input", { bubbles: true }));
      }
    });
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

    if (!Number.isInteger(quantity)) {
      setFieldError(quantityInput, "Fractional shares are not supported. Quantity must be a whole number.");
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

// =========================================
// CSV TRANSACTION IMPORT WORKFLOW
// =========================================

async function setupCsvTransactionImport() {
  const importCard = document.getElementById("csvImportCard");
  if (!importCard) return;

  const openBtn = document.getElementById("openCsvImportBtn");
  const closeBtn = document.getElementById("closeCsvImportBtn");
  const cancelStep1Btn = document.getElementById("cancelCsvStep1Btn");
  const downloadTemplateBtn = document.getElementById("downloadCsvTemplateBtn");
  const portfolioSelect = document.getElementById("csvPortfolioSelect");
  const dropzone = document.getElementById("csvDropzone");
  const fileInput = document.getElementById("csvFileInput");
  const selectedFileCard = document.getElementById("selectedFileCard");
  const selectedFileName = document.getElementById("selectedFileName");
  const selectedFileSize = document.getElementById("selectedFileSize");
  const removeFileBtn = document.getElementById("removeFileBtn");
  const rawTextInput = document.getElementById("csvRawTextInput");
  const parseAndValidateBtn = document.getElementById("parseAndValidateBtn");

  // Stepper elements
  const stepIndicators = [
    document.getElementById("step1Indicator"),
    document.getElementById("step2Indicator"),
    document.getElementById("step3Indicator"),
    document.getElementById("step4Indicator")
  ];
  const stepLines = [
    document.getElementById("stepLine1"),
    document.getElementById("stepLine2"),
    document.getElementById("stepLine3")
  ];
  const stepPanels = [
    document.getElementById("csvStep1Panel"),
    document.getElementById("csvStep2Panel"),
    document.getElementById("csvStep3Panel"),
    document.getElementById("csvStep4Panel")
  ];

  // Step 2 elements
  const totalRowsEl = document.getElementById("previewTotalRows");
  const validRowsEl = document.getElementById("previewValidRows");
  const invalidRowsEl = document.getElementById("previewInvalidRows");
  const totalAmountEl = document.getElementById("previewTotalAmount");
  const statusAlert = document.getElementById("previewStatusAlert");
  const allowPartialSwitch = document.getElementById("allowPartialImportSwitch");
  const partialModeWarning = document.getElementById("partialModeWarning");
  const importModeDesc = document.getElementById("importModeDesc");
  const previewTableBody = document.getElementById("csvPreviewTableBody");
  const backToUploadBtn = document.getElementById("backToUploadBtn");
  const confirmImportBtn = document.getElementById("confirmImportBtn");

  // Step 4 elements
  const resultPortfolioName = document.getElementById("resultPortfolioName");
  const resultImportedCount = document.getElementById("resultImportedCount");
  const resultSkippedCount = document.getElementById("resultSkippedCount");
  const resultTotalAmount = document.getElementById("resultTotalAmount");
  const resultSkippedContainer = document.getElementById("resultSkippedTableContainer");
  const resultSkippedBody = document.getElementById("resultSkippedTableBody");
  const importAnotherBtn = document.getElementById("importAnotherCsvBtn");
  const viewTransactionsBtn = document.getElementById("viewImportedTransactionsBtn");

  let currentSelectedFile = null;
  let currentValidatedCsvContent = "";
  let currentPreviewData = null;

  async function loadImportPortfolios() {
    try {
      const res = await apiRequest("/portfolios");
      const portfolios = res.data || [];
      if (portfolioSelect) {
        portfolioSelect.innerHTML = '<option value="">Select target portfolio</option>' +
          portfolios.map(p => `<option value="${p.portfolioId}">${escapeHtml(p.portfolioName)}</option>`).join("");
      }
    } catch (err) {
      console.error("Failed to load portfolios for CSV import:", err);
    }
  }

  function setStep(stepIndex) {
    stepPanels.forEach((panel, i) => {
      if (panel) panel.classList.toggle("d-none", i !== stepIndex);
    });

    stepIndicators.forEach((ind, i) => {
      if (!ind) return;
      ind.classList.toggle("active", i === stepIndex);
      ind.classList.toggle("completed", i < stepIndex);
    });

    stepLines.forEach((line, i) => {
      if (!line) return;
      line.classList.toggle("completed", i < stepIndex);
    });
  }

  function updateParseButtonState() {
    const hasPortfolio = portfolioSelect && portfolioSelect.value;
    const hasFile = currentSelectedFile !== null;
    const hasText = rawTextInput && rawTextInput.value.trim().length > 0;
    if (parseAndValidateBtn) {
      parseAndValidateBtn.disabled = !hasPortfolio || (!hasFile && !hasText);
    }
  }

  function resetImportWorkflow() {
    currentSelectedFile = null;
    currentValidatedCsvContent = "";
    currentPreviewData = null;
    if (fileInput) fileInput.value = "";
    if (rawTextInput) rawTextInput.value = "";
    if (selectedFileCard) selectedFileCard.classList.add("d-none");
    if (dropzone) dropzone.classList.remove("d-none");
    if (allowPartialSwitch) allowPartialSwitch.checked = false;
    if (partialModeWarning) partialModeWarning.classList.add("d-none");
    if (importModeDesc) {
      importModeDesc.textContent = "Atomic Import (Default): All valid rows insert or NO rows insert. Guarantees complete database integrity.";
    }
    setStep(0);
    updateParseButtonState();
  }

  if (openBtn) {
    openBtn.addEventListener("click", async () => {
      const txFormCard = document.getElementById("transactionFormCard");
      if (txFormCard) txFormCard.classList.add("form-hidden");

      resetImportWorkflow();
      await loadImportPortfolios();
      importCard.classList.remove("form-hidden");
      importCard.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  }

  function closeImportCard() {
    importCard.classList.add("form-hidden");
    resetImportWorkflow();
  }

  if (closeBtn) closeBtn.addEventListener("click", closeImportCard);
  if (cancelStep1Btn) cancelStep1Btn.addEventListener("click", closeImportCard);

  if (downloadTemplateBtn) {
    downloadTemplateBtn.addEventListener("click", () => {
      const templateContent = [
        "transaction_date,company_ticker,transaction_type,quantity,price_per_share,notes",
        "2026-10-01,GP,BUY,100,410.00,Initial position in Grameenphone",
        "2026-10-02,BATBC,BUY,50,518.50,Long-term dividend holding",
        "2026-10-05,GP,SELL,20,425.00,Partial profit taking"
      ].join("\r\n");

      const blob = new Blob([templateContent], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "sharesync_transactions_template.csv";
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    });
  }

  if (dropzone && fileInput) {
    dropzone.addEventListener("click", () => fileInput.click());

    dropzone.addEventListener("dragover", (e) => {
      e.preventDefault();
      dropzone.classList.add("dragover");
    });

    dropzone.addEventListener("dragleave", () => {
      dropzone.classList.remove("dragover");
    });

    dropzone.addEventListener("drop", (e) => {
      e.preventDefault();
      dropzone.classList.remove("dragover");
      if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
        handleFileSelection(e.dataTransfer.files[0]);
      }
    });

    fileInput.addEventListener("change", () => {
      if (fileInput.files && fileInput.files.length > 0) {
        handleFileSelection(fileInput.files[0]);
      }
    });
  }

  function handleFileSelection(file) {
    const ext = file.name.split(".").pop().toLowerCase();
    if (ext !== "csv" && ext !== "txt") {
      showToast("Invalid file type. Please upload a .csv or .txt file.", "danger");
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      showToast("File size exceeds 5 MB limit.", "danger");
      return;
    }

    currentSelectedFile = file;
    if (selectedFileName) selectedFileName.textContent = file.name;
    if (selectedFileSize) {
      const kb = (file.size / 1024).toFixed(1);
      selectedFileSize.textContent = `${kb} KB`;
    }
    if (selectedFileCard) selectedFileCard.classList.remove("d-none");
    if (dropzone) dropzone.classList.add("d-none");
    updateParseButtonState();
  }

  if (removeFileBtn) {
    removeFileBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      currentSelectedFile = null;
      if (fileInput) fileInput.value = "";
      if (selectedFileCard) selectedFileCard.classList.add("d-none");
      if (dropzone) dropzone.classList.remove("d-none");
      updateParseButtonState();
    });
  }

  if (portfolioSelect) {
    portfolioSelect.addEventListener("change", updateParseButtonState);
  }

  if (rawTextInput) {
    rawTextInput.addEventListener("input", updateParseButtonState);
  }

  if (parseAndValidateBtn) {
    parseAndValidateBtn.addEventListener("click", async () => {
      const portfolioId = parseInt(portfolioSelect.value, 10);
      if (!portfolioId) {
        showToast("Please select a target portfolio.", "warning");
        return;
      }

      parseAndValidateBtn.disabled = true;
      parseAndValidateBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span> Validating CSV...';

      try {
        let csvText = "";
        let fileName = null;

        if (currentSelectedFile) {
          csvText = await currentSelectedFile.text();
          fileName = currentSelectedFile.name;
        } else if (rawTextInput && rawTextInput.value.trim()) {
          csvText = rawTextInput.value.trim();
          fileName = "pasted_transactions.csv";
        }

        if (!csvText.trim()) {
          throw new Error("No CSV data provided.");
        }

        currentValidatedCsvContent = csvText;

        const payload = {
          portfolioId,
          csvContent: csvText,
          fileName
        };

        const res = await apiRequest("/transactions/import/validate", {
          method: "POST",
          body: JSON.stringify(payload)
        });

        currentPreviewData = res.data;
        renderPreview(currentPreviewData);
        setStep(1);
      } catch (err) {
        console.error("CSV validation failed:", err);
        showToast("Validation Error: " + err.message, "danger");
      } finally {
        parseAndValidateBtn.disabled = false;
        parseAndValidateBtn.innerHTML = '<i class="bi bi-shield-check me-1"></i> Parse &amp; Validate CSV';
      }
    });
  }

  function renderPreview(preview) {
    if (!preview) return;

    if (totalRowsEl) totalRowsEl.textContent = preview.totalRows;
    if (validRowsEl) validRowsEl.textContent = preview.validRowsCount;
    if (invalidRowsEl) invalidRowsEl.textContent = preview.invalidRowsCount;
    if (totalAmountEl) {
      totalAmountEl.textContent = "৳" + Number(preview.totalEstimatedAmount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    if (statusAlert) {
      if (preview.isValid) {
        statusAlert.className = "alert alert-success d-flex align-items-center gap-2";
        statusAlert.innerHTML = `
          <i class="bi bi-check-circle-fill fs-5"></i>
          <div>
            <strong>Validation Passed!</strong> All ${preview.totalRows} transaction rows are valid and verified against portfolio holdings and DSE catalog. Ready for atomic import.
          </div>
        `;
      } else {
        statusAlert.className = "alert alert-danger d-flex align-items-center gap-2";
        statusAlert.innerHTML = `
          <i class="bi bi-exclamation-triangle-fill fs-5"></i>
          <div>
            <strong>Validation Errors Found!</strong> ${preview.invalidRowsCount} of ${preview.totalRows} row(s) contain issues. In Atomic mode, all issues must be resolved before importing. Alternatively, enable Partial Import to proceed with valid rows only.
          </div>
        `;
      }
    }

    updateConfirmButtonState();

    if (previewTableBody) {
      previewTableBody.innerHTML = preview.rows.map(row => {
        const isBuy = row.transactionType === "BUY";
        const badgeClass = isBuy ? "buy-badge" : "sell-badge";
        const statusBadge = row.isValid
          ? '<span class="badge bg-success-subtle text-success border border-success-subtle px-2 py-1"><i class="bi bi-check-lg me-1"></i>Valid</span>'
          : '<span class="badge bg-danger-subtle text-danger border border-danger-subtle px-2 py-1"><i class="bi bi-x-circle me-1"></i>Invalid</span>';

        const rowBg = row.isValid ? "" : 'style="background-color: rgba(220, 53, 69, 0.04);"';

        const dateStr = row.transactionDate ? row.transactionDate.split("T")[0] : escapeHtml(row.dateString || "-");
        const qtyStr = Number(row.quantity || 0).toLocaleString();
        const priceStr = "৳" + Number(row.pricePerShare || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        const totalStr = "৳" + Number(row.totalAmount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

        let errorsHtml = "";
        if (!row.isValid && row.errors && row.errors.length > 0) {
          errorsHtml = row.errors.map(e => `
            <div class="csv-error-callout">
              <span class="csv-error-field">${escapeHtml(e.field)}:</span> ${escapeHtml(e.problem)}
              <span class="csv-correction-hint">💡 Suggestion: ${escapeHtml(e.suggestedCorrection)}</span>
            </div>
          `).join("");
        } else if (row.notes) {
          errorsHtml = `<small class="text-muted"><i class="bi bi-chat-left-text me-1"></i>${escapeHtml(row.notes)}</small>`;
        } else {
          errorsHtml = '<small class="text-muted">Verified OK</small>';
        }

        return `
          <tr ${rowBg}>
            <td class="fw-bold">${row.rowNumber}</td>
            <td>${dateStr}</td>
            <td><strong>${escapeHtml(row.companyTicker)}</strong></td>
            <td><span class="transaction-badge ${badgeClass}">${escapeHtml(row.transactionType || "BUY")}</span></td>
            <td>${qtyStr}</td>
            <td>${priceStr}</td>
            <td>${totalStr}</td>
            <td>${statusBadge}</td>
            <td>${errorsHtml}</td>
          </tr>
        `;
      }).join("");
    }
  }

  function updateConfirmButtonState() {
    if (!currentPreviewData || !confirmImportBtn) return;

    const isPartial = allowPartialSwitch && allowPartialSwitch.checked;
    if (isPartial) {
      confirmImportBtn.disabled = currentPreviewData.validRowsCount === 0;
      confirmImportBtn.innerHTML = `<i class="bi bi-check2-circle me-1"></i> Import Valid Rows (${currentPreviewData.validRowsCount})`;
    } else {
      confirmImportBtn.disabled = !currentPreviewData.isValid;
      confirmImportBtn.innerHTML = `<i class="bi bi-check2-circle me-1"></i> Confirm &amp; Import (${currentPreviewData.totalRows})`;
    }
  }

  if (allowPartialSwitch) {
    allowPartialSwitch.addEventListener("change", () => {
      const isPartial = allowPartialSwitch.checked;
      if (partialModeWarning) partialModeWarning.classList.toggle("d-none", !isPartial);
      if (importModeDesc) {
        importModeDesc.textContent = isPartial
          ? "Partial Import Mode: Valid rows will be imported atomically; invalid rows will be skipped and reported in the summary."
          : "Atomic Import (Default): All valid rows insert or NO rows insert. Guarantees complete database integrity.";
      }
      updateConfirmButtonState();
    });
  }

  if (backToUploadBtn) {
    backToUploadBtn.addEventListener("click", () => {
      setStep(0);
    });
  }

  if (confirmImportBtn) {
    confirmImportBtn.addEventListener("click", async () => {
      if (!currentPreviewData || !currentValidatedCsvContent) return;

      const portfolioId = parseInt(portfolioSelect.value, 10);
      const allowPartial = allowPartialSwitch && allowPartialSwitch.checked;

      setStep(2);

      try {
        const payload = {
          portfolioId,
          csvContent: currentValidatedCsvContent,
          allowPartialImport: allowPartial
        };

        const res = await apiRequest("/transactions/import/execute", {
          method: "POST",
          body: JSON.stringify(payload)
        });

        const result = res.data;

        if (resultPortfolioName) resultPortfolioName.textContent = result.portfolioName || "Portfolio";
        if (resultImportedCount) resultImportedCount.textContent = result.successCount;
        if (resultSkippedCount) resultSkippedCount.textContent = result.skippedCount;
        if (resultTotalAmount) {
          resultTotalAmount.textContent = "৳" + Number(result.totalAmount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }

        if (result.skippedRows && result.skippedRows.length > 0) {
          if (resultSkippedContainer) resultSkippedContainer.classList.remove("d-none");
          if (resultSkippedBody) {
            resultSkippedBody.innerHTML = result.skippedRows.map(s => `
              <tr>
                <td class="fw-bold">${s.rowNumber}</td>
                <td class="font-monospace text-danger">${escapeHtml(s.field)}</td>
                <td>${escapeHtml(s.problem)}</td>
                <td class="text-muted">${escapeHtml(s.suggestedCorrection)}</td>
              </tr>
            `).join("");
          }
        } else {
          if (resultSkippedContainer) resultSkippedContainer.classList.add("d-none");
        }

        setStep(3);
        showToast(result.message || "Transactions imported successfully!", "success");

        if (typeof loadTransactionHistory === "function") {
          loadTransactionHistory();
        }
      } catch (err) {
        console.error("Execution of CSV import failed:", err);
        setStep(1);
        showToast("Import failed: " + err.message, "danger");
      }
    });
  }

  if (importAnotherBtn) {
    importAnotherBtn.addEventListener("click", () => {
      resetImportWorkflow();
    });
  }

  if (viewTransactionsBtn) {
    viewTransactionsBtn.addEventListener("click", () => {
      closeImportCard();
      const listCard = document.querySelector(".transaction-list-card");
      if (listCard) {
        listCard.scrollIntoView({ behavior: "smooth", block: "start" });
      }
      if (typeof loadTransactionHistory === "function") {
        loadTransactionHistory();
      }
    });
  }
}

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

let currentTxPage = 1;

async function loadTransactionHistory(page = 1) {
  const table = document.querySelector(".transaction-table");
  const tbody = document.getElementById("transactionTableBody");
  if (!table || !tbody) return;

  currentTxPage = typeof page === "number" ? page : 1;

  const typeFilter = document.getElementById("transactionTypeFilter");
  const companyFilter = document.getElementById("transactionCompanyFilter");
  const portfolioFilter = document.getElementById("transactionPortfolioFilter");
  const dateFromInput = document.getElementById("txFilterDateFrom");
  const dateToInput = document.getElementById("txFilterDateTo");
  const minPriceInput = document.getElementById("txFilterMinPrice");
  const maxPriceInput = document.getElementById("txFilterMaxPrice");
  const minQtyInput = document.getElementById("txFilterMinQty");
  const maxQtyInput = document.getElementById("txFilterMaxQty");
  const sortBySelect = document.getElementById("txSortBy");
  const sortDirSelect = document.getElementById("txSortDirection");
  const pageSizeSelect = document.getElementById("txPageSize");

  const countLabel = document.getElementById("transactionCountSubtitle");
  const paginationSummary = document.getElementById("txPaginationSummary") || document.querySelector(".pagination-row span");
  const paginationButtons = document.getElementById("txPaginationButtons") || document.querySelector(".pagination-buttons");

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
  if (dateFromInput && dateFromInput.value) {
    params.append("startDate", dateFromInput.value);
  }
  if (dateToInput && dateToInput.value) {
    params.append("endDate", dateToInput.value);
  }
  if (minPriceInput && minPriceInput.value !== "") {
    params.append("minPrice", minPriceInput.value);
  }
  if (maxPriceInput && maxPriceInput.value !== "") {
    params.append("maxPrice", maxPriceInput.value);
  }
  if (minQtyInput && minQtyInput.value !== "") {
    params.append("minQuantity", minQtyInput.value);
  }
  if (maxQtyInput && maxQtyInput.value !== "") {
    params.append("maxQuantity", maxQtyInput.value);
  }
  if (sortBySelect && sortBySelect.value) {
    params.append("sortBy", sortBySelect.value);
  }
  if (sortDirSelect && sortDirSelect.value) {
    params.append("sortDirection", sortDirSelect.value);
  }

  const pageSize = pageSizeSelect ? parseInt(pageSizeSelect.value, 10) || 25 : 25;
  params.append("page", currentTxPage);
  params.append("pageSize", pageSize);

  tbody.innerHTML = `
    <tr>
      <td colspan="8" class="text-center py-4 text-muted">
        <div class="spinner-border spinner-border-sm me-2" role="status"></div>
        Loading transaction records...
      </td>
    </tr>
  `;

  try {
    const res = await apiRequest(`/transactions?${params.toString()}`);
    let transactions = [];
    let currentPage = currentTxPage;
    let totalItems = 0;
    let totalPages = 1;

    if (res.data && Array.isArray(res.data.items)) {
      transactions = res.data.items;
      currentPage = res.data.page || currentTxPage;
      totalItems = res.data.totalItems ?? transactions.length;
      totalPages = res.data.totalPages || 1;
    } else if (Array.isArray(res.data)) {
      transactions = res.data;
      totalItems = transactions.length;
      totalPages = Math.ceil(totalItems / pageSize) || 1;
    }

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
      if (paginationSummary) paginationSummary.textContent = "Showing 0 of 0 transactions";
      if (paginationButtons) paginationButtons.innerHTML = "";
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
                <strong><a href="company.html?id=${t.companyId}" class="text-decoration-none text-body fw-bold">${escapeHtml(t.tickerSymbol)}</a></strong>
                <small><a href="company.html?id=${t.companyId}" class="text-decoration-none text-muted">${escapeHtml(t.companyName)}</a></small>
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
      countLabel.textContent = `${totalItems} transaction${totalItems === 1 ? "" : "s"} recorded`;
    }

    const fromItem = (currentPage - 1) * pageSize + 1;
    const toItem = Math.min(currentPage * pageSize, totalItems);
    if (paginationSummary) {
      paginationSummary.textContent = `Showing ${fromItem}–${toItem} of ${totalItems} transactions`;
    }

    if (paginationButtons) {
      renderPaginationButtons(paginationButtons, currentPage, totalPages);
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
    if (paginationSummary) paginationSummary.textContent = "Error";
    if (paginationButtons) paginationButtons.innerHTML = "";
  }
}

function renderPaginationButtons(container, currentPage, totalPages) {
  if (totalPages <= 1) {
    container.innerHTML = `
      <button type="button" class="current-page">1</button>
    `;
    return;
  }

  let html = `
    <button type="button" ${currentPage <= 1 ? "disabled" : ""} onclick="loadTransactionHistory(${currentPage - 1})" aria-label="Previous page">
      <i class="bi bi-chevron-left"></i>
    </button>
  `;

  const maxButtons = 5;
  let startPage = Math.max(1, currentPage - Math.floor(maxButtons / 2));
  let endPage = Math.min(totalPages, startPage + maxButtons - 1);
  if (endPage - startPage + 1 < maxButtons) {
    startPage = Math.max(1, endPage - maxButtons + 1);
  }

  if (startPage > 1) {
    html += `<button type="button" onclick="loadTransactionHistory(1)">1</button>`;
    if (startPage > 2) {
      html += `<button type="button" disabled style="cursor: default;">...</button>`;
    }
  }

  for (let p = startPage; p <= endPage; p++) {
    if (p === currentPage) {
      html += `<button type="button" class="current-page" aria-current="page">${p}</button>`;
    } else {
      html += `<button type="button" onclick="loadTransactionHistory(${p})">${p}</button>`;
    }
  }

  if (endPage < totalPages) {
    if (endPage < totalPages - 1) {
      html += `<button type="button" disabled style="cursor: default;">...</button>`;
    }
    html += `<button type="button" onclick="loadTransactionHistory(${totalPages})">${totalPages}</button>`;
  }

  html += `
    <button type="button" ${currentPage >= totalPages ? "disabled" : ""} onclick="loadTransactionHistory(${currentPage + 1})" aria-label="Next page">
      <i class="bi bi-chevron-right"></i>
    </button>
  `;

  container.innerHTML = html;
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

  const applyBtn = document.getElementById("txApplyFiltersBtn");
  const resetBtn = document.getElementById("txResetFiltersBtn");
  const pageSizeSelect = document.getElementById("txPageSize");
  const sortBySelect = document.getElementById("txSortBy");
  const sortDirSelect = document.getElementById("txSortDirection");
  const exportButton = document.getElementById("exportTransactions");
  const filterForm = document.getElementById("txFilterForm");

  // Immediate filter dropdowns
  document.getElementById("transactionTypeFilter")?.addEventListener("change", () => loadTransactionHistory(1));
  document.getElementById("transactionCompanyFilter")?.addEventListener("change", () => loadTransactionHistory(1));
  document.getElementById("transactionPortfolioFilter")?.addEventListener("change", () => loadTransactionHistory(1));
  pageSizeSelect?.addEventListener("change", () => loadTransactionHistory(1));
  sortBySelect?.addEventListener("change", () => loadTransactionHistory(1));
  sortDirSelect?.addEventListener("change", () => loadTransactionHistory(1));

  applyBtn?.addEventListener("click", () => loadTransactionHistory(1));
  filterForm?.addEventListener("submit", (e) => {
    e.preventDefault();
    loadTransactionHistory(1);
  });

  resetBtn?.addEventListener("click", () => {
    if (filterForm) filterForm.reset();
    const typeFilter = document.getElementById("transactionTypeFilter");
    const companyFilter = document.getElementById("transactionCompanyFilter");
    const portfolioFilter = document.getElementById("transactionPortfolioFilter");
    if (typeFilter) typeFilter.value = "all";
    if (companyFilter) companyFilter.value = "all";
    if (portfolioFilter) portfolioFilter.value = "all";
    if (sortBySelect) sortBySelect.value = "date";
    if (sortDirSelect) sortDirSelect.value = "desc";
    if (pageSizeSelect) pageSizeSelect.value = "25";
    loadTransactionHistory(1);
  });

  // Table header click-to-sort
  const handleSortHeader = (field) => {
    if (sortBySelect && sortDirSelect) {
      if (sortBySelect.value === field) {
        sortDirSelect.value = sortDirSelect.value === "asc" ? "desc" : "asc";
      } else {
        sortBySelect.value = field;
        sortDirSelect.value = "desc";
      }
      loadTransactionHistory(1);
    }
  };

  document.getElementById("thSortDate")?.addEventListener("click", () => handleSortHeader("date"));
  document.getElementById("thSortQty")?.addEventListener("click", () => handleSortHeader("quantity"));
  document.getElementById("thSortPrice")?.addEventListener("click", () => handleSortHeader("price"));
  document.getElementById("thSortTotal")?.addEventListener("click", () => handleSortHeader("value"));

  exportButton?.addEventListener("click", () => exportTableCsv(table, "sharesync-transactions.csv"));

  // Initial load
  loadTransactionHistory(1);
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

  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const strokeColor = isDark ? "#10B981" : "#16A34A";
  const gradTop = isDark ? "rgba(16, 185, 129, 0.22)" : "rgba(22, 163, 74, 0.16)";
  const gradBottom = isDark ? "rgba(16, 185, 129, 0.0)" : "rgba(22, 163, 74, 0.0)";

  const gradient = ctx.createLinearGradient(0, 0, 0, 220);
  gradient.addColorStop(0, gradTop);
  gradient.addColorStop(1, gradBottom);

  portfolioChart = new Chart(ctx, {
    type: "line",

    data: {
      labels: [],

      datasets: [
        {
          label: "Portfolio Value",

          data: [],

          borderColor: strokeColor,
          backgroundColor: gradient,
          borderWidth: 2.5,
          pointRadius: 3,
          pointHoverRadius: 5,
          pointBackgroundColor: strokeColor,
          pointBorderColor: isDark ? "#111726" : "#ffffff",
          pointBorderWidth: 2,
          tension: 0.3,
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
          backgroundColor: isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)",
          titleColor: "#F8FAFC",
          bodyColor: "#F8FAFC",
          borderColor: isDark ? "rgba(255, 255, 255, 0.12)" : "transparent",
          borderWidth: isDark ? 1 : 0,
          padding: 10,
          cornerRadius: 8,
          displayColors: false,
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
            color: isDark ? "#94A3B8" : "#64748B",
            font: {
              size: 11,
            },
          },
        },

        y: {
          beginAtZero: false,
          border: {
            display: false,
          },
          grid: {
            color: isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(226, 232, 240, 0.7)",
          },
          ticks: {
            color: isDark ? "#94A3B8" : "#64748B",
            font: {
              size: 11,
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

  async function triggerReportExport(format, buttonEl) {
    const reportType = reportTypeSelect ? reportTypeSelect.value : "holdings";
    const portfolioId = portfolioSelect ? portfolioSelect.value : "";
    const period = periodSelect ? periodSelect.value : "6";

    const reportNames = {
      "holdings": "Portfolio Holdings",
      "performance": "Portfolio Performance / P&L",
      "transactions": "Transaction History",
      "company-sector": "Company & Sector Allocation",
      "dividends": "Dividend Income",
      "watchlist": "Watchlist & Target Prices"
    };
    const reportDisplayName = reportNames[reportType] || "Report";

    const originalHtml = buttonEl ? buttonEl.innerHTML : "";
    if (buttonEl) {
      buttonEl.disabled = true;
      buttonEl.innerHTML = `<span class="spinner-border spinner-border-sm me-1" role="status"></span>Exporting...`;
    }

    try {
      const params = new URLSearchParams();
      params.append("reportType", reportType);
      params.append("format", format);
      if (portfolioId) params.append("portfolioId", portfolioId);
      if (period) params.append("period", period);

      const { blob, filename } = await apiDownloadBlob(`/reports/export?${params.toString()}`);

      const fallbackExt = format.toLowerCase() === "pdf" ? ".pdf" : (format.toLowerCase() === "excel" ? ".xlsx" : ".csv");
      const downloadName = filename || `ShareSync_${reportType}_${new Date().toISOString().slice(0, 10)}${fallbackExt}`;

      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.style.display = "none";
      a.href = url;
      a.download = downloadName;
      document.body.appendChild(a);
      a.click();
      setTimeout(() => {
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      }, 200);

      showToast(`Exported ${reportDisplayName} (${format.toUpperCase()}) successfully.`, "success");
    } catch (err) {
      console.error("Export error:", err);
      showToast(err.message || `Failed to export ${format.toUpperCase()} report.`, "danger");
    } finally {
      if (buttonEl) {
        buttonEl.disabled = false;
        buttonEl.innerHTML = originalHtml;
      }
    }
  }

  // Hook up event listeners for both dropdown and table action buttons
  ["exportPdfBtn", "quickExportPdfBtn"].forEach(id => {
    const btn = document.getElementById(id);
    if (btn) btn.addEventListener("click", () => triggerReportExport("pdf", btn));
  });

  ["exportExcelBtn", "quickExportExcelBtn"].forEach(id => {
    const btn = document.getElementById(id);
    if (btn) btn.addEventListener("click", () => triggerReportExport("excel", btn));
  });

  ["exportCsvBtn", "quickExportCsvBtn"].forEach(id => {
    const btn = document.getElementById(id);
    if (btn) btn.addEventListener("click", () => triggerReportExport("csv", btn));
  });

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

      const isDark = document.documentElement.getAttribute("data-theme") === "dark";
      const strokeColor = isDark ? "#38BDF8" : "#1D4ED8";
      const ctx = perfCanvas.getContext("2d");
      const gradient = ctx.createLinearGradient(0, 0, 0, 260);
      gradient.addColorStop(0, isDark ? "rgba(56, 189, 248, 0.2)" : "rgba(29, 78, 216, 0.12)");
      gradient.addColorStop(1, isDark ? "rgba(56, 189, 248, 0.0)" : "rgba(29, 78, 216, 0.0)");

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
            borderColor: strokeColor,
            backgroundColor: gradient,
            borderWidth: 2.5,
            pointRadius: data.length > 20 ? 1 : 3.5,
            pointHoverRadius: 6,
            pointBackgroundColor: strokeColor,
            pointBorderColor: isDark ? "#111726" : "#ffffff",
            pointBorderWidth: 2,
            tension: 0.28,
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
              backgroundColor: isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)",
              titleColor: "#F8FAFC",
              bodyColor: "#F8FAFC",
              borderColor: isDark ? "rgba(255, 255, 255, 0.12)" : "transparent",
              borderWidth: isDark ? 1 : 0,
              padding: 10,
              cornerRadius: 8,
              callbacks: {
                label: (ctx) => "৳" + Number(ctx.raw).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
              }
            }
          },
          scales: {
            y: {
              beginAtZero: false,
              grid: { color: isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(148, 163, 184, 0.15)" },
              ticks: {
                color: isDark ? "#94A3B8" : "#64748B",
                callback: (val) => "৳" + (val >= 1000 ? (val / 1000).toFixed(0) + "k" : val)
              }
            },
            x: {
              grid: { display: false },
              ticks: { color: isDark ? "#94A3B8" : "#64748B" }
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

      const isDark = document.documentElement.getAttribute("data-theme") === "dark";
      const lightPalette = ["#1D4ED8", "#10B981", "#F59E0B", "#8B5CF6", "#06B6D4", "#EC4899", "#64748B"];
      const darkPalette = ["#3B82F6", "#10B981", "#F59E0B", "#A855F7", "#22D3EE", "#F43F5E", "#94A3B8"];
      const colors = isDark ? darkPalette : lightPalette;
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
            backgroundColor: data.length ? bgColors : [isDark ? "#1E293B" : "#E2E8F0"],
            borderColor: isDark ? "#111726" : "#ffffff",
            borderWidth: 2,
            hoverOffset: 4
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: "68%",
          plugins: {
            legend: { display: false },
            tooltip: {
              backgroundColor: isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)",
              titleColor: "#F8FAFC",
              bodyColor: "#F8FAFC",
              borderColor: isDark ? "rgba(255, 255, 255, 0.12)" : "transparent",
              borderWidth: isDark ? 1 : 0,
              padding: 10,
              cornerRadius: 8,
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

      const isDark = document.documentElement.getAttribute("data-theme") === "dark";
      reportActivityChartInstance = new Chart(txCanvas, {
        type: "bar",
        data: {
          labels: months.length ? months : ["No activity"],
          datasets: [
            {
              label: "BUY",
              data: buyCounts.length ? buyCounts : [0],
              backgroundColor: isDark ? "#10B981" : "#059669",
              borderRadius: 4,
              borderSkipped: false
            },
            {
              label: "SELL",
              data: sellCounts.length ? sellCounts : [0],
              backgroundColor: isDark ? "#F59E0B" : "#D97706",
              borderRadius: 4,
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
              labels: {
                usePointStyle: true,
                pointStyle: "circle",
                boxWidth: 8,
                color: isDark ? "#94A3B8" : "#475569",
                font: { size: 12, weight: "500" }
              }
            },
            tooltip: {
              backgroundColor: isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)",
              borderColor: isDark ? "rgba(255, 255, 255, 0.12)" : "transparent",
              borderWidth: isDark ? 1 : 0,
              padding: 10,
              cornerRadius: 8
            }
          },
          scales: {
            y: {
              beginAtZero: true,
              grid: { color: isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(148, 163, 184, 0.15)" },
              ticks: { stepSize: 1, color: isDark ? "#94A3B8" : "#64748B" }
            },
            x: {
              grid: { display: false },
              ticks: { color: isDark ? "#94A3B8" : "#64748B" }
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
                    <strong><a href="company.html?id=${h.companyId}" class="text-decoration-none text-body fw-bold">${escapeHtml(h.tickerSymbol)}</a></strong>
                    <small><a href="company.html?id=${h.companyId}" class="text-decoration-none text-muted">${escapeHtml(h.companyName)}</a></small>
                  </div>
                </div>
              </td>
              <td>${Number(h.shares || 0).toLocaleString()}</td>
              <td>৳${Number(h.averageBuyPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
              <td>
                ৳${Number(h.currentPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                <button type="button" class="price-history-btn ms-1" onclick="openPriceHistoryModal(${h.companyId}, '${escapeHtml(h.tickerSymbol)}', '${escapeHtml(h.companyName)}', ${h.currentPrice})" title="View Price Trend &amp; History">
                  <i class="bi bi-graph-up"></i>
                </button>
              </td>
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
      const txs = (Array.isArray(txRes?.data) ? txRes.data : (txRes?.data?.items || [])).slice(0, 5);
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

// =========================================
// HISTORICAL MARKET PRICE TRACKING
// =========================================

let priceHistoryChartInstance = null;
let currentHistCompanyId = null;
let currentHistTicker = "";
let currentHistCompanyName = "";
let currentHistPeriod = "1M";

window.openPriceHistoryModal = function(companyId, tickerSymbol, companyName, currentPrice) {
  if (!companyId) return;

  currentHistCompanyId = companyId;
  currentHistTicker = tickerSymbol || "STOCK";
  currentHistCompanyName = companyName || "";

  // Set modal header company info
  const logoEl = document.getElementById("histModalLogo");
  if (logoEl) logoEl.textContent = currentHistTicker.slice(0, 2).toUpperCase();

  const tickerEl = document.getElementById("histModalTicker");
  if (tickerEl) tickerEl.textContent = currentHistTicker;

  const nameEl = document.getElementById("histModalName");
  if (nameEl) nameEl.textContent = currentHistCompanyName;

  const priceEl = document.getElementById("histModalCurrentPrice");
  if (priceEl) priceEl.textContent = currentPrice ? formatBDT(currentPrice) : "৳0.00";

  // Reset stats to loading state
  const changeBadge = document.getElementById("histModalChangeBadge");
  if (changeBadge) {
    changeBadge.className = "badge bg-secondary-subtle text-secondary";
    changeBadge.textContent = "Loading...";
  }

  // Ensure timeframe buttons have click handlers attached
  setupPriceHistoryTimeframeButtons();

  // Open modal
  const modalEl = document.getElementById("priceHistoryModal");
  if (modalEl && typeof bootstrap !== "undefined" && bootstrap.Modal) {
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();
  }

  // Load default period (1M)
  loadPriceHistoryData(companyId, currentHistPeriod || "1M");
};

function setupPriceHistoryTimeframeButtons() {
  const container = document.getElementById("histTimeframeButtons");
  if (!container || container.dataset.listenersAttached === "true") return;

  container.dataset.listenersAttached = "true";
  const buttons = container.querySelectorAll(".timeframe-btn");
  buttons.forEach(btn => {
    btn.addEventListener("click", () => {
      const period = btn.dataset.period;
      if (!period || !currentHistCompanyId) return;
      currentHistPeriod = period;
      loadPriceHistoryData(currentHistCompanyId, period);
    });
  });

  const retryBtn = document.getElementById("histRetryBtn");
  if (retryBtn) {
    retryBtn.addEventListener("click", () => {
      if (currentHistCompanyId) {
        loadPriceHistoryData(currentHistCompanyId, currentHistPeriod);
      }
    });
  }
}

async function loadPriceHistoryData(companyId, period) {
  if (!companyId) return;

  const loadingEl = document.getElementById("histLoadingState");
  const errorEl = document.getElementById("histErrorState");
  const emptyEl = document.getElementById("histEmptyState");
  const singleAlertEl = document.getElementById("histSinglePointAlert");
  const chartContainerEl = document.getElementById("histChartContainer");
  const errorMessageEl = document.getElementById("histErrorMessage");

  // Update active button state
  const buttons = document.querySelectorAll("#histTimeframeButtons .timeframe-btn");
  buttons.forEach(b => {
    if (b.dataset.period === period) {
      b.classList.add("active");
    } else {
      b.classList.remove("active");
    }
  });

  // Reset UI states
  if (loadingEl) loadingEl.classList.remove("d-none");
  if (errorEl) errorEl.classList.add("d-none");
  if (emptyEl) emptyEl.classList.add("d-none");
  if (singleAlertEl) singleAlertEl.classList.add("d-none");
  if (chartContainerEl) chartContainerEl.classList.add("d-none");

  try {
    const res = await apiRequest(`/companies/${companyId}/price-history?period=${encodeURIComponent(period)}&limit=300`);
    const data = res.data;

    if (loadingEl) loadingEl.classList.add("d-none");

    // Populate header & stats
    const currentPriceEl = document.getElementById("histModalCurrentPrice");
    if (currentPriceEl && data.currentPrice) {
      currentPriceEl.textContent = formatBDT(data.currentPrice);
    }

    const highEl = document.getElementById("histStatHigh");
    if (highEl) highEl.textContent = data.periodHigh ? formatBDT(data.periodHigh) : "৳0.00";

    const lowEl = document.getElementById("histStatLow");
    if (lowEl) lowEl.textContent = data.periodLow ? formatBDT(data.periodLow) : "৳0.00";

    const pointsEl = document.getElementById("histStatPoints");
    if (pointsEl) pointsEl.textContent = data.totalSnapshots || 0;

    const changeBadge = document.getElementById("histModalChangeBadge");
    if (changeBadge) {
      const isUp = (data.periodChange || 0) >= 0;
      const sign = isUp ? "+" : "-";
      const absChange = Math.abs(data.periodChange || 0).toFixed(2);
      const absPct = Math.abs(data.periodChangePercentage || 0).toFixed(2);
      if (isUp) {
        changeBadge.className = "badge bg-success-subtle text-success border border-success-subtle fw-semibold";
      } else {
        changeBadge.className = "badge bg-danger-subtle text-danger border border-danger-subtle fw-semibold";
      }
      changeBadge.textContent = `${sign}৳${absChange} (${sign}${absPct}%)`;
    }

    const history = data.history || [];

    if (history.length > 0) {
      const latest = history[history.length - 1];
      const syncTimeEl = document.getElementById("histModalSyncTime");
      if (syncTimeEl) {
        const d = new Date(latest.recordedAt);
        syncTimeEl.innerHTML = `<i class="bi bi-clock me-1"></i>Last snapshot: ${d.toLocaleDateString("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}`;
      }
    }

    // Handle 0 records
    if (history.length === 0) {
      if (emptyEl) emptyEl.classList.remove("d-none");
      if (priceHistoryChartInstance) {
        priceHistoryChartInstance.destroy();
        priceHistoryChartInstance = null;
      }
      return;
    }

    // Handle 1 record
    if (history.length === 1) {
      if (singleAlertEl) singleAlertEl.classList.remove("d-none");
    }

    // Render Chart
    if (chartContainerEl) chartContainerEl.classList.remove("d-none");
    renderPriceHistoryChart(history, period, (data.periodChange || 0) >= 0);

  } catch (err) {
    console.error("Failed to load price history:", err);
    if (loadingEl) loadingEl.classList.add("d-none");
    if (chartContainerEl) chartContainerEl.classList.add("d-none");
    if (errorEl) errorEl.classList.remove("d-none");
    if (errorMessageEl) {
      errorMessageEl.textContent = sanitizeErrorMessage(err.message, err.status);
    }
  }
}

function renderPriceHistoryChart(history, period, isUp) {
  const canvas = document.getElementById("priceHistoryChart");
  if (!canvas) return;

  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const strokeColor = isUp ? (isDark ? "#10B981" : "#16A34A") : (isDark ? "#F43F5E" : "#DC2626");
  const gradTop = isUp
    ? (isDark ? "rgba(16, 185, 129, 0.25)" : "rgba(22, 163, 74, 0.18)")
    : (isDark ? "rgba(244, 63, 94, 0.25)" : "rgba(220, 38, 38, 0.18)");
  const gradBottom = isUp ? "rgba(16, 185, 129, 0.0)" : "rgba(244, 63, 94, 0.0)";

  const ctx = canvas.getContext("2d");
  const gradient = ctx.createLinearGradient(0, 0, 0, 300);
  gradient.addColorStop(0, gradTop);
  gradient.addColorStop(1, gradBottom);

  const labels = history.map(h => {
    const d = new Date(h.recordedAt);
    if (period === "1D") {
      return d.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" });
    }
    if (period === "1W" || period === "1M") {
      return d.toLocaleDateString("en-US", { month: "short", day: "numeric" });
    }
    return d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "2-digit" });
  });

  const prices = history.map(h => h.price);

  if (priceHistoryChartInstance) {
    priceHistoryChartInstance.destroy();
    priceHistoryChartInstance = null;
  }

  priceHistoryChartInstance = new Chart(ctx, {
    type: "line",
    data: {
      labels: labels,
      datasets: [{
        label: "Market Price",
        data: prices,
        borderColor: strokeColor,
        borderWidth: 2.5,
        backgroundColor: gradient,
        fill: true,
        tension: 0.3,
        pointRadius: history.length === 1 ? 5 : (history.length > 50 ? 0 : 3),
        pointHoverRadius: 6,
        pointBackgroundColor: strokeColor,
        pointBorderColor: isDark ? "#111726" : "#ffffff",
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
          backgroundColor: isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)",
          titleColor: isDark ? "#F8FAFC" : "#FFFFFF",
          bodyColor: isDark ? "#E2E8F0" : "#FFFFFF",
          borderColor: isDark ? "rgba(255, 255, 255, 0.12)" : "transparent",
          borderWidth: isDark ? 1 : 0,
          titleFont: { size: 12, weight: "600" },
          bodyFont: { size: 12 },
          padding: 10,
          cornerRadius: 8,
          displayColors: false,
          callbacks: {
            title: items => {
              const idx = items[0].dataIndex;
              const r = history[idx];
              if (!r) return items[0].label;
              const d = new Date(r.recordedAt);
              return d.toLocaleDateString("en-US", {
                month: "short",
                day: "numeric",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
              });
            },
            label: context => {
              const idx = context.dataIndex;
              const r = history[idx];
              const lines = [`Price: ৳${Number(context.parsed.y).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`];
              if (r) {
                if (r.openPrice) lines.push(`Open: ৳${Number(r.openPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`);
                if (r.highPrice) lines.push(`High: ৳${Number(r.highPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`);
                if (r.lowPrice) lines.push(`Low: ৳${Number(r.lowPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`);
                if (r.volume) lines.push(`Volume: ${Number(r.volume).toLocaleString()}`);
              }
              return lines;
            }
          }
        }
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: {
            font: { size: 11 },
            color: isDark ? "#94A3B8" : "#64748b",
            maxTicksLimit: 8
          }
        },
        y: {
          grid: {
            color: isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(226, 232, 240, 0.7)",
            drawBorder: false
          },
          ticks: {
            font: { size: 11 },
            color: isDark ? "#94A3B8" : "#64748b",
            callback: value => "৳" + Number(value).toFixed(2)
          }
        }
      }
    }
  });
}

// =========================================
// COMPANY DETAIL PAGE (FEATURE 2)
// =========================================

let companyDetailChartInstance = null;
let currentCompanyDetailId = null;
let currentCompanyDetailData = null;
let currentCompanyDetailPeriod = "1M";

async function setupCompanyDetailPage() {
  const container = document.getElementById("companyContentContainer");
  if (!container) return; // Not on company.html

  const urlParams = new URLSearchParams(window.location.search);
  const rawId = urlParams.get("id");
  const companyId = parseInt(rawId, 10);

  const loadingEl = document.getElementById("companyLoadingState");
  const errorEl = document.getElementById("companyErrorState");
  const errorMsgEl = document.getElementById("companyErrorMessage");
  const errorDetailEl = document.getElementById("companyErrorDetail");
  const retryBtn = document.getElementById("companyRetryBtn");

  if (!rawId || isNaN(companyId) || companyId <= 0) {
    if (loadingEl) loadingEl.classList.add("d-none");
    if (errorEl) errorEl.classList.remove("d-none");
    if (errorMsgEl) errorMsgEl.textContent = "Invalid Company Identifier";
    if (errorDetailEl) errorDetailEl.textContent = "Please provide a valid company ID in the URL, e.g. company.html?id=1";
    return;
  }

  currentCompanyDetailId = companyId;

  if (retryBtn) {
    retryBtn.onclick = () => loadCompanyDetailPageData(companyId);
  }

  setupCompanyDetailTimeframeButtons();
  setupCompanyDetailWatchlistActions();

  await loadCompanyDetailPageData(companyId);
}

async function loadCompanyDetailPageData(companyId) {
  const loadingEl = document.getElementById("companyLoadingState");
  const errorEl = document.getElementById("companyErrorState");
  const errorMsgEl = document.getElementById("companyErrorMessage");
  const errorDetailEl = document.getElementById("companyErrorDetail");
  const contentEl = document.getElementById("companyContentContainer");

  if (loadingEl) loadingEl.classList.remove("d-none");
  if (errorEl) errorEl.classList.add("d-none");
  if (contentEl) contentEl.classList.add("d-none");

  try {
    const res = await apiRequest(`/companies/${companyId}/detail`);
    const data = res.data;
    if (!data) throw new Error("Company profile data not found.");

    currentCompanyDetailData = data;

    if (loadingEl) loadingEl.classList.add("d-none");
    if (contentEl) contentEl.classList.remove("d-none");

    renderCompanyDetailHeader(data);
    renderCompanyKeyMetrics(data);
    renderCompanyUserPosition(data);
    renderCompanyWatchlistStatus(data);
    renderCompanyDividendHistory(data);

    await loadCompanyDetailChart(companyId, currentCompanyDetailPeriod || "1M");
  } catch (err) {
    console.error("Failed to load company detail:", err);
    if (loadingEl) loadingEl.classList.add("d-none");
    if (contentEl) contentEl.classList.add("d-none");
    if (errorEl) errorEl.classList.remove("d-none");
    if (errorMsgEl) {
      errorMsgEl.textContent = err.status === 404 ? "Company Not Found" : "Unable to Load Company";
    }
    if (errorDetailEl) {
      errorDetailEl.textContent = sanitizeErrorMessage(err.message, err.status);
    }
  }
}

function renderCompanyDetailHeader(d) {
  // Breadcrumb & Page title
  const breadcrumbEl = document.getElementById("breadcrumbCompanyName");
  if (breadcrumbEl) breadcrumbEl.textContent = d.companyName || d.tickerSymbol;
  document.title = `ShareSync | ${d.tickerSymbol} - ${d.companyName}`;

  // Logo & Name
  const logoEl = document.getElementById("companyLogo");
  if (logoEl) logoEl.textContent = (d.tickerSymbol || "SS").slice(0, 2).toUpperCase();

  const nameEl = document.getElementById("companyName");
  if (nameEl) nameEl.textContent = d.companyName;

  const tickerEl = document.getElementById("companyTicker");
  if (tickerEl) tickerEl.textContent = d.tickerSymbol;

  const sectorEl = document.getElementById("companySector");
  if (sectorEl) sectorEl.textContent = d.sector || "Unclassified";

  const priceEl = document.getElementById("companyCurrentPrice");
  if (priceEl) priceEl.textContent = formatBDT(d.currentPrice);

  const changeBadge = document.getElementById("companyDayChangeBadge");
  if (changeBadge) {
    const isUp = (d.dayChange || 0) >= 0;
    const sign = isUp ? "+" : "-";
    const absChange = Math.abs(d.dayChange || 0).toFixed(2);
    const absPct = Math.abs(d.dayChangePercentage || 0).toFixed(2);

    if (d.dayChange === null || d.dayChange === undefined) {
      changeBadge.className = "badge bg-secondary-subtle text-secondary border";
      changeBadge.textContent = "0.00%";
    } else if (isUp) {
      changeBadge.className = "badge bg-success-subtle text-success border border-success-subtle fw-semibold";
      changeBadge.textContent = `${sign}৳${absChange} (${sign}${absPct}%)`;
    } else {
      changeBadge.className = "badge bg-danger-subtle text-danger border border-danger-subtle fw-semibold";
      changeBadge.textContent = `${sign}৳${absChange} (${sign}${absPct}%)`;
    }
  }

  // Action Buttons
  const actionContainer = document.getElementById("companyActionButtons");
  if (actionContainer) {
    let html = "";
    if (d.userOwnsShares) {
      html += `
        <a href="portfolio.html" class="btn btn-sm btn-outline-primary">
          <i class="bi bi-briefcase me-1"></i> View Position
        </a>
      `;
    }
    if (d.isInWatchlist) {
      html += `
        <button type="button" class="btn btn-sm btn-outline-danger" id="heroRemoveWatchlistBtn">
          <i class="bi bi-bookmark-dash me-1"></i> Remove from Watchlist
        </button>
      `;
    } else {
      html += `
        <button type="button" class="btn btn-sm btn-outline-primary" id="heroAddWatchlistBtn">
          <i class="bi bi-bookmark-plus me-1"></i> Add to Watchlist
        </button>
      `;
    }
    html += `
      <a href="transactions.html?companyId=${d.companyId}&type=BUY" class="btn btn-sm btn-primary">
        <i class="bi bi-plus-circle me-1"></i> Record BUY
      </a>
    `;
    actionContainer.innerHTML = html;

    // Attach listeners
    const heroAddBtn = document.getElementById("heroAddWatchlistBtn");
    if (heroAddBtn) {
      heroAddBtn.onclick = () => openCompanyWatchlistModal();
    }
    const heroRemoveBtn = document.getElementById("heroRemoveWatchlistBtn");
    if (heroRemoveBtn) {
      heroRemoveBtn.onclick = () => removeCompanyFromWatchlist();
    }
  }
}

function renderCompanyKeyMetrics(d) {
  const tickerEl = document.getElementById("statTicker");
  if (tickerEl) tickerEl.textContent = d.tickerSymbol;

  const sectorEl = document.getElementById("statSector");
  if (sectorEl) sectorEl.textContent = d.sector || "General";

  const marketCapEl = document.getElementById("statMarketCap");
  if (marketCapEl) {
    marketCapEl.textContent = d.marketCap ? formatBDT(d.marketCap) : "N/A";
  }

  const yearHighEl = document.getElementById("statYearHigh");
  if (yearHighEl) {
    yearHighEl.textContent = d.yearHigh ? formatBDT(d.yearHigh) : formatBDT(d.currentPrice);
  }

  const yearLowEl = document.getElementById("statYearLow");
  if (yearLowEl) {
    yearLowEl.textContent = d.yearLow ? formatBDT(d.yearLow) : formatBDT(d.currentPrice);
  }
}

function renderCompanyUserPosition(d) {
  const badgeEl = document.getElementById("companyOwnershipBadge");
  const ownedView = document.getElementById("companyOwnedPositionView");
  const notOwnedView = document.getElementById("companyNotOwnedView");

  if (d.userOwnsShares && d.userPosition) {
    const pos = d.userPosition;
    if (badgeEl) {
      badgeEl.className = "badge bg-success-subtle text-success border border-success-subtle";
      badgeEl.textContent = `Holding ${pos.totalShares.toLocaleString()} Shares`;
    }
    if (ownedView) ownedView.classList.remove("d-none");
    if (notOwnedView) notOwnedView.classList.add("d-none");

    const sharesEl = document.getElementById("positionShares");
    if (sharesEl) sharesEl.textContent = pos.totalShares.toLocaleString();

    const avgPriceEl = document.getElementById("positionAvgPrice");
    if (avgPriceEl) avgPriceEl.textContent = formatBDT(pos.averageCost);

    const costBasisEl = document.getElementById("positionCostBasis");
    if (costBasisEl) costBasisEl.textContent = formatBDT(pos.costBasis);

    const mktValEl = document.getElementById("positionMarketValue");
    if (mktValEl) mktValEl.textContent = formatBDT(pos.currentValue);

    // Unrealized P/L
    const pl = pos.unrealizedProfitLoss || 0;
    const plPct = pos.unrealizedProfitLossPercentage || 0;
    const isUp = pl >= 0;
    const sign = isUp ? "+" : "-";

    const plEl = document.getElementById("positionUnrealizedPL");
    if (plEl) {
      plEl.className = `fw-bold mb-0 ${isUp ? "text-success" : "text-danger"}`;
      plEl.textContent = `${sign}${formatBDT(Math.abs(pl))}`;
    }

    const plPctEl = document.getElementById("positionReturnPct");
    if (plPctEl) {
      plPctEl.className = `fw-semibold ${isUp ? "text-success" : "text-danger"}`;
      plPctEl.textContent = `(${sign}${Math.abs(plPct).toFixed(2)}%)`;
    }

    // Portfolio breakdown
    const breakdownCont = document.getElementById("positionBreakdownContainer");
    const breakdownList = document.getElementById("positionBreakdownList");
    if (breakdownCont && breakdownList) {
      if (pos.portfolioBreakdown && pos.portfolioBreakdown.length > 0) {
        breakdownCont.classList.remove("d-none");
        breakdownList.innerHTML = pos.portfolioBreakdown.map(b => `
          <div class="list-group-item d-flex justify-content-between align-items-center px-0 py-2 bg-transparent">
            <div>
              <span class="fw-semibold text-body">${escapeHtml(b.portfolioName)}</span>
              <small class="text-muted d-block">${b.shares.toLocaleString()} shares @ ${formatBDT(b.averageCost)}</small>
            </div>
            <div class="text-end">
              <span class="fw-semibold text-body">${formatBDT(b.marketValue)}</span>
              <small class="d-block ${b.unrealizedProfitLoss >= 0 ? "text-success" : "text-danger"}">
                ${b.unrealizedProfitLoss >= 0 ? "+" : ""}${formatBDT(b.unrealizedProfitLoss)}
              </small>
            </div>
          </div>
        `).join("");
      } else {
        breakdownCont.classList.add("d-none");
      }
    }
  } else {
    if (badgeEl) {
      badgeEl.className = "badge bg-secondary-subtle text-secondary border";
      badgeEl.textContent = "Not Owned";
    }
    if (ownedView) ownedView.classList.add("d-none");
    if (notOwnedView) notOwnedView.classList.remove("d-none");
  }
}

function renderCompanyWatchlistStatus(d) {
  const badgeEl = document.getElementById("companyWatchlistBadge");
  const activeView = document.getElementById("companyWatchlistActiveView");
  const inactiveView = document.getElementById("companyWatchlistInactiveView");

  if (d.isInWatchlist) {
    if (badgeEl) {
      badgeEl.className = "badge bg-info-subtle text-info border border-info-subtle";
      badgeEl.textContent = "Watching";
    }
    if (activeView) activeView.classList.remove("d-none");
    if (inactiveView) inactiveView.classList.add("d-none");

    const nameEl = document.getElementById("companyActiveWatchlistName");
    if (nameEl) nameEl.textContent = d.watchlistName || "Primary Watchlist";

    const targetEl = document.getElementById("companyActiveTargetPrice");
    if (targetEl) targetEl.textContent = d.targetPrice ? formatBDT(d.targetPrice) : "None set";

    const statusEl = document.getElementById("companyActiveTargetStatus");
    if (statusEl) {
      if (d.targetPrice) {
        if (d.currentPrice <= d.targetPrice) {
          statusEl.innerHTML = `<span class="badge bg-success-subtle text-success border border-success-subtle"><i class="bi bi-check-circle me-1"></i> Target Reached / Buying Zone</span>`;
        } else {
          const diff = d.currentPrice - d.targetPrice;
          statusEl.innerHTML = `<span class="badge bg-warning-subtle text-warning border border-warning-subtle"><i class="bi bi-clock me-1"></i> ৳${diff.toFixed(2)} above target</span>`;
        }
      } else {
        statusEl.innerHTML = `<span class="text-muted">No target configured</span>`;
      }
    }

    const editBtn = document.getElementById("companyQuickEditTargetBtn");
    if (editBtn) {
      editBtn.onclick = () => editCompanyTargetPrice();
    }

    const removeBtn = document.getElementById("companyQuickRemoveWatchlistBtn");
    if (removeBtn) {
      removeBtn.onclick = () => removeCompanyFromWatchlist();
    }
  } else {
    if (badgeEl) {
      badgeEl.className = "badge bg-secondary-subtle text-secondary border";
      badgeEl.textContent = "Not Watching";
    }
    if (activeView) activeView.classList.add("d-none");
    if (inactiveView) inactiveView.classList.remove("d-none");

    const addCardBtn = document.getElementById("companyAddWatchlistCardBtn");
    if (addCardBtn) {
      addCardBtn.onclick = () => openCompanyWatchlistModal();
    }
  }
}

function renderCompanyDividendHistory(d) {
  const countEl = document.getElementById("companyDividendCount");
  const bodyEl = document.getElementById("companyDividendsBody");

  const divs = d.dividendHistory || [];
  if (countEl) countEl.textContent = `${divs.length} declared`;

  if (!bodyEl) return;

  if (divs.length === 0) {
    bodyEl.innerHTML = `
      <tr>
        <td colspan="5" class="text-center py-4 text-muted">
          <i class="bi bi-cash-coin fs-4 d-block mb-1"></i>
          No corporate dividend declarations on record for this company.
        </td>
      </tr>
    `;
    return;
  }

  bodyEl.innerHTML = divs.map(item => {
    const declDate = new Date(item.declarationDate);
    const declStr = isNaN(declDate.getTime()) ? escapeHtml(item.declarationDate) : declDate.toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
    
    let payStr = "Pending";
    let isPaid = false;
    if (item.paymentDate) {
      const payDate = new Date(item.paymentDate);
      if (!isNaN(payDate.getTime())) {
        payStr = payDate.toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
        isPaid = payDate < new Date();
      }
    }

    const statusBadge = isPaid
      ? `<span class="badge bg-success-subtle text-success border border-success-subtle">Paid</span>`
      : `<span class="badge bg-warning-subtle text-warning border border-warning-subtle">Upcoming</span>`;

    const incomeStr = item.estimatedUserIncome !== null && item.estimatedUserIncome !== undefined
      ? `<span class="fw-semibold text-success">${formatBDT(item.estimatedUserIncome)}</span>`
      : `<span class="text-muted">—</span>`;

    return `
      <tr>
        <td>${declStr}</td>
        <td>${payStr}</td>
        <td class="fw-semibold">${formatBDT(item.dividendPerShare)}</td>
        <td>${incomeStr}</td>
        <td>${statusBadge}</td>
      </tr>
    `;
  }).join("");
}

function setupCompanyDetailTimeframeButtons() {
  const container = document.getElementById("companyTimeframeButtons");
  if (!container || container.dataset.initialized === "true") return;

  container.dataset.initialized = "true";
  const buttons = container.querySelectorAll(".timeframe-btn");
  buttons.forEach(btn => {
    btn.addEventListener("click", () => {
      const period = btn.dataset.period;
      if (!period || !currentCompanyDetailId) return;
      currentCompanyDetailPeriod = period;

      buttons.forEach(b => b.classList.remove("active"));
      btn.classList.add("active");

      loadCompanyDetailChart(currentCompanyDetailId, period);
    });
  });
}

async function loadCompanyDetailChart(companyId, period) {
  const loadingEl = document.getElementById("companyChartLoading");
  const emptyEl = document.getElementById("companyChartEmpty");
  const containerEl = document.getElementById("companyChartContainer");

  if (loadingEl) loadingEl.classList.remove("d-none");
  if (emptyEl) emptyEl.classList.add("d-none");

  try {
    const res = await apiRequest(`/companies/${companyId}/price-history?period=${encodeURIComponent(period)}&limit=300`);
    const data = res.data;

    if (loadingEl) loadingEl.classList.add("d-none");

    const highEl = document.getElementById("companyChartHigh");
    if (highEl) highEl.textContent = data.periodHigh ? formatBDT(data.periodHigh) : "৳0.00";

    const lowEl = document.getElementById("companyChartLow");
    if (lowEl) lowEl.textContent = data.periodLow ? formatBDT(data.periodLow) : "৳0.00";

    const changeEl = document.getElementById("companyChartChange");
    if (changeEl) {
      const isUp = (data.periodChange || 0) >= 0;
      const sign = isUp ? "+" : "";
      changeEl.className = `fs-6 ${isUp ? "text-success" : "text-danger"}`;
      changeEl.textContent = `${sign}৳${Number(data.periodChange || 0).toFixed(2)} (${sign}${Number(data.periodChangePercentage || 0).toFixed(2)}%)`;
    }

    const snapsEl = document.getElementById("companyChartSnapshots");
    if (snapsEl) snapsEl.textContent = (data.totalSnapshots || 0).toLocaleString();

    const history = data.history || [];
    if (history.length === 0) {
      if (emptyEl) emptyEl.classList.remove("d-none");
      if (containerEl) containerEl.classList.add("d-none");
      if (companyDetailChartInstance) {
        companyDetailChartInstance.destroy();
        companyDetailChartInstance = null;
      }
      return;
    }

    if (containerEl) containerEl.classList.remove("d-none");
    renderCompanyPriceChartCanvas(history, period, (data.periodChange || 0) >= 0);
  } catch (err) {
    console.error("Failed to load company chart data:", err);
    if (loadingEl) loadingEl.classList.add("d-none");
    if (emptyEl) emptyEl.classList.remove("d-none");
    if (containerEl) containerEl.classList.add("d-none");
  }
}

function renderCompanyPriceChartCanvas(history, period, isUp) {
  const canvas = document.getElementById("companyPriceHistoryCanvas");
  if (!canvas) return;

  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const strokeColor = isUp ? (isDark ? "#10B981" : "#16A34A") : (isDark ? "#F43F5E" : "#DC2626");
  const gradTop = isUp
    ? (isDark ? "rgba(16, 185, 129, 0.28)" : "rgba(22, 163, 74, 0.20)")
    : (isDark ? "rgba(244, 63, 94, 0.28)" : "rgba(220, 38, 38, 0.20)");
  const gradBottom = isUp ? "rgba(16, 185, 129, 0.0)" : "rgba(244, 63, 94, 0.0)";

  const ctx = canvas.getContext("2d");
  const gradient = ctx.createLinearGradient(0, 0, 0, 320);
  gradient.addColorStop(0, gradTop);
  gradient.addColorStop(1, gradBottom);

  const labels = history.map(h => {
    const d = new Date(h.recordedAt);
    if (period === "1D") {
      return d.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" });
    }
    if (period === "1W" || period === "1M") {
      return d.toLocaleDateString("en-US", { month: "short", day: "numeric" });
    }
    return d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "2-digit" });
  });

  const prices = history.map(h => h.price);

  if (companyDetailChartInstance) {
    companyDetailChartInstance.destroy();
    companyDetailChartInstance = null;
  }

  companyDetailChartInstance = new Chart(ctx, {
    type: "line",
    data: {
      labels: labels,
      datasets: [{
        label: "Price",
        data: prices,
        borderColor: strokeColor,
        borderWidth: 2.5,
        backgroundColor: gradient,
        fill: true,
        tension: 0.3,
        pointRadius: history.length === 1 ? 5 : (history.length > 50 ? 0 : 3),
        pointHoverRadius: 6,
        pointBackgroundColor: strokeColor,
        pointBorderColor: isDark ? "#111726" : "#ffffff",
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
          backgroundColor: isDark ? "rgba(17, 23, 38, 0.95)" : "rgba(15, 23, 42, 0.95)",
          titleColor: isDark ? "#F8FAFC" : "#FFFFFF",
          bodyColor: isDark ? "#E2E8F0" : "#FFFFFF",
          borderColor: isDark ? "rgba(255, 255, 255, 0.12)" : "transparent",
          borderWidth: isDark ? 1 : 0,
          titleFont: { size: 12, weight: "600" },
          bodyFont: { size: 12 },
          padding: 10,
          cornerRadius: 8,
          displayColors: false,
          callbacks: {
            title: items => {
              const idx = items[0].dataIndex;
              const r = history[idx];
              if (!r) return items[0].label;
              const d = new Date(r.recordedAt);
              return d.toLocaleDateString("en-US", {
                month: "short",
                day: "numeric",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
              });
            },
            label: context => {
              const idx = context.dataIndex;
              const r = history[idx];
              const lines = [`Price: ৳${Number(context.parsed.y).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`];
              if (r) {
                if (r.openPrice) lines.push(`Open: ৳${Number(r.openPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`);
                if (r.highPrice) lines.push(`High: ৳${Number(r.highPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`);
                if (r.lowPrice) lines.push(`Low: ৳${Number(r.lowPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`);
                if (r.volume) lines.push(`Volume: ${Number(r.volume).toLocaleString()}`);
              }
              return lines;
            }
          }
        }
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: {
            font: { size: 11 },
            color: isDark ? "#94A3B8" : "#64748b",
            maxTicksLimit: 8
          }
        },
        y: {
          grid: {
            color: isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(226, 232, 240, 0.7)",
            drawBorder: false
          },
          ticks: {
            font: { size: 11 },
            color: isDark ? "#94A3B8" : "#64748b",
            callback: value => "৳" + Number(value).toFixed(2)
          }
        }
      }
    }
  });
}

function setupCompanyDetailWatchlistActions() {
  const form = document.getElementById("companyWatchlistForm");
  if (!form || form.dataset.initialized === "true") return;

  form.dataset.initialized = "true";

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const select = document.getElementById("companyWatchlistSelect");
    const targetInput = document.getElementById("companyWatchlistTargetInput");
    const submitBtn = document.getElementById("saveCompanyWatchlistBtn");

    const watchlistId = select ? parseInt(select.value, 10) : null;
    const targetPrice = targetInput && targetInput.value ? parseFloat(targetInput.value) : null;

    if (!watchlistId) {
      showToast("Please select a target watchlist.", "warning");
      return;
    }

    if (submitBtn) {
      submitBtn.disabled = true;
      submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Saving...';
    }

    try {
      await apiRequest(`/watchlists/${watchlistId}/items`, {
        method: "POST",
        body: JSON.stringify({
          companyId: currentCompanyDetailId,
          targetPrice: targetPrice
        })
      });

      showToast("Company added to watchlist successfully.", "success");

      const modalEl = document.getElementById("companyWatchlistModal");
      if (modalEl && typeof bootstrap !== "undefined" && bootstrap.Modal) {
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
      }

      await loadCompanyDetailPageData(currentCompanyDetailId);
    } catch (err) {
      showToast(err.message || "Failed to add to watchlist.", "danger");
    } finally {
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = '<i class="bi bi-bookmark-plus me-1"></i> Add to Watchlist';
      }
    }
  });
}

async function openCompanyWatchlistModal() {
  const select = document.getElementById("companyWatchlistSelect");
  const targetInput = document.getElementById("companyWatchlistTargetInput");
  const modalEl = document.getElementById("companyWatchlistModal");

  if (!modalEl) return;

  if (targetInput && currentCompanyDetailData) {
    targetInput.value = currentCompanyDetailData.targetPrice || currentCompanyDetailData.currentPrice || "";
  }

  if (select) {
    select.innerHTML = '<option value="">Loading watchlists...</option>';
    try {
      const res = await apiRequest("/watchlists");
      const watchlists = res.data || [];
      if (watchlists.length === 0) {
        select.innerHTML = '<option value="">No watchlists found. Create one first.</option>';
      } else {
        select.innerHTML = watchlists.map(w => `
          <option value="${w.watchlistId}">${escapeHtml(w.name)} (${w.itemsCount || 0} items)</option>
        `).join("");
      }
    } catch (err) {
      select.innerHTML = '<option value="">Failed to load watchlists</option>';
    }
  }

  if (typeof bootstrap !== "undefined" && bootstrap.Modal) {
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();
  }
}

async function removeCompanyFromWatchlist() {
  if (!currentCompanyDetailData || !currentCompanyDetailData.watchlistId) return;

  if (!confirm(`Remove ${currentCompanyDetailData.tickerSymbol} from watchlist "${currentCompanyDetailData.watchlistName || 'Watchlist'}"?`)) {
    return;
  }

  try {
    await apiRequest(`/watchlists/${currentCompanyDetailData.watchlistId}/items/${currentCompanyDetailId}`, {
      method: "DELETE"
    });
    showToast("Removed from watchlist.", "info");
    await loadCompanyDetailPageData(currentCompanyDetailId);
  } catch (err) {
    showToast(err.message || "Failed to remove from watchlist.", "danger");
  }
}

async function editCompanyTargetPrice() {
  if (!currentCompanyDetailData || !currentCompanyDetailData.watchlistId) return;

  const current = currentCompanyDetailData.targetPrice || currentCompanyDetailData.currentPrice || "";
  const input = prompt(`Enter new target price for ${currentCompanyDetailData.tickerSymbol} (৳):`, current);
  if (input === null) return; // user cancelled

  const newTarget = parseFloat(input);
  if (isNaN(newTarget) || newTarget <= 0) {
    showToast("Please enter a valid positive target price.", "warning");
    return;
  }

  try {
    await apiRequest(`/watchlists/${currentCompanyDetailData.watchlistId}/items/${currentCompanyDetailId}`, {
      method: "PUT",
      body: JSON.stringify({ targetPrice: newTarget })
    });
    showToast("Target price updated.", "success");
    await loadCompanyDetailPageData(currentCompanyDetailId);
  } catch (err) {
    showToast(err.message || "Failed to update target price.", "danger");
  }
}

// =========================================
// ADVANCED PORTFOLIO ANALYTICS (FEATURE 3)
// =========================================

let currentAnalyticsPortfolioId = null;
let currentAnalyticsPeriod = "ALL";
let analyticsPerformanceChartInstance = null;
let analyticsSectorChartInstance = null;
let analyticsCompanyChartInstance = null;
let riskCompanyChartInstance = null;
let riskSectorChartInstance = null;

async function setupAnalyticsPage() {
  const contentEl = document.getElementById("analyticsContent");
  if (!contentEl) return; // Not on analytics.html

  // Read portfolioId from query string if available
  const urlParams = new URLSearchParams(window.location.search);
  const paramPortfolioId = urlParams.get("portfolioId");
  if (paramPortfolioId && !isNaN(parseInt(paramPortfolioId, 10))) {
    currentAnalyticsPortfolioId = parseInt(paramPortfolioId, 10);
  }

  // Populate Portfolio Select
  const select = document.getElementById("analyticsPortfolioSelect");
  if (select) {
    try {
      const res = await apiRequest("/portfolios");
      const portfolios = res.data || [];
      select.innerHTML = '<option value="">Consolidated (All Portfolios)</option>' +
        portfolios.map(p => `
          <option value="${p.portfolioId}" ${currentAnalyticsPortfolioId === p.portfolioId ? "selected" : ""}>
            ${escapeHtml(p.name)}
          </option>
        `).join("");
    } catch (err) {
      console.error("Failed to load user portfolios for analytics:", err);
    }

    select.addEventListener("change", () => {
      const val = select.value;
      currentAnalyticsPortfolioId = val ? parseInt(val, 10) : null;
      loadAnalyticsData(currentAnalyticsPortfolioId, currentAnalyticsPeriod);
    });
  }

  // Timeframe buttons
  const timeframeContainer = document.getElementById("analyticsTimeframeButtons");
  if (timeframeContainer) {
    const buttons = timeframeContainer.querySelectorAll(".perf-pill-btn");
    buttons.forEach(btn => {
      btn.addEventListener("click", () => {
        const period = btn.dataset.period;
        if (!period) return;
        currentAnalyticsPeriod = period;
        buttons.forEach(b => b.classList.remove("active"));
        btn.classList.add("active");
        loadAnalyticsData(currentAnalyticsPortfolioId, currentAnalyticsPeriod);
      });
    });
  }

  // Refresh & Retry buttons
  const refreshBtn = document.getElementById("analyticsRefreshBtn");
  if (refreshBtn) {
    refreshBtn.addEventListener("click", () => {
      loadAnalyticsData(currentAnalyticsPortfolioId, currentAnalyticsPeriod);
    });
  }

  const retryBtn = document.getElementById("analyticsRetryBtn");
  if (retryBtn) {
    retryBtn.addEventListener("click", () => {
      loadAnalyticsData(currentAnalyticsPortfolioId, currentAnalyticsPeriod);
    });
  }

  // Initial load
  setupBenchmarkControls();
  await loadAnalyticsData(currentAnalyticsPortfolioId, currentAnalyticsPeriod);
}

async function loadAnalyticsData(portfolioId, period) {
  const loadingEl = document.getElementById("analyticsLoadingState");
  const errorEl = document.getElementById("analyticsErrorState");
  const errorMsgEl = document.getElementById("analyticsErrorMessage");
  const emptyEl = document.getElementById("analyticsEmptyState");
  const contentEl = document.getElementById("analyticsContent");

  if (loadingEl) loadingEl.classList.remove("d-none");
  if (errorEl) errorEl.classList.add("d-none");
  if (emptyEl) emptyEl.classList.add("d-none");
  if (contentEl) contentEl.classList.add("d-none");

  try {
    let url = `/analytics?period=${encodeURIComponent(period || "ALL")}`;
    if (portfolioId) {
      url += `&portfolioId=${portfolioId}`;
    }

    const res = await apiRequest(url);
    const data = res.data;

    if (!data) {
      throw new Error("Unable to retrieve analytics data from server.");
    }

    if (loadingEl) loadingEl.classList.add("d-none");

    // Check if portfolio is completely empty
    if (data.totalHoldingsCount === 0 && data.portfolioValue === 0) {
      if (emptyEl) emptyEl.classList.remove("d-none");
      return;
    }

    if (contentEl) contentEl.classList.remove("d-none");

    renderPortfolioRiskDashboard(data);
    renderAnalyticsKPIs(data);
    renderAnalyticsConcentration(data);
    renderAnalyticsPerformanceChart(data.historicalPerformance);
    renderAnalyticsSectorAllocation(data.sectorAllocation);
    renderAnalyticsCompanyAllocation(data.companyAllocation);
    renderAnalyticsGainersAndLosers(data.topGainers, data.topLosers);
    renderAnalyticsFormulas(data.metricFormulas);
    loadBenchmarkComparison(currentAnalyticsPortfolioId, currentBenchmarkCode, currentBenchmarkPeriod);

  } catch (err) {
    console.error("Analytics fetch error:", err);
    if (loadingEl) loadingEl.classList.add("d-none");
    if (emptyEl) emptyEl.classList.add("d-none");
    if (contentEl) contentEl.classList.add("d-none");
    if (errorEl) errorEl.classList.remove("d-none");
    if (errorMsgEl) {
      errorMsgEl.textContent = sanitizeErrorMessage(err.message, err.status);
    }
  }
}


// =========================================
// WEB FEATURE 5: BENCHMARK COMPARISON
// =========================================

let currentBenchmarkCode = "DSEX";
let currentBenchmarkPeriod = "ALL";
let benchmarkComparisonChartInstance = null;

async function setupBenchmarkControls() {
  const select = document.getElementById("benchmarkSelect");
  if (select && select.dataset.initialized !== "true") {
    select.dataset.initialized = "true";
    try {
      const res = await apiRequest("/benchmarks");
      const list = res.data || [];
      if (list.length > 0) {
        select.innerHTML = list.map(b => `<option value="${escapeHtml(b.benchmarkCode)}">${escapeHtml(b.name)}</option>`).join("");
      }
    } catch (err) {
      console.warn("Failed to load benchmark list:", err);
    }

    select.addEventListener("change", () => {
      currentBenchmarkCode = select.value || "DSEX";
      loadBenchmarkComparison(currentAnalyticsPortfolioId, currentBenchmarkCode, currentBenchmarkPeriod);
    });
  }

  const buttonsContainer = document.getElementById("benchmarkTimeframeButtons");
  if (buttonsContainer && buttonsContainer.dataset.initialized !== "true") {
    buttonsContainer.dataset.initialized = "true";
    const buttons = buttonsContainer.querySelectorAll(".perf-pill-btn");
    buttons.forEach(btn => {
      btn.addEventListener("click", () => {
        const period = btn.dataset.period;
        if (!period) return;
        currentBenchmarkPeriod = period;
        buttons.forEach(b => b.classList.remove("active"));
        btn.classList.add("active");
        loadBenchmarkComparison(currentAnalyticsPortfolioId, currentBenchmarkCode, currentBenchmarkPeriod);
      });
    });
  }
}

async function loadBenchmarkComparison(portfolioId, benchmarkCode, period) {
  const alertEl = document.getElementById("benchmarkInsufficientDataAlert");
  const alertText = document.getElementById("benchmarkInsufficientDataText");
  const chartCanvas = document.getElementById("benchmarkComparisonChart");
  const emptyState = document.getElementById("benchmarkChartEmptyState");

  const portReturnEl = document.getElementById("benchmarkPortfolioReturn");
  const portStartEl = document.getElementById("benchmarkPortfolioStart");
  const portEndEl = document.getElementById("benchmarkPortfolioEnd");
  const benchTitleEl = document.getElementById("benchmarkLabelTitle");
  const benchBadgeEl = document.getElementById("benchmarkCodeBadge");
  const benchReturnEl = document.getElementById("benchmarkReturn");
  const benchStartEl = document.getElementById("benchmarkStartVal");
  const benchEndEl = document.getElementById("benchmarkEndVal");
  const diffEl = document.getElementById("benchmarkDifference");
  const diffBadgeEl = document.getElementById("benchmarkOutperformanceBadge");
  const noticeEl = document.getElementById("benchmarkLimitationNotice");

  try {
    let url = `/benchmarks/compare?benchmarkCode=${encodeURIComponent(benchmarkCode || "DSEX")}&period=${encodeURIComponent(period || "ALL")}`;
    if (portfolioId) {
      url += `&portfolioId=${portfolioId}`;
    }

    const res = await apiRequest(url);
    const data = res.data;
    if (!data) return;

    if (noticeEl && data.dataLimitationNotice) {
      noticeEl.textContent = data.dataLimitationNotice;
    }

    // Update timeframe buttons availability
    const buttonsContainer = document.getElementById("benchmarkTimeframeButtons");
    if (buttonsContainer) {
      const buttons = buttonsContainer.querySelectorAll(".perf-pill-btn");
      buttons.forEach(btn => {
        const p = btn.dataset.period;
        const isAvail = data.availablePeriods && data.availablePeriods.includes(p);
        if (isAvail) {
          btn.style.opacity = "1";
          btn.style.pointerEvents = "auto";
          btn.title = "";
        } else {
          btn.style.opacity = "0.4";
          btn.title = `Insufficient historical data for ${p}`;
        }
      });
    }

    if (!data.hasSufficientData) {
      if (alertEl) {
        alertEl.classList.remove("d-none");
        if (alertText) alertText.textContent = data.message || "Insufficient historical data for benchmark comparison in this period.";
      }
      if (chartCanvas) chartCanvas.style.display = "none";
      if (emptyState) emptyState.classList.remove("d-none");

      if (portReturnEl) portReturnEl.textContent = "—";
      if (benchReturnEl) benchReturnEl.textContent = "—";
      if (diffEl) diffEl.textContent = "—";
      if (diffBadgeEl) {
        diffBadgeEl.className = "badge bg-secondary";
        diffBadgeEl.textContent = "Data Limited";
      }
      return;
    }

    // Sufficient Data
    if (alertEl) alertEl.classList.add("d-none");
    if (chartCanvas) chartCanvas.style.display = "block";
    if (emptyState) emptyState.classList.add("d-none");

    const pSign = data.portfolioReturnPercentage >= 0 ? "+" : "";
    const bSign = data.benchmarkReturnPercentage >= 0 ? "+" : "";
    const dSign = data.outperformancePercentage >= 0 ? "+" : "";

    if (portReturnEl) {
      portReturnEl.textContent = `${pSign}${data.portfolioReturnPercentage.toFixed(2)}%`;
      portReturnEl.className = `fs-3 fw-bold mb-1 ${data.portfolioReturnPercentage >= 0 ? "text-success" : "text-danger"}`;
    }
    if (portStartEl) portStartEl.textContent = formatBDT(data.portfolioStartingValue);
    if (portEndEl) portEndEl.textContent = formatBDT(data.portfolioEndingValue);

    if (benchTitleEl) benchTitleEl.textContent = `${data.benchmarkName || "Benchmark"} Return`;
    if (benchBadgeEl) benchBadgeEl.textContent = data.benchmarkCode;
    if (benchReturnEl) {
      benchReturnEl.textContent = `${bSign}${data.benchmarkReturnPercentage.toFixed(2)}%`;
      benchReturnEl.className = `fs-3 fw-bold mb-1 ${data.benchmarkReturnPercentage >= 0 ? "text-success" : "text-danger"}`;
    }
    if (benchStartEl) benchStartEl.textContent = Number(data.benchmarkStartingValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (benchEndEl) benchEndEl.textContent = Number(data.benchmarkEndingValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    if (diffEl) {
      diffEl.textContent = `${dSign}${data.outperformancePercentage.toFixed(2)}%`;
      diffEl.className = `fs-3 fw-bold mb-1 ${data.isOutperforming ? "text-success" : "text-danger"}`;
    }
    if (diffBadgeEl) {
      if (data.isOutperforming) {
        diffBadgeEl.className = "badge bg-success";
        diffBadgeEl.textContent = `Outperforming (${dSign}${data.outperformancePercentage.toFixed(2)}%)`;
      } else {
        diffBadgeEl.className = "badge bg-danger";
        diffBadgeEl.textContent = `Underperforming (${dSign}${data.outperformancePercentage.toFixed(2)}%)`;
      }
    }

    renderBenchmarkChart(data);
  } catch (err) {
    console.error("Failed to load benchmark comparison:", err);
  }
}

function renderBenchmarkChart(data) {
  const canvas = document.getElementById("benchmarkComparisonChart");
  if (!canvas) return;

  if (benchmarkComparisonChartInstance) {
    benchmarkComparisonChartInstance.destroy();
    benchmarkComparisonChartInstance = null;
  }

  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const ctx = canvas.getContext("2d");

  benchmarkComparisonChartInstance = new Chart(ctx, {
    type: "line",
    data: {
      labels: data.labels,
      datasets: [
        {
          label: `${data.portfolioName || "Portfolio"} Return (%)`,
          data: data.portfolioReturns,
          borderColor: "#2563EB",
          backgroundColor: isDark ? "rgba(37, 99, 235, 0.15)" : "rgba(37, 99, 235, 0.08)",
          fill: true,
          tension: 0.3,
          borderWidth: 2.5,
          pointRadius: 4,
          pointHoverRadius: 6,
          pointBackgroundColor: "#2563EB"
        },
        {
          label: `${data.benchmarkCode || "Benchmark"} Return (%)`,
          data: data.benchmarkReturns,
          borderColor: "#F59E0B",
          backgroundColor: "transparent",
          borderDash: [5, 5],
          fill: false,
          tension: 0.3,
          borderWidth: 2.5,
          pointRadius: 3,
          pointHoverRadius: 5,
          pointBackgroundColor: "#F59E0B"
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        mode: "index",
        intersect: false
      },
      plugins: {
        legend: {
          position: "top",
          labels: {
            font: { size: 12 },
            color: isDark ? "#CBD5E1" : "#475569",
            usePointStyle: true
          }
        },
        tooltip: {
          callbacks: {
            label: context => {
              const datasetLabel = context.dataset.label || "";
              const val = context.raw;
              const sign = val >= 0 ? "+" : "";
              const idx = context.dataIndex;
              let extra = "";
              if (context.datasetIndex === 0 && data.portfolioValues && data.portfolioValues[idx] !== undefined) {
                extra = ` (${formatBDT(data.portfolioValues[idx])})`;
              } else if (context.datasetIndex === 1 && data.benchmarkValues && data.benchmarkValues[idx] !== undefined) {
                extra = ` (${Number(data.benchmarkValues[idx]).toLocaleString()} pts)`;
              }
              return ` ${datasetLabel}: ${sign}${Number(val).toFixed(2)}%${extra}`;
            }
          }
        }
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: { color: isDark ? "#94A3B8" : "#64748B", font: { size: 11 } }
        },
        y: {
          grid: { color: isDark ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.05)" },
          ticks: {
            color: isDark ? "#94A3B8" : "#64748B",
            font: { size: 11 },
            callback: value => `${value >= 0 ? "+" : ""}${value}%`
          }
        }
      }
    }
  });
}

function renderPortfolioRiskDashboard(d) {
  // 1. Total Holdings & Sectors
  const totalHoldingsEl = document.getElementById("riskTotalHoldings");
  if (totalHoldingsEl) totalHoldingsEl.textContent = d.totalHoldingsCount || 0;

  const totalSectorsEl = document.getElementById("riskTotalSectors");
  if (totalSectorsEl) totalSectorsEl.textContent = d.totalSectorsCount || 0;

  // 2. Largest Holding %
  const largestHoldingVal = d.largestHoldingWeight || d.topHoldingWeight || 0;
  const largestHoldingEl = document.getElementById("riskLargestHolding");
  if (largestHoldingEl) largestHoldingEl.textContent = `${largestHoldingVal.toFixed(1)}%`;

  const topCo = d.companyAllocation && d.companyAllocation.length > 0 ? d.companyAllocation[0] : null;
  const largestHoldingTickerEl = document.getElementById("riskLargestHoldingTicker");
  if (largestHoldingTickerEl) {
    largestHoldingTickerEl.textContent = topCo ? `${topCo.tickerSymbol} (${topCo.allocationPercentage.toFixed(1)}%)` : "—";
  }

  // 3. Largest Sector %
  const largestSectorVal = d.largestSectorWeight || (d.sectorAllocation && d.sectorAllocation.length > 0 ? d.sectorAllocation[0].allocationPercentage : 0);
  const largestSectorEl = document.getElementById("riskLargestSector");
  if (largestSectorEl) largestSectorEl.textContent = `${largestSectorVal.toFixed(1)}%`;

  const topSec = d.sectorAllocation && d.sectorAllocation.length > 0 ? d.sectorAllocation[0] : null;
  const largestSectorNameEl = document.getElementById("riskLargestSectorName");
  if (largestSectorNameEl) {
    largestSectorNameEl.textContent = topSec ? `${topSec.sectorName} (${topSec.allocationPercentage.toFixed(1)}%)` : "—";
  }

  // 4. Diversification Score
  const scoreVal = Math.round(d.diversificationScore || 0);
  const divScoreEl = document.getElementById("riskDiversificationScore");
  if (divScoreEl) divScoreEl.textContent = `${scoreVal}%`;

  const divStatusEl = document.getElementById("riskDiversificationStatus");
  if (divStatusEl) divStatusEl.textContent = d.concentrationStatus || "Diversified";

  // Concentration Badge
  const badgeEl = document.getElementById("riskConcentrationBadge");
  if (badgeEl) {
    const status = d.concentrationStatus || "Diversified";
    if (status === "Diversified") {
      badgeEl.className = "concentration-pill bg-success-subtle text-success border border-success-subtle";
      badgeEl.innerHTML = '<i class="bi bi-shield-check"></i> Diversified';
    } else if (status === "Moderately Concentrated") {
      badgeEl.className = "concentration-pill bg-warning-subtle text-warning border border-warning-subtle";
      badgeEl.innerHTML = '<i class="bi bi-shield-exclamation"></i> Moderate Concentration';
    } else {
      badgeEl.className = "concentration-pill bg-danger-subtle text-danger border border-danger-subtle";
      badgeEl.innerHTML = '<i class="bi bi-exclamation-octagon"></i> Highly Concentrated';
    }
  }

  // Badges
  const badgeCompanyCount = document.getElementById("badgeCompanyAllocationCount");
  if (badgeCompanyCount) badgeCompanyCount.textContent = `${d.companyAllocation ? d.companyAllocation.length : 0} Companies`;

  const badgeSectorCount = document.getElementById("badgeSectorAllocationCount");
  if (badgeSectorCount) badgeSectorCount.textContent = `${d.sectorAllocation ? d.sectorAllocation.length : 0} Sectors`;

  // Render Risk Charts and Top Holdings List
  renderRiskCompanyChart(d.companyAllocation || []);
  renderRiskSectorChart(d.sectorAllocation || []);
  renderRiskTopHoldingsList(d.companyAllocation || []);
}

function renderRiskCompanyChart(companies) {
  const canvas = document.getElementById("riskCompanyChart");
  if (!canvas) return;

  if (riskCompanyChartInstance) {
    riskCompanyChartInstance.destroy();
    riskCompanyChartInstance = null;
  }

  if (!companies || companies.length === 0) return;

  const palette = [
    "#2563EB", "#10B981", "#8B5CF6", "#F59E0B", "#06B6D4",
    "#EC4899", "#6366F1", "#14B8A6", "#F97316", "#64748B"
  ];
  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const ctx = canvas.getContext("2d");

  riskCompanyChartInstance = new Chart(ctx, {
    type: "doughnut",
    data: {
      labels: companies.map(c => c.tickerSymbol),
      datasets: [
        {
          data: companies.map(c => c.marketValue),
          backgroundColor: palette.slice(0, companies.length),
          borderWidth: 2,
          borderColor: isDark ? "#1E293B" : "#FFFFFF"
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label: context => {
              const val = context.raw;
              const pct = companies[context.dataIndex]?.allocationPercentage || 0;
              return ` ${context.label}: ৳${Number(val).toLocaleString()} (${pct.toFixed(1)}%)`;
            }
          }
        }
      },
      cutout: "65%"
    }
  });
}

function renderRiskSectorChart(sectors) {
  const canvas = document.getElementById("riskSectorChart");
  if (!canvas) return;

  if (riskSectorChartInstance) {
    riskSectorChartInstance.destroy();
    riskSectorChartInstance = null;
  }

  if (!sectors || sectors.length === 0) return;

  const palette = [
    "#3B82F6", "#34D399", "#A78BFA", "#FBBF24", "#38BDF8",
    "#F472B6", "#818CF8", "#2DD4BF", "#FB923C", "#94A3B8"
  ];
  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const ctx = canvas.getContext("2d");

  riskSectorChartInstance = new Chart(ctx, {
    type: "doughnut",
    data: {
      labels: sectors.map(s => s.sectorName),
      datasets: [
        {
          data: sectors.map(s => s.marketValue),
          backgroundColor: palette.slice(0, sectors.length),
          borderWidth: 2,
          borderColor: isDark ? "#1E293B" : "#FFFFFF"
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label: context => {
              const val = context.raw;
              const pct = sectors[context.dataIndex]?.allocationPercentage || 0;
              return ` ${context.label}: ৳${Number(val).toLocaleString()} (${pct.toFixed(1)}%)`;
            }
          }
        }
      },
      cutout: "65%"
    }
  });
}

function renderRiskTopHoldingsList(companies) {
  const listEl = document.getElementById("riskTopHoldingsList");
  if (!listEl) return;

  if (!companies || companies.length === 0) {
    listEl.innerHTML = '<div class="text-center text-muted py-4 small">No active holdings in portfolio</div>';
    return;
  }

  listEl.innerHTML = companies.map((c, index) => {
    const rank = index + 1;
    const pct = c.allocationPercentage || 0;
    return `
      <div class="top-holding-item">
        <div class="d-flex align-items-center gap-2" style="min-width: 0;">
          <div class="top-holding-rank">${rank}</div>
          <div style="min-width: 0;">
            <div class="d-flex align-items-center gap-2">
              <a href="company.html?id=${c.companyId}" class="fw-bold text-decoration-none text-truncate" style="color: var(--color-accent); font-size: 13px;">
                ${escapeHtml(c.tickerSymbol)}
              </a>
              <span class="text-muted small text-truncate d-none d-sm-inline" style="font-size: 11px;">
                ${escapeHtml(c.companyName)}
              </span>
            </div>
            <div class="text-muted" style="font-size: 11px;">
              ${Number(c.shares).toLocaleString()} shares &bull; ${formatBDT(c.marketValue)}
            </div>
          </div>
        </div>
        <div class="text-end flex-shrink-0 ms-2">
          <span class="badge ${rank === 1 ? 'bg-primary' : 'bg-secondary-subtle text-body border'}" style="font-size: 12px; font-weight: 600;">
            ${pct.toFixed(1)}%
          </span>
        </div>
      </div>
    `;
  }).join("");
}

function renderAnalyticsKPIs(d) {
  // 1. Current Portfolio Value
  const valEl = document.getElementById("kpiPortfolioValue");
  if (valEl) valEl.textContent = formatBDT(d.portfolioValue);

  const invEl = document.getElementById("kpiInvestedCapital");
  if (invEl) invEl.textContent = formatBDT(d.investedCapital);

  // 2. Unrealized P/L
  const unrlPL = d.unrealizedProfitLoss || 0;
  const unrlPct = d.unrealizedReturnPercentage || 0;
  const isUnrlUp = unrlPL >= 0;
  const unrlSign = isUnrlUp ? "+" : "-";

  const unrlPLEl = document.getElementById("kpiUnrealizedPL");
  if (unrlPLEl) {
    unrlPLEl.className = `metric-value ${isUnrlUp ? "text-success" : "text-danger"}`;
    unrlPLEl.textContent = `${unrlSign}${formatBDT(Math.abs(unrlPL))}`;
  }

  const unrlBadgeEl = document.getElementById("kpiUnrealizedReturnBadge");
  if (unrlBadgeEl) {
    unrlBadgeEl.className = `badge ${isUnrlUp ? "bg-success-subtle text-success border border-success-subtle" : "bg-danger-subtle text-danger border border-danger-subtle"}`;
    unrlBadgeEl.textContent = `${unrlSign}${Math.abs(unrlPct).toFixed(2)}%`;
  }

  // 3. Realized P/L
  const rlPL = d.realizedProfitLoss || 0;
  const isRlUp = rlPL >= 0;
  const rlSign = isRlUp ? "+" : "-";
  const rlEl = document.getElementById("kpiRealizedPL");
  if (rlEl) {
    rlEl.className = `metric-value ${rlPL === 0 ? "text-body" : (isRlUp ? "text-success" : "text-danger")}`;
    rlEl.textContent = rlPL === 0 ? formatBDT(0) : `${rlSign}${formatBDT(Math.abs(rlPL))}`;
  }

  // 4. Dividend Income & Yield
  const divIncEl = document.getElementById("kpiDividendIncome");
  if (divIncEl) divIncEl.textContent = formatBDT(d.dividendIncome || 0);

  const divYieldEl = document.getElementById("kpiDividendYield");
  if (divYieldEl) divYieldEl.textContent = `${(d.dividendYield || 0).toFixed(2)}%`;

  // 5. Total Return
  const totRet = d.totalReturn || 0;
  const totPct = d.totalReturnPercentage || 0;
  const isTotUp = totRet >= 0;
  const totSign = isTotUp ? "+" : "-";

  const totRetEl = document.getElementById("kpiTotalReturn");
  if (totRetEl) {
    totRetEl.className = `metric-value ${isTotUp ? "text-success" : "text-danger"}`;
    totRetEl.textContent = `${totSign}${formatBDT(Math.abs(totRet))}`;
  }

  const totBadgeEl = document.getElementById("kpiTotalReturnBadge");
  if (totBadgeEl) {
    totBadgeEl.className = `badge ${isTotUp ? "bg-success-subtle text-success border border-success-subtle" : "bg-danger-subtle text-danger border border-danger-subtle"}`;
    totBadgeEl.textContent = `${totSign}${Math.abs(totPct).toFixed(2)}%`;
  }
}

function renderAnalyticsConcentration(d) {
  // Concentration Status Badge
  const badgeEl = document.getElementById("concentrationBadge");
  if (badgeEl) {
    const status = d.concentrationStatus || "Diversified";
    if (status === "Diversified") {
      badgeEl.className = "concentration-pill bg-success-subtle text-success border border-success-subtle";
      badgeEl.innerHTML = '<i class="bi bi-shield-check"></i> Diversified';
    } else if (status === "Moderately Concentrated") {
      badgeEl.className = "concentration-pill bg-warning-subtle text-warning border border-warning-subtle";
      badgeEl.innerHTML = '<i class="bi bi-shield-exclamation"></i> Moderate Concentration';
    } else {
      badgeEl.className = "concentration-pill bg-danger-subtle text-danger border border-danger-subtle";
      badgeEl.innerHTML = '<i class="bi bi-exclamation-octagon"></i> Highly Concentrated';
    }
  }

  // HHI Gauge and Score
  const hhiText = document.getElementById("hhiScoreText");
  if (hhiText) hhiText.textContent = Math.round(d.herfindahlIndex || 0).toLocaleString();

  const hhiBar = document.getElementById("hhiBarFill");
  if (hhiBar) {
    const score = d.herfindahlIndex || 0;
    const pct = Math.min(100, Math.max(5, (score / 4000) * 100));
    hhiBar.style.width = `${pct}%`;
    if (score < 1500) {
      hhiBar.className = "hhi-gauge-fill bg-success";
    } else if (score < 2500) {
      hhiBar.className = "hhi-gauge-fill bg-warning";
    } else {
      hhiBar.className = "hhi-gauge-fill bg-danger";
    }
  }

  // Weights & Counts
  const top1El = document.getElementById("metricTop1Weight");
  if (top1El) top1El.textContent = `${(d.topHoldingWeight || 0).toFixed(2)}%`;

  const top3El = document.getElementById("metricTop3Weight");
  if (top3El) top3El.textContent = `${(d.top3Concentration || 0).toFixed(2)}%`;

  const top5El = document.getElementById("metricTop5Weight");
  if (top5El) top5El.textContent = `${(d.top5Concentration || 0).toFixed(2)}%`;

  const countHoldingsEl = document.getElementById("statHoldingsCount");
  if (countHoldingsEl) countHoldingsEl.textContent = d.totalHoldingsCount || 0;

  const countSectorsEl = document.getElementById("statSectorsCount");
  if (countSectorsEl) countSectorsEl.textContent = d.totalSectorsCount || 0;
}

function renderAnalyticsPerformanceChart(chartDto) {
  const startEl = document.getElementById("chartStartingValue");
  const endEl = document.getElementById("chartEndingValue");
  const netEl = document.getElementById("chartNetChange");

  if (!chartDto) return;

  if (startEl) startEl.textContent = formatBDT(chartDto.startingValue || 0);
  if (endEl) endEl.textContent = formatBDT(chartDto.endingValue || 0);

  if (netEl) {
    const net = chartDto.netChange || 0;
    const netPct = chartDto.netChangePercentage || 0;
    const isUp = net >= 0;
    const sign = isUp ? "+" : "-";
    netEl.className = `fw-bold ms-1 ${isUp ? "text-success" : "text-danger"}`;
    netEl.textContent = `${sign}${formatBDT(Math.abs(net))} (${sign}${Math.abs(netPct).toFixed(2)}%)`;
  }

  const canvas = document.getElementById("analyticsPerformanceChart");
  if (!canvas) return;

  if (analyticsPerformanceChartInstance) {
    analyticsPerformanceChartInstance.destroy();
    analyticsPerformanceChartInstance = null;
  }

  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const ctx = canvas.getContext("2d");

  // Create gradient
  const gradient = ctx.createLinearGradient(0, 0, 0, 200);
  gradient.addColorStop(0, isDark ? "rgba(37, 99, 235, 0.4)" : "rgba(37, 99, 235, 0.25)");
  gradient.addColorStop(1, isDark ? "rgba(37, 99, 235, 0.0)" : "rgba(37, 99, 235, 0.0)");

  const labels = chartDto.labels && chartDto.labels.length > 0 ? chartDto.labels : ["Today"];
  const values = chartDto.values && chartDto.values.length > 0 ? chartDto.values : [chartDto.endingValue || 0];

  analyticsPerformanceChartInstance = new Chart(ctx, {
    type: "line",
    data: {
      labels: labels,
      datasets: [
        {
          label: "Portfolio Value (BDT)",
          data: values,
          borderColor: "#2563EB",
          backgroundColor: gradient,
          fill: true,
          tension: 0.3,
          borderWidth: 2.5,
          pointRadius: values.length <= 15 ? 4 : 2,
          pointBackgroundColor: "#2563EB",
          pointHoverRadius: 6
        }
      ]
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
          backgroundColor: isDark ? "rgba(15, 23, 42, 0.95)" : "rgba(255, 255, 255, 0.95)",
          titleColor: isDark ? "#F8FAFC" : "#0F172A",
          bodyColor: isDark ? "#CBD5E1" : "#334155",
          borderColor: isDark ? "#334155" : "#E2E8F0",
          borderWidth: 1,
          padding: 10,
          callbacks: {
            label: context => ` Portfolio Value: ৳${Number(context.raw).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
          }
        }
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: {
            font: { size: 10 },
            color: isDark ? "#94A3B8" : "#64748B",
            maxTicksLimit: 7
          }
        },
        y: {
          grid: {
            color: isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(226, 232, 240, 0.7)",
            drawBorder: false
          },
          ticks: {
            font: { size: 10 },
            color: isDark ? "#94A3B8" : "#64748B",
            callback: value => "৳" + Number(value).toLocaleString()
          }
        }
      }
    }
  });
}

function renderAnalyticsSectorAllocation(sectors) {
  const badgeEl = document.getElementById("badgeSectorCount");
  if (badgeEl) badgeEl.textContent = `${sectors ? sectors.length : 0} Sectors`;

  const tableBody = document.getElementById("analyticsSectorTableBody");
  if (tableBody) {
    if (!sectors || sectors.length === 0) {
      tableBody.innerHTML = '<tr><td colspan="3" class="text-center text-muted py-3">No sector data</td></tr>';
    } else {
      tableBody.innerHTML = sectors.map(s => `
        <tr>
          <td>
            <div class="fw-semibold text-truncate" style="max-width: 140px;">${escapeHtml(s.sectorName)}</div>
            <div class="text-muted" style="font-size: 11px;">${s.holdingsCount} holding${s.holdingsCount === 1 ? '' : 's'}</div>
          </td>
          <td class="text-end fw-semibold">${formatBDT(s.marketValue)}</td>
          <td class="text-end">
            <span class="badge bg-primary-subtle text-primary border border-primary-subtle">${s.allocationPercentage.toFixed(1)}%</span>
          </td>
        </tr>
      `).join("");
    }
  }

  const canvas = document.getElementById("analyticsSectorChart");
  if (!canvas) return;

  if (analyticsSectorChartInstance) {
    analyticsSectorChartInstance.destroy();
    analyticsSectorChartInstance = null;
  }

  if (!sectors || sectors.length === 0) return;

  const palette = [
    "#2563EB", "#10B981", "#8B5CF6", "#F59E0B", "#06B6D4",
    "#EC4899", "#6366F1", "#14B8A6", "#F97316", "#64748B"
  ];

  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const ctx = canvas.getContext("2d");

  analyticsSectorChartInstance = new Chart(ctx, {
    type: "doughnut",
    data: {
      labels: sectors.map(s => s.sectorName),
      datasets: [
        {
          data: sectors.map(s => s.marketValue),
          backgroundColor: palette.slice(0, sectors.length),
          borderWidth: 2,
          borderColor: isDark ? "#1E293B" : "#FFFFFF"
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label: context => {
              const val = context.raw;
              const pct = sectors[context.dataIndex]?.allocationPercentage || 0;
              return ` ${context.label}: ৳${Number(val).toLocaleString()} (${pct.toFixed(1)}%)`;
            }
          }
        }
      },
      cutout: "68%"
    }
  });
}

function renderAnalyticsCompanyAllocation(companies) {
  const badgeEl = document.getElementById("badgeCompanyCount");
  if (badgeEl) badgeEl.textContent = `${companies ? companies.length : 0} Companies`;

  const tableBody = document.getElementById("analyticsCompanyTableBody");
  if (tableBody) {
    if (!companies || companies.length === 0) {
      tableBody.innerHTML = '<tr><td colspan="3" class="text-center text-muted py-3">No holdings</td></tr>';
    } else {
      tableBody.innerHTML = companies.map(c => `
        <tr>
          <td>
            <a href="company.html?id=${c.companyId}" class="fw-bold text-decoration-none" style="color: var(--color-accent);">
              ${escapeHtml(c.tickerSymbol)}
            </a>
            <div class="text-muted text-truncate" style="max-width: 140px; font-size: 11px;">
              ${escapeHtml(c.companyName)}
            </div>
          </td>
          <td class="text-end fw-semibold">${formatBDT(c.marketValue)}</td>
          <td class="text-end">
            <span class="badge bg-secondary-subtle text-secondary border">${c.allocationPercentage.toFixed(1)}%</span>
          </td>
        </tr>
      `).join("");
    }
  }

  const canvas = document.getElementById("analyticsCompanyChart");
  if (!canvas) return;

  if (analyticsCompanyChartInstance) {
    analyticsCompanyChartInstance.destroy();
    analyticsCompanyChartInstance = null;
  }

  if (!companies || companies.length === 0) return;

  const palette = [
    "#3B82F6", "#34D399", "#A78BFA", "#FBBF24", "#38BDF8",
    "#F472B6", "#818CF8", "#2DD4BF", "#FB923C", "#94A3B8"
  ];

  const isDark = document.documentElement.getAttribute("data-theme") === "dark";
  const ctx = canvas.getContext("2d");

  analyticsCompanyChartInstance = new Chart(ctx, {
    type: "doughnut",
    data: {
      labels: companies.map(c => c.tickerSymbol),
      datasets: [
        {
          data: companies.map(c => c.marketValue),
          backgroundColor: palette.slice(0, companies.length),
          borderWidth: 2,
          borderColor: isDark ? "#1E293B" : "#FFFFFF"
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label: context => {
              const val = context.raw;
              const pct = companies[context.dataIndex]?.allocationPercentage || 0;
              return ` ${context.label}: ৳${Number(val).toLocaleString()} (${pct.toFixed(1)}%)`;
            }
          }
        }
      },
      cutout: "68%"
    }
  });
}

function renderAnalyticsGainersAndLosers(gainers, losers) {
  const gainersBody = document.getElementById("analyticsTopGainersBody");
  if (gainersBody) {
    if (!gainers || gainers.length === 0) {
      gainersBody.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-3">No profitable positions currently</td></tr>';
    } else {
      gainersBody.innerHTML = gainers.map(item => `
        <tr>
          <td>
            <a href="company.html?id=${item.companyId}" class="fw-bold text-decoration-none" style="color: var(--color-accent);">
              ${escapeHtml(item.tickerSymbol)}
            </a>
            <div class="text-muted text-truncate" style="max-width: 140px; font-size: 11px;">
              ${escapeHtml(item.companyName)}
            </div>
          </td>
          <td class="text-end">${formatBDT(item.averageBuyPrice)}</td>
          <td class="text-end fw-semibold">${formatBDT(item.currentPrice)}</td>
          <td class="text-end text-success fw-semibold">+${formatBDT(item.unrealizedProfitLoss)}</td>
          <td class="text-end">
            <span class="badge bg-success-subtle text-success border border-success-subtle">
              +${item.returnPercentage.toFixed(2)}%
            </span>
          </td>
        </tr>
      `).join("");
    }
  }

  const losersBody = document.getElementById("analyticsTopLosersBody");
  if (losersBody) {
    if (!losers || losers.length === 0) {
      losersBody.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-3">No losing positions currently</td></tr>';
    } else {
      losersBody.innerHTML = losers.map(item => `
        <tr>
          <td>
            <a href="company.html?id=${item.companyId}" class="fw-bold text-decoration-none" style="color: var(--color-accent);">
              ${escapeHtml(item.tickerSymbol)}
            </a>
            <div class="text-muted text-truncate" style="max-width: 140px; font-size: 11px;">
              ${escapeHtml(item.companyName)}
            </div>
          </td>
          <td class="text-end">${formatBDT(item.averageBuyPrice)}</td>
          <td class="text-end fw-semibold">${formatBDT(item.currentPrice)}</td>
          <td class="text-end text-danger fw-semibold">-${formatBDT(Math.abs(item.unrealizedProfitLoss))}</td>
          <td class="text-end">
            <span class="badge bg-danger-subtle text-danger border border-danger-subtle">
              ${item.returnPercentage.toFixed(2)}%
            </span>
          </td>
        </tr>
      `).join("");
    }
  }
}

function renderAnalyticsFormulas(formulas) {
  const body = document.getElementById("analyticsFormulasTableBody");
  if (!body) return;

  const defaultDefinitions = [
    {
      metric: "Net Holdings Quantity",
      formula: "CurrentQuantity = &Sigma;(BUY Quantity) - &Sigma;(SELL Quantity)",
      source: "VW_PORTFOLIO_HOLDINGS / Oracle Views"
    },
    {
      metric: "Weighted Average Purchase Price",
      formula: "WeightedAvgBuyPrice = &Sigma;(BUY Quantity &times; Buy Price) / &Sigma;(BUY Quantity)",
      source: "Weighted-Average Cost Model (SACRED)"
    },
    {
      metric: "Invested Capital",
      formula: "InvestedCapital = &Sigma;(CurrentQuantity &times; WeightedAvgBuyPrice)",
      source: "Consolidated Portfolio Accounting"
    },
    {
      metric: "Current Market Value",
      formula: "MarketValue = &Sigma;(CurrentQuantity &times; Current DSE Market Price)",
      source: "DSE Real-Time Market Feed"
    },
    {
      metric: "Unrealized Profit / Loss",
      formula: "Unrealized P/L = Current Market Value - Invested Capital",
      source: "Mark-to-Market Accounting"
    },
    {
      metric: "Realized Profit / Loss",
      formula: "Realized P/L = &Sigma;((Sell Price - WeightedAvgBuyPrice) &times; Quantity Sold)",
      source: "Closed SELL Orders / Truncated Lots"
    },
    {
      metric: "Dividend Income & Yield",
      formula: "Dividend Yield = (Total Dividend Income / Current Portfolio Value) &times; 100",
      source: "Corporate Actions / Cash Dividends"
    },
    {
      metric: "Total Economic Return",
      formula: "Total Return = Unrealized P/L + Realized P/L + Dividend Income",
      source: "Consolidated Performance Measurement"
    },
    {
      metric: "Herfindahl-Hirschman Index (HHI)",
      formula: "HHI = &Sigma;(Asset Allocation Weight %)&sup2; (0 to 10,000 scale)",
      source: "DOJ/FTC Portfolio Concentration Standard"
    }
  ];

  body.innerHTML = defaultDefinitions.map(d => `
    <tr>
      <td class="fw-semibold">${d.metric}</td>
      <td><code>${d.formula}</code></td>
      <td class="text-muted">${d.source}</td>
    </tr>
  `).join("");
}





// =========================================
// PRICE & PORTFOLIO ALERTS (WEB FEATURE 4)
// =========================================

let alertsDataCache = [];
let companiesForAlerts = [];
let portfoliosForAlerts = [];
let currentAlertFilter = "ALL";
let alertPendingDeleteId = null;

async function setupAlertsPage() {
  const container = document.getElementById("alertsContainer");
  if (!container) return;

  const filterBtns = document.querySelectorAll("#alertFilterGroup button");
  const openModalBtn = document.getElementById("openCreateAlertModalBtn");
  const emptyStateCreateBtn = document.getElementById("emptyStateCreateBtn");
  const evaluateBtn = document.getElementById("evaluateAlertsBtn");
  const createForm = document.getElementById("createAlertForm");
  const alertTypeSelect = document.getElementById("alertTypeSelect");
  const companySelect = document.getElementById("alertCompanySelect");
  const portfolioSelect = document.getElementById("alertPortfolioSelect");
  const confirmDeleteBtn = document.getElementById("confirmDeleteAlertBtn");
  const alertsGrid = document.getElementById("alertsGrid");

  // 1. Filter button clicks
  filterBtns.forEach(btn => {
    btn.addEventListener("click", () => {
      filterBtns.forEach(b => {
        b.classList.remove("btn-primary", "active");
        b.classList.add("btn-outline-primary");
      });
      btn.classList.remove("btn-outline-primary");
      btn.classList.add("btn-primary", "active");
      currentAlertFilter = btn.dataset.filter || "ALL";
      renderAlertsList();
    });
  });

  // 2. Open Modal Handlers
  if (openModalBtn) {
    openModalBtn.addEventListener("click", openCreateAlertModal);
  }
  if (emptyStateCreateBtn) {
    emptyStateCreateBtn.addEventListener("click", openCreateAlertModal);
  }

  // 3. Alert Type Change
  if (alertTypeSelect) {
    alertTypeSelect.addEventListener("change", handleAlertTypeChange);
  }

  // 4. Target selector changes for dynamic price/value preview
  if (companySelect) {
    companySelect.addEventListener("change", () => {
      const selectedId = parseInt(companySelect.value, 10);
      const c = companiesForAlerts.find(x => x.companyId === selectedId);
      const preview = document.getElementById("selectedCompanyPricePreview");
      if (preview) {
        preview.textContent = c ? formatBDT(c.currentPrice) : "৳0.00";
      }
    });
  }

  if (portfolioSelect) {
    portfolioSelect.addEventListener("change", () => {
      const selectedId = parseInt(portfolioSelect.value, 10);
      const p = portfoliosForAlerts.find(x => x.portfolioId === selectedId);
      const preview = document.getElementById("selectedPortfolioValPreview");
      if (preview) {
        preview.textContent = p ? formatBDT(p.totalMarketValue || p.totalValue || 0) : "৳0.00";
      }
    });
  }

  // 5. Create alert form submission
  if (createForm) {
    createForm.addEventListener("submit", handleCreateAlertSubmit);
  }

  // 6. Evaluate alerts button
  if (evaluateBtn) {
    evaluateBtn.addEventListener("click", handleEvaluateAlertsClick);
  }

  // 7. Confirm Delete Button
  if (confirmDeleteBtn) {
    confirmDeleteBtn.addEventListener("click", handleConfirmDeleteAlert);
  }

  // 8. Grid Event Delegation for Toggle and Delete
  if (alertsGrid) {
    alertsGrid.addEventListener("click", async (e) => {
      const toggleBtn = e.target.closest("[data-action='toggle']");
      if (toggleBtn) {
        const id = parseInt(toggleBtn.dataset.id, 10);
        await handleToggleAlert(id, toggleBtn);
        return;
      }

      const deleteBtn = e.target.closest("[data-action='delete']");
      if (deleteBtn) {
        const id = parseInt(deleteBtn.dataset.id, 10);
        promptDeleteAlert(id);
        return;
      }
    });
  }

  // 9. Load dependencies and initial alerts
  await Promise.all([
    loadCompaniesForAlerts(),
    loadPortfoliosForAlerts(),
    fetchUserAlerts()
  ]);
}

async function loadCompaniesForAlerts() {
  const companySelect = document.getElementById("alertCompanySelect");
  if (!companySelect) return;

  try {
    const res = await apiRequest("/companies");
    companiesForAlerts = (res && res.data) ? res.data : (Array.isArray(res) ? res : []);
    companiesForAlerts.sort((a, b) => (a.tickerSymbol || "").localeCompare(b.tickerSymbol || ""));

    companySelect.innerHTML = `<option value="" disabled selected>Choose a company...</option>` +
      companiesForAlerts.map(c => `
        <option value="${c.companyId}">
          ${escapeHtml(c.tickerSymbol)} — ${escapeHtml(c.companyName)} (${formatBDT(c.currentPrice)})
        </option>
      `).join("");
  } catch (err) {
    console.error("Failed to load companies for alerts:", err);
  }
}

async function loadPortfoliosForAlerts() {
  const portfolioSelect = document.getElementById("alertPortfolioSelect");
  if (!portfolioSelect) return;

  try {
    const res = await apiRequest("/portfolios");
    portfoliosForAlerts = (res && res.data) ? res.data : (Array.isArray(res) ? res : []);

    portfolioSelect.innerHTML = `<option value="" disabled selected>Choose a portfolio...</option>` +
      portfoliosForAlerts.map(p => `
        <option value="${p.portfolioId}">
          ${escapeHtml(p.portfolioName)} (${formatBDT(p.totalMarketValue || p.totalValue || 0)})
        </option>
      `).join("");
  } catch (err) {
    console.error("Failed to load portfolios for alerts:", err);
  }
}

async function fetchUserAlerts() {
  const loadingEl = document.getElementById("alertsLoadingState");
  const emptyEl = document.getElementById("alertsEmptyState");
  const gridEl = document.getElementById("alertsGrid");

  if (loadingEl) loadingEl.classList.remove("d-none");
  if (emptyEl) emptyEl.classList.add("d-none");
  if (gridEl) gridEl.classList.add("d-none");

  try {
    const res = await apiRequest("/alerts");
    alertsDataCache = (res && res.data) ? res.data : (Array.isArray(res) ? res : []);
    updateAlertKpis();
    renderAlertsList();
  } catch (err) {
    console.error("Failed to fetch alerts:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  } finally {
    if (loadingEl) loadingEl.classList.add("d-none");
  }
}

function updateAlertKpis() {
  const totalEl = document.getElementById("kpiTotalAlerts");
  const activeEl = document.getElementById("kpiActiveAlerts");
  const trigEl = document.getElementById("kpiTriggeredAlerts");
  const disEl = document.getElementById("kpiDisabledAlerts");

  const total = alertsDataCache.length;
  const active = alertsDataCache.filter(a => a.status === "ACTIVE").length;
  const triggered = alertsDataCache.filter(a => a.status === "TRIGGERED").length;
  const disabled = alertsDataCache.filter(a => a.status === "DISABLED").length;

  if (totalEl) totalEl.textContent = total;
  if (activeEl) activeEl.textContent = active;
  if (trigEl) trigEl.textContent = triggered;
  if (disEl) disEl.textContent = disabled;
}

function renderAlertsList() {
  const emptyEl = document.getElementById("alertsEmptyState");
  const gridEl = document.getElementById("alertsGrid");
  if (!gridEl) return;

  let filtered = alertsDataCache;
  if (currentAlertFilter === "ACTIVE") {
    filtered = alertsDataCache.filter(a => a.status === "ACTIVE");
  } else if (currentAlertFilter === "TRIGGERED") {
    filtered = alertsDataCache.filter(a => a.status === "TRIGGERED");
  } else if (currentAlertFilter === "DISABLED") {
    filtered = alertsDataCache.filter(a => a.status === "DISABLED");
  }

  if (filtered.length === 0) {
    gridEl.classList.add("d-none");
    if (emptyEl) emptyEl.classList.remove("d-none");
    return;
  }

  if (emptyEl) emptyEl.classList.add("d-none");
  gridEl.classList.remove("d-none");

  gridEl.innerHTML = filtered.map(a => renderAlertCard(a)).join("");
}

function renderAlertCard(a) {
  let typeBadge = "";
  let targetHeader = "";
  let comparisonText = "";

  if (a.alertType === "PRICE_ABOVE") {
    typeBadge = `<span class="badge bg-success-subtle text-success border border-success-subtle px-2 py-1"><i class="bi bi-arrow-up-right me-1"></i>Price Above</span>`;
    targetHeader = `
      <div class="d-flex align-items-center gap-2">
        <a href="company.html?id=${a.companyId}" class="fw-bold fs-5 text-decoration-none text-body hover-primary">
          ${escapeHtml(a.tickerSymbol || "Company")}
        </a>
        <span class="text-muted small">· ${escapeHtml(a.companyName || "")}</span>
      </div>`;
    comparisonText = `
      <div class="d-flex justify-content-between text-muted small mt-2">
        <span>Current Price: <strong class="text-body">${formatBDT(a.currentPrice)}</strong></span>
        <span>Target Threshold: <strong class="text-success">${formatBDT(a.thresholdValue)}</strong></span>
      </div>`;
  } else if (a.alertType === "PRICE_BELOW") {
    typeBadge = `<span class="badge bg-danger-subtle text-danger border border-danger-subtle px-2 py-1"><i class="bi bi-arrow-down-right me-1"></i>Price Below</span>`;
    targetHeader = `
      <div class="d-flex align-items-center gap-2">
        <a href="company.html?id=${a.companyId}" class="fw-bold fs-5 text-decoration-none text-body hover-primary">
          ${escapeHtml(a.tickerSymbol || "Company")}
        </a>
        <span class="text-muted small">· ${escapeHtml(a.companyName || "")}</span>
      </div>`;
    comparisonText = `
      <div class="d-flex justify-content-between text-muted small mt-2">
        <span>Current Price: <strong class="text-body">${formatBDT(a.currentPrice)}</strong></span>
        <span>Target Threshold: <strong class="text-danger">${formatBDT(a.thresholdValue)}</strong></span>
      </div>`;
  } else if (a.alertType === "PORTFOLIO_VALUE_ABOVE") {
    typeBadge = `<span class="badge bg-primary-subtle text-primary border border-primary-subtle px-2 py-1"><i class="bi bi-graph-up-arrow me-1"></i>Portfolio Above</span>`;
    targetHeader = `
      <div class="d-flex align-items-center gap-2">
        <a href="portfolio.html" class="fw-bold fs-5 text-decoration-none text-body hover-primary">
          ${escapeHtml(a.portfolioName || "Portfolio")}
        </a>
        <span class="text-muted small">· Portfolio Valuation</span>
      </div>`;
    comparisonText = `
      <div class="d-flex justify-content-between text-muted small mt-2">
        <span>Current Valuation: <strong class="text-body">${formatBDT(a.currentPortfolioValue || 0)}</strong></span>
        <span>Target Threshold: <strong class="text-primary">${formatBDT(a.thresholdValue)}</strong></span>
      </div>`;
  } else {
    // PORTFOLIO_VALUE_BELOW
    typeBadge = `<span class="badge bg-warning-subtle text-warning border border-warning-subtle px-2 py-1"><i class="bi bi-graph-down-arrow me-1"></i>Portfolio Below</span>`;
    targetHeader = `
      <div class="d-flex align-items-center gap-2">
        <a href="portfolio.html" class="fw-bold fs-5 text-decoration-none text-body hover-primary">
          ${escapeHtml(a.portfolioName || "Portfolio")}
        </a>
        <span class="text-muted small">· Portfolio Valuation</span>
      </div>`;
    comparisonText = `
      <div class="d-flex justify-content-between text-muted small mt-2">
        <span>Current Valuation: <strong class="text-body">${formatBDT(a.currentPortfolioValue || 0)}</strong></span>
        <span>Target Threshold: <strong class="text-warning">${formatBDT(a.thresholdValue)}</strong></span>
      </div>`;
  }

  let statusBadge = "";
  let toggleBtn = "";

  if (a.status === "ACTIVE") {
    statusBadge = `<span class="badge bg-success-subtle text-success border border-success-subtle"><i class="bi bi-broadcast me-1"></i>Active</span>`;
    toggleBtn = `<button class="btn btn-sm btn-outline-secondary d-flex align-items-center gap-1" data-action="toggle" data-id="${a.alertId}" title="Pause alert monitoring"><i class="bi bi-pause-fill"></i> Pause</button>`;
  } else if (a.status === "TRIGGERED") {
    statusBadge = `<span class="badge bg-warning-subtle text-warning border border-warning-subtle"><i class="bi bi-bell-fill me-1"></i>Triggered</span>`;
    toggleBtn = `<button class="btn btn-sm btn-outline-primary d-flex align-items-center gap-1" data-action="toggle" data-id="${a.alertId}" title="Reactivate monitoring"><i class="bi bi-arrow-repeat"></i> Reactivate</button>`;
  } else {
    statusBadge = `<span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle"><i class="bi bi-pause-circle me-1"></i>Disabled</span>`;
    toggleBtn = `<button class="btn btn-sm btn-outline-success d-flex align-items-center gap-1" data-action="toggle" data-id="${a.alertId}" title="Enable alert monitoring"><i class="bi bi-play-fill"></i> Enable</button>`;
  }

  let triggeredBanner = "";
  if (a.triggeredAt) {
    const d = new Date(a.triggeredAt);
    const dateFormatted = d.toLocaleDateString("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
    triggeredBanner = `
      <div class="alert alert-warning py-2 px-3 small mt-3 mb-1 d-flex align-items-start gap-2">
        <i class="bi bi-exclamation-triangle-fill text-warning flex-shrink-0 mt-1"></i>
        <div>
          <div class="fw-semibold">${escapeHtml(a.message || "Threshold condition met.")}</div>
          <div class="text-muted" style="font-size: 11px;">Triggered on ${dateFormatted} · Paused until reactivated</div>
        </div>
      </div>`;
  }

  return `
    <div class="col-md-6 col-xl-4" id="alertCard_${a.alertId}">
      <div class="dashboard-card h-100 alert-item-card ${a.status === 'TRIGGERED' ? 'border-start border-warning border-3' : ''}">
        <div class="d-flex flex-column justify-content-between h-100">
          <div>
            <div class="d-flex align-items-center justify-content-between mb-2">
              ${typeBadge}
              ${statusBadge}
            </div>
            ${targetHeader}
            ${comparisonText}
            ${triggeredBanner}
          </div>
          <div class="d-flex align-items-center justify-content-between pt-3 mt-2 border-top">
            <span class="text-muted small" style="font-size: 11px;">
              Created ${new Date(a.createdAt).toLocaleDateString("en-US", { month: "short", day: "numeric" })}
            </span>
            <div class="d-flex align-items-center gap-2">
              ${toggleBtn}
              <button class="btn btn-sm btn-outline-danger" data-action="delete" data-id="${a.alertId}" title="Delete alert">
                <i class="bi bi-trash3"></i>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `;
}

function handleAlertTypeChange() {
  const alertTypeSelect = document.getElementById("alertTypeSelect");
  const companyGroup = document.getElementById("companySelectGroup");
  const portfolioGroup = document.getElementById("portfolioSelectGroup");
  const alertTypeHint = document.getElementById("alertTypeHint");
  const companySelect = document.getElementById("alertCompanySelect");
  const portfolioSelect = document.getElementById("alertPortfolioSelect");

  if (!alertTypeSelect) return;

  const type = alertTypeSelect.value;
  if (type === "PRICE_ABOVE") {
    if (companyGroup) companyGroup.classList.remove("d-none");
    if (portfolioGroup) portfolioGroup.classList.add("d-none");
    if (companySelect) companySelect.required = true;
    if (portfolioSelect) portfolioSelect.required = false;
    if (alertTypeHint) alertTypeHint.textContent = "Notifies you when the company stock price reaches or exceeds the threshold.";
  } else if (type === "PRICE_BELOW") {
    if (companyGroup) companyGroup.classList.remove("d-none");
    if (portfolioGroup) portfolioGroup.classList.add("d-none");
    if (companySelect) companySelect.required = true;
    if (portfolioSelect) portfolioSelect.required = false;
    if (alertTypeHint) alertTypeHint.textContent = "Notifies you when the company stock price falls to or below the threshold.";
  } else if (type === "PORTFOLIO_VALUE_ABOVE") {
    if (companyGroup) companyGroup.classList.add("d-none");
    if (portfolioGroup) portfolioGroup.classList.remove("d-none");
    if (companySelect) companySelect.required = false;
    if (portfolioSelect) portfolioSelect.required = true;
    if (alertTypeHint) alertTypeHint.textContent = "Notifies you when your total portfolio market valuation reaches or exceeds the threshold.";
  } else if (type === "PORTFOLIO_VALUE_BELOW") {
    if (companyGroup) companyGroup.classList.add("d-none");
    if (portfolioGroup) portfolioGroup.classList.remove("d-none");
    if (companySelect) companySelect.required = false;
    if (portfolioSelect) portfolioSelect.required = true;
    if (alertTypeHint) alertTypeHint.textContent = "Notifies you when your total portfolio market valuation falls to or below the threshold.";
  }
}

function openCreateAlertModal() {
  const modalEl = document.getElementById("createAlertModal");
  if (!modalEl) return;

  const form = document.getElementById("createAlertForm");
  if (form) form.reset();

  handleAlertTypeChange();

  const compPreview = document.getElementById("selectedCompanyPricePreview");
  if (compPreview) compPreview.textContent = "৳0.00";
  const portPreview = document.getElementById("selectedPortfolioValPreview");
  if (portPreview) portPreview.textContent = "৳0.00";

  const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
  modal.show();
}

async function handleCreateAlertSubmit(e) {
  e.preventDefault();

  const type = document.getElementById("alertTypeSelect")?.value;
  const companySelect = document.getElementById("alertCompanySelect");
  const portfolioSelect = document.getElementById("alertPortfolioSelect");
  const thresholdInput = document.getElementById("alertThresholdInput");
  const submitBtn = document.getElementById("submitCreateAlertBtn");

  const threshold = parseFloat(thresholdInput?.value || "0");
  if (isNaN(threshold) || threshold <= 0) {
    showToast("Please enter a valid positive threshold amount.", "danger");
    return;
  }

  const isPriceAlert = type === "PRICE_ABOVE" || type === "PRICE_BELOW";
  const companyId = isPriceAlert ? parseInt(companySelect?.value || "0", 10) : null;
  const portfolioId = !isPriceAlert ? parseInt(portfolioSelect?.value || "0", 10) : null;

  if (isPriceAlert && (!companyId || isNaN(companyId))) {
    showToast("Please select a target company for the price alert.", "danger");
    return;
  }

  if (!isPriceAlert && (!portfolioId || isNaN(portfolioId))) {
    showToast("Please select a target portfolio for the portfolio alert.", "danger");
    return;
  }

  const payload = {
    alertType: type,
    thresholdValue: threshold,
    companyId: companyId,
    portfolioId: portfolioId
  };

  const originalHtml = submitBtn ? submitBtn.innerHTML : "";
  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span>Saving...`;
  }

  try {
    const res = await apiRequest("/alerts", {
      method: "POST",
      body: JSON.stringify(payload)
    });

    const modalEl = document.getElementById("createAlertModal");
    if (modalEl) {
      const modal = bootstrap.Modal.getInstance(modalEl);
      if (modal) modal.hide();
    }

    showToast(res?.message || "Alert created successfully!", "success");
    await fetchUserAlerts();
    if (typeof window.refreshNotifications === 'function') window.refreshNotifications();
  } catch (err) {
    console.error("Failed to create alert:", err);
    if (err.status === 409) {
      showToast("An active alert with these exact parameters already exists.", "warning");
    } else {
      showToast(sanitizeErrorMessage(err.message, err.status), "danger");
    }
  } finally {
    if (submitBtn) {
      submitBtn.disabled = false;
      submitBtn.innerHTML = originalHtml;
    }
  }
}

async function handleToggleAlert(alertId, btnEl) {
  if (btnEl) btnEl.disabled = true;

  try {
    const res = await apiRequest(`/alerts/${alertId}/toggle`, {
      method: "PATCH"
    });

    const updated = res.data;
    if (updated) {
      const idx = alertsDataCache.findIndex(a => a.alertId === alertId);
      if (idx !== -1) {
        alertsDataCache[idx] = updated;
      }
      updateAlertKpis();
      renderAlertsList();
      showToast(`Alert is now ${updated.status.toLowerCase()}.`, "success");
    } else {
      await fetchUserAlerts();
    }
  } catch (err) {
    console.error("Failed to toggle alert:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  } finally {
    if (btnEl) btnEl.disabled = false;
  }
}

function promptDeleteAlert(alertId) {
  alertPendingDeleteId = alertId;
  const modalEl = document.getElementById("deleteAlertModal");
  if (modalEl) {
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();
  }
}

async function handleConfirmDeleteAlert() {
  if (!alertPendingDeleteId) return;

  const btn = document.getElementById("confirmDeleteAlertBtn");
  if (btn) btn.disabled = true;

  try {
    await apiRequest(`/alerts/${alertPendingDeleteId}`, {
      method: "DELETE"
    });

    const modalEl = document.getElementById("deleteAlertModal");
    if (modalEl) {
      const modal = bootstrap.Modal.getInstance(modalEl);
      if (modal) modal.hide();
    }

    showToast("Alert removed successfully.", "success");
    alertsDataCache = alertsDataCache.filter(a => a.alertId !== alertPendingDeleteId);
    alertPendingDeleteId = null;
    updateAlertKpis();
    renderAlertsList();
  } catch (err) {
    console.error("Failed to delete alert:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  } finally {
    if (btn) btn.disabled = false;
  }
}

async function handleEvaluateAlertsClick() {
  const btn = document.getElementById("evaluateAlertsBtn");
  const originalHtml = btn ? btn.innerHTML : "";
  if (btn) {
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span>Evaluating...`;
  }

  try {
    const res = await apiRequest("/alerts/evaluate", {
      method: "POST"
    });

    const triggeredCount = res?.data?.triggeredCount || 0;
    if (triggeredCount > 0) {
      showToast(`Evaluation complete: ${triggeredCount} alert(s) triggered!`, "warning");
    } else {
      showToast("Evaluation complete: No alert thresholds crossed.", "info");
    }

    await fetchUserAlerts();
  } catch (err) {
    console.error("Failed to evaluate alerts:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  } finally {
    if (btn) {
      btn.disabled = false;
      btn.innerHTML = originalHtml;
    }
  }
}


// =========================================
// ADMIN PANEL MANAGEMENT
// =========================================

let adminOverviewData = null;
let adminCompaniesCache = [];
let adminSectorsCache = [];
let adminUsersCache = [];
let adminAuditLogsCache = [];
let pendingAdminDeleteAction = null;

window.switchAdminTab = function(tabName) {
  const navItems = document.querySelectorAll(".admin-nav-item");
  navItems.forEach(item => {
    if (item.getAttribute("data-tab") === tabName) {
      item.classList.add("active");
    } else {
      item.classList.remove("active");
    }
  });

  const tabPanes = document.querySelectorAll(".admin-tab-pane");
  tabPanes.forEach(pane => {
    if (pane.id === `tab-${tabName}`) {
      pane.classList.add("active");
    } else {
      pane.classList.remove("active");
    }
  });

  // Trigger lazy loading
  if (tabName === "overview") fetchAdminOverview();
  else if (tabName === "diagnostics") fetchAdminDiagnostics();
  else if (tabName === "companies") fetchAdminCompanies();
  else if (tabName === "sectors") fetchAdminSectors();
  else if (tabName === "users") fetchAdminUsers();
  else if (tabName === "sync") fetchAdminOverview();
  else if (tabName === "audit") fetchAdminAuditLogs();
};

async function setupAdminPage() {
  const page = document.getElementById("adminPage");
  if (!page) return;

  // Strict role check on frontend to complement backend authorization
  const currentUser = getCurrentUser();
  if (!currentUser) {
    window.location.href = "login.html";
    return;
  }

  let userRole = (currentUser.role || "").toUpperCase();
  if (userRole !== "ADMIN") {
    try {
      const meRes = await apiRequest("/auth/me");
      if (meRes?.data?.role?.toUpperCase() === "ADMIN") {
        currentUser.role = "ADMIN";
        localStorage.setItem("sharesync_user", JSON.stringify({ ...currentUser, ...meRes.data }));
      } else {
        showToast("Access Denied: Administrative privileges required.", "danger");
        setTimeout(() => { window.location.href = "index.html"; }, 800);
        return;
      }
    } catch {
      showToast("Access Denied: Administrative privileges required.", "danger");
      setTimeout(() => { window.location.href = "index.html"; }, 800);
      return;
    }
  }

  // Setup tab click listeners
  document.querySelectorAll(".admin-nav-item").forEach(item => {
    item.addEventListener("click", () => {
      const tab = item.getAttribute("data-tab");
      if (tab) switchAdminTab(tab);
    });
  });

  // Overview refresh & quick sync
  document.getElementById("adminRefreshOverviewBtn")?.addEventListener("click", fetchAdminOverview);
  document.getElementById("refreshDiagnosticsBtn")?.addEventListener("click", fetchAdminDiagnostics);
  document.getElementById("quickDseSyncBtn")?.addEventListener("click", triggerAdminDseSync);

  // Companies filters & actions
  document.getElementById("companySearchInput")?.addEventListener("input", filterAndRenderCompanies);
  document.getElementById("companySectorFilter")?.addEventListener("change", filterAndRenderCompanies);
  document.getElementById("companyStatusFilter")?.addEventListener("change", filterAndRenderCompanies);
  document.getElementById("openAddCompanyModalBtn")?.addEventListener("click", openAddCompanyModal);
  document.getElementById("companyForm")?.addEventListener("submit", handleCompanyFormSubmit);

  // Sectors actions
  document.getElementById("openAddSectorModalBtn")?.addEventListener("click", openAddSectorModal);
  document.getElementById("sectorForm")?.addEventListener("submit", handleSectorFormSubmit);

  // Users filter
  document.getElementById("userSearchInput")?.addEventListener("input", filterAndRenderUsers);

  // DSE Sync
  document.getElementById("triggerDseSyncBtn")?.addEventListener("click", triggerAdminDseSync);

  // Audit Logs
  document.getElementById("refreshAuditLogsBtn")?.addEventListener("click", fetchAdminAuditLogs);

  // Delete modal confirmation
  document.getElementById("confirmDeleteActionBtn")?.addEventListener("click", async () => {
    if (typeof pendingAdminDeleteAction === "function") {
      await pendingAdminDeleteAction();
    }
  });

  // Initial load
  await fetchAdminOverview();
  await fetchAdminDiagnostics();
  await fetchAdminSectors(false);
}

// -----------------------------------------
// SYSTEM DIAGNOSTICS (FEATURE 10)
// -----------------------------------------
async function fetchAdminDiagnostics() {
  try {
    const res = await apiRequest("/admin/diagnostics");
    if (res?.data) {
      renderAdminDiagnostics(res.data);
    }
  } catch (err) {
    console.error("Failed to load admin diagnostics:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

function renderAdminDiagnostics(diag) {
  const setEl = (id, val) => {
    const el = document.getElementById(id);
    if (el) el.textContent = val;
  };

  const dbBadge = document.getElementById("diagDbStatus");
  if (dbBadge) {
    const isConn = diag.isDatabaseConnected || diag.databaseStatus === "Connected";
    dbBadge.className = `badge ${isConn ? "bg-success-subtle text-success border-success-subtle" : "bg-danger-subtle text-danger border-danger-subtle"} border px-2 py-1`;
    dbBadge.innerHTML = `<i class="bi bi-circle-fill me-1" style="font-size: 8px;"></i> ${escapeHtml(diag.databaseStatus || (isConn ? "Connected" : "Disconnected"))}`;
  }

  const appBadge = document.getElementById("diagAppStatus");
  if (appBadge) {
    appBadge.className = "badge bg-success-subtle text-success border border-success-subtle px-2 py-1";
    appBadge.innerHTML = `<i class="bi bi-circle-fill me-1" style="font-size: 8px;"></i> ${escapeHtml(diag.applicationStatus || "Healthy")}`;
  }

  const dseBadge = document.getElementById("diagDseStatus");
  if (dseBadge) {
    dseBadge.className = "badge bg-primary-subtle text-primary border border-primary-subtle px-2 py-1";
    dseBadge.innerHTML = `<i class="bi bi-circle-fill me-1" style="font-size: 8px;"></i> ${escapeHtml(diag.dseSyncStatus || "Running")}`;
  }

  setEl("diagUsersCount", (diag.usersCount ?? 0).toLocaleString());
  setEl("diagCompaniesCount", (diag.companiesCount ?? 0).toLocaleString());
  setEl("diagPortfoliosCount", (diag.portfoliosCount ?? 0).toLocaleString());
  setEl("diagTransactionsCount", (diag.transactionsCount ?? 0).toLocaleString());
  setEl("diagAuditRecordsCount", (diag.auditRecordsCount ?? 0).toLocaleString());

  const syncTimeStr = diag.lastSyncTime ? new Date(diag.lastSyncTime).toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit", hour12: true }) : "Never";
  setEl("diagLastSyncTime", syncTimeStr);

  const syncStatusBadge = document.getElementById("diagSyncStatus");
  if (syncStatusBadge) {
    syncStatusBadge.textContent = diag.syncStatus || "Successful";
  }
}

// -----------------------------------------
// OVERVIEW TAB
// -----------------------------------------
async function fetchAdminOverview() {
  try {
    const res = await apiRequest("/admin/overview");
    if (res?.data) {
      adminOverviewData = res.data;
      renderAdminOverview(res.data);
    }
  } catch (err) {
    console.error("Failed to load admin overview:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

function renderAdminOverview(data) {
  const setEl = (id, val) => {
    const el = document.getElementById(id);
    if (el) el.textContent = val;
  };

  setEl("kpiTotalUsers", (data.totalUsers || 0).toLocaleString());
  setEl("kpiActiveUsers", `${(data.activeUsers || 0).toLocaleString()} active accounts`);
  setEl("kpiTotalCompanies", (data.totalCompanies || 0).toLocaleString());
  setEl("kpiActiveCompanies", `${(data.activeCompanies || 0).toLocaleString()} active trading tickers`);
  setEl("kpiTotalSectors", (data.totalSectors || 0).toLocaleString());
  setEl("kpiTotalTransactions", (data.totalTransactions || 0).toLocaleString());
  setEl("kpiTotalPortfolios", `${(data.totalPortfolios || 0).toLocaleString()} active portfolios`);

  const lastSyncStr = data.lastSyncTime ? new Date(data.lastSyncTime).toLocaleString("en-US", { dateStyle: "medium", timeStyle: "medium" }) : "Never";
  setEl("overviewLastSync", lastSyncStr);
  setEl("syncConsoleLastDate", lastSyncStr);
}

// -----------------------------------------
// COMPANIES TAB
// -----------------------------------------
async function fetchAdminCompanies() {
  const tbody = document.getElementById("companiesTableBody");
  if (tbody) {
    tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4 text-muted"><span class="spinner-border spinner-border-sm me-2"></span>Loading companies...</td></tr>`;
  }

  try {
    // Ensure sectors are fetched first for dropdowns
    if (adminSectorsCache.length === 0) {
      await fetchAdminSectors(false);
    }

    const res = await apiRequest("/admin/companies");
    adminCompaniesCache = res?.data || [];
    populateCompanySectorFilter();
    filterAndRenderCompanies();
  } catch (err) {
    console.error("Failed to fetch companies:", err);
    if (tbody) {
      tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4 text-danger">Failed to load companies: ${escapeHtml(err.message)}</td></tr>`;
    }
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

function populateCompanySectorFilter() {
  const filterSelect = document.getElementById("companySectorFilter");
  const modalSelect = document.getElementById("companySectorInput");

  if (filterSelect) {
    const currentVal = filterSelect.value;
    filterSelect.innerHTML = `<option value="">All Sectors</option>` +
      adminSectorsCache.map(s => `<option value="${s.sectorId}">${escapeHtml(s.sectorName)}</option>`).join("");
    filterSelect.value = currentVal;
  }

  if (modalSelect) {
    modalSelect.innerHTML = `<option value="">-- Select Sector --</option>` +
      adminSectorsCache.map(s => `<option value="${s.sectorId}">${escapeHtml(s.sectorName)}</option>`).join("");
  }
}

function filterAndRenderCompanies() {
  const search = (document.getElementById("companySearchInput")?.value || "").toLowerCase().trim();
  const sectorFilter = document.getElementById("companySectorFilter")?.value;
  const statusFilter = document.getElementById("companyStatusFilter")?.value;

  const filtered = adminCompaniesCache.filter(c => {
    const matchSearch = !search ||
      (c.tickerSymbol || "").toLowerCase().includes(search) ||
      (c.companyName || "").toLowerCase().includes(search);

    const matchSector = !sectorFilter || String(c.sectorId) === String(sectorFilter);
    const matchStatus = !statusFilter ||
      (statusFilter === "active" && c.isActive) ||
      (statusFilter === "inactive" && !c.isActive);

    return matchSearch && matchSector && matchStatus;
  });

  renderCompaniesTable(filtered);
}

function renderCompaniesTable(companies) {
  const tbody = document.getElementById("companiesTableBody");
  if (!tbody) return;

  if (companies.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4 text-muted">No companies found matching criteria.</td></tr>`;
    return;
  }

  tbody.innerHTML = companies.map(c => {
    const change = Number(c.dayChange || 0);
    const changeClass = change > 0 ? "text-success" : (change < 0 ? "text-danger" : "text-muted");
    const changeSign = change > 0 ? "+" : "";
    const statusBadge = c.isActive
      ? `<span class="badge status-badge-active px-2 py-1"><i class="bi bi-check-circle me-1"></i>Active</span>`
      : `<span class="badge status-badge-inactive px-2 py-1"><i class="bi bi-x-circle me-1"></i>Inactive</span>`;

    const toggleActionText = c.isActive ? "Deactivate" : "Activate";
    const toggleIcon = c.isActive ? "bi-pause-circle" : "bi-play-circle";
    const toggleBtnClass = c.isActive ? "btn-outline-warning" : "btn-outline-success";

    return `
      <tr>
        <td class="ps-4">
          <span class="badge bg-secondary-subtle text-dark border fw-bold">${escapeHtml(c.tickerSymbol)}</span>
        </td>
        <td>
          <div class="fw-semibold text-truncate" style="max-width: 220px;" title="${escapeHtml(c.companyName)}">${escapeHtml(c.companyName)}</div>
        </td>
        <td>
          <span class="text-muted fs-sm">${escapeHtml(c.sectorName || "Unassigned")}</span>
        </td>
        <td class="text-end fw-semibold">
          BDT ${Number(c.currentPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </td>
        <td class="text-end ${changeClass} fs-sm fw-medium">
          ${changeSign}${change.toFixed(2)} (${c.dayChangePercent ? Number(c.dayChangePercent).toFixed(2) : "0.00"}%)
        </td>
        <td class="text-center">
          ${statusBadge}
        </td>
        <td class="text-end pe-4">
          <div class="btn-group btn-group-sm">
            <button class="btn btn-outline-secondary" onclick="openEditCompanyModal(${c.companyId})" title="Edit Reference Data">
              <i class="bi bi-pencil"></i>
            </button>
            <button class="btn ${toggleBtnClass}" onclick="toggleCompanyStatus(${c.companyId}, ${!c.isActive})" title="${toggleActionText}">
              <i class="bi ${toggleIcon}"></i>
            </button>
            <button class="btn btn-outline-danger" onclick="confirmDeleteCompany(${c.companyId}, '${escapeHtml(c.tickerSymbol)}')" title="Delete Company">
              <i class="bi bi-trash"></i>
            </button>
          </div>
        </td>
      </tr>
    `;
  }).join("");
}

function openAddCompanyModal() {
  document.getElementById("companyModalLabel").textContent = "Add New Company";
  document.getElementById("companyIdInput").value = "";
  document.getElementById("companyTickerInput").value = "";
  document.getElementById("companyTickerInput").disabled = false;
  document.getElementById("companyNameInput").value = "";
  document.getElementById("companySectorInput").value = "";
  document.getElementById("companyPriceInput").value = "";
  document.getElementById("companyDayChangeInput").value = "0.00";
  document.getElementById("companyHighInput").value = "";
  document.getElementById("companyLowInput").value = "";
  document.getElementById("companyIsActiveInput").checked = true;

  populateCompanySectorFilter();
  const modalEl = document.getElementById("companyModal");
  if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function openEditCompanyModal(companyId) {
  const company = adminCompaniesCache.find(c => c.companyId === companyId);
  if (!company) return;

  document.getElementById("companyModalLabel").textContent = `Edit Company (${company.tickerSymbol})`;
  document.getElementById("companyIdInput").value = company.companyId;
  document.getElementById("companyTickerInput").value = company.tickerSymbol;
  document.getElementById("companyTickerInput").disabled = true; // Protect ticker immutability
  document.getElementById("companyNameInput").value = company.companyName;
  document.getElementById("companySectorInput").value = company.sectorId || "";
  document.getElementById("companyPriceInput").value = company.currentPrice || 0;
  document.getElementById("companyDayChangeInput").value = company.dayChange || 0;
  document.getElementById("companyHighInput").value = company.dayHigh || company.currentPrice || 0;
  document.getElementById("companyLowInput").value = company.dayLow || company.currentPrice || 0;
  document.getElementById("companyIsActiveInput").checked = company.isActive;

  populateCompanySectorFilter();
  document.getElementById("companySectorInput").value = company.sectorId || "";

  const modalEl = document.getElementById("companyModal");
  if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

async function handleCompanyFormSubmit(e) {
  e.preventDefault();
  const submitBtn = document.getElementById("saveCompanySubmitBtn");
  if (submitBtn) submitBtn.disabled = true;

  const companyId = document.getElementById("companyIdInput").value;
  const isEdit = !!companyId;

  const payload = {
    companyName: document.getElementById("companyNameInput").value.trim(),
    sectorId: document.getElementById("companySectorInput").value ? Number(document.getElementById("companySectorInput").value) : null,
    currentPrice: parseFloat(document.getElementById("companyPriceInput").value) || 0,
    dayChange: parseFloat(document.getElementById("companyDayChangeInput").value) || 0,
    dayHigh: parseFloat(document.getElementById("companyHighInput").value) || parseFloat(document.getElementById("companyPriceInput").value) || 0,
    dayLow: parseFloat(document.getElementById("companyLowInput").value) || parseFloat(document.getElementById("companyPriceInput").value) || 0,
    isActive: document.getElementById("companyIsActiveInput").checked
  };

  if (!isEdit) {
    payload.tickerSymbol = document.getElementById("companyTickerInput").value.trim().toUpperCase();
  }

  try {
    if (isEdit) {
      await apiRequest(`/admin/companies/${companyId}`, {
        method: "PUT",
        body: JSON.stringify(payload)
      });
      showToast("Company updated successfully.", "success");
    } else {
      await apiRequest("/admin/companies", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      showToast("New company registered successfully.", "success");
    }

    const modalEl = document.getElementById("companyModal");
    if (modalEl) bootstrap.Modal.getInstance(modalEl)?.hide();

    await fetchAdminCompanies();
    fetchAdminOverview();
  } catch (err) {
    console.error("Save company failed:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  } finally {
    if (submitBtn) submitBtn.disabled = false;
  }
}

async function toggleCompanyStatus(companyId, newStatus) {
  try {
    await apiRequest(`/admin/companies/${companyId}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive: newStatus })
    });
    showToast(`Company ${newStatus ? "activated" : "deactivated"} successfully.`, "success");
    await fetchAdminCompanies();
    fetchAdminOverview();
  } catch (err) {
    console.error("Failed to toggle company status:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

function confirmDeleteCompany(companyId, ticker) {
  document.getElementById("deleteConfirmMessage").innerHTML = `
    Are you sure you want to delete company <strong>${escapeHtml(ticker)}</strong>?<br>
    <small class="text-muted">If this company has existing buy/sell transaction history, destructive deletion will be blocked safely.</small>
  `;

  pendingAdminDeleteAction = async () => {
    const modalEl = document.getElementById("deleteConfirmModal");
    const modal = bootstrap.Modal.getInstance(modalEl);
    try {
      await apiRequest(`/admin/companies/${companyId}`, { method: "DELETE" });
      showToast(`Company '${ticker}' was deleted successfully.`, "success");
      if (modal) modal.hide();
      await fetchAdminCompanies();
      fetchAdminOverview();
    } catch (err) {
      console.error("Delete company error:", err);
      // Backend returns safe 400 Bad Request if referenced by transactions
      showToast(sanitizeErrorMessage(err.message, err.status), "danger");
      if (modal) modal.hide();
    }
  };

  const modalEl = document.getElementById("deleteConfirmModal");
  if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

// -----------------------------------------
// SECTORS TAB
// -----------------------------------------
async function fetchAdminSectors(render = true) {
  const tbody = document.getElementById("sectorsTableBody");
  if (render && tbody) {
    tbody.innerHTML = `<tr><td colspan="4" class="text-center py-4 text-muted"><span class="spinner-border spinner-border-sm me-2"></span>Loading sectors...</td></tr>`;
  }

  try {
    const res = await apiRequest("/admin/sectors");
    adminSectorsCache = res?.data || [];
    populateCompanySectorFilter();
    if (render) renderSectorsTable(adminSectorsCache);
  } catch (err) {
    console.error("Failed to fetch sectors:", err);
    if (render && tbody) {
      tbody.innerHTML = `<tr><td colspan="4" class="text-center py-4 text-danger">Failed to load sectors: ${escapeHtml(err.message)}</td></tr>`;
    }
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

function renderSectorsTable(sectors) {
  const tbody = document.getElementById("sectorsTableBody");
  if (!tbody) return;

  if (sectors.length === 0) {
    tbody.innerHTML = `<tr><td colspan="4" class="text-center py-4 text-muted">No sectors configured.</td></tr>`;
    return;
  }

  tbody.innerHTML = sectors.map(s => {
    return `
      <tr>
        <td class="ps-4 fw-semibold">${escapeHtml(s.sectorName)}</td>
        <td class="text-muted fs-sm">${escapeHtml(s.description || "-")}</td>
        <td class="text-center">
          <span class="badge bg-secondary-subtle text-dark border">${s.companiesCount ?? s.companyCount ?? 0} companies</span>
        </td>
        <td class="text-end pe-4">
          <div class="btn-group btn-group-sm">
            <button class="btn btn-outline-secondary" onclick="openEditSectorModal(${s.sectorId})" title="Edit Sector">
              <i class="bi bi-pencil"></i>
            </button>
            <button class="btn btn-outline-danger" onclick="confirmDeleteSector(${s.sectorId}, '${escapeHtml(s.sectorName)}', ${s.companiesCount ?? s.companyCount ?? 0})" title="Delete Sector">
              <i class="bi bi-trash"></i>
            </button>
          </div>
        </td>
      </tr>
    `;
  }).join("");
}

function openAddSectorModal() {
  document.getElementById("sectorModalLabel").textContent = "Add Market Sector";
  document.getElementById("sectorIdInput").value = "";
  document.getElementById("sectorNameInput").value = "";
  document.getElementById("sectorDescInput").value = "";

  const modalEl = document.getElementById("sectorModal");
  if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function openEditSectorModal(sectorId) {
  const sector = adminSectorsCache.find(s => s.sectorId === sectorId);
  if (!sector) return;

  document.getElementById("sectorModalLabel").textContent = "Edit Market Sector";
  document.getElementById("sectorIdInput").value = sector.sectorId;
  document.getElementById("sectorNameInput").value = sector.sectorName;
  document.getElementById("sectorDescInput").value = sector.description || "";

  const modalEl = document.getElementById("sectorModal");
  if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

async function handleSectorFormSubmit(e) {
  e.preventDefault();
  const submitBtn = document.getElementById("saveSectorSubmitBtn");
  if (submitBtn) submitBtn.disabled = true;

  const sectorId = document.getElementById("sectorIdInput").value;
  const isEdit = !!sectorId;

  const payload = {
    sectorName: document.getElementById("sectorNameInput").value.trim(),
    description: document.getElementById("sectorDescInput").value.trim()
  };

  try {
    if (isEdit) {
      await apiRequest(`/admin/sectors/${sectorId}`, {
        method: "PUT",
        body: JSON.stringify(payload)
      });
      showToast("Sector updated successfully.", "success");
    } else {
      await apiRequest("/admin/sectors", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      showToast("New sector created successfully.", "success");
    }

    const modalEl = document.getElementById("sectorModal");
    if (modalEl) bootstrap.Modal.getInstance(modalEl)?.hide();

    await fetchAdminSectors();
    fetchAdminOverview();
  } catch (err) {
    console.error("Save sector failed:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  } finally {
    if (submitBtn) submitBtn.disabled = false;
  }
}

function confirmDeleteSector(sectorId, sectorName, companyCount) {
  document.getElementById("deleteConfirmMessage").innerHTML = `
    Are you sure you want to delete sector <strong>${escapeHtml(sectorName)}</strong>?<br>
    ${companyCount > 0 ? `<div class="text-danger mt-2 fw-semibold"><i class="bi bi-exclamation-triangle me-1"></i>This sector has ${companyCount} associated company/companies and cannot be deleted until reassigned.</div>` : ""}
  `;

  pendingAdminDeleteAction = async () => {
    const modalEl = document.getElementById("deleteConfirmModal");
    const modal = bootstrap.Modal.getInstance(modalEl);
    try {
      await apiRequest(`/admin/sectors/${sectorId}`, { method: "DELETE" });
      showToast(`Sector '${sectorName}' deleted successfully.`, "success");
      if (modal) modal.hide();
      await fetchAdminSectors();
      fetchAdminOverview();
    } catch (err) {
      console.error("Delete sector error:", err);
      showToast(sanitizeErrorMessage(err.message, err.status), "danger");
      if (modal) modal.hide();
    }
  };

  const modalEl = document.getElementById("deleteConfirmModal");
  if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

// -----------------------------------------
// USERS TAB
// -----------------------------------------
async function fetchAdminUsers() {
  const tbody = document.getElementById("usersTableBody");
  if (tbody) {
    tbody.innerHTML = `<tr><td colspan="6" class="text-center py-4 text-muted"><span class="spinner-border spinner-border-sm me-2"></span>Loading users...</td></tr>`;
  }

  try {
    const res = await apiRequest("/admin/users");
    adminUsersCache = res?.data || [];
    filterAndRenderUsers();
  } catch (err) {
    console.error("Failed to fetch users:", err);
    if (tbody) {
      tbody.innerHTML = `<tr><td colspan="6" class="text-center py-4 text-danger">Failed to load users: ${escapeHtml(err.message)}</td></tr>`;
    }
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

function filterAndRenderUsers() {
  const search = (document.getElementById("userSearchInput")?.value || "").toLowerCase().trim();
  const filtered = adminUsersCache.filter(u => {
    return !search ||
      (u.fullName || "").toLowerCase().includes(search) ||
      (u.email || "").toLowerCase().includes(search);
  });

  renderUsersTable(filtered);
}

function renderUsersTable(users) {
  const tbody = document.getElementById("usersTableBody");
  if (!tbody) return;

  if (users.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6" class="text-center py-4 text-muted">No users found.</td></tr>`;
    return;
  }

  tbody.innerHTML = users.map(u => {
    const roleBadge = u.role === "ADMIN"
      ? `<span class="badge role-badge-admin px-2 py-1"><i class="bi bi-shield-check me-1"></i>ADMIN</span>`
      : `<span class="badge role-badge-investor px-2 py-1"><i class="bi bi-person me-1"></i>INVESTOR</span>`;

    const statusBadge = u.isActive
      ? `<span class="badge status-badge-active px-2 py-1"><i class="bi bi-check-circle me-1"></i>Active</span>`
      : `<span class="badge status-badge-inactive px-2 py-1"><i class="bi bi-dash-circle me-1"></i>Disabled</span>`;

    const toggleStatusText = u.isActive ? "Disable" : "Enable";
    const toggleStatusBtnClass = u.isActive ? "btn-outline-warning" : "btn-outline-success";
    const toggleRoleTarget = u.role === "ADMIN" ? "INVESTOR" : "ADMIN";
    const toggleRoleTitle = u.role === "ADMIN" ? "Demote to Investor" : "Promote to Admin";

    const createdStr = u.createdAt ? new Date(u.createdAt).toLocaleDateString("en-US", { dateStyle: "medium" }) : "-";

    return `
      <tr>
        <td class="ps-4">
          <div class="fw-semibold">${escapeHtml(u.fullName)}</div>
          <div class="text-muted fs-xs">${escapeHtml(u.email)}</div>
        </td>
        <td>${roleBadge}</td>
        <td class="text-center">${statusBadge}</td>
        <td class="fs-sm text-muted">${createdStr}</td>
        <td>
          <span class="badge bg-secondary-subtle text-dark border">${u.portfoliosCount ?? u.portfolioCount ?? 0} portfolios</span>
        </td>
        <td class="text-end pe-4">
          <div class="btn-group btn-group-sm">
            <button class="btn ${toggleStatusBtnClass}" onclick="toggleUserStatus(${u.userId}, ${!u.isActive})" title="${toggleStatusText} Account">
              <i class="bi ${u.isActive ? "bi-slash-circle" : "bi-check-circle"}"></i> ${toggleStatusText}
            </button>
            <button class="btn btn-outline-secondary" onclick="changeUserRole(${u.userId}, '${toggleRoleTarget}')" title="${toggleRoleTitle}">
              <i class="bi bi-arrow-repeat"></i> Set ${toggleRoleTarget}
            </button>
          </div>
        </td>
      </tr>
    `;
  }).join("");
}

async function toggleUserStatus(userId, newStatus) {
  try {
    await apiRequest(`/admin/users/${userId}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive: newStatus })
    });
    showToast(`User account ${newStatus ? "enabled" : "disabled"} successfully.`, "success");
    await fetchAdminUsers();
    fetchAdminOverview();
  } catch (err) {
    console.error("Failed to toggle user status:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

async function changeUserRole(userId, newRole) {
  try {
    await apiRequest(`/admin/users/${userId}/role`, {
      method: "PATCH",
      body: JSON.stringify({ role: newRole })
    });
    showToast(`User role changed to ${newRole}.`, "success");
    await fetchAdminUsers();
  } catch (err) {
    console.error("Failed to change user role:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

// -----------------------------------------
// DSE SYNCHRONIZATION TAB
// -----------------------------------------
async function triggerAdminDseSync() {
  const syncBtn = document.getElementById("triggerDseSyncBtn");
  const quickBtn = document.getElementById("quickDseSyncBtn");
  const container = document.getElementById("syncResultContainer");

  const setBtnsDisabled = (disabled) => {
    if (syncBtn) {
      syncBtn.disabled = disabled;
      syncBtn.innerHTML = disabled
        ? `<span class="spinner-border spinner-border-sm me-2"></span>Synchronizing Market Data...`
        : `<i class="bi bi-arrow-repeat fs-5"></i><span>Run DSE Synchronization Now</span>`;
    }
    if (quickBtn) {
      quickBtn.disabled = disabled;
      quickBtn.innerHTML = disabled
        ? `<span class="spinner-border spinner-border-sm me-1"></span>Syncing...`
        : `<i class="bi bi-cloud-arrow-down"></i> Sync Market Data`;
    }
  };

  setBtnsDisabled(true);

  if (container) {
    container.innerHTML = `
      <div class="text-primary py-3">
        <span class="spinner-border spinner-border-sm me-2"></span>
        Connecting to Dhaka Stock Exchange feed via IDsePriceService...
      </div>
    `;
  }

  const startTime = Date.now();

  try {
    const res = await apiRequest("/admin/sync/dse", { method: "POST" });
    const elapsed = ((Date.now() - startTime) / 1000).toFixed(2);
    const result = res?.data;

    showToast(res?.message || "Market data synchronized successfully.", "success");

    if (container) {
      container.innerHTML = `
        <div class="text-success fw-bold mb-2">
          <i class="bi bi-check-circle-fill me-1"></i> Synchronization Successful (${elapsed}s)
        </div>
        <div class="mb-1"><span class="text-muted">Updated Companies:</span> <strong>${result?.updatedCompaniesCount ?? 0}</strong></div>
        <div class="mb-1"><span class="text-muted">Skipped / Unchanged:</span> <strong>${result?.skippedCompaniesCount ?? 0}</strong></div>
        <div class="mb-1"><span class="text-muted">Synced At:</span> ${result?.syncedAt ? new Date(result.syncedAt).toLocaleTimeString() : new Date().toLocaleTimeString()}</div>
        <div class="mt-2 text-muted fs-xs border-top pt-2">${escapeHtml(result?.message || "Authoritative DSE quote stream synced.")}</div>
      `;
    }

    await fetchAdminOverview();
    if (adminCompaniesCache.length > 0) {
      await fetchAdminCompanies();
    }
  } catch (err) {
    console.error("DSE Sync failed:", err);
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
    if (container) {
      container.innerHTML = `
        <div class="text-danger fw-bold mb-2">
          <i class="bi bi-exclamation-triangle-fill me-1"></i> Synchronization Failed
        </div>
        <div class="text-danger fs-xs">${escapeHtml(err.message)}</div>
      `;
    }
  } finally {
    setBtnsDisabled(false);
  }
}

// -----------------------------------------
// AUDIT LOGS TAB
// -----------------------------------------
async function fetchAdminAuditLogs() {
  const tbody = document.getElementById("auditLogsTableBody");
  if (tbody) {
    tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4 text-muted"><span class="spinner-border spinner-border-sm me-2"></span>Loading audit logs...</td></tr>`;
  }

  try {
    const res = await apiRequest("/admin/audit-logs?take=100");
    adminAuditLogsCache = res?.data || [];
    renderAuditLogsTable(adminAuditLogsCache);
  } catch (err) {
    console.error("Failed to load audit logs:", err);
    if (tbody) {
      tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4 text-danger">Failed to load audit logs: ${escapeHtml(err.message)}</td></tr>`;
    }
    showToast(sanitizeErrorMessage(err.message, err.status), "danger");
  }
}

function renderAuditLogsTable(logs) {
  const tbody = document.getElementById("auditLogsTableBody");
  if (!tbody) return;

  if (logs.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4 text-muted">No audit logs found.</td></tr>`;
    return;
  }

  tbody.innerHTML = logs.map(l => {
    const actionBadge = l.action === "INSERT"
      ? `<span class="badge bg-success-subtle text-success border">INSERT</span>`
      : (l.action === "DELETE"
        ? `<span class="badge bg-danger-subtle text-danger border">DELETE</span>`
        : `<span class="badge bg-primary-subtle text-primary border">${escapeHtml(l.action)}</span>`);

    const dateStr = l.changedAt ? new Date(l.changedAt).toLocaleString("en-US", { dateStyle: "short", timeStyle: "medium" }) : "-";

    return `
      <tr>
        <td class="ps-4 fw-mono fs-xs text-muted">#${l.logId}</td>
        <td class="fs-xs">${dateStr}</td>
        <td>${actionBadge}</td>
        <td class="fs-sm fw-semibold">${escapeHtml(l.tableName)}</td>
        <td class="fs-xs text-muted">${l.recordId ?? "-"}</td>
        <td class="fs-xs text-muted">${l.userId ? `User #${l.userId}` : "System / Trigger"}</td>
        <td class="pe-4 fs-xs text-muted" style="max-width: 250px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="${escapeHtml(l.details || "")}">
          ${escapeHtml(l.details || "-")}
        </td>
      </tr>
    `;
  }).join("");
}


// =========================================
// INVESTMENT SIMULATOR PAGE
// =========================================
let simCompaniesCache = [];
let simPortfoliosCache = [];
let simComparisonChartInstance = null;
let simAllocationChartInstance = null;

async function setupSimulatorPage() {
  const form = document.getElementById("simulatorForm");
  if (!form) return;

  const portfolioSelect = document.getElementById("simPortfolioSelect");
  const companySelect = document.getElementById("simCompanySelect");
  const typeBuyBtn = document.getElementById("simTypeBuyBtn");
  const typeSellBtn = document.getElementById("simTypeSellBtn");
  const txTypeInput = document.getElementById("simTransactionType");
  const quantityInput = document.getElementById("simQuantity");
  const priceInput = document.getElementById("simPrice");
  const tradeAmountDisplay = document.getElementById("simEstimatedTradeAmount");
  const companyPriceLabel = document.getElementById("simCompanyPriceLabel");
  const holdingsBadge = document.getElementById("simCurrentHoldingsBadge");
  const oversellWarning = document.getElementById("simOversellWarning");
  const quantityHelper = document.getElementById("simQuantityHelper");
  const submitBtn = document.getElementById("simRunBtn");
  const resetBtn = document.getElementById("simResetBtn");
  const alertContainer = document.getElementById("simAlertContainer");
  const emptyState = document.getElementById("simEmptyState");
  const resultsContainer = document.getElementById("simResultsContainer");

  let currentCompanyPrice = 0;
  let currentCompanyHoldings = 0;

  function showAlert(msg, tone = "danger") {
    if (!alertContainer) return;
    alertContainer.className = `alert alert-${tone} alert-dismissible fade show`;
    alertContainer.innerHTML = `
      <i class="bi bi-exclamation-triangle-fill me-2"></i>
      <span>${escapeHtml(msg)}</span>
      <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    alertContainer.classList.remove("d-none");
  }

  function hideAlert() {
    if (alertContainer) alertContainer.classList.add("d-none");
  }

  function updateEstimatedAmount() {
    const qty = parseFloat(quantityInput?.value || 0);
    const prc = parseFloat(priceInput?.value || 0);
    const total = qty > 0 && prc > 0 ? qty * prc : 0;
    if (tradeAmountDisplay) {
      tradeAmountDisplay.textContent = `BDT ${total.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }

    const isSell = txTypeInput?.value === "SELL";
    if (isSell && qty > currentCompanyHoldings) {
      if (oversellWarning) oversellWarning.classList.remove("d-none");
    } else {
      if (oversellWarning) oversellWarning.classList.add("d-none");
    }
  }

  async function updateHoldingsBadge() {
    const pId = parseInt(portfolioSelect?.value || 0);
    const cId = parseInt(companySelect?.value || 0);
    if (!pId || !cId) {
      if (holdingsBadge) holdingsBadge.textContent = "Holdings: —";
      currentCompanyHoldings = 0;
      return;
    }

    try {
      const res = await apiRequest(`/transactions/available-shares?portfolioId=${pId}&companyId=${cId}`);
      let shares = 0;
      if (typeof res?.data === "number") {
        shares = res.data;
      } else if (res?.data && typeof res.data.availableShares === "number") {
        shares = res.data.availableShares;
      } else if (res?.data && typeof res.data.availableQuantity === "number") {
        shares = res.data.availableQuantity;
      } else if (res && typeof res.availableShares === "number") {
        shares = res.availableShares;
      } else if (res?.data && !isNaN(Number(res.data))) {
        shares = Number(res.data);
      }
      currentCompanyHoldings = shares;
      if (holdingsBadge) {
        holdingsBadge.textContent = `Holdings: ${currentCompanyHoldings.toLocaleString()} shares`;
      }
    } catch (_) {
      currentCompanyHoldings = 0;
      if (holdingsBadge) holdingsBadge.textContent = "Holdings: 0 shares";
    }
    updateEstimatedAmount();
  }

  // Load Portfolios & Companies
  try {
    const [pRes, cRes] = await Promise.all([
      apiRequest("/portfolios"),
      apiRequest("/companies")
    ]);

    simPortfoliosCache = pRes?.data || [];
    simCompaniesCache = (cRes?.data || []).filter(c => c.isActive !== false);

    // Populate Portfolios
    if (portfolioSelect) {
      if (simPortfoliosCache.length === 0) {
        portfolioSelect.innerHTML = `<option value="" disabled selected>No portfolios found. Please create one.</option>`;
      } else {
        portfolioSelect.innerHTML = simPortfoliosCache.map(p =>
          `<option value="${p.portfolioId}">${escapeHtml(p.portfolioName)}</option>`
        ).join("");
      }
    }

    // Populate Companies
    if (companySelect) {
      if (simCompaniesCache.length === 0) {
        companySelect.innerHTML = `<option value="" disabled selected>No active companies available.</option>`;
      } else {
        companySelect.innerHTML = [
          `<option value="" disabled selected>-- Select a company --</option>`,
          ...simCompaniesCache.map(c =>
            `<option value="${c.companyId}" data-price="${c.currentPrice}" data-ticker="${escapeHtml(c.tickerSymbol)}">${escapeHtml(c.tickerSymbol)} — ${escapeHtml(c.companyName)}</option>`
          )
        ].join("");
      }
    }
  } catch (err) {
    console.error("Failed to load simulator reference data:", err);
    showAlert("Failed to load initial portfolio or company data. Please refresh.", "danger");
  }

  // Event: Portfolio Change
  portfolioSelect?.addEventListener("change", () => {
    updateHoldingsBadge();
  });

  // Event: Company Change
  companySelect?.addEventListener("change", () => {
    const opt = companySelect.options[companySelect.selectedIndex];
    if (opt) {
      const price = parseFloat(opt.getAttribute("data-price") || 0);
      currentCompanyPrice = price;
      if (priceInput) priceInput.value = price > 0 ? price.toFixed(2) : "";
      if (companyPriceLabel) {
        companyPriceLabel.textContent = `Market Price: BDT ${price.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      }
    }
    updateHoldingsBadge();
  });

  // Event: Quick Price Buttons
  document.getElementById("chipMarketPrice")?.addEventListener("click", () => {
    if (currentCompanyPrice > 0 && priceInput) {
      priceInput.value = currentCompanyPrice.toFixed(2);
      updateEstimatedAmount();
    }
  });

  document.getElementById("chipMinus5")?.addEventListener("click", () => {
    if (currentCompanyPrice > 0 && priceInput) {
      priceInput.value = (currentCompanyPrice * 0.95).toFixed(2);
      updateEstimatedAmount();
    }
  });

  document.getElementById("chipPlus5")?.addEventListener("click", () => {
    if (currentCompanyPrice > 0 && priceInput) {
      priceInput.value = (currentCompanyPrice * 1.05).toFixed(2);
      updateEstimatedAmount();
    }
  });

  document.getElementById("chipPlus10")?.addEventListener("click", () => {
    if (currentCompanyPrice > 0 && priceInput) {
      priceInput.value = (currentCompanyPrice * 1.10).toFixed(2);
      updateEstimatedAmount();
    }
  });

  // Event: BUY / SELL Toggles
  typeBuyBtn?.addEventListener("click", () => {
    txTypeInput.value = "BUY";
    typeBuyBtn.className = "type-toggle-btn flex-fill active buy-active";
    typeSellBtn.className = "type-toggle-btn flex-fill";
    if (quantityHelper) quantityHelper.textContent = "Shares to buy";
    updateEstimatedAmount();
  });

  typeSellBtn?.addEventListener("click", () => {
    txTypeInput.value = "SELL";
    typeSellBtn.className = "type-toggle-btn flex-fill active sell-active";
    typeBuyBtn.className = "type-toggle-btn flex-fill";
    if (quantityHelper) quantityHelper.textContent = "Shares to sell";
    updateEstimatedAmount();
  });

  quantityInput?.addEventListener("input", updateEstimatedAmount);
  priceInput?.addEventListener("input", updateEstimatedAmount);

  // Event: Reset
  resetBtn?.addEventListener("click", () => {
    form.reset();
    txTypeInput.value = "BUY";
    typeBuyBtn.className = "type-toggle-btn flex-fill active buy-active";
    typeSellBtn.className = "type-toggle-btn flex-fill";
    if (tradeAmountDisplay) tradeAmountDisplay.textContent = "BDT 0.00";
    if (companyPriceLabel) companyPriceLabel.textContent = "Market Price: —";
    if (holdingsBadge) holdingsBadge.textContent = "Holdings: —";
    if (oversellWarning) oversellWarning.classList.add("d-none");
    if (emptyState) emptyState.classList.remove("d-none");
    if (resultsContainer) resultsContainer.classList.add("d-none");
    hideAlert();
  });

  // Event: Submit Simulation
  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    hideAlert();

    const pId = parseInt(portfolioSelect?.value || 0);
    const cId = parseInt(companySelect?.value || 0);
    const txType = txTypeInput?.value || "BUY";
    const qty = parseFloat(quantityInput?.value || 0);
    const price = parseFloat(priceInput?.value || 0);

    if (!pId) {
      showAlert("Please select a target portfolio.");
      portfolioSelect?.focus();
      return;
    }
    if (!cId) {
      showAlert("Please select a company/stock.");
      companySelect?.focus();
      return;
    }
    if (!qty || qty <= 0) {
      showAlert("Quantity must be greater than zero.");
      quantityInput?.focus();
      return;
    }
    if (!Number.isInteger(qty)) {
      showAlert("Fractional shares are not supported. Quantity must be a whole number.");
      quantityInput?.focus();
      return;
    }
    if (!price || price <= 0) {
      showAlert("Hypothetical price must be greater than zero.");
      priceInput?.focus();
      return;
    }

    if (txType === "SELL" && qty > currentCompanyHoldings) {
      showAlert(`Cannot simulate SELL order: Requested quantity (${qty}) exceeds your current available holdings (${currentCompanyHoldings}) of this company.`);
      quantityInput?.focus();
      return;
    }

    const origHtml = submitBtn.innerHTML;
    submitBtn.disabled = true;
    submitBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>Simulating...`;

    try {
      const res = await apiRequest("/simulator", {
        method: "POST",
        body: JSON.stringify({
          portfolioId: pId,
          companyId: cId,
          transactionType: txType,
          quantity: qty,
          hypotheticalPrice: price
        })
      });

      const data = res?.data;
      if (!data) throw new Error("No simulation data received from server.");

      renderSimulationResults(data);
      if (emptyState) emptyState.classList.add("d-none");
      if (resultsContainer) resultsContainer.classList.remove("d-none");

      resultsContainer.scrollIntoView({ behavior: "smooth", block: "start" });
      showToast("Hypothetical simulation calculated successfully!", "success");
    } catch (err) {
      console.error("Simulation error:", err);
      showAlert(sanitizeErrorMessage(err.message, err.status), "danger");
    } finally {
      submitBtn.disabled = false;
      submitBtn.innerHTML = origHtml;
    }
  });
}

function renderSimulationResults(data) {
  const isBuy = data.transactionType === "BUY";

  // Visual Flow
  const flowTradeBadge = document.getElementById("flowTradeBadge");
  if (flowTradeBadge) {
    flowTradeBadge.className = `badge ${isBuy ? "bg-success" : "bg-danger"}`;
    flowTradeBadge.textContent = data.transactionType;
  }
  const flowTradeSummary = document.getElementById("flowTradeSummary");
  if (flowTradeSummary) {
    flowTradeSummary.textContent = `${data.quantity.toLocaleString()} ${data.tickerSymbol} @ BDT ${data.hypotheticalPrice.toFixed(2)}`;
  }
  const flowTradeValue = document.getElementById("flowTradeValue");
  if (flowTradeValue) {
    flowTradeValue.textContent = `BDT ${data.totalHypotheticalAmount.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }
  const flowCurrentQty = document.getElementById("flowCurrentQty");
  if (flowCurrentQty) flowCurrentQty.textContent = data.currentHoldingQuantity.toLocaleString();
  const flowCurrentAvgCost = document.getElementById("flowCurrentAvgCost");
  if (flowCurrentAvgCost) flowCurrentAvgCost.textContent = `BDT ${data.currentAverageCost.toFixed(2)}`;

  const flowSimQty = document.getElementById("flowSimQty");
  if (flowSimQty) flowSimQty.textContent = data.simulatedHoldingQuantity.toLocaleString();
  const flowSimAvgCost = document.getElementById("flowSimAvgCost");
  if (flowSimAvgCost) flowSimAvgCost.textContent = `BDT ${data.simulatedAverageCost.toFixed(2)}`;

  // Helpers for Delta Formatting
  const fmtDelta = (val, isCurrency = true, isPct = false) => {
    const sign = val > 0 ? "+" : "";
    const suffix = isPct ? "%" : "";
    const prefix = isCurrency ? "BDT " : "";
    const numStr = val.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return `${sign}${prefix}${numStr}${suffix}`;
  };

  const getDeltaClass = (val, invert = false) => {
    if (val === 0) return "delta-neutral";
    const isGood = invert ? val < 0 : val > 0;
    return isGood ? "delta-pos" : "delta-neg";
  };

  // Metric 1: Quantity
  const valSimQty = document.getElementById("valSimQty");
  if (valSimQty) valSimQty.textContent = data.simulatedHoldingQuantity.toLocaleString();
  const valCurrentQty = document.getElementById("valCurrentQty");
  if (valCurrentQty) valCurrentQty.textContent = data.currentHoldingQuantity.toLocaleString();
  const badgeQtyDelta = document.getElementById("badgeQtyDelta");
  if (badgeQtyDelta) {
    badgeQtyDelta.className = `metric-delta-badge ${getDeltaClass(data.quantityChange)}`;
    badgeQtyDelta.textContent = `${data.quantityChange > 0 ? "+" : ""}${data.quantityChange.toLocaleString()}`;
  }

  // Metric 2: Average Cost
  const valSimAvgCost = document.getElementById("valSimAvgCost");
  if (valSimAvgCost) valSimAvgCost.textContent = `BDT ${data.simulatedAverageCost.toFixed(2)}`;
  const valCurrentAvgCost = document.getElementById("valCurrentAvgCost");
  if (valCurrentAvgCost) valCurrentAvgCost.textContent = `BDT ${data.currentAverageCost.toFixed(2)}`;
  const badgeAvgCostDelta = document.getElementById("badgeAvgCostDelta");
  if (badgeAvgCostDelta) {
    badgeAvgCostDelta.className = `metric-delta-badge ${getDeltaClass(data.averageCostChange, true)}`;
    badgeAvgCostDelta.textContent = fmtDelta(data.averageCostChange, true);
  }

  // Metric 3: Position Value
  const valSimPosVal = document.getElementById("valSimPosVal");
  if (valSimPosVal) valSimPosVal.textContent = `BDT ${data.simulatedPositionMarketValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const valCurrentPosVal = document.getElementById("valCurrentPosVal");
  if (valCurrentPosVal) valCurrentPosVal.textContent = `BDT ${data.currentPositionMarketValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const badgePosValDelta = document.getElementById("badgePosValDelta");
  if (badgePosValDelta) {
    badgePosValDelta.className = `metric-delta-badge ${getDeltaClass(data.positionValueChange)}`;
    badgePosValDelta.textContent = fmtDelta(data.positionValueChange, true);
  }

  // Metric 4: Unrealized PL
  const valSimPL = document.getElementById("valSimPL");
  if (valSimPL) {
    valSimPL.textContent = `BDT ${data.simulatedUnrealizedProfitLoss.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    valSimPL.className = `mb-0 fw-bold fw-mono ${data.simulatedUnrealizedProfitLoss >= 0 ? "text-success" : "text-danger"}`;
  }
  const valSimPLPct = document.getElementById("valSimPLPct");
  if (valSimPLPct) {
    valSimPLPct.textContent = `(${data.simulatedUnrealizedProfitLossPercentage >= 0 ? "+" : ""}${data.simulatedUnrealizedProfitLossPercentage.toFixed(2)}%)`;
    valSimPLPct.className = `fs-xs fw-semibold ${data.simulatedUnrealizedProfitLossPercentage >= 0 ? "text-success" : "text-danger"}`;
  }
  const valCurrentPL = document.getElementById("valCurrentPL");
  if (valCurrentPL) valCurrentPL.textContent = `BDT ${data.currentUnrealizedProfitLoss.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const valCurrentPLPct = document.getElementById("valCurrentPLPct");
  if (valCurrentPLPct) valCurrentPLPct.textContent = `${data.currentUnrealizedProfitLossPercentage.toFixed(2)}%`;
  const plPctDelta = data.simulatedUnrealizedProfitLossPercentage - data.currentUnrealizedProfitLossPercentage;
  const badgePlPctDelta = document.getElementById("badgePlPctDelta");
  if (badgePlPctDelta) {
    badgePlPctDelta.className = `metric-delta-badge ${getDeltaClass(plPctDelta)}`;
    badgePlPctDelta.textContent = `${plPctDelta >= 0 ? "+" : ""}${plPctDelta.toFixed(2)}%`;
  }

  // Metric 5: Allocation
  const valSimAlloc = document.getElementById("valSimAlloc");
  if (valSimAlloc) valSimAlloc.textContent = `${data.simulatedAllocationPercentage.toFixed(1)}%`;
  const valCurrentAlloc = document.getElementById("valCurrentAlloc");
  if (valCurrentAlloc) valCurrentAlloc.textContent = `${data.currentAllocationPercentage.toFixed(1)}%`;
  const badgeAllocDelta = document.getElementById("badgeAllocDelta");
  if (badgeAllocDelta) {
    badgeAllocDelta.className = `metric-delta-badge ${getDeltaClass(data.allocationPercentageChange)}`;
    badgeAllocDelta.textContent = `${data.allocationPercentageChange >= 0 ? "+" : ""}${data.allocationPercentageChange.toFixed(1)}%`;
  }

  // Metric 6: Total Portfolio Value
  const valSimPortVal = document.getElementById("valSimPortVal");
  if (valSimPortVal) valSimPortVal.textContent = `BDT ${data.simulatedPortfolioTotalValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const valCurrentPortVal = document.getElementById("valCurrentPortVal");
  if (valCurrentPortVal) valCurrentPortVal.textContent = `BDT ${data.currentPortfolioTotalValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const badgePortValDelta = document.getElementById("badgePortValDelta");
  if (badgePortValDelta) {
    badgePortValDelta.className = `metric-delta-badge ${getDeltaClass(data.portfolioTotalValueChange)}`;
    badgePortValDelta.textContent = fmtDelta(data.portfolioTotalValueChange, true);
  }

  // Sell Callout
  const sellCallout = document.getElementById("simSellCallout");
  if (sellCallout) {
    if (!isBuy) {
      sellCallout.classList.remove("d-none");
      const sellCalloutPrice = document.getElementById("sellCalloutPrice");
      if (sellCalloutPrice) sellCalloutPrice.textContent = `BDT ${data.hypotheticalPrice.toFixed(2)}`;
      const sellCalloutRealizedPL = document.getElementById("sellCalloutRealizedPL");
      if (sellCalloutRealizedPL) {
        sellCalloutRealizedPL.textContent = `${data.realizedProfitLoss >= 0 ? "+" : ""}BDT ${data.realizedProfitLoss.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
        sellCalloutRealizedPL.className = `fs-4 fw-bold fw-mono ${data.realizedProfitLoss >= 0 ? "text-success" : "text-danger"}`;
      }
      const sellCalloutProceeds = document.getElementById("sellCalloutProceeds");
      if (sellCalloutProceeds) {
        sellCalloutProceeds.textContent = `BDT ${data.totalHypotheticalAmount.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      }
    } else {
      sellCallout.classList.add("d-none");
    }
  }

  // Detailed Table
  const elRowCurrentQty = document.getElementById("rowCurrentQty");
  if (elRowCurrentQty) elRowCurrentQty.textContent = `${data.currentHoldingQuantity.toLocaleString()} shares`;
  const elRowTradeQty = document.getElementById("rowTradeQty");
  if (elRowTradeQty) elRowTradeQty.textContent = `${isBuy ? "+" : "-"}${data.quantity.toLocaleString()} shares`;
  const elRowSimQty = document.getElementById("rowSimQty");
  if (elRowSimQty) elRowSimQty.textContent = `${data.simulatedHoldingQuantity.toLocaleString()} shares`;
  const elRowDeltaQty = document.getElementById("rowDeltaQty");
  if (elRowDeltaQty) elRowDeltaQty.textContent = `${data.quantityChange >= 0 ? "+" : ""}${data.quantityChange.toLocaleString()}`;

  const elRowCurrentAvgCost = document.getElementById("rowCurrentAvgCost");
  if (elRowCurrentAvgCost) elRowCurrentAvgCost.textContent = `BDT ${data.currentAverageCost.toFixed(2)}`;
  const elRowTradePrice = document.getElementById("rowTradePrice");
  if (elRowTradePrice) elRowTradePrice.textContent = `BDT ${data.hypotheticalPrice.toFixed(2)}`;
  const elRowSimAvgCost = document.getElementById("rowSimAvgCost");
  if (elRowSimAvgCost) elRowSimAvgCost.textContent = `BDT ${data.simulatedAverageCost.toFixed(2)}`;
  const elRowDeltaAvgCost = document.getElementById("rowDeltaAvgCost");
  if (elRowDeltaAvgCost) elRowDeltaAvgCost.textContent = fmtDelta(data.averageCostChange, true);

  const elRowCurrentCostBasis = document.getElementById("rowCurrentCostBasis");
  if (elRowCurrentCostBasis) elRowCurrentCostBasis.textContent = `BDT ${data.currentPositionCostBasis.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const elRowTradeAmount = document.getElementById("rowTradeAmount");
  if (elRowTradeAmount) elRowTradeAmount.textContent = `BDT ${data.totalHypotheticalAmount.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const elRowSimCostBasis = document.getElementById("rowSimCostBasis");
  if (elRowSimCostBasis) elRowSimCostBasis.textContent = `BDT ${data.simulatedPositionCostBasis.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const costDelta = data.simulatedPositionCostBasis - data.currentPositionCostBasis;
  const elRowDeltaCostBasis = document.getElementById("rowDeltaCostBasis");
  if (elRowDeltaCostBasis) elRowDeltaCostBasis.textContent = fmtDelta(costDelta, true);

  const elRowCurrentMarketVal = document.getElementById("rowCurrentMarketVal");
  if (elRowCurrentMarketVal) elRowCurrentMarketVal.textContent = `BDT ${data.currentPositionMarketValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const elRowSimMarketVal = document.getElementById("rowSimMarketVal");
  if (elRowSimMarketVal) elRowSimMarketVal.textContent = `BDT ${data.simulatedPositionMarketValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const elRowDeltaMarketVal = document.getElementById("rowDeltaMarketVal");
  if (elRowDeltaMarketVal) elRowDeltaMarketVal.textContent = fmtDelta(data.positionValueChange, true);

  const elRowCurrentUnrealized = document.getElementById("rowCurrentUnrealized");
  if (elRowCurrentUnrealized) elRowCurrentUnrealized.textContent = `BDT ${data.currentUnrealizedProfitLoss.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${data.currentUnrealizedProfitLossPercentage.toFixed(2)}%)`;
  const elRowSimUnrealized = document.getElementById("rowSimUnrealized");
  if (elRowSimUnrealized) elRowSimUnrealized.textContent = `BDT ${data.simulatedUnrealizedProfitLoss.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${data.simulatedUnrealizedProfitLossPercentage.toFixed(2)}%)`;
  const elRowDeltaUnrealized = document.getElementById("rowDeltaUnrealized");
  if (elRowDeltaUnrealized) elRowDeltaUnrealized.textContent = fmtDelta(data.simulatedUnrealizedProfitLoss - data.currentUnrealizedProfitLoss, true);

  const elRowCurrentWeight = document.getElementById("rowCurrentWeight");
  if (elRowCurrentWeight) elRowCurrentWeight.textContent = `${data.currentAllocationPercentage.toFixed(1)}%`;
  const elRowSimWeight = document.getElementById("rowSimWeight");
  if (elRowSimWeight) elRowSimWeight.textContent = `${data.simulatedAllocationPercentage.toFixed(1)}%`;
  const elRowDeltaWeight = document.getElementById("rowDeltaWeight");
  if (elRowDeltaWeight) elRowDeltaWeight.textContent = `${data.allocationPercentageChange >= 0 ? "+" : ""}${data.allocationPercentageChange.toFixed(1)}%`;

  const elRowCurrentPortVal = document.getElementById("rowCurrentPortVal");
  if (elRowCurrentPortVal) elRowCurrentPortVal.textContent = `BDT ${data.currentPortfolioTotalValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const elRowSimPortVal = document.getElementById("rowSimPortVal");
  if (elRowSimPortVal) elRowSimPortVal.textContent = `BDT ${data.simulatedPortfolioTotalValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const elRowDeltaPortVal = document.getElementById("rowDeltaPortVal");
  if (elRowDeltaPortVal) elRowDeltaPortVal.textContent = fmtDelta(data.portfolioTotalValueChange, true);

  const simTimestamp = document.getElementById("simTimestamp");
  if (simTimestamp) simTimestamp.textContent = new Date().toLocaleTimeString();

  // Render Charts
  renderSimulationCharts(data);
}

function renderSimulationCharts(data) {
  if (typeof Chart === "undefined") return;

  // Chart 1: Valuation Comparison (Grouped Bar)
  const barCanvas = document.getElementById("simComparisonChart");
  if (barCanvas) {
    if (simComparisonChartInstance) simComparisonChartInstance.destroy();

    const ctx = barCanvas.getContext("2d");
    simComparisonChartInstance = new Chart(ctx, {
      type: "bar",
      data: {
        labels: ["Position Cost Basis", "Position Market Value", "Total Portfolio Value"],
        datasets: [
          {
            label: "Current",
            data: [data.currentPositionCostBasis, data.currentPositionMarketValue, data.currentPortfolioTotalValue],
            backgroundColor: "rgba(100, 116, 139, 0.7)",
            borderColor: "rgba(100, 116, 139, 1)",
            borderWidth: 1,
            borderRadius: 4
          },
          {
            label: "Simulated",
            data: [data.simulatedPositionCostBasis, data.simulatedPositionMarketValue, data.simulatedPortfolioTotalValue],
            backgroundColor: data.transactionType === "BUY" ? "rgba(22, 122, 74, 0.7)" : "rgba(37, 99, 235, 0.7)",
            borderColor: data.transactionType === "BUY" ? "rgba(22, 122, 74, 1)" : "rgba(37, 99, 235, 1)",
            borderWidth: 1,
            borderRadius: 4
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: "top" },
          tooltip: {
            callbacks: {
              label: (item) => `${item.dataset.label}: BDT ${item.raw.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
            }
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            ticks: {
              callback: (val) => "BDT " + val.toLocaleString()
            }
          }
        }
      }
    });
  }

  // Chart 2: Allocation Donut
  const donutCanvas = document.getElementById("simAllocationChart");
  if (donutCanvas) {
    if (simAllocationChartInstance) simAllocationChartInstance.destroy();

    const stockWeight = data.simulatedAllocationPercentage;
    const otherWeight = Math.max(0, 100 - stockWeight);

    const donutStockWeightBadge = document.getElementById("donutStockWeightBadge");
    if (donutStockWeightBadge) {
      donutStockWeightBadge.textContent = `${data.tickerSymbol}: ${stockWeight.toFixed(1)}%`;
    }

    const ctx = donutCanvas.getContext("2d");
    simAllocationChartInstance = new Chart(ctx, {
      type: "doughnut",
      data: {
        labels: [data.tickerSymbol, "Other Portfolio Assets"],
        datasets: [
          {
            data: [stockWeight, otherWeight],
            backgroundColor: ["#1D4ED8", "#94A3B8"],
            hoverOffset: 4,
            borderWidth: 2
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: "bottom" },
          tooltip: {
            callbacks: {
              label: (item) => ` ${item.label}: ${item.raw.toFixed(1)}%`
            }
          }
        },
        cutout: "70%"
      }
    });
  }
}


// =========================================
// PORTFOLIO GOALS & PROGRESS TRACKING
// =========================================
let goalsCache = [];
let goalsPortfoliosCache = [];

async function setupGoalsPage() {
  const gridContainer = document.getElementById("goalsGridContainer");
  if (!gridContainer) return;

  const emptyState = document.getElementById("goalsEmptyState");
  const filterPortfolio = document.getElementById("filterPortfolioSelect");
  const filterStatus = document.getElementById("filterStatusSelect");
  const refreshBtn = document.getElementById("refreshGoalsBtn");
  const modalEl = document.getElementById("goalModal");
  const goalForm = document.getElementById("goalForm");
  const goalModalTitle = document.getElementById("goalModalTitle");
  const goalIdInput = document.getElementById("goalId");
  const goalPortfolioSelect = document.getElementById("goalPortfolioSelect");
  const goalTypeSelect = document.getElementById("goalTypeSelect");
  const goalTitleInput = document.getElementById("goalTitle");
  const goalTargetValueInput = document.getElementById("goalTargetValue");
  const goalTargetDateInput = document.getElementById("goalTargetDate");
  const goalDescriptionInput = document.getElementById("goalDescription");
  const goalActiveSwitchContainer = document.getElementById("goalActiveSwitchContainer");
  const goalIsActiveInput = document.getElementById("goalIsActive");
  const saveGoalBtn = document.getElementById("saveGoalBtn");
  const goalTypeHelpText = document.getElementById("goalTypeHelpText");

  // Load Portfolios into filter and modal dropdowns
  try {
    const pRes = await apiRequest("/portfolios");
    goalsPortfoliosCache = pRes?.data || [];

    if (filterPortfolio) {
      filterPortfolio.innerHTML = [
        '<option value="">All Portfolios</option>',
        ...goalsPortfoliosCache.map(p => `<option value="${p.portfolioId}">${escapeHtml(p.portfolioName)}</option>`)
      ].join("");
    }

    if (goalPortfolioSelect) {
      goalPortfolioSelect.innerHTML = [
        '<option value="" disabled selected>Select a portfolio...</option>',
        ...goalsPortfoliosCache.map(p => `<option value="${p.portfolioId}">${escapeHtml(p.portfolioName)}</option>`)
      ].join("");
    }
  } catch (err) {
    console.error("Failed to load portfolios for goals:", err);
  }

  // Load Goals
  async function loadGoals() {
    gridContainer.innerHTML = `
      <div class="col-12 text-center py-5">
        <div class="spinner-border text-primary spinner-border-sm me-2" role="status"></div>
        <span class="text-muted fs-sm">Calculating live progress from portfolio holdings...</span>
      </div>
    `;

    const pId = filterPortfolio?.value ? parseInt(filterPortfolio.value) : null;
    let endpoint = "/goals";
    if (pId) {
      endpoint += `?portfolioId=${pId}`;
    }

    try {
      const res = await apiRequest(endpoint);
      goalsCache = res?.data || [];
      renderGoals();
    } catch (err) {
      console.error("Failed to load goals:", err);
      gridContainer.innerHTML = `
        <div class="col-12 text-center py-5 text-danger">
          <i class="bi bi-exclamation-triangle-fill fs-2 d-block mb-2"></i>
          <h6>Failed to load goals</h6>
          <p class="fs-xs text-muted">${escapeHtml(err.message)}</p>
          <button class="btn btn-sm btn-outline-primary" id="retryGoalsBtn">Try Again</button>
        </div>
      `;
      document.getElementById("retryGoalsBtn")?.addEventListener("click", loadGoals);
    }
  }

  function updateKpis(goals) {
    const totalEl = document.getElementById("kpiTotalGoals");
    const onTrackEl = document.getElementById("kpiOnTrackGoals");
    const atRiskEl = document.getElementById("kpiAtRiskGoals");
    const achievedEl = document.getElementById("kpiAchievedGoals");
    const portfoliosLinkedEl = document.getElementById("kpiPortfoliosLinked");

    const activeGoals = goals.filter(g => g.isActive);
    const onTrack = activeGoals.filter(g => g.status === "ON_TRACK").length;
    const atRisk = activeGoals.filter(g => g.status === "AT_RISK").length;
    const achieved = goals.filter(g => g.status === "ACHIEVED").length;
    const uniquePortfolios = new Set(goals.map(g => g.portfolioId)).size;

    if (totalEl) totalEl.textContent = activeGoals.length;
    if (onTrackEl) onTrackEl.textContent = onTrack;
    if (atRiskEl) atRiskEl.textContent = atRisk;
    if (achievedEl) achievedEl.textContent = achieved;
    if (portfoliosLinkedEl) portfoliosLinkedEl.textContent = `Linked to ${uniquePortfolios} portfolio${uniquePortfolios === 1 ? "" : "s"}`;
  }

  function renderGoals() {
    updateKpis(goalsCache);

    const statusFilter = filterStatus?.value || "ALL";
    let filtered = goalsCache;
    if (statusFilter !== "ALL") {
      filtered = filtered.filter(g => g.status === statusFilter);
    }

    if (filtered.length === 0) {
      gridContainer.innerHTML = "";
      if (emptyState) emptyState.classList.remove("d-none");
      return;
    }

    if (emptyState) emptyState.classList.add("d-none");

    gridContainer.innerHTML = filtered.map(g => {
      let statusClass = "status-on-track";
      let statusIcon = "bi-check-circle-fill";
      let statusLabel = "On Track";
      let progressColor = "var(--color-positive)";

      if (g.status === "ACHIEVED") {
        statusClass = "status-achieved";
        statusIcon = "bi-trophy-fill";
        statusLabel = "Achieved";
        progressColor = "var(--color-accent)";
      } else if (g.status === "AT_RISK") {
        statusClass = "status-at-risk";
        statusIcon = "bi-exclamation-triangle-fill";
        statusLabel = "At Risk";
        progressColor = "var(--color-warning)";
      }

      let typeIcon = "bi-cash-stack";
      let typeLabel = "Valuation Target";
      if (g.goalType === "TARGET_RETURN") {
        typeIcon = "bi-graph-up-arrow";
        typeLabel = "Return / Profit Target";
      } else if (g.goalType === "TARGET_DIVIDEND_INCOME") {
        typeIcon = "bi-coin";
        typeLabel = "Dividend Target";
      }

      const cappedPct = Math.min(100, Math.max(0, g.progressPercentage));
      const formattedTarget = `BDT ${g.targetValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      const formattedCurrent = `BDT ${g.currentValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      const formattedRemaining = g.remainingValue > 0
        ? `BDT ${g.remainingValue.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
        : "None (Target Met)";

      let dateBadge = '<span class="text-muted fs-xs">No deadline set</span>';
      if (g.targetDate) {
        const d = new Date(g.targetDate);
        const isPast = d < new Date();
        const dateStr = d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
        dateBadge = `<span class="fs-xs ${isPast && g.status !== 'ACHIEVED' ? 'text-danger fw-semibold' : 'text-muted'}"><i class="bi bi-calendar-event me-1"></i>${dateStr}</span>`;
      }

      return `
        <div class="col-lg-4 col-md-6">
          <div class="goal-card">
            <div>
              <div class="d-flex align-items-center justify-content-between mb-2">
                <span class="status-pill ${statusClass}">
                  <i class="bi ${statusIcon}"></i> ${statusLabel}
                </span>
                <span class="badge bg-secondary-subtle text-secondary fs-xs">
                  <i class="bi ${typeIcon} me-1"></i> ${typeLabel}
                </span>
              </div>

              <h5 class="fw-bold mb-1 text-truncate" title="${escapeHtml(g.title)}">${escapeHtml(g.title)}</h5>
              <div class="d-flex align-items-center gap-2 mb-2">
                <span class="badge bg-light text-dark border fs-xs">
                  <i class="bi bi-briefcase me-1"></i> ${escapeHtml(g.portfolioName)}
                </span>
                ${!g.isActive ? '<span class="badge bg-danger-subtle text-danger fs-xs">Paused</span>' : ''}
              </div>

              ${g.description ? `<p class="text-muted fs-xs mb-3" style="min-height: 36px; line-height: 1.4;">${escapeHtml(g.description)}</p>` : '<div style="min-height: 20px;"></div>'}

              <!-- Progress bar -->
              <div class="d-flex justify-content-between align-items-baseline mb-1">
                <span class="text-muted fs-xs fw-semibold">PROGRESS</span>
                <span class="fw-bold fw-mono fs-sm" style="color: ${progressColor};">${g.progressPercentage.toFixed(1)}%</span>
              </div>

              <div class="goal-progress-bar-wrapper">
                <div class="goal-progress-fill" style="width: ${cappedPct}%; background: ${progressColor};"></div>
              </div>

              <!-- Numbers Grid -->
              <div class="row g-2 text-center my-2 p-2 rounded bg-surface-soft border">
                <div class="col-4">
                  <span class="text-muted fs-xs d-block">Current</span>
                  <strong class="fs-xs fw-mono text-truncate d-block">${formattedCurrent}</strong>
                </div>
                <div class="col-4 border-start border-end">
                  <span class="text-muted fs-xs d-block">Target</span>
                  <strong class="fs-xs fw-mono text-truncate d-block">${formattedTarget}</strong>
                </div>
                <div class="col-4">
                  <span class="text-muted fs-xs d-block">Remaining</span>
                  <strong class="fs-xs fw-mono text-truncate d-block ${g.remainingValue === 0 ? 'text-success' : ''}">${formattedRemaining}</strong>
                </div>
              </div>
            </div>

            <!-- Footer / Actions -->
            <div class="d-flex align-items-center justify-content-between pt-3 mt-2 border-top">
              <div>${dateBadge}</div>
              <div class="d-flex gap-1">
                <button type="button" class="btn btn-sm btn-outline-secondary px-2 py-1 edit-goal-btn" data-id="${g.goalId}" title="Edit Goal">
                  <i class="bi bi-pencil"></i>
                </button>
                <button type="button" class="btn btn-sm btn-outline-danger px-2 py-1 delete-goal-btn" data-id="${g.goalId}" title="Delete Goal">
                  <i class="bi bi-trash"></i>
                </button>
              </div>
            </div>
          </div>
        </div>
      `;
    }).join("");

    // Attach Edit and Delete listeners
    gridContainer.querySelectorAll(".edit-goal-btn").forEach(btn => {
      btn.addEventListener("click", () => {
        const id = parseInt(btn.getAttribute("data-id"));
        const goal = goalsCache.find(x => x.goalId === id);
        if (!goal) return;

        if (goalModalTitle) goalModalTitle.textContent = "Edit Financial Goal";
        if (goalIdInput) goalIdInput.value = goal.goalId;
        if (goalPortfolioSelect) {
          goalPortfolioSelect.value = goal.portfolioId;
          goalPortfolioSelect.disabled = true;
        }
        if (goalTypeSelect) {
          goalTypeSelect.value = goal.goalType;
          goalTypeSelect.disabled = true;
        }
        if (goalTitleInput) goalTitleInput.value = goal.title;
        if (goalTargetValueInput) goalTargetValueInput.value = goal.targetValue;
        if (goalTargetDateInput) {
          goalTargetDateInput.value = goal.targetDate ? goal.targetDate.split("T")[0] : "";
        }
        if (goalDescriptionInput) goalDescriptionInput.value = goal.description || "";
        if (goalActiveSwitchContainer) goalActiveSwitchContainer.style.display = "block";
        if (goalIsActiveInput) goalIsActiveInput.checked = goal.isActive;

        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();
      });
    });

    gridContainer.querySelectorAll(".delete-goal-btn").forEach(btn => {
      btn.addEventListener("click", async () => {
        const id = parseInt(btn.getAttribute("data-id"));
        const goal = goalsCache.find(x => x.goalId === id);
        if (!goal) return;

        if (!confirm(`Are you sure you want to delete the goal "${goal.title}"?`)) return;

        try {
          await apiRequest(`/goals/${id}`, { method: "DELETE" });
          showToast("Goal deleted successfully.", "success");
          loadGoals();
        } catch (err) {
          console.error("Delete goal failed:", err);
          showToast(sanitizeErrorMessage(err.message, err.status), "danger");
        }
      });
    });
  }

  // Type Help Text update
  goalTypeSelect?.addEventListener("change", () => {
    const val = goalTypeSelect.value;
    if (val === "TARGET_PORTFOLIO_VALUE") {
      goalTypeHelpText.textContent = "Tracks total market value of all net holdings in this portfolio.";
    } else if (val === "TARGET_RETURN") {
      goalTypeHelpText.textContent = "Tracks cumulative unrealized profit/loss across all active holdings.";
    } else if (val === "TARGET_DIVIDEND_INCOME") {
      goalTypeHelpText.textContent = "Tracks total dividend income generated by stocks held in this portfolio.";
    }
  });

  // Modal Reset on Open
  document.getElementById("openCreateGoalBtn")?.addEventListener("click", () => {
    goalForm?.reset();
    if (goalModalTitle) goalModalTitle.textContent = "Create Financial Goal";
    if (goalIdInput) goalIdInput.value = "";
    if (goalPortfolioSelect) goalPortfolioSelect.disabled = false;
    if (goalTypeSelect) goalTypeSelect.disabled = false;
    if (goalActiveSwitchContainer) goalActiveSwitchContainer.style.display = "none";
    if (goalIsActiveInput) goalIsActiveInput.checked = true;
  });

  // Goal Form Submit
  goalForm?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const isEdit = !!goalIdInput?.value;
    const goalId = parseInt(goalIdInput?.value || 0);
    const pId = parseInt(goalPortfolioSelect?.value || 0);
    const type = goalTypeSelect?.value || "TARGET_PORTFOLIO_VALUE";
    const title = (goalTitleInput?.value || "").trim();
    const targetVal = parseFloat(goalTargetValueInput?.value || 0);
    const targetDate = goalTargetDateInput?.value ? goalTargetDateInput.value : null;
    const desc = (goalDescriptionInput?.value || "").trim();
    const isActive = goalIsActiveInput?.checked ?? true;

    if (!isEdit && !pId) {
      showToast("Please select a portfolio.", "danger");
      goalPortfolioSelect?.focus();
      return;
    }
    if (!title) {
      showToast("Please enter a goal title.", "danger");
      goalTitleInput?.focus();
      return;
    }
    if (!targetVal || targetVal <= 0) {
      showToast("Target amount must be greater than zero.", "danger");
      goalTargetValueInput?.focus();
      return;
    }

    const origBtnHtml = saveGoalBtn.innerHTML;
    saveGoalBtn.disabled = true;
    saveGoalBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Saving...';

    try {
      if (isEdit) {
        await apiRequest(`/goals/${goalId}`, {
          method: "PUT",
          body: JSON.stringify({
            title,
            targetValue: targetVal,
            targetDate,
            description: desc,
            isActive
          })
        });
        showToast("Goal updated successfully!", "success");
      } else {
        await apiRequest("/goals", {
          method: "POST",
          body: JSON.stringify({
            portfolioId: pId,
            goalType: type,
            title,
            targetValue: targetVal,
            targetDate,
            description: desc
          })
        });
        showToast("Goal created successfully!", "success");
      }

      const modal = bootstrap.Modal.getInstance(modalEl);
      modal?.hide();
      loadGoals();
    } catch (err) {
      console.error("Save goal error:", err);
      showToast(sanitizeErrorMessage(err.message, err.status), "danger");
    } finally {
      saveGoalBtn.disabled = false;
      saveGoalBtn.innerHTML = origBtnHtml;
    }
  });

  // Filter Event Listeners
  filterPortfolio?.addEventListener("change", loadGoals);
  filterStatus?.addEventListener("change", renderGoals);
  refreshBtn?.addEventListener("click", loadGoals);

  // Initial Load
  loadGoals();
}


// =========================================
// USER ACTIVITY TIMELINE (FEATURE 7)
// =========================================

let currentActivityPage = 1;

async function loadActivityTimeline(page = 1) {
  const container = document.getElementById("timelineContainer");
  if (!container) return;

  currentActivityPage = typeof page === "number" ? page : 1;

  const typeFilter = document.getElementById("activityTypeFilter");
  const dateFromInput = document.getElementById("activityDateFrom");
  const dateToInput = document.getElementById("activityDateTo");
  const pageSizeSelect = document.getElementById("activityPageSize");
  const subtitle = document.getElementById("activitySubtitle");
  const paginationRow = document.getElementById("activityPaginationRow");
  const paginationSummary = document.getElementById("activityPaginationSummary");
  const paginationButtons = document.getElementById("activityPaginationButtons");

  const pageSize = pageSizeSelect ? parseInt(pageSizeSelect.value, 10) || 25 : 25;
  const params = new URLSearchParams();
  params.append("page", currentActivityPage);
  params.append("pageSize", pageSize);

  if (typeFilter && typeFilter.value && typeFilter.value !== "ALL") {
    params.append("eventType", typeFilter.value);
  }
  if (dateFromInput && dateFromInput.value) {
    params.append("startDate", dateFromInput.value);
  }
  if (dateToInput && dateToInput.value) {
    params.append("endDate", dateToInput.value);
  }

  container.innerHTML = `
    <div class="text-center py-5 text-muted">
      <div class="spinner-border spinner-border-sm me-2" role="status"></div>
      Loading activity history...
    </div>
  `;

  try {
    const res = await apiRequest(`/activities?${params.toString()}`);
    const data = res.data || {};
    const items = data.items || [];
    const totalItems = data.totalItems ?? items.length;
    const totalPages = data.totalPages || 1;
    const currentPage = data.page || currentActivityPage;

    if (subtitle) {
      subtitle.textContent = `${totalItems} event${totalItems === 1 ? "" : "s"} recorded`;
    }

    if (!items.length) {
      container.innerHTML = `
        <div class="text-center py-5 text-muted">
          <i class="bi bi-clock-history fs-1 d-block mb-2 text-secondary"></i>
          <h5 class="fw-semibold mb-1">No Activity Found</h5>
          <p class="mb-0" style="font-size: 13px;">No events match your current filter criteria.</p>
        </div>
      `;
      if (paginationRow) paginationRow.style.display = "none";
      return;
    }

    // Group items by day label: Today, Yesterday, or formatted date
    const now = new Date();
    const todayStr = now.toISOString().split("T")[0];
    const yesterday = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    const yesterdayStr = yesterday.toISOString().split("T")[0];

    const groups = {};
    items.forEach(item => {
      const d = new Date(item.timestamp);
      const dateKey = isNaN(d.getTime()) ? "Other" : d.toISOString().split("T")[0];
      let groupLabel = dateKey;
      if (dateKey === todayStr) {
        groupLabel = "Today";
      } else if (dateKey === yesterdayStr) {
        groupLabel = "Yesterday";
      } else if (!isNaN(d.getTime())) {
        groupLabel = d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
      }

      if (!groups[groupLabel]) {
        groups[groupLabel] = [];
      }
      groups[groupLabel].push(item);
    });

    let html = '<div class="timeline-flow">';
    for (const [groupLabel, groupItems] of Object.entries(groups)) {
      html += `
        <div class="timeline-date-group">
          <div class="timeline-date-badge">
            <i class="bi bi-calendar3"></i> ${escapeHtml(groupLabel)}
          </div>
      `;

      groupItems.forEach(ev => {
        const d = new Date(ev.timestamp);
        const timeFormatted = isNaN(d.getTime())
          ? ""
          : d.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" });
        const icon = ev.icon || "bi-dot";
        const badgeClass = ev.badgeClass || "primary";

        html += `
          <div class="timeline-item">
            <div class="timeline-dot ${badgeClass}">
              <i class="bi ${escapeHtml(icon)}"></i>
            </div>
            <div class="timeline-card">
              <div class="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-1">
                <div class="d-flex align-items-center gap-2">
                  <span class="fw-bold text-body" style="font-size: 14px;">● ${escapeHtml(ev.title)}</span>
                </div>
                <span class="text-muted" style="font-size: 12px; font-variant-numeric: tabular-nums;">
                  <i class="bi bi-clock me-1"></i>${escapeHtml(timeFormatted)}
                </span>
              </div>
              <p class="mb-0 text-muted" style="font-size: 13px; line-height: 1.45;">
                ${escapeHtml(ev.description)}
              </p>
            </div>
          </div>
        `;
      });

      html += '</div>'; // End timeline-date-group
    }
    html += '</div>'; // End timeline-flow

    container.innerHTML = html;

    // Render pagination
    if (paginationRow) {
      paginationRow.style.display = totalPages > 1 || totalItems > pageSize ? "flex" : "none";
      const fromItem = (currentPage - 1) * pageSize + 1;
      const toItem = Math.min(currentPage * pageSize, totalItems);
      if (paginationSummary) {
        paginationSummary.textContent = `Showing ${fromItem}–${toItem} of ${totalItems} events`;
      }
      if (paginationButtons) {
        renderActivityPaginationButtons(paginationButtons, currentPage, totalPages);
      }
    }
  } catch (err) {
    console.error("Failed to load activity timeline:", err);
    container.innerHTML = `
      <div class="text-center py-5 text-danger">
        <i class="bi bi-exclamation-triangle fs-2 d-block mb-2"></i>
        <h6 class="fw-semibold">Error Loading Activity</h6>
        <p class="mb-0" style="font-size: 13px;">${escapeHtml(err.message)}</p>
      </div>
    `;
    if (paginationRow) paginationRow.style.display = "none";
  }
}

function renderActivityPaginationButtons(container, currentPage, totalPages) {
  if (totalPages <= 1) {
    container.innerHTML = `<button type="button" class="current-page">1</button>`;
    return;
  }

  let html = `
    <button type="button" ${currentPage <= 1 ? "disabled" : ""} onclick="loadActivityTimeline(${currentPage - 1})" aria-label="Previous page">
      <i class="bi bi-chevron-left"></i>
    </button>
  `;

  const maxButtons = 5;
  let startPage = Math.max(1, currentPage - Math.floor(maxButtons / 2));
  let endPage = Math.min(totalPages, startPage + maxButtons - 1);
  if (endPage - startPage + 1 < maxButtons) {
    startPage = Math.max(1, endPage - maxButtons + 1);
  }

  if (startPage > 1) {
    html += `<button type="button" onclick="loadActivityTimeline(1)">1</button>`;
    if (startPage > 2) {
      html += `<button type="button" disabled style="cursor: default;">...</button>`;
    }
  }

  for (let p = startPage; p <= endPage; p++) {
    if (p === currentPage) {
      html += `<button type="button" class="current-page" aria-current="page">${p}</button>`;
    } else {
      html += `<button type="button" onclick="loadActivityTimeline(${p})">${p}</button>`;
    }
  }

  if (endPage < totalPages) {
    if (endPage < totalPages - 1) {
      html += `<button type="button" disabled style="cursor: default;">...</button>`;
    }
    html += `<button type="button" onclick="loadActivityTimeline(${totalPages})">${totalPages}</button>`;
  }

  html += `
    <button type="button" ${currentPage >= totalPages ? "disabled" : ""} onclick="loadActivityTimeline(${currentPage + 1})" aria-label="Next page">
      <i class="bi bi-chevron-right"></i>
    </button>
  `;

  container.innerHTML = html;
}

function setupActivityTimeline() {
  const container = document.getElementById("timelineContainer");
  if (!container) return;

  const typeFilter = document.getElementById("activityTypeFilter");
  const dateFrom = document.getElementById("activityDateFrom");
  const dateTo = document.getElementById("activityDateTo");
  const pageSize = document.getElementById("activityPageSize");
  const applyBtn = document.getElementById("applyActivityFilterBtn");
  const resetBtn = document.getElementById("resetActivityFilterBtn");
  const refreshBtn = document.getElementById("refreshActivityBtn");

  applyBtn?.addEventListener("click", () => loadActivityTimeline(1));
  typeFilter?.addEventListener("change", () => loadActivityTimeline(1));
  pageSize?.addEventListener("change", () => loadActivityTimeline(1));
  refreshBtn?.addEventListener("click", () => loadActivityTimeline(currentActivityPage));

  resetBtn?.addEventListener("click", () => {
    if (typeFilter) typeFilter.value = "ALL";
    if (dateFrom) dateFrom.value = "";
    if (dateTo) dateTo.value = "";
    if (pageSize) pageSize.value = "25";
    loadActivityTimeline(1);
  });

  loadActivityTimeline(1);
}


// =========================================
// DHAKA STOCK EXCHANGE (DSE) MARKET DIRECTORY & ADD FROM ANY TAB
// =========================================

let _cachedDseMarketList = null;
let _loadingDseMarketList = null;

async function fetchDseMarketList(forceRefresh = false) {
  if (_cachedDseMarketList && !forceRefresh) return _cachedDseMarketList;
  if (_loadingDseMarketList) return _loadingDseMarketList;
  _loadingDseMarketList = (async () => {
    try {
      const res = await apiRequest("/companies/dse-market-list");
      if (res && res.success && Array.isArray(res.data)) {
        _cachedDseMarketList = res.data;
      } else {
        _cachedDseMarketList = [];
      }
    } catch (e) {
      console.warn("Failed to load DSE market list:", e);
      _cachedDseMarketList = [];
    } finally {
      _loadingDseMarketList = null;
    }
    return _cachedDseMarketList;
  })();
  return _loadingDseMarketList;
}
window.fetchDseMarketList = fetchDseMarketList;

window.quickAddDseCompany = async function(symbol, preferredName, preferredSector, event, targetSelectId) {
  if (event) {
    event.preventDefault();
    event.stopPropagation();
  }
  const btn = event?.currentTarget || (event?.target?.tagName === 'BUTTON' ? event.target : event?.target?.closest('button'));
  const originalHtml = btn ? btn.innerHTML : '';
  if (btn) {
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span> Adding...';
  }

  try {
    const res = await apiRequest("/companies/add-from-dse", {
      method: "POST",
      body: JSON.stringify({
        symbol: symbol,
        name: preferredName || symbol,
        sector: preferredSector || "Other"
      })
    });

    if (res && res.success && res.data) {
      const comp = res.data;
      if (typeof showToast === "function") {
        showToast("success", `${comp.tickerSymbol} (${comp.companyName}) added to ShareSync with live price ৳${Number(comp.currentPrice || 0).toFixed(2)}!`);
      }

      // Update cache
      if (_cachedDseMarketList) {
        const item = _cachedDseMarketList.find(x => x.symbol.toUpperCase() === symbol.toUpperCase());
        if (item) {
          item.isAdded = true;
          item.localCompanyId = comp.companyId;
          item.localPrice = comp.currentPrice;
        }
      }

      // Update button appearance
      if (btn) {
        btn.disabled = true;
        btn.className = "btn btn-sm btn-outline-success";
        btn.innerHTML = '<i class="bi bi-check2-circle me-1"></i> Added';
      }

      // Update select dropdowns across pages
      const selectIds = [targetSelectId, 'watchlistCompany', 'company', 'dividendCompany', 'alertCompany', 'simCompanySelect'].filter(Boolean);
      selectIds.forEach(id => {
        const sel = document.getElementById(id);
        if (sel) {
          let opt = sel.querySelector(`option[value="${comp.companyId}"]`);
          if (!opt) {
            opt = document.createElement('option');
            opt.value = comp.companyId;
            opt.textContent = `${comp.tickerSymbol} - ${comp.companyName}`;
            sel.appendChild(opt);
          }
          sel.value = comp.companyId;
          sel.dispatchEvent(new Event('change'));
        }
      });

      // If admin companies table exists, refresh it
      if (typeof loadAdminCompanies === "function") {
        loadAdminCompanies();
      }

      return comp;
    } else {
      throw new Error(res?.message || "Failed to add company from DSE.");
    }
  } catch (err) {
    console.error("Failed to add DSE company:", err);
    if (typeof showToast === "function") {
      showToast("error", err.message || "Failed to add company from DSE.");
    } else {
      alert("Error: " + err.message);
    }
    if (btn) {
      btn.disabled = false;
      btn.innerHTML = originalHtml;
    }
  }
};

window.openDseMarketModal = async function(options = {}) {
  let modalEl = document.getElementById("dseMarketModal");
  if (!modalEl) {
    const modalHtml = `
      <div class="modal fade" id="dseMarketModal" tabindex="-1" aria-labelledby="dseMarketModalTitle" aria-hidden="true">
        <div class="modal-dialog modal-lg modal-dialog-scrollable modal-dialog-centered">
          <div class="modal-content" style="background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; box-shadow: 0 20px 40px rgba(0,0,0,0.2);">
            <div class="modal-header border-bottom pb-3">
              <div>
                <h5 class="modal-title fw-bold d-flex align-items-center gap-2 mb-1" id="dseMarketModalTitle" style="color: var(--color-text);">
                  <i class="bi bi-bank text-primary"></i> Dhaka Stock Exchange (DSE) Directory
                </h5>
                <p class="text-muted mb-0" style="font-size: 13px;">Browse 400+ Dhaka Stock Exchange listed companies and add any stock into ShareSync.</p>
              </div>
              <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
            </div>
            <div class="modal-body p-3">
              <div class="row g-2 mb-3">
                <div class="col-12 col-md-7">
                  <div class="input-group input-group-sm">
                    <span class="input-group-text bg-body-tertiary border-end-0 text-muted"><i class="bi bi-search"></i></span>
                    <input type="text" id="dseModalSearchInput" class="form-control bg-body-tertiary border-start-0" placeholder="Search by symbol (e.g. RENATA, BATBC, GP) or name..." style="font-size: 13px;">
                  </div>
                </div>
                <div class="col-12 col-md-5">
                  <select id="dseModalSectorFilter" class="form-select form-select-sm" style="font-size: 13px;">
                    <option value="">All Sectors</option>
                  </select>
                </div>
              </div>
              <div id="dseModalListContainer" style="min-height: 250px; max-height: 460px; overflow-y: auto;">
                <div class="p-4 text-center text-muted">
                  <div class="spinner-border spinner-border-sm me-2 text-primary" role="status"></div>
                  Loading Dhaka Stock Exchange market directory...
                </div>
              </div>
            </div>
            <div class="modal-footer border-top d-flex justify-content-between py-2">
              <span class="text-muted" style="font-size: 12px;" id="dseModalCountText">Loading companies...</span>
              <button type="button" class="btn btn-secondary btn-sm" data-bs-dismiss="modal">Close</button>
            </div>
          </div>
        </div>
      </div>
    `;
    document.body.insertAdjacentHTML('beforeend', modalHtml);
    modalEl = document.getElementById("dseMarketModal");
  }

  modalEl._targetSelectId = options.targetSelectId || null;
  const bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
  bsModal.show();

  const container = document.getElementById("dseModalListContainer");
  const searchInput = document.getElementById("dseModalSearchInput");
  const sectorFilter = document.getElementById("dseModalSectorFilter");
  const countText = document.getElementById("dseModalCountText");

  searchInput.value = "";
  sectorFilter.value = "";

  const list = await fetchDseMarketList();

  if (sectorFilter.options.length <= 1) {
    const sectors = [...new Set(list.map(x => x.sector).filter(Boolean))].sort();
    sectors.forEach(sec => {
      const opt = document.createElement("option");
      opt.value = sec;
      opt.textContent = sec;
      sectorFilter.appendChild(opt);
    });
  }

  function renderFiltered() {
    const q = searchInput.value.trim().toLowerCase();
    const sec = sectorFilter.value;
    const filtered = list.filter(item => {
      const matchQ = !q || item.symbol.toLowerCase().includes(q) || (item.name && item.name.toLowerCase().includes(q));
      const matchSec = !sec || item.sector === sec;
      return matchQ && matchSec;
    });

    countText.textContent = `Showing ${filtered.length} of ${list.length} DSE companies`;

    if (filtered.length === 0) {
      container.innerHTML = `
        <div class="text-center py-5 text-muted">
          <i class="bi bi-search fs-3 mb-2 d-block"></i>
          No DSE companies match "${escapeHtml(q)}".
        </div>
      `;
      return;
    }

    let html = `<div class="d-flex flex-column gap-2">`;
    filtered.slice(0, 100).forEach(item => {
      const capText = item.marketCapMn ? `Cap: ৳${Number(item.marketCapMn).toLocaleString()}M` : '';
      const catBadge = item.category ? `<span class="badge bg-secondary-subtle text-secondary border px-1" style="font-size: 10px;">Cat: ${escapeHtml(item.category)}</span>` : '';
      
      html += `
        <div class="d-flex align-items-center justify-content-between p-2 rounded border bg-body-tertiary">
          <div style="min-width: 0; flex: 1;" class="me-2">
            <div class="d-flex align-items-center gap-2">
              <strong class="fs-6 text-primary">${escapeHtml(item.symbol)}</strong>
              ${catBadge}
              <span class="badge bg-info-subtle text-info border" style="font-size: 11px;">${escapeHtml(item.sector || 'General')}</span>
            </div>
            <div class="text-truncate text-body-secondary mt-1" style="font-size: 12px;" title="${escapeHtml(item.name || item.symbol)}">
              ${escapeHtml(item.name || item.symbol)}
              ${capText ? `<span class="text-muted ms-2" style="font-size: 11px;">• ${capText}</span>` : ''}
            </div>
          </div>
          <div>
            ${item.isAdded ? `
              <div class="d-flex align-items-center gap-2">
                <span class="badge bg-success-subtle text-success border border-success-subtle py-1 px-2" style="font-size: 11px;">
                  <i class="bi bi-check-circle-fill me-1"></i> Tracked
                </span>
                <a href="company.html?symbol=${encodeURIComponent(item.symbol)}" class="btn btn-outline-secondary btn-sm py-1 px-2" style="font-size: 11px;">
                  View
                </a>
              </div>
            ` : `
              <button class="btn btn-primary btn-sm py-1 px-3 d-flex align-items-center gap-1" style="font-size: 12px; font-weight: 500;" onclick="window.quickAddDseCompany('${escapeHtml(item.symbol)}', '${escapeHtml((item.name || item.symbol).replace(/'/g, "\\\'"))}', '${escapeHtml((item.sector || '').replace(/'/g, "\\\'"))}', event, '${modalEl._targetSelectId || ''}')">
                <i class="bi bi-plus-lg"></i> Add
              </button>
            `}
          </div>
        </div>
      `;
    });
    if (filtered.length > 100) {
      html += `<div class="text-center py-2 text-muted" style="font-size: 12px;">+ ${filtered.length - 100} more companies. Use the search box above to narrow down.</div>`;
    }
    html += `</div>`;
    container.innerHTML = html;
  }

  searchInput.oninput = renderFiltered;
  sectorFilter.onchange = renderFiltered;
  renderFiltered();
};

// =========================================
// GLOBAL SEARCH COMPONENT (FEATURE 8)
// =========================================

function setupGlobalSearch() {
  const searchInput = document.getElementById("globalSearchInput");
  const searchDropdown = document.getElementById("globalSearchDropdown");
  const searchResults = document.getElementById("globalSearchResults");
  const clearBtn = document.getElementById("clearGlobalSearchBtn");

  // Preload DSE Market list in background
  fetchDseMarketList().catch(() => {});

  // Add DSE Market button to topbar next to search container if present
  const searchContainer = document.querySelector(".global-search-container");
  if (searchContainer && !document.getElementById("topbarDseBtn")) {
    const topbarDseBtn = document.createElement("button");
    topbarDseBtn.id = "topbarDseBtn";
    topbarDseBtn.type = "button";
    topbarDseBtn.className = "btn btn-sm btn-outline-primary d-none d-lg-inline-flex align-items-center gap-1 ms-2";
    topbarDseBtn.style.cssText = "border-radius: 8px; font-size: 12px; font-weight: 500; white-space: nowrap;";
    topbarDseBtn.innerHTML = '<i class="bi bi-bank"></i> <span>DSE Market</span>';
    topbarDseBtn.title = "Explore & Add Dhaka Stock Exchange Companies";
    topbarDseBtn.onclick = () => window.openDseMarketModal();
    searchContainer.parentNode.insertBefore(topbarDseBtn, searchContainer.nextSibling);
  }

  if (!searchInput || !searchDropdown || !searchResults) return;

  let debounceTimer = null;
  let activeIndex = -1;

  function closeDropdown() {
    searchDropdown.classList.add("d-none");
    activeIndex = -1;
  }

  function openDropdown() {
    searchDropdown.classList.remove("d-none");
  }

  clearBtn?.addEventListener("click", () => {
    searchInput.value = "";
    clearBtn.classList.add("d-none");
    closeDropdown();
    searchInput.focus();
  });

  document.addEventListener("click", (e) => {
    if (!e.target.closest(".global-search-container")) {
      closeDropdown();
    }
  });

  searchInput.addEventListener("focus", () => {
    if (searchInput.value.trim().length > 0 && !searchDropdown.classList.contains("d-none")) {
      openDropdown();
    }
  });

  searchInput.addEventListener("input", () => {
    const q = searchInput.value.trim();

    if (q.length > 0) {
      clearBtn?.classList.remove("d-none");
    } else {
      clearBtn?.classList.add("d-none");
      closeDropdown();
      searchResults.innerHTML = "";
      return;
    }

    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      executeSearch(q);
    }, 280);
  });

  searchInput.addEventListener("keydown", (e) => {
    const items = searchResults.querySelectorAll(".search-result-item");
    if (!items.length) return;

    if (e.key === "ArrowDown") {
      e.preventDefault();
      activeIndex = (activeIndex + 1) % items.length;
      updateActiveItem(items);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      activeIndex = (activeIndex - 1 + items.length) % items.length;
      updateActiveItem(items);
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (activeIndex >= 0 && activeIndex < items.length) {
        items[activeIndex].click();
      } else if (items.length > 0) {
        items[0].click();
      }
    } else if (e.key === "Escape") {
      closeDropdown();
    }
  });

  function updateActiveItem(items) {
    items.forEach((item, idx) => {
      item.classList.toggle("active", idx === activeIndex);
      if (idx === activeIndex) {
        item.scrollIntoView({ block: "nearest" });
      }
    });
  }

  async function executeSearch(query) {
    openDropdown();
    searchResults.innerHTML = `
      <div class="p-3 text-center text-muted">
        <div class="spinner-border spinner-border-sm me-2" role="status"></div>
        Searching...
      </div>
    `;

    try {
      const res = await apiRequest(`/search?q=${encodeURIComponent(query)}&limit=5`);
      const data = res.data || {};
      const companies = data.companies || [];
      const sectors = data.sectors || [];
      const portfolios = data.portfolios || [];
      const watchlist = data.watchlist || [];
      const totalCount = data.totalCount ?? (companies.length + sectors.length + portfolios.length + watchlist.length);

      if (totalCount === 0) {
        searchResults.innerHTML = `
          <div class="p-3 text-center text-muted">
            <i class="bi bi-search me-1"></i> No matching results for "${escapeHtml(query)}"
          </div>
        `;
        return;
      }

      let html = "";

      // 1. Companies
      if (companies.length > 0) {
        html += '<div class="search-category-header"><i class="bi bi-building me-1"></i> Companies</div>';
        companies.forEach(c => {
          html += `
            <a href="${escapeHtml(c.url)}" class="search-result-item">
              <div>
                <div class="result-title">${escapeHtml(c.title)}</div>
                <div class="result-subtitle">${escapeHtml(c.subtitle)}</div>
              </div>
              <span class="badge bg-primary-subtle text-primary border border-primary-subtle result-badge">${escapeHtml(c.badge || "")}</span>
            </a>
          `;
        });
      }

      // 2. Sectors
      if (sectors.length > 0) {
        html += '<div class="search-category-header"><i class="bi bi-pie-chart me-1"></i> Sectors</div>';
        sectors.forEach(s => {
          html += `
            <a href="${escapeHtml(s.url)}" class="search-result-item">
              <div>
                <div class="result-title">${escapeHtml(s.title)}</div>
                <div class="result-subtitle">${escapeHtml(s.subtitle)}</div>
              </div>
              <span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle result-badge">Sector</span>
            </a>
          `;
        });
      }

      // 3. Portfolios
      if (portfolios.length > 0) {
        html += '<div class="search-category-header"><i class="bi bi-briefcase me-1"></i> Portfolios</div>';
        portfolios.forEach(p => {
          html += `
            <a href="${escapeHtml(p.url)}" class="search-result-item">
              <div>
                <div class="result-title">${escapeHtml(p.title)}</div>
                <div class="result-subtitle">${escapeHtml(p.subtitle)}</div>
              </div>
              <span class="badge bg-success-subtle text-success border border-success-subtle result-badge">Portfolio</span>
            </a>
          `;
        });
      }

      // 4. Watchlist
      if (watchlist.length > 0) {
        html += '<div class="search-category-header"><i class="bi bi-bookmark me-1"></i> Watchlist</div>';
        watchlist.forEach(w => {
          html += `
            <a href="${escapeHtml(w.url)}" class="search-result-item">
              <div>
                <div class="result-title">${escapeHtml(w.title)}</div>
                <div class="result-subtitle">${escapeHtml(w.subtitle)}</div>
              </div>
              <span class="badge bg-info-subtle text-info border border-info-subtle result-badge">${escapeHtml(w.badge || "Watchlist")}</span>
            </a>
          `;
        });
      }

      // 5. Dhaka Stock Exchange Market Search (Add any company from DSE)
      try {
        const dseList = await fetchDseMarketList();
        const qUpper = query.trim().toUpperCase();
        const dseMatches = dseList.filter(d => 
          (d.symbol.toUpperCase().includes(qUpper) || (d.name && d.name.toUpperCase().includes(qUpper))) &&
          !companies.some(c => c.badge === d.symbol || c.title.includes(d.symbol))
        ).slice(0, 4);

        if (dseMatches.length > 0) {
          html += '<div class="search-category-header d-flex justify-content-between align-items-center"><span><i class="bi bi-bank text-primary me-1"></i> Dhaka Stock Exchange (Live API)</span><span class="badge bg-primary-subtle text-primary border" style="font-size: 10px;">Market Feed</span></div>';
          dseMatches.forEach(dm => {
            html += `
              <div class="search-result-item d-flex align-items-center justify-content-between p-2">
                <div style="min-width: 0; flex: 1;">
                  <div class="result-title fw-bold text-primary">${escapeHtml(dm.symbol)} <span class="badge bg-secondary-subtle text-secondary" style="font-size: 10px;">${escapeHtml(dm.sector || 'DSE')}</span></div>
                  <div class="result-subtitle text-truncate" style="font-size: 11px;">${escapeHtml(dm.name || dm.symbol)}</div>
                </div>
                ${dm.isAdded ? `
                  <a href="company.html?symbol=${encodeURIComponent(dm.symbol)}" class="btn btn-outline-secondary btn-sm py-1 px-2 ms-2" style="font-size: 11px;">View</a>
                ` : `
                  <button class="btn btn-sm btn-primary py-1 px-2 ms-2 d-flex align-items-center gap-1" onclick="window.quickAddDseCompany('${escapeHtml(dm.symbol)}', '${escapeHtml((dm.name || dm.symbol).replace(/'/g, "\\\'"))}', '${escapeHtml((dm.sector || '').replace(/'/g, "\\\'"))}', event)" style="font-size: 11px;">
                    <i class="bi bi-plus-lg"></i> Add
                  </button>
                `}
              </div>
            `;
          });
        }
      } catch (dseErr) {
        console.warn("DSE search match error:", dseErr);
      }

      // Always show link to browse all DSE companies
      html += `
        <div class="p-2 border-top text-center bg-body-tertiary">
          <a href="javascript:void(0)" onclick="window.openDseMarketModal()" class="text-decoration-none fw-medium text-primary" style="font-size: 12px;">
            <i class="bi bi-grid-3x3-gap me-1"></i> Browse & Add from 400+ Dhaka Stock Exchange Companies &rarr;
          </a>
        </div>
      `;

      searchResults.innerHTML = html;
      activeIndex = -1;
    } catch (err) {
      console.error("Global search failed:", err);
      searchResults.innerHTML = `
        <div class="p-3 text-center text-danger">
          <i class="bi bi-exclamation-triangle me-1"></i> Search failed: ${escapeHtml(err.message)}
        </div>
      `;
    }
  }
}
