document.addEventListener("DOMContentLoaded", () => {
    const toggleButtons = document.querySelectorAll("[data-tree-toggle]");

    toggleButtons.forEach((button) => {
        const targetId = button.getAttribute("aria-controls");
        if (!targetId) {
            return;
        }

        const target = document.getElementById(targetId);
        if (!target) {
            return;
        }

        button.addEventListener("click", () => {
            const isExpanded = button.getAttribute("aria-expanded") === "true";
            button.setAttribute("aria-expanded", (!isExpanded).toString());
            target.hidden = isExpanded;
        });
    });
});
