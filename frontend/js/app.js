// =========================================
// SHARESYNC
// Main JavaScript file
// =========================================


document.addEventListener("DOMContentLoaded", () => {

    console.log("ShareSync dashboard loaded.");

    setupSidebarNavigation();

    setupTransactionButton();

});


// =========================================
// SIDEBAR NAVIGATION
// =========================================

function setupSidebarNavigation() {

    const links = document.querySelectorAll(".sidebar-link");

    links.forEach((link) => {

        link.addEventListener("click", (event) => {

            event.preventDefault();

            links.forEach((item) => {
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

        alert(
            "Transaction form will be connected here in the next stage."
        );

    });

}