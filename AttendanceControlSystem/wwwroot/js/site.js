(function () {
    "use strict";

    function setupMenu() {
        var toggle = document.querySelector(".menu-toggle");
        var navigation = document.getElementById("primary-navigation");

        if (!toggle || !navigation) {
            return;
        }

        toggle.addEventListener("click", function () {
            var isOpen = navigation.classList.toggle("is-open");
            toggle.setAttribute("aria-expanded", isOpen ? "true" : "false");
        });
    }

    function setupActiveNavigation() {
        var currentPath = window.location.pathname.replace(/\/+$/, "").toLowerCase();

        document.querySelectorAll(".nav-link").forEach(function (link) {
            var linkPath = link.pathname.replace(/\/+$/, "").toLowerCase();

            if (currentPath === linkPath) {
                link.classList.add("active");
                link.setAttribute("aria-current", "page");
            } else {
                link.classList.remove("active");
                link.removeAttribute("aria-current");
            }
        });
    }

    function setupConfirmations() {
        document.querySelectorAll("form[data-confirm], form[onsubmit*=\"confirm\"]").forEach(function (form) {
            form.addEventListener("submit", function (event) {
                var message = form.getAttribute("data-confirm") || "آیا مطمئن هستید؟";

                if (!window.confirm(message)) {
                    event.preventDefault();
                }
            });
        });
    }

    function closeMenuOnNavigation() {
        var toggle = document.querySelector(".menu-toggle");
        var navigation = document.getElementById("primary-navigation");

        if (!toggle || !navigation) {
            return;
        }

        navigation.querySelectorAll("a").forEach(function (link) {
            link.addEventListener("click", function () {
                navigation.classList.remove("is-open");
                toggle.setAttribute("aria-expanded", "false");
            });
        });
    }

    function initialize() {
        setupMenu();
        setupActiveNavigation();
        setupConfirmations();
        closeMenuOnNavigation();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize);
    } else {
        initialize();
    }
})();
