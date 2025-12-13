// Form işlemleri ve validasyon

// İletişim formu gönderimi - Form server'a gönderilecek
// Contact form submission - Form will be submitted to server
function submitContactForm(event) {
  // Form doğrulama - Form validation
  const form = event.target;
  const formData = new FormData(form);
  const data = Object.fromEntries(formData);
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  // Gerekli alanları kontrol et - Check required fields
  const requiredFields = ['fullName', 'email', 'subject', 'message', 'kvkk'];
  let isValid = true;
  
  for (let field of requiredFields) {
    if (!data[field] || data[field].trim() === '') {
      isValid = false;
      break;
    }
  }
  
  if (!isValid) {
    event.preventDefault();
    showFormMessage('error', 
      currentLang === 'tr' ? 'Lütfen tüm zorunlu alanları doldurun!' : 'Please fill in all required fields!'
    );
    return false;
  }
  
  // E-posta formatını kontrol et - Check email format
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(data.email)) {
    event.preventDefault();
    showFormMessage('error', 
      currentLang === 'tr' ? 'Geçerli bir e-posta adresi girin!' : 'Please enter a valid email address!'
    );
    return false;
  }
  
  // Form geçerli, server'a gönderilecek - Form is valid, will be submitted to server
  return true;
}

// Form mesajı gösterme fonksiyonu
function showFormMessage(type, message) {
  // Mevcut mesajları kaldır
  const existingMessages = document.querySelectorAll('.form-message, .auth-message');
  existingMessages.forEach(msg => msg.remove());
  
  // Yeni mesaj oluştur
  const messageDiv = document.createElement('div');
  messageDiv.className = `form-message ${type}`;
  messageDiv.innerHTML = `
    <i class="fa-solid ${type === 'success' ? 'fa-check-circle' : 'fa-exclamation-triangle'}"></i>
    <span>${message}</span>
  `;
  
  // Mesajı forma ekle
  const form = document.querySelector('.contact-form, .auth-form');
  if (form) {
    form.insertBefore(messageDiv, form.firstChild);
    messageDiv.style.display = 'flex';
    
    // 5 saniye sonra mesajı kaldır
    setTimeout(() => {
      messageDiv.style.opacity = '0';
      setTimeout(() => {
        if (messageDiv.parentNode) {
          messageDiv.remove();
        }
      }, 300);
    }, 5000);
  }
}

// FAQ açma/kapama
function toggleFaq(faqNumber) {
  const faqAnswer = document.getElementById(`faq-${faqNumber}`);
  const faqQuestion = faqAnswer ? faqAnswer.previousElementSibling : null;
  
  if (faqAnswer) {
    const isOpen = faqAnswer.style.display === 'block';
    
    // Tüm FAQ'ları kapat
    document.querySelectorAll('.faq-answer').forEach(answer => {
      answer.style.display = 'none';
    });
    
    document.querySelectorAll('.faq-question i').forEach(icon => {
      icon.classList.remove('fa-chevron-up');
      icon.classList.add('fa-chevron-down');
    });
    
    // Seçilen FAQ'ı aç (eğer kapalıysa)
    if (!isOpen) {
      faqAnswer.style.display = 'block';
      const icon = faqQuestion ? faqQuestion.querySelector('i') : null;
      if (icon) {
        icon.classList.remove('fa-chevron-down');
        icon.classList.add('fa-chevron-up');
      }
    }
  }
}

