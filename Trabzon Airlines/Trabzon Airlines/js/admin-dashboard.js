// ============================================
// ADMIN DASHBOARD JAVASCRIPT - Dashboard Sayfası JavaScript
// ADMIN DASHBOARD JAVASCRIPT - Dashboard Page JavaScript
// ============================================

// Sayfa yüklendiğinde çalışacak fonksiyonlar
// Functions to run when page loads
$(document).ready(function() {
    // İstatistik kartlarına animasyon ekle
    // Add animation to statistics cards
    animateStatCards();
    
    // Tablolara hover efekti ekle
    // Add hover effect to tables
    initTableHover();
    
    // Sayıları animasyonlu göster
    // Show numbers with animation
    animateNumbers();
});

// ============================================
// İSTATİSTİK KARTLARI ANİMASYONU - Statistics Cards Animation
// ============================================

// İstatistik kartlarını animasyonlu göster
// Show statistics cards with animation
function animateStatCards() {
    // Tüm istatistik kartlarını seç
    // Select all statistics cards
    const cards = document.querySelectorAll('.stat-card');
    
    // Her kart için
    // For each card
    cards.forEach((card, index) => {
        // Başlangıçta gizle
        // Hide initially
        card.style.opacity = '0'; // Opaklık sıfır
        card.style.transform = 'translateY(20px)'; // Aşağı kaydır
        
        // Animasyonu başlat
        // Start animation
        setTimeout(() => {
            card.style.transition = 'all 0.5s ease'; // Geçiş efekti
            card.style.opacity = '1'; // Opaklık bir
            card.style.transform = 'translateY(0)'; // Kaydırma yok
        }, index * 100); // Her kart için 100ms gecikme
    });
}

// ============================================
// TABLO HOVER EFEKTİ - Table Hover Effect
// ============================================

// Tablolara hover efekti ekle
// Add hover effect to tables
function initTableHover() {
    // Tüm tablo satırlarını seç
    // Select all table rows
    const rows = document.querySelectorAll('.dashboard-table tbody tr');
    
    // Her satır için
    // For each row
    rows.forEach(row => {
        // Mouse üzerine gelince
        // On mouse enter
        row.addEventListener('mouseenter', function() {
            this.style.transition = 'background 0.2s'; // Geçiş efekti
        });
        
        // Mouse ayrılınca
        // On mouse leave
        row.addEventListener('mouseleave', function() {
            // Varsayılan duruma dön (CSS'te zaten tanımlı)
            // Return to default state (already defined in CSS)
        });
    });
}

// ============================================
// SAYI ANİMASYONU - Number Animation
// ============================================

// Sayıları animasyonlu göster
// Show numbers with animation
function animateNumbers() {
    // Tüm istatistik değerlerini seç
    // Select all statistics values
    const values = document.querySelectorAll('.stat-card-value');
    
    // Her değer için
    // For each value
    values.forEach(value => {
        // Mevcut değeri al
        // Get current value
        const targetValue = parseInt(value.textContent) || 0; // Sayıya çevir
        
        // Başlangıç değeri
        // Starting value
        let currentValue = 0; // Sıfırdan başla
        
        // Animasyon hızı
        // Animation speed
        const increment = Math.ceil(targetValue / 30); // Artış miktarı
        const duration = 1000; // Süre (ms)
        const stepTime = duration / (targetValue / increment); // Adım süresi
        
        // Animasyon fonksiyonu
        // Animation function
        const timer = setInterval(() => {
            // Değeri artır
            // Increment value
            currentValue += increment; // Artış ekle
            
            // Hedef değere ulaştıysa
            // If reached target value
            if (currentValue >= targetValue) {
                currentValue = targetValue; // Hedef değeri ayarla
                clearInterval(timer); // Timer'ı durdur
            }
            
            // Değeri güncelle
            // Update value
            value.textContent = currentValue.toLocaleString(); // Sayı formatı
        }, stepTime); // Adım süresi kadar bekle
    });
}

// ============================================
// YENİLEME FONKSİYONU - Refresh Function
// ============================================

// Sayfayı yenile
// Refresh page
function refreshDashboard() {
    // Sayfayı yeniden yükle
    // Reload page
    location.reload(); // Sayfayı yenile
}

// Otomatik yenileme (opsiyonel - 5 dakikada bir)
// Auto refresh (optional - every 5 minutes)
// setInterval(refreshDashboard, 300000); // 5 dakika = 300000ms

// ============================================
// UTILITY FUNCTIONS - Yardımcı Fonksiyonlar
// UTILITY FUNCTIONS - Helper Functions
// ============================================

// Tarih formatla
// Format date
function formatDate(dateString) {
    const date = new Date(dateString); // Tarih nesnesi oluştur
    const day = String(date.getDate()).padStart(2, '0'); // Gün (2 haneli)
    const month = String(date.getMonth() + 1).padStart(2, '0'); // Ay (2 haneli)
    const year = date.getFullYear(); // Yıl
    const hours = String(date.getHours()).padStart(2, '0'); // Saat (2 haneli)
    const minutes = String(date.getMinutes()).padStart(2, '0'); // Dakika (2 haneli)
    
    return `${day}.${month}.${year} ${hours}:${minutes}`; // Formatlanmış tarih
}

// Para formatla
// Format currency
function formatCurrency(amount) {
    return new Intl.NumberFormat('tr-TR', {
        style: 'currency', // Para birimi formatı
        currency: 'TRY' // Türk Lirası
    }).format(amount); // Formatlanmış para
}

