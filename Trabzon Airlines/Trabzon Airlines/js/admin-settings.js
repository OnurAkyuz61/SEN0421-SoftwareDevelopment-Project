// ============================================
// ADMIN SETTINGS JAVASCRIPT - Ayarlar Sayfası JavaScript
// ADMIN SETTINGS JAVASCRIPT - Settings Page JavaScript
// ============================================

// Sayfa yüklendiğinde çalışacak fonksiyonlar
// Functions to run when page loads
$(document).ready(function() {
    // Form validasyonunu başlat
    // Initialize form validation
    initFormValidation();
    
    // Şifre eşleşme kontrolünü başlat
    // Initialize password match check
    initPasswordMatchCheck();
});

// ============================================
// FORM VALİDASYONU - Form Validation
// ============================================

// Form validasyonunu başlat
// Initialize form validation
function initFormValidation() {
    // Formu seç
    // Select form
    const form = document.getElementById('settingsForm'); // Form
    
    // Eğer form varsa
    // If form exists
    if (form) {
        // Form gönderildiğinde
        // On form submit
        form.addEventListener('submit', function(e) {
            // Şifre değiştirme kontrolü
            // Password change check
            const newPassword = document.getElementById('newPassword').value; // Yeni şifre
            const confirmPassword = document.getElementById('confirmPassword').value; // Şifre onay
            const currentPassword = document.getElementById('currentPassword').value; // Mevcut şifre
            
            // Eğer yeni şifre girilmişse
            // If new password is entered
            if (newPassword || confirmPassword || currentPassword) {
                // Tüm alanların dolu olması gerekir
                // All fields must be filled
                if (!newPassword || !confirmPassword || !currentPassword) {
                    e.preventDefault(); // Form gönderimini engelle
                    alert('Şifre değiştirmek için tüm şifre alanlarını doldurmalısınız.'); // Uyarı mesajı
                    return false; // False döndür
                }
                
                // Şifre eşleşme kontrolü
                // Password match check
                if (newPassword !== confirmPassword) {
                    e.preventDefault(); // Form gönderimini engelle
                    alert('Yeni şifre ve onay şifresi eşleşmiyor.'); // Uyarı mesajı
                    return false; // False döndür
                }
            }
        });
    }
}

// ============================================
// ŞİFRE EŞLEŞME KONTROLÜ - Password Match Check
// ============================================

// Şifre eşleşme kontrolünü başlat
// Initialize password match check
function initPasswordMatchCheck() {
    // Yeni şifre ve onay şifresi alanlarını seç
    // Select new password and confirm password fields
    const newPassword = document.getElementById('newPassword'); // Yeni şifre
    const confirmPassword = document.getElementById('confirmPassword'); // Şifre onay
    
    // Eğer alanlar varsa
    // If fields exist
    if (newPassword && confirmPassword) {
        // Onay şifresi alanı değiştiğinde
        // When confirm password field changes
        confirmPassword.addEventListener('input', function() {
            // Eğer şifreler eşleşmiyorsa
            // If passwords don't match
            if (newPassword.value !== confirmPassword.value) {
                confirmPassword.setCustomValidity('Şifreler eşleşmiyor.'); // Özel validasyon mesajı
            } else {
                confirmPassword.setCustomValidity(''); // Validasyon mesajını temizle
            }
        });
    }
}

