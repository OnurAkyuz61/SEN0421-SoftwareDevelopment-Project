// Dosya yükleme alanı: dosya seç, sürükle-bırak, Ctrl+V ile ekran görüntüsü yapıştır.
// Tüm dosyalar tek bir <input name="files"> içinde toplanır ve form ile gönderilir.
// Bir sayfada birden fazla alan olabilir (örn. "dosya ekle" ve "yanıt yaz" formları);
// yapıştırma, kullanıcının en son dokunduğu alana gider.
(function () {
    const zones = [...document.querySelectorAll('[data-upload]')];
    if (!zones.length) return;

    let activeZone = zones[0];

    // Sınırlar sunucudan gelir (appsettings.json → Uploads); her alanın kendi
    // sınırı olabilir çünkü talep eki ile yorum eki boyutu farklıdır.
    // Buradaki kontrol yalnızca kolaylık: asıl denetim sunucuda yapılır.
    const limit = (zone, attr, fallback) => parseInt(zone.dataset[attr], 10) || fallback;
    const human = bytes => bytes < 1024 * 1024
        ? Math.max(1, Math.round(bytes / 1024)) + ' KB'
        : Math.round(bytes / 1024 / 1024 * 10) / 10 + ' MB';

    zones.forEach(zone => {
        const MAX = limit(zone, 'maxFiles', 5);
        const MAX_SIZE = limit(zone, 'maxBytes', 10 * 1024 * 1024);
        const input = zone.querySelector('input[name="files"]');
        const picker = zone.querySelector('[data-image-picker]');
        const list = zone.querySelector('.file-list');
        const box = zone.querySelector('.dropzone');
        const dt = new DataTransfer();

        function add(files) {
            for (const f of files) {
                if (dt.items.length >= MAX) { alert('En fazla ' + MAX + ' dosya ekleyebilirsiniz.'); break; }
                if (f.size > MAX_SIZE) { alert('"' + f.name + '" dosya başına ' + human(MAX_SIZE) + ' sınırını aşıyor.'); continue; }
                // Panodan yapıştırılan görüntüye anlamlı bir ad ver
                const file = (f.name === 'image.png' || !f.name)
                    ? new File([f], 'ekran-goruntusu-' + Date.now() + '.png', { type: f.type })
                    : f;
                dt.items.add(file);
            }
            input.files = dt.files;
            render();
        }

        function render() {
            list.innerHTML = '';
            [...dt.files].forEach((f, i) => {
                const li = document.createElement('li');
                li.innerHTML =
                    '<i class="bi ' + (f.type.startsWith('image/') ? 'bi-image' : 'bi-file-earmark') + '"></i>' +
                    '<span class="flex-grow-1 text-truncate"></span>' +
                    '<small class="text-muted">' + Math.max(1, Math.round(f.size / 1024)) + ' KB</small>' +
                    '<button type="button" class="btn btn-sm btn-link text-danger p-0" title="Kaldır"><i class="bi bi-x-lg"></i></button>';
                li.querySelector('span').textContent = f.name;
                li.querySelector('button').onclick = () => { dt.items.remove(i); input.files = dt.files; render(); };
                list.appendChild(li);
            });
        }

        // Seçilen yeni dosyalar mevcut listeye eklenir (öncekiler silinmez)
        input.addEventListener('change', () => add([...input.files]));
        picker?.addEventListener('change', () => { add([...picker.files]); picker.value = ''; });

        box.addEventListener('dragover', e => { e.preventDefault(); box.classList.add('drag'); });
        box.addEventListener('dragleave', () => box.classList.remove('drag'));
        box.addEventListener('drop', e => { e.preventDefault(); box.classList.remove('drag'); add([...e.dataTransfer.files]); });

        // Yapıştırmanın hangi alana gideceğini belirlemek için son dokunulan alanı izle
        const markActive = () => { activeZone = zone; };
        zone.closest('form')?.addEventListener('pointerdown', markActive);
        zone.closest('form')?.addEventListener('focusin', markActive);

        zone._addFiles = add;
    });

    // Tek bir yapıştırma dinleyicisi; dosya yalnızca aktif alana eklenir
    document.addEventListener('paste', e => {
        const images = [...(e.clipboardData?.files || [])].filter(f => f.type.startsWith('image/'));
        if (!images.length) return;
        e.preventDefault();
        (activeZone || zones[0])._addFiles(images);
    });
})();
