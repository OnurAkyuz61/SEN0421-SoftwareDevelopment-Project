// ============================================
// ADMIN BOOKINGS JAVASCRIPT - Rezervasyon Yönetimi Sayfası JavaScript
// ADMIN BOOKINGS JAVASCRIPT - Booking Management Page JavaScript
// ============================================

// Sayfa yüklendiğinde çalışacak fonksiyonlar
// Functions to run when page loads
$(document).ready(function() {
    // Arama fonksiyonunu başlat
    // Initialize search function
    initBookingSearch();
});

// ============================================
// REZERVASYON ARAMA - Booking Search
// ============================================

// Rezervasyon arama fonksiyonunu başlat
// Initialize booking search function
function initBookingSearch() {
    // Arama giriş alanını seç
    // Select search input field
    const searchInput = document.getElementById('bookingSearch'); // Arama girişi
    
    // Eğer arama girişi varsa
    // If search input exists
    if (searchInput) {
        // Her tuş vuruşunda arama yap
        // Search on each key press
        searchInput.addEventListener('input', function() {
            const searchTerm = this.value.toLowerCase(); // Arama terimi (küçük harf)
            const table = document.getElementById('bookingsTable'); // Tablo
            
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

