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

});

// =========================================
// TRANSACTION FORM
// =========================================

function setupTransactionForm() {

    const form = document.getElementById("transactionForm");

    if (!form) {
        return;
    }


    const formCard =
        document.getElementById("transactionFormCard");

    const openButton =
        document.getElementById("openTransactionForm");

    const closeButton =
        document.getElementById("closeTransactionForm");

    const cancelButton =
        document.getElementById("cancelTransaction");


    // -----------------------------------------
    // Open form
    // -----------------------------------------

    if (openButton) {

        openButton.addEventListener("click", () => {

            formCard.classList.remove("form-hidden");

            formCard.scrollIntoView({
                behavior: "smooth",
                block: "start"
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

        closeButton.addEventListener(
            "click",
            closeForm
        );

    }


    if (cancelButton) {

        cancelButton.addEventListener(
            "click",
            closeForm
        );

    }


    // -----------------------------------------
    // Buy / Sell buttons
    // -----------------------------------------

    const typeButtons =
        document.querySelectorAll(".type-button");

    const typeInput =
        document.getElementById("transactionType");


    typeButtons.forEach((button) => {

        button.addEventListener("click", () => {

            typeButtons.forEach((item) => {

                item.classList.remove("active");

            });


            button.classList.add("active");


            typeInput.value =
                button.dataset.type;


            console.log(
                "Transaction type:",
                typeInput.value
            );

        });

    });


    // -----------------------------------------
    // Form submission
    // -----------------------------------------

    form.addEventListener("submit", (event) => {

        event.preventDefault();


        const transactionType =
            typeInput.value;

        const company =
            document.getElementById("company").value;

        const quantity =
            document.getElementById("quantity").value;

        const price =
            document.getElementById("price").value;


        if (!company || !quantity || !price) {

            alert(
                "Please complete the required fields."
            );

            return;

        }


        const total =
            Number(quantity) * Number(price);


        alert(
            `${transactionType} transaction ready.\n\n` +
            `Company: ${company}\n` +
            `Quantity: ${quantity}\n` +
            `Price: ৳${Number(price).toFixed(2)}\n` +
            `Total: ৳${total.toLocaleString()}`
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
