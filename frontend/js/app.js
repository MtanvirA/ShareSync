// =========================================
// SHARESYNC
// Main JavaScript file
// =========================================

document.addEventListener("DOMContentLoaded", () => {
  console.log("ShareSync dashboard loaded.");

  setupSidebarNavigation();

  setupTransactionButton();

  createPortfolioChart();

  setupChartPeriod();

  setupTransactionForm();

  setupWatchlistForm();

  setupDividendForm();

  setupReports();

});


// =========================================
// DIVIDEND FORM
// =========================================

function setupDividendForm() {

    const form =
        document.getElementById("dividendForm");

    if (!form) {
        return;
    }

    const formCard =
        document.getElementById("dividendFormCard");

    const openButton =
        document.getElementById("openDividendForm");

    const closeButton =
        document.getElementById("closeDividendForm");

    const cancelButton =
        document.getElementById("cancelDividend");


    openButton.addEventListener("click", () => {

        formCard.classList.remove("form-hidden");

        formCard.scrollIntoView({
            behavior: "smooth",
            block: "start"
        });

    });


    function closeForm() {

        formCard.classList.add("form-hidden");

    }


    closeButton.addEventListener(
        "click",
        closeForm
    );

    cancelButton.addEventListener(
        "click",
        closeForm
    );


    form.addEventListener("submit", (event) => {

        event.preventDefault();


        const company =
            document.getElementById(
                "dividendCompany"
            ).value;

        const amount =
            document.getElementById(
                "dividendPerShare"
            ).value;

        const declaration =
            document.getElementById(
                "declarationDate"
            ).value;

        const payment =
            document.getElementById(
                "paymentDate"
            ).value;


        if (!company || !amount ||
            !declaration || !payment) {

            alert("Please complete all fields.");

            return;
        }


        if (payment < declaration) {

            alert(
                "Payment date cannot be earlier than the declaration date."
            );

            return;
        }


        alert(
            `${company} dividend recorded successfully.\n` +
            `Dividend per share: ৳${Number(amount).toFixed(2)}`
        );


        form.reset();

        closeForm();

    });

}

// =========================================
// WATCHLIST FORM
// =========================================

function setupWatchlistForm() {
  const form = document.getElementById("watchlistForm");

  if (!form) {
    return;
  }

  const formCard = document.getElementById("watchlistFormCard");

  const openButton = document.getElementById("openWatchlistForm");

  const closeButton = document.getElementById("closeWatchlistForm");

  const cancelButton = document.getElementById("cancelWatchlist");

  // -----------------------------------------
  // Open form
  // -----------------------------------------

  openButton.addEventListener("click", () => {
    formCard.classList.remove("form-hidden");

    formCard.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  });

  // -----------------------------------------
  // Close form
  // -----------------------------------------

  function closeForm() {
    formCard.classList.add("form-hidden");
  }

  closeButton.addEventListener("click", closeForm);

  cancelButton.addEventListener("click", closeForm);

  // -----------------------------------------
  // Submit
  // -----------------------------------------

  form.addEventListener("submit", (event) => {
    event.preventDefault();

    const company = document.getElementById("watchlistCompany").value;

    const targetPrice = document.getElementById("targetPrice").value;

    if (!company) {
      alert("Please select a company.");

      return;
    }

    let message = `${company} is ready to be added to your watchlist.`;

    if (targetPrice) {
      message += `\nTarget price: ৳${Number(targetPrice).toFixed(2)}`;
    }

    alert(message);

    form.reset();

    closeForm();
  });

  // -----------------------------------------
  // Remove buttons
  // -----------------------------------------

  const removeButtons = document.querySelectorAll(".table-action-button");

  removeButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const confirmed = confirm("Remove this company from your watchlist?");

      if (confirmed) {
        const row = button.closest("tr");

        if (row) {
          row.remove();
        }
      }
    });
  });
}

// =========================================
// TRANSACTION FORM
// =========================================

function setupTransactionForm() {
  const form = document.getElementById("transactionForm");

  if (!form) {
    return;
  }

  const formCard = document.getElementById("transactionFormCard");

  const openButton = document.getElementById("openTransactionForm");

  const closeButton = document.getElementById("closeTransactionForm");

  const cancelButton = document.getElementById("cancelTransaction");

  // -----------------------------------------
  // Open form
  // -----------------------------------------

  if (openButton) {
    openButton.addEventListener("click", () => {
      formCard.classList.remove("form-hidden");

      formCard.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    });
  }

  // -----------------------------------------
  // Close form
  // -----------------------------------------

  function closeForm() {
    formCard.classList.add("form-hidden");
  }

  if (closeButton) {
    closeButton.addEventListener("click", closeForm);
  }

  if (cancelButton) {
    cancelButton.addEventListener("click", closeForm);
  }

  // -----------------------------------------
  // Buy / Sell buttons
  // -----------------------------------------

  const typeButtons = document.querySelectorAll(".type-button");

  const typeInput = document.getElementById("transactionType");

  typeButtons.forEach((button) => {
    button.addEventListener("click", () => {
      typeButtons.forEach((item) => {
        item.classList.remove("active");
      });

      button.classList.add("active");

      typeInput.value = button.dataset.type;

      console.log("Transaction type:", typeInput.value);
    });
  });

  // -----------------------------------------
  // Form submission
  // -----------------------------------------

  form.addEventListener("submit", (event) => {
    event.preventDefault();

    const transactionType = typeInput.value;

    const company = document.getElementById("company").value;

    const quantity = document.getElementById("quantity").value;

    const price = document.getElementById("price").value;

    if (!company || !quantity || !price) {
      alert("Please complete the required fields.");

      return;
    }

    const total = Number(quantity) * Number(price);

    alert(
      `${transactionType} transaction ready.\n\n` +
        `Company: ${company}\n` +
        `Quantity: ${quantity}\n` +
        `Price: ৳${Number(price).toFixed(2)}\n` +
        `Total: ৳${total.toLocaleString()}`,
    );

    form.reset();

    typeInput.value = "BUY";

    typeButtons.forEach((button) => {
      button.classList.remove("active");
    });

    typeButtons[0].classList.add("active");
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
    console.log("Selected period:", selector.value);
  });
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
// TRANSACTION BUTTON
// =========================================

function setupTransactionButton() {
  const button = document.querySelector(".primary-button");

  if (!button) {
    return;
  }

  button.addEventListener("click", () => {
    alert("Transaction form will be connected here in the next stage.");
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

  new Chart(ctx, {
    type: "line",

    data: {
      labels: ["Apr", "May", "Jun", "Jul", "Aug", "Sep"],

      datasets: [
        {
          label: "Portfolio Value",

          data: [82000, 91000, 88000, 104000, 116000, 125400],

          borderWidth: 2,

          pointRadius: 3,

          pointHoverRadius: 5,

          tension: 0.35,

          fill: false,
        },
      ],
    },

    options: {
      responsive: true,

      maintainAspectRatio: false,

      plugins: {
        legend: {
          display: false,
        },

        tooltip: {
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

          ticks: {
            display: false,
          },
        },

        y: {
          beginAtZero: false,

          grid: {
            color: "#eef0f3",
          },

          ticks: {
            display: false,
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

    const portfolioChart =
        document.getElementById("portfolioValueReport");

    if (!portfolioChart) {
        return;
    }


    // -----------------------------------------
    // Portfolio value chart
    // -----------------------------------------

    new Chart(portfolioChart, {

        type: "line",

        data: {

            labels: [
                "Apr",
                "May",
                "Jun",
                "Jul",
                "Aug",
                "Sep"
            ],

            datasets: [{

                label: "Portfolio Value",

                data: [
                    132000,
                    141500,
                    149800,
                    158600,
                    171200,
                    186450
                ],

                borderWidth: 2,

                pointRadius: 3,

                pointHoverRadius: 5,

                tension: 0.35,

                fill: true

            }]

        },

        options: {

            responsive: true,

            maintainAspectRatio: false,

            plugins: {

                legend: {
                    display: false
                }

            },

            scales: {

                y: {

                    beginAtZero: false,

                    ticks: {

                        callback: function(value) {

                            return "৳" +
                                Number(value)
                                    .toLocaleString();

                        }

                    }

                },

                x: {

                    grid: {
                        display: false
                    }

                }

            }

        }

    });


    // -----------------------------------------
    // Asset allocation
    // -----------------------------------------

    const allocationChart =
        document.getElementById("allocationChart");


    if (allocationChart) {

        new Chart(allocationChart, {

            type: "doughnut",

            data: {

                labels: [
                    "Grameenphone",
                    "BEXIMCO",
                    "BAT Bangladesh",
                    "Square Pharma"
                ],

                datasets: [{

                    data: [
                        32,
                        26,
                        24,
                        18
                    ],

                    borderWidth: 0

                }]

            },

            options: {

                responsive: true,

                maintainAspectRatio: false,

                cutout: "68%",

                plugins: {

                    legend: {
                        display: false
                    }

                }

            }

        });

    }


    // -----------------------------------------
    // Transaction activity
    // -----------------------------------------

    const transactionChart =
        document.getElementById(
            "transactionActivityChart"
        );


    if (transactionChart) {

        new Chart(transactionChart, {

            type: "bar",

            data: {

                labels: [
                    "Apr",
                    "May",
                    "Jun",
                    "Jul",
                    "Aug",
                    "Sep"
                ],

                datasets: [

                    {

                        label: "BUY",

                        data: [
                            4,
                            3,
                            5,
                            2,
                            6,
                            4
                        ],

                        borderWidth: 0

                    },

                    {

                        label: "SELL",

                        data: [
                            1,
                            2,
                            1,
                            3,
                            1,
                            2
                        ],

                        borderWidth: 0

                    }

                ]

            },

            options: {

                responsive: true,

                maintainAspectRatio: false,

                plugins: {

                    legend: {

                        position: "bottom"

                    }

                },

                scales: {

                    y: {

                        beginAtZero: true,

                        ticks: {

                            stepSize: 1

                        }

                    },

                    x: {

                        grid: {
                            display: false
                        }

                    }

                }

            }

        });

    }

}