// Paketler sayfası filtreleme ve etkileşim fonksiyonları
// Packages page filter and interaction functions

// Sayfa yüklendiğinde çalışacak fonksiyonlar
// Functions to run when page loads
document.addEventListener('DOMContentLoaded', function() {
    // Tüm filtre sekmelerini seç
    // Select all filter tabs
    const filterTabs = document.querySelectorAll('.filter-tab');
    
    // Tüm paket kartlarını seç
    // Select all package cards
    const packageCards = document.querySelectorAll('.package-card');

    // Her filtre sekmesine tıklama olayı ekle
    // Add click event to each filter tab
    filterTabs.forEach(tab => {
        tab.addEventListener('click', function() {
            // Tüm sekmelerden aktif sınıfını kaldır
            // Remove active class from all tabs
            filterTabs.forEach(t => t.classList.remove('active'));
            
            // Tıklanan sekmeye aktif sınıfını ekle
            // Add active class to clicked tab
            this.classList.add('active');

            // Seçilen filtre değerini al
            // Get selected filter value
            const filter = this.getAttribute('data-filter');

            // Paketleri filtrele
            // Filter packages
            packageCards.forEach(card => {
                // Kartın kategori bilgisini al
                // Get card's category information
                const categories = card.getAttribute('data-category');
                
                // Eğer "tümü" seçildiyse veya kategori eşleşiyorsa göster
                // If "all" is selected or category matches, show
                if (filter === 'all' || categories.includes(filter)) {
                    card.style.display = 'flex';
                    card.style.animation = 'fadeInUp 0.6s ease';
                } else {
                    // Aksi halde gizle
                    // Otherwise hide
                    card.style.display = 'none';
                }
            });
        });
    });

    // Paket detay görüntüleme butonları
    // Package detail view buttons
    const viewButtons = document.querySelectorAll('.package-view-btn');
    
    // Her görüntüleme butonuna tıklama olayı ekle
    // Add click event to each view button
    viewButtons.forEach(btn => {
        btn.addEventListener('click', function(e) {
            // Olayın yayılmasını durdur
            // Stop event propagation
            e.stopPropagation();
            
            // En yakın paket kartını bul
            // Find closest package card
            const card = this.closest('.package-card');
            
            // Paket başlığını al
            // Get package title
            const title = card.querySelector('.package-title').textContent;
            
            // Bilgilendirme mesajı göster
            // Show information message
            alert('Detaylar yakında eklenecek: ' + title);
        });
    });

    // Paket rezervasyon butonları
    // Package booking buttons
    const bookButtons = document.querySelectorAll('.package-book-btn');
    
    // Her rezervasyon butonuna tıklama olayı ekle
    // Add click event to each booking button
    bookButtons.forEach(btn => {
        btn.addEventListener('click', function(e) {
            // Olayın yayılmasını durdur
            // Stop event propagation
            e.stopPropagation();
            
            // En yakın paket kartını bul
            // Find closest package card
            const card = this.closest('.package-card');
            
            // Paket başlığını al
            // Get package title
            const title = card.querySelector('.package-title').textContent;
            
            // Paket fiyatını al
            // Get package price
            const price = card.querySelector('.price-new').textContent;
            
            // Mevcut dil ayarını al (varsayılan: Türkçe)
            // Get current language setting (default: Turkish)
            const currentLanguage = localStorage.getItem('language') || 'tr';
            
            // Dil ayarına göre mesaj oluştur
            // Create message according to language setting
            const message = currentLanguage === 'tr' 
                ? `Rezervasyon sayfası yakında eklenecek.\n\nPaket: ${title}\nFiyat: ${price}`
                : `Booking page coming soon.\n\nPackage: ${title}\nPrice: ${price}`;
            
            // Bilgilendirme mesajı göster
            // Show information message
            alert(message);
        });
    });
});
