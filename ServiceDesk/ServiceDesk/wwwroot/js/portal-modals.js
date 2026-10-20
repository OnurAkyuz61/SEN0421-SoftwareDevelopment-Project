// Portal: talep satırına tıklayınca detay popup; kapalı taleplerde anket popup.
(function () {
    const cfg = window.portalTicket || {};
    const modalEl = document.getElementById("ticketModal");
    if (!modalEl) {
        // Tam sayfa Details: yalnızca anket butonu
        bindSurveyButtons(document);
        return;
    }

    const bodyEl = document.getElementById("ticketModalBody");
    const titleEl = document.getElementById("ticketModalTitle");
    const fullLink = document.getElementById("ticketFullLink");
    const bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
    let pendingSurvey = false;

    async function openTicket(id, openSurvey) {
        pendingSurvey = !!openSurvey;
        bodyEl.innerHTML =
            '<div class="text-center text-muted py-5">' +
            '<div class="spinner-border spinner-border-sm text-primary" role="status"></div>' +
            '<div class="small mt-2">Yükleniyor…</div></div>';
        titleEl.textContent = "Talep detayı";
        if (fullLink) {
            fullLink.href = (cfg.detailsUrl || "/Portal/Details") + "/" + id;
            fullLink.classList.remove("d-none");
        }
        bsModal.show();

        try {
            const res = await fetch((cfg.partialUrl || "/Portal/DetailsPartial") + "/" + id, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!res.ok) throw new Error("not found");
            bodyEl.innerHTML = await res.text();
            bindSurveyButtons(bodyEl);
            // Partial içindeki script etiketlerini çalıştır (upload.js)
            bodyEl.querySelectorAll("script").forEach(function (old) {
                const s = document.createElement("script");
                if (old.src) s.src = old.src;
                else s.textContent = old.textContent;
                document.body.appendChild(s);
                old.remove();
            });
            if (pendingSurvey) showSurveyModal(id);
        } catch {
            bodyEl.innerHTML =
                '<div class="alert alert-danger m-0">Talep yüklenemedi. Sayfayı yenileyip tekrar deneyin.</div>';
        }
    }

    function bindRows() {
        document.querySelectorAll(".ticket-row[data-ticket-id]").forEach(function (row) {
            const open = function () {
                openTicket(row.dataset.ticketId, row.dataset.needsSurvey === "1");
            };
            row.addEventListener("click", open);
            row.addEventListener("keydown", function (e) {
                if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    open();
                }
            });
        });
    }

    bindRows();

    // Form sonrası ?open=3&survey=1 ile geri dönüş
    if (cfg.openId) openTicket(cfg.openId, cfg.showSurvey);

    // Nested survey modal kapanınca arka plan kaymasın
    modalEl.addEventListener("hidden.bs.modal", function () {
        document.querySelectorAll(".modal.show").forEach(function (m) {
            bootstrap.Modal.getInstance(m)?.hide();
        });
    });
})();

function bindSurveyButtons(root) {
    (root || document).querySelectorAll("[data-open-survey]").forEach(function (btn) {
        btn.addEventListener("click", function () {
            showSurveyModal(btn.getAttribute("data-open-survey"));
        });
    });
}

function showSurveyModal(id) {
    const el = document.getElementById("surveyModal-" + id);
    if (!el) return;
    // İç içe modal: anketi body'ye taşı ki backdrop doğru çalışsın
    if (el.parentElement !== document.body) document.body.appendChild(el);
    bootstrap.Modal.getOrCreateInstance(el).show();
}
