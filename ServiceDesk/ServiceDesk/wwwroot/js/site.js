// ServiceDesk — yönetim paneli ve portalın ortak betikleri
(function () {
    'use strict';

    // ───── Sidebar (tablet / telefon) ─────────────────────────────────────
    function initSidebar() {
        const body = document.body;
        const open = () => body.classList.add('sidebar-open');
        const close = () => body.classList.remove('sidebar-open');

        document.querySelectorAll('[data-sidebar-open]').forEach(b => b.addEventListener('click', open));
        document.querySelectorAll('[data-sidebar-close]').forEach(b => b.addEventListener('click', close));
        document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });
        window.matchMedia('(min-width: 992px)').addEventListener('change', e => { if (e.matches) close(); });
    }

    // ───── Oturum süresi uyarısı ──────────────────────────────────────────
    function initSessionTimeout() {
        const modalEl = document.getElementById('sessionModal');
        if (!modalEl || !window.bootstrap) return;

        const idle = +modalEl.dataset.idleSeconds;
        const warn = +modalEl.dataset.warnSeconds;
        const absoluteEnd = Date.now() + (+modalEl.dataset.absoluteLeft) * 1000;
        const keepAliveUrl = modalEl.dataset.keepaliveUrl;
        const loginUrl = modalEl.dataset.loginUrl;
        const countEl = modalEl.querySelector('[data-session-count]');
        const modal = new bootstrap.Modal(modalEl);
        const STORE_KEY = 'sd-last-activity';

        // Son sunucu isteği zamanı: sekmeler arasında paylaşılır
        let lastActivity = Date.now();
        try { localStorage.setItem(STORE_KEY, String(lastActivity)); } catch (_) { }
        window.addEventListener('storage', e => {
            if (e.key === STORE_KEY && e.newValue) {
                lastActivity = Math.max(lastActivity, +e.newValue);
                if (modalEl.classList.contains('show')) modal.hide();
            }
        });

        const fmt = s => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;

        function tick() {
            const now = Date.now();
            const end = Math.min(lastActivity + idle * 1000, absoluteEnd);
            const left = Math.max(0, Math.round((end - now) / 1000));

            if (left <= 0) {
                window.location.href = loginUrl;
                return;
            }
            if (left <= warn) {
                countEl.textContent = fmt(left);
                if (!modalEl.classList.contains('show')) modal.show();
            }
        }

        modalEl.querySelector('[data-session-extend]').addEventListener('click', async (e) => {
            const btn = e.currentTarget;
            btn.disabled = true;
            try {
                const res = await fetch(keepAliveUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } });
                if (!res.ok || res.redirected) throw new Error('expired');
                lastActivity = Date.now();
                try { localStorage.setItem(STORE_KEY, String(lastActivity)); } catch (_) { }
                modal.hide();
            } catch (_) {
                window.location.href = loginUrl;
            } finally {
                btn.disabled = false;
            }
        });

        setInterval(tick, 1000);
    }

    document.addEventListener('DOMContentLoaded', () => {
        initSidebar();
        initSessionTimeout();
    });
})();
