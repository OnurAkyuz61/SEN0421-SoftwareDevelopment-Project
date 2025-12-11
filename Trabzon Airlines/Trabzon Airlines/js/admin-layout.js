// ============================================
// ADMIN LAYOUT JAVASCRIPT - Admin Paneli Genel JavaScript
// ADMIN LAYOUT JAVASCRIPT - General JavaScript for Admin Panel
// ============================================

// Sayfa yüklendiğinde çalışacak fonksiyonlar
// Functions to run when page loads
$(document).ready(function() {
    // Admin user dropdown menüsünü başlat
    // Initialize admin user dropdown menu
    initAdminUserMenu();
    
    // Sidebar durumunu kontrol et (localStorage'dan)
    // Check sidebar state (from localStorage)
    checkSidebarState();
    
    // Dışarı tıklandığında dropdown'ları kapat
    // Close dropdowns when clicking outside
    $(document).on('click', function(e) {
        // Admin user dropdown kontrolü
        // Admin user dropdown check
        if (!$(e.target).closest('.admin-user-menu').length) {
            $('#adminUserDropdown').removeClass('show'); // Dropdown'u kapat
        }
    });
});

// ============================================
// ADMIN SIDEBAR TOGGLE - Sidebar Açma/Kapama
// ADMIN SIDEBAR TOGGLE - Toggle Sidebar Open/Close
// ============================================

// Sidebar'ı aç/kapat
// Toggle sidebar open/close
function toggleAdminSidebar() {
    const sidebar = document.getElementById('adminSidebar'); // Sidebar elementi
    const mainContent = document.getElementById('adminMainContent'); // Ana içerik elementi
    
    // Sidebar'ın açık/kapalı durumunu değiştir
    // Toggle sidebar open/closed state
    if (sidebar.classList.contains('closed')) {
        sidebar.classList.remove('closed'); // Aç
        sidebar.classList.add('open'); // Açık sınıfı ekle
        localStorage.setItem('adminSidebarOpen', 'true'); // localStorage'a kaydet
    } else {
        sidebar.classList.add('closed'); // Kapat
        sidebar.classList.remove('open'); // Açık sınıfını kaldır
        localStorage.setItem('adminSidebarOpen', 'false'); // localStorage'a kaydet
    }
}

// Sidebar durumunu kontrol et
// Check sidebar state
function checkSidebarState() {
    const sidebar = document.getElementById('adminSidebar'); // Sidebar elementi
    const isOpen = localStorage.getItem('adminSidebarOpen'); // localStorage'dan durumu al
    
    // Eğer durum kaydedilmişse uygula
    // If state is saved, apply it
    if (isOpen === 'false') {
        sidebar.classList.add('closed'); // Kapalı sınıfı ekle
        sidebar.classList.remove('open'); // Açık sınıfını kaldır
    } else {
        sidebar.classList.remove('closed'); // Kapalı sınıfını kaldır
        sidebar.classList.add('open'); // Açık sınıfı ekle
    }
    
    // Mobil cihazlarda varsayılan olarak kapalı
    // Default closed on mobile devices
    if (window.innerWidth <= 768) {
        sidebar.classList.add('closed'); // Kapalı sınıfı ekle
        sidebar.classList.remove('open'); // Açık sınıfını kaldır
    }
}

// ============================================
// ADMIN USER MENU - Kullanıcı Menüsü
// ADMIN USER MENU - User Menu
// ============================================

// Admin user menüsünü başlat
// Initialize admin user menu
function initAdminUserMenu() {
    // Dropdown menüyü başlangıçta gizle
    // Hide dropdown menu initially
    $('#adminUserDropdown').removeClass('show'); // Show sınıfını kaldır
}

// Admin user dropdown menüsünü aç/kapat
// Toggle admin user dropdown menu
function toggleAdminUserMenu() {
    const dropdown = document.getElementById('adminUserDropdown'); // Dropdown elementi
    
    // Dropdown'ın açık/kapalı durumunu değiştir
    // Toggle dropdown open/closed state
    if (dropdown.classList.contains('show')) {
        dropdown.classList.remove('show'); // Kapat
    } else {
        dropdown.classList.add('show'); // Aç
    }
}

// ============================================
// RESPONSIVE HANDLING - Mobil Uyumluluk
// RESPONSIVE HANDLING - Mobile Compatibility
// ============================================

// Pencere boyutu değiştiğinde
// When window size changes
$(window).on('resize', function() {
    const sidebar = document.getElementById('adminSidebar'); // Sidebar elementi
    
    // Mobil cihazlarda sidebar'ı kapat
    // Close sidebar on mobile devices
    if (window.innerWidth <= 768) {
        sidebar.classList.add('closed'); // Kapalı sınıfı ekle
        sidebar.classList.remove('open'); // Açık sınıfını kaldır
    } else {
        // Desktop'ta sidebar durumunu kontrol et
        // Check sidebar state on desktop
        checkSidebarState();
    }
});

// ============================================
// UTILITY FUNCTIONS - Yardımcı Fonksiyonlar
// UTILITY FUNCTIONS - Helper Functions
// ============================================

// Başarı mesajı göster
// Show success message
function showAdminSuccess(message) {
    // Toast mesajı oluştur (eğer toast sistemi varsa)
    // Create toast message (if toast system exists)
    console.log('Success:', message); // Konsola yazdır
}

// Hata mesajı göster
// Show error message
function showAdminError(message) {
    // Toast mesajı oluştur (eğer toast sistemi varsa)
    // Create toast message (if toast system exists)
    console.error('Error:', message); // Konsola yazdır
}

// Form validasyonu
// Form validation
function validateAdminForm(formId) {
    const form = document.getElementById(formId); // Form elementi
    
    // Form bulunamadıysa false döndür
    // If form not found, return false
    if (!form) {
        return false; // False döndür
    }
    
    // HTML5 validasyonunu kontrol et
    // Check HTML5 validation
    if (!form.checkValidity()) {
        form.reportValidity(); // Validasyon hatalarını göster
        return false; // False döndür
    }
    
    return true; // True döndür
}

