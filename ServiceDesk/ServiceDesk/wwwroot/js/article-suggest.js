// Talep başlığı yazılırken bilgi bankası önerisi.
//
// Amaç kullanıcıyı talep açmaktan vazgeçirmek değil, çözümü zaten yazılmışsa
// göstermek (bilgi bankasının asıl işi). Uç: GET api/search/articles
//
// İstek sınırı: uç RateLimitPolicies.Search ile korunuyor, bu yüzden her
// tuşta istek atılmaz — yazma durduktan sonra tek istek gider (debounce).
(function () {
    const input = document.getElementById('suggestSource');
    const box = document.getElementById('suggestBox');
    if (!input || !box) return;

    const MIN = 3;          // bu uzunluğun altında öneri aranmaz
    const DELAY = 350;       // yazma durduktan sonra beklenen süre (ms)

    let timer = null;
    let lastQuery = '';
    let controller = null;

    function hide() {
        box.hidden = true;
        box.innerHTML = '';
    }

    function render(items) {
        if (!items || items.length === 0) { hide(); return; }

        const list = items.map(function (a) {
            const kategori = a.categoryName
                ? '<span class="tag tag-blue ms-1">' + escapeHtml(a.categoryName) + '</span>'
                : '';
            const kullanim = a.usedCount > 0
                ? '<span class="text-muted small ms-1">' + a.usedCount + ' talepte işe yaradı</span>'
                : '';
            return '<li class="mb-1">'
                + '<a href="' + encodeURI(a.url) + '" target="_blank" rel="noopener">'
                + escapeHtml(a.title) + '</a>'
                + kategori + kullanim + '</li>';
        }).join('');

        box.innerHTML =
            '<div class="alert alert-info mb-0">'
            + '<div class="fw-semibold mb-1"><i class="bi bi-lightbulb"></i> Belki talep açmanıza gerek yok</div>'
            + '<div class="small text-muted mb-2">Bu konuda bilgi bankasında şunlar var:</div>'
            + '<ul class="mb-0 ps-3">' + list + '</ul></div>';
        box.hidden = false;
    }

    // Sunucudan gelen metin olduğu gibi HTML'e yazılmaz
    function escapeHtml(s) {
        return String(s === null || s === undefined ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    async function ara() {
        const q = input.value.trim();
        if (q.length < MIN) { hide(); return; }
        if (q === lastQuery) return;
        lastQuery = q;

        // Önceki istek hâlâ sürüyorsa iptal edilir: geç gelen yanıt yeni
        // yazılan metnin önerisini ezmesin
        if (controller) controller.abort();
        controller = new AbortController();

        try {
            const res = await fetch('/api/search/articles?q=' + encodeURIComponent(q) + '&take=5',
                { headers: { 'Accept': 'application/json' }, signal: controller.signal });

            // 429 (istek sınırı) ve 401 (oturum bitti) dahil: öneri
            // gösterilmez, form çalışmaya devam eder
            if (!res.ok) { hide(); return; }

            const data = await res.json();
            render(data.items);
        } catch (e) {
            if (e.name !== 'AbortError') hide();
        }
    }

    input.addEventListener('input', function () {
        clearTimeout(timer);
        timer = setTimeout(ara, DELAY);
    });
})();
