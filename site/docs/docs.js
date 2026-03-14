document.addEventListener("DOMContentLoaded", () => {
    const body = document.body;
    const toggleButtons = document.querySelectorAll("[data-sidebar-toggle]");

    function syncButtons()
    {
        const isCollapsed = body.classList.contains("sidebar-collapsed");
        toggleButtons.forEach((button) =>
        {
            button.setAttribute("aria-expanded", (!isCollapsed).toString());
            button.textContent = isCollapsed ? "Show navigation" : "Hide navigation";
        });
    }

    toggleButtons.forEach((button) =>
    {
        button.addEventListener("click", () =>
        {
            body.classList.toggle("sidebar-collapsed");
            syncButtons();
        });
    });

    syncButtons();
});
