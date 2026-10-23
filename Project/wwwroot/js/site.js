// FabriTrack — client-side UX enhancements
(function () {
    "use strict";

    /* ---------------- Sidebar toggle (mobile) ---------------- */
    var sidebar = document.getElementById("sidebar");
    var backdrop = document.getElementById("sidebarBackdrop");
    var toggleBtn = document.getElementById("sidebarToggle");

    function closeSidebar() {
        if (sidebar) sidebar.classList.remove("show");
        if (backdrop) backdrop.classList.remove("show");
    }

    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener("click", function () {
            sidebar.classList.toggle("show");
            if (backdrop) backdrop.classList.toggle("show");
        });
    }
    if (backdrop) backdrop.addEventListener("click", closeSidebar);

    /* ---------------- Top loading bar + button spinners ---------------- */
    var loadingBar = document.getElementById("loadingBar");

    function showLoadingBar() {
        if (loadingBar) loadingBar.classList.add("active");
    }

    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (form.tagName !== "FORM" || form.dataset.noSpinner) return;
        showLoadingBar();
        var submitBtn = form.querySelector('button[type="submit"]');
        if (submitBtn && !submitBtn.classList.contains("is-loading")) {
            submitBtn.classList.add("is-loading");
            submitBtn.dataset.originalHtml = submitBtn.innerHTML;
            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>' + submitBtn.textContent.trim();
        }
    });

    document.querySelectorAll("a.nav-link, .sidebar a").forEach(function (a) {
        a.addEventListener("click", function () {
            if (a.getAttribute("href") && !a.getAttribute("href").startsWith("#")) showLoadingBar();
        });
    });

    /* ---------------- Toast notifications ---------------- */
    var toastStack = document.getElementById("toastStack");

    function showToast(message, type) {
        if (!toastStack || !message) return;
        var isError = type === "danger";
        var el = document.createElement("div");
        el.className = "toast align-items-center text-bg-" + (isError ? "light" : "light") + " border-0 " + (isError ? "border-danger" : "border-success");
        el.setAttribute("role", "alert");
        el.setAttribute("aria-live", "assertive");
        el.setAttribute("aria-atomic", "true");
        el.innerHTML =
            '<div class="d-flex">' +
            '<div class="toast-body"><i class="bi ' + (isError ? "bi-x-circle text-danger" : "bi-check-circle text-success") + ' me-2"></i>' + message + "</div>" +
            '<button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>' +
            "</div>";
        toastStack.appendChild(el);
        var toast = new bootstrap.Toast(el, { delay: 4500 });
        toast.show();
        el.addEventListener("hidden.bs.toast", function () { el.remove(); });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var successData = document.getElementById("toast-success-data");
        var errorData = document.getElementById("toast-error-data");
        if (successData) showToast(successData.dataset.toastMessage, "success");
        if (errorData) showToast(errorData.dataset.toastMessage, "danger");
    });

    /* ---------------- Theme toggle (light/dark) ---------------- */
    function applyTheme(theme) {
        try {
            if (!theme) theme = 'light';
            document.documentElement.setAttribute('data-bs-theme', theme);
            localStorage.setItem('ft-theme', theme);
            var btn = document.getElementById('themeToggle');
            if (btn) {
                var icon = btn.querySelector('i');
                if (icon) icon.className = (theme === 'dark') ? 'bi bi-sun' : 'bi bi-moon';
            }
        } catch (e) { /* ignore */ }
    }

    document.addEventListener('DOMContentLoaded', function () {
        var saved = localStorage.getItem('ft-theme');
        if (!saved) {
            // follow system preference
            var prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
            saved = prefersDark ? 'dark' : 'light';
        }
        applyTheme(saved);

        var themeBtn = document.getElementById('themeToggle');
        if (themeBtn) {
            themeBtn.addEventListener('click', function () {
                var current = document.documentElement.getAttribute('data-bs-theme') || 'light';
                var next = (current === 'dark') ? 'light' : 'dark';
                applyTheme(next);
            });
        }
    });

    /* ---------------- Page load micro-interaction ---------------- */
    document.addEventListener('DOMContentLoaded', function () {
        if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        document.body.classList.add('ft-loaded');
    });

    /* ---------------- Delete confirmation modal ---------------- */
    var deleteModalEl = document.getElementById("deleteModal");
    if (deleteModalEl) {
        deleteModalEl.addEventListener("show.bs.modal", function (event) {
            var trigger = event.relatedTarget;
            if (!trigger) return;
            var action = trigger.getAttribute("data-form-action");
            var name = trigger.getAttribute("data-item-name") || "this item";
            var form = document.getElementById("deleteModalForm");
            var nameEl = document.getElementById("deleteModalItemName");
            if (form && action) form.setAttribute("action", action);
            if (nameEl) nameEl.textContent = name;
        });
    }

    /* ---------------- Restock modal ---------------- */
    var restockModalEl = document.getElementById("restockModal");
    if (restockModalEl) {
        restockModalEl.addEventListener("show.bs.modal", function (event) {
            var trigger = event.relatedTarget;
            if (!trigger) return;
            var id = trigger.getAttribute("data-fabric-id");
            var name = trigger.getAttribute("data-item-name") || "item";
            var stock = trigger.getAttribute("data-current-stock") || "—";
            document.getElementById("restockModalFabricId").value = id;
            document.getElementById("restockModalItemName").textContent = name;
            document.getElementById("restockModalCurrentStock").textContent = stock;
            var qtyInput = document.getElementById("restockModalQuantity");
            qtyInput.value = "";
        });
    }

    /* ---------------- jQuery unobtrusive validation styling ---------------- */
    if (window.jQuery) {
        jQuery.validator && jQuery.validator.setDefaults({
            highlight: function (element) {
                jQuery(element).addClass("is-invalid").removeClass("is-valid");
            },
            unhighlight: function (element) {
                jQuery(element).addClass("is-valid").removeClass("is-invalid");
            }
        });
    }

    /* ---------------- Generic table enhancer: search, sort, paginate ----------------
       Apply to any <table class="data-table"> with a <thead> and <tbody>.
       Optional per-column sort: add data-sort on the <th>.
       Optional: wrap the table with data-page-size="10" to change page size.
    ------------------------------------------------------------------------------- */
    function enhanceTable(table) {
        var tbody = table.tBodies[0];
        if (!tbody) return;
        var allRows = Array.prototype.slice.call(tbody.rows);
        if (allRows.length === 0) return;

        var pageSize = parseInt(table.getAttribute("data-page-size") || "8", 10);
        var state = { query: "", sortIndex: -1, sortDir: 1, page: 1 };

        // Toolbar (search)
        var toolbar = document.createElement("div");
        toolbar.className = "table-toolbar";
        var searchWrap = document.createElement("div");
        searchWrap.className = "input-group table-search";
        searchWrap.innerHTML = '<span class="input-group-text"><i class="bi bi-search"></i></span>' +
            '<input type="search" class="form-control" placeholder="Search table..." aria-label="Search table">' +
            '<button class="btn btn-outline-secondary d-none d-md-inline" type="button" title="Clear search" aria-label="Clear search"><i class="bi bi-x-lg"></i></button>';
        toolbar.appendChild(searchWrap);
        table.parentNode.insertBefore(toolbar, table);
        var searchInput = searchWrap.querySelector("input");

        // Pagination footer
        var pager = document.createElement("div");
        pager.className = "table-pagination";
        table.parentNode.insertBefore(pager, table.nextSibling);

        // Sortable headers
        var headers = Array.prototype.slice.call(table.tHead ? table.tHead.rows[0].cells : []);
        headers.forEach(function (th, idx) {
            if (th.hasAttribute("data-sort")) {
                th.addEventListener("click", function () {
                    if (state.sortIndex === idx) {
                        state.sortDir *= -1;
                    } else {
                        state.sortIndex = idx;
                        state.sortDir = 1;
                    }
                    headers.forEach(function (h) { h.querySelectorAll(".bi").forEach(function (i) { i.remove(); }); });
                    var icon = document.createElement("i");
                    icon.className = "bi " + (state.sortDir === 1 ? "bi-caret-up-fill" : "bi-caret-down-fill");
                    th.appendChild(icon);
                    render();
                });
            }
        });

        function getFilteredSorted() {
            var q = state.query.trim().toLowerCase();
            var rows = allRows.filter(function (r) {
                return !q || r.textContent.toLowerCase().indexOf(q) !== -1;
            });
            if (state.sortIndex >= 0) {
                rows.sort(function (a, b) {
                    var aCell = (a.cells[state.sortIndex] ? a.cells[state.sortIndex] : null);
                    var bCell = (b.cells[state.sortIndex] ? b.cells[state.sortIndex] : null);
                    var av = aCell ? (aCell.getAttribute('data-sort-value') || aCell.textContent).trim() : "";
                    var bv = bCell ? (bCell.getAttribute('data-sort-value') || bCell.textContent).trim() : "";
                    var an = parseFloat(av.replace(/[^0-9.\-]/g, ""));
                    var bn = parseFloat(bv.replace(/[^0-9.\-]/g, ""));
                    var cmp;
                    if (!isNaN(an) && !isNaN(bn) && av.match(/[0-9]/) && bv.match(/[0-9]/)) {
                        cmp = an - bn;
                    } else {
                        cmp = av.localeCompare(bv);
                    }
                    return cmp * state.sortDir;
                });
            }
            return rows;
        }

        function render() {
            var rows = getFilteredSorted();
            var totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
            if (state.page > totalPages) state.page = totalPages;

            allRows.forEach(function (r) { r.style.display = "none"; });

            var start = (state.page - 1) * pageSize;
            var pageRows = rows.slice(start, start + pageSize);
            pageRows.forEach(function (r) { tbody.appendChild(r); r.style.display = ""; });

            // Empty state
            var emptyRow = tbody.querySelector(".table-empty-row");
            if (rows.length === 0) {
                if (!emptyRow) {
                    emptyRow = document.createElement("tr");
                    emptyRow.className = "table-empty-row";
                    var td = document.createElement("td");
                    td.colSpan = (table.tHead && table.tHead.rows[0]) ? table.tHead.rows[0].cells.length : 1;
                    td.innerHTML = '<i class="bi bi-inbox me-1"></i> No matching results';
                    emptyRow.appendChild(td);
                    tbody.appendChild(emptyRow);
                }
                emptyRow.style.display = "";
            } else if (emptyRow) {
                emptyRow.style.display = "none";
            }

            pager.innerHTML = "";
            var info = document.createElement("div");
            info.textContent = rows.length === 0
                ? "0 results"
                : "Showing " + (start + 1) + "–" + Math.min(start + pageSize, rows.length) + " of " + rows.length;
            pager.appendChild(info);

            if (totalPages > 1) {
                var nav = document.createElement("div");
                nav.className = "btn-group btn-group-sm";
                var prev = document.createElement("button");
                prev.type = "button";
                prev.className = "btn btn-outline-secondary";
                prev.innerHTML = '<i class="bi bi-chevron-left"></i>';
                prev.disabled = state.page <= 1;
                prev.addEventListener("click", function () { state.page--; render(); });
                var label = document.createElement("button");
                label.type = "button";
                label.className = "btn btn-outline-secondary disabled";
                label.textContent = "Page " + state.page + " of " + totalPages;
                var next = document.createElement("button");
                next.type = "button";
                next.className = "btn btn-outline-secondary";
                next.innerHTML = '<i class="bi bi-chevron-right"></i>';
                next.disabled = state.page >= totalPages;
                next.addEventListener("click", function () { state.page++; render(); });
                nav.appendChild(prev);
                nav.appendChild(label);
                nav.appendChild(next);
                pager.appendChild(nav);
            }
        }

        searchInput.addEventListener("input", function () {
            state.query = searchInput.value;
            state.page = 1;
            render();
        });
        var clearBtn = searchWrap.querySelector('button');
        if (clearBtn) {
            clearBtn.addEventListener('click', function () { searchInput.value = ''; searchInput.dispatchEvent(new Event('input')); });
            searchInput.addEventListener('input', function () { clearBtn.classList.toggle('d-none', !searchInput.value); });
        }

        render();
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("table.data-table").forEach(enhanceTable);
    });
})();
