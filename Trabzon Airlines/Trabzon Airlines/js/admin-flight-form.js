// ============================================
// ADMIN FLIGHT FORM JAVASCRIPT - Uçuş Formu JavaScript
// ADMIN FLIGHT FORM JAVASCRIPT - Flight Form JavaScript
// ============================================

// Sayfa yüklendiğinde çalışacak fonksiyonlar
// Functions to run when page loads
$(document).ready(function() {
    // Form validasyonunu başlat
    // Initialize form validation
    initFormValidation();
    
    // Tarih kontrolünü başlat
    // Initialize date check
    initDateCheck();
});

// ============================================
// FORM VALİDASYONU - Form Validation
// ============================================

// Form validasyonunu başlat
// Initialize form validation
function initFormValidation() {
    // Formu seç
    // Select form
    const form = document.querySelector('.flight-form'); // Form
    
    // Eğer form varsa
    // If form exists
    if (form) {
        // Form gönderildiğinde
        // On form submit
        form.addEventListener('submit', function(e) {
            // HTML5 validasyonunu kontrol et
            // Check HTML5 validation
            if (!form.checkValidity()) {
                e.preventDefault(); // Form gönderimini engelle
                form.reportValidity(); // Validasyon hatalarını göster
                return false; // False döndür
            }
        });
    }
}

// ============================================
// TARİH KONTROLÜ - Date Check
// ============================================

// Tarih kontrolünü başlat
// Initialize date check
function initDateCheck() {
    // Kalkış ve varış zamanı alanlarını seç
    // Select departure and arrival time fields
    const departureTime = document.getElementById('DepartureTime'); // Kalkış zamanı
    const arrivalTime = document.getElementById('ArrivalTime'); // Varış zamanı
    
    // Eğer alanlar varsa
    // If fields exist
    if (departureTime && arrivalTime) {
        // Varış zamanı değiştiğinde
        // When arrival time changes
        arrivalTime.addEventListener('change', function() {
            // Kalkış zamanından önce olamaz
            // Cannot be before departure time
            if (departureTime.value && arrivalTime.value) {
                if (new Date(arrivalTime.value) <= new Date(departureTime.value)) {
                    arrivalTime.setCustomValidity('Varış zamanı kalkış zamanından sonra olmalıdır.'); // Özel validasyon mesajı
                } else {
                    arrivalTime.setCustomValidity(''); // Validasyon mesajını temizle
                }
            }
        });
    }
}

