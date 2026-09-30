// ============================================
// ADMIN FLIGHTS JAVASCRIPT - Uçuş Yönetimi Sayfası JavaScript
// ADMIN FLIGHTS JAVASCRIPT - Flight Management Page JavaScript
// ============================================

// Sayfa yüklendiğinde çalışacak fonksiyonlar
// Functions to run when page loads
$(document).ready(function() {
    // Arama fonksiyonunu başlat
    // Initialize search function
    initFlightSearch();
    
    // Tabloya hover efekti ekle
    // Add hover effect to table
    initTableHover();
});

// ============================================
// UÇUŞ ARAMA - Flight Search
// ============================================

// Uçuş arama fonksiyonunu başlat
// Initialize flight search function
function initFlightSearch() {
    // Arama giriş alanını seç
    // Select search input field
    const searchInput = document.getElementById('flightSearch'); // Arama girişi
    
    // Eğer arama girişi varsa
    // If search input exists
    if (searchInput) {
        // Her tuş vuruşunda arama yap
        // Search on each key press
        searchInput.addEventListener('input', function() {
            const searchTerm = this.value.toLowerCase(); // Arama terimi (küçük harf)
            const table = document.getElementById('flightsTable'); // Tablo
            
            // Eğer tablo varsa
            // If table exists
            if (table) {
                const rows = table.getElementsByTagName('tr'); // Tüm satırlar
                
                // Her satır için (başlık hariç)
                // For each row (except header)
                for (let i = 1; i < rows.length; i++) {
                    const row = rows[i]; // Mevcut satır
                    const text = row.textContent.toLowerCase(); // Satır metni (küçük harf)
                    
                    // Eğer arama terimi satırda varsa göster, yoksa gizle
                    // If search term exists in row, show, otherwise hide
                    if (text.includes(searchTerm)) {
                        row.style.display = ''; // Göster
                    } else {
                        row.style.display = 'none'; // Gizle
                    }
                }
            }
        });
    }
}

// ============================================
// TABLO HOVER EFEKTİ - Table Hover Effect
// ============================================

// Tabloya hover efekti ekle
// Add hover effect to table
function initTableHover() {
    // Tüm tablo satırlarını seç
    // Select all table rows
    const rows = document.querySelectorAll('.admin-table tbody tr');
    
    // Her satır için
    // For each row
    rows.forEach(row => {
        // Mouse üzerine gelince
        // On mouse enter
        row.addEventListener('mouseenter', function() {
            this.style.transition = 'background 0.2s'; // Geçiş efekti
        });
    });
}

// ============================================
// SİLME ONAYI - Delete Confirmation
// ============================================

// Silme işlemini onayla
// Confirm delete action
function confirmDelete(flightNumber) {
    // Onay mesajı göster
    // Show confirmation message
    return confirm(`"${flightNumber}" uçuşunu silmek istediğinizden emin misiniz?`); // Onay mesajı
}

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

