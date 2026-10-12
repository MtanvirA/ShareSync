/**
 * ShareSync Learning Center - Simplified & Clean Client-Side Script
 * File: frontend/js/learning-center.js
 * FEATURE: Learning Center
 * 
 * Provides instant search filtering across market terms, portfolio concepts,
 * and formula cards, plus expand/collapse all technical details controls.
 * Zero network requests - 100% offline and beginner-friendly.
 */

(function () {
  "use strict";

  document.addEventListener("DOMContentLoaded", function () {
    // --- 1. Global / Term Search Filtering ---
    const searchInput = document.getElementById("learnSearchInput");
    const clearSearchBtn = document.getElementById("clearLearnSearchBtn");
    const countBadge = document.getElementById("learnCountBadge");
    const filterPills = document.querySelectorAll(".filter-btn-pill");

    let activeCategory = "all";
    let activeQuery = "";

    function filterItems() {
      const q = activeQuery.toLowerCase().trim();
      const searchableCards = document.querySelectorAll("[data-searchable='true']");
      let visibleCount = 0;

      searchableCards.forEach((card) => {
        const cat = card.getAttribute("data-category") || "";
        const matchesCategory = activeCategory === "all" || cat === activeCategory;

        if (!matchesCategory) {
          card.classList.add("d-none");
          return;
        }

        if (!q) {
          card.classList.remove("d-none");
          visibleCount++;
          return;
        }

        const text = card.textContent.toLowerCase();
        if (text.includes(q)) {
          card.classList.remove("d-none");
          visibleCount++;
        } else {
          card.classList.add("d-none");
        }
      });

      if (countBadge) {
        countBadge.textContent = `${visibleCount} items visible`;
      }

      // Check section visibility for empty states
      ["market-terms-container", "portfolio-concepts-container", "formulas-container"].forEach((id) => {
        const container = document.getElementById(id);
        if (container) {
          const visibleInSec = container.querySelectorAll("[data-searchable='true']:not(.d-none)").length;
          const emptyNotice = container.querySelector(".section-empty-notice");
          if (emptyNotice) {
            emptyNotice.classList.toggle("d-none", visibleInSec > 0);
          }
        }
      });
    }

    if (searchInput) {
      searchInput.addEventListener("input", function () {
        activeQuery = this.value;
        if (clearSearchBtn) {
          clearSearchBtn.classList.toggle("d-none", !activeQuery);
        }
        filterItems();
      });
    }

    if (clearSearchBtn) {
      clearSearchBtn.addEventListener("click", function () {
        if (searchInput) {
          searchInput.value = "";
          activeQuery = "";
          searchInput.focus();
        }
        this.classList.add("d-none");
        filterItems();
      });
    }

    // Category filter pills
    filterPills.forEach((btn) => {
      btn.addEventListener("click", function () {
        filterPills.forEach((b) => b.classList.remove("active"));
        this.classList.add("active");
        activeCategory = this.getAttribute("data-category") || "all";
        filterItems();
      });
    });

    // --- 2. Expand / Collapse All Technical Details ---
    const toggleAllTechBtn = document.getElementById("toggleAllTechBtn");
    let allTechExpanded = false;

    if (toggleAllTechBtn) {
      toggleAllTechBtn.addEventListener("click", function () {
        allTechExpanded = !allTechExpanded;
        this.innerHTML = allTechExpanded
          ? '<i class="bi bi-chevron-up me-1"></i>Hide Technical Details'
          : '<i class="bi bi-chevron-down me-1"></i>Show All Technical Details';

        document.querySelectorAll(".tech-details-collapse details").forEach((det) => {
          det.open = allTechExpanded;
        });
      });
    }

    // Initialize counts
    filterItems();
  });
})();
