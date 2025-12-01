// Fırsatlar sayfası fonksiyonları
// Deals page functions

// Sayfa yüklendiğinde çalışacak fonksiyonlar
// Functions to run when page loads
document.addEventListener('DOMContentLoaded', function() {
    // Tüm fırsat butonlarını seç (büyük ve küçük butonlar)
    // Select all deal buttons (large and small buttons)
    const dealButtons = document.querySelectorAll('.deal-btn, .deal-btn-small');
    
    // Her fırsat butonuna tıklama olayı ekle
    // Add click event to each deal button
    dealButtons.forEach(btn => {
        btn.addEventListener('click', function(e) {
            // Varsayılan form gönderimini engelle
            // Prevent default form submission
            e.preventDefault();
            
            // En yakın fırsat kartını bul (öne çıkan veya normal kart)
            // Find closest deal card (featured or normal card)
            const card = this.closest('.featured-deal-card, .deal-card');
            
            // Fırsat başlığını al
            // Get deal title
            const title = card.querySelector('.deal-title').textContent;
            
            // Mevcut dil ayarını al (varsayılan: Türkçe)
            // Get current language setting (default: Turkish)
            const currentLanguage = localStorage.getItem('language') || 'tr';
            
            // Dil ayarına göre mesaj oluştur
            // Create message according to language setting
            const message = currentLanguage === 'tr' 
                ? `Rezervasyon sayfası yakında eklenecek.\n\nFırsat: ${title}`
                : `Booking page coming soon.\n\nDeal: ${title}`;
            
            // Bilgilendirme mesajı göster
            // Show information message
            alert(message);
        });
    });

    // Newsletter (haber bülteni) formunu seç
    // Select newsletter form
    const newsletterForm = document.querySelector('.newsletter-form');
    
    // Eğer form varsa gönderim olayı ekle
    // If form exists, add submit event
    if (newsletterForm) {
        newsletterForm.addEventListener('submit', handleNewsletter);
    }
});

// Newsletter form gönderim işleme fonksiyonu
// Newsletter form submission handler function
function handleNewsletter(event) {
    // Varsayılan form gönderimini engelle
    // Prevent default form submission
    event.preventDefault();
    
    // E-posta input alanından değeri al
    // Get value from email input field
    const email = event.target.querySelector('input[type="email"]').value;
    
    // Mevcut dil ayarını al (varsayılan: Türkçe)
    // Get current language setting (default: Turkish)
    const currentLanguage = localStorage.getItem('language') || 'tr';
    
    // Dil ayarına göre teşekkür mesajı oluştur
    // Create thank you message according to language setting
    const message = currentLanguage === 'tr' 
        ? `Teşekkürler! ${email} adresine fırsat bildirimleri gönderilecek.`
        : `Thank you! Deal notifications will be sent to ${email}.`;
    
    // Bilgilendirme mesajı göster
    // Show information message
    alert(message);
    
    // Formu temizle
    // Reset form
    event.target.reset();
}
