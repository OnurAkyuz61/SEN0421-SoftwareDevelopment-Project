/* ================== AUTH FUNCTIONS ================== */
// Kimlik doğrulama fonksiyonları

// Şifre göster/gizle
function togglePassword(inputId) {
    const input = document.getElementById(inputId);
    const button = input && input.nextElementSibling ? input.nextElementSibling.querySelector('i') : null;
    
    if (input && button) {
      if (input.type === 'password') {
        input.type = 'text';
        button.classList.remove('fa-eye');
        button.classList.add('fa-eye-slash');
      } else {
        input.type = 'password';
        button.classList.remove('fa-eye-slash');
        button.classList.add('fa-eye');
      }
    }
  }
  
  // TC Kimlik numarası input'unu aç/kapat
  function toggleTcInput() {
    const tcInput = document.getElementById('tcNumber');
    const checkbox = document.getElementById('notTurkishCitizen');
    
    if (tcInput && checkbox) {
      tcInput.disabled = checkbox.checked;
      if (checkbox.checked) {
        tcInput.value = '';
        tcInput.style.backgroundColor = '#f5f5f5';
        tcInput.style.color = '#999';
      } else {
        tcInput.style.backgroundColor = '';
        tcInput.style.color = '';
      }
    }
  }
  
  // Şifre gücü kontrolü
  function checkPasswordStrength() {
    const signupPasswordEl = document.getElementById('signupPassword');
    const password = signupPasswordEl ? signupPasswordEl.value : '';
    const strengthFill = document.getElementById('strengthFill');
    const strengthText = document.getElementById('strengthText');
    
    if (!strengthFill || !strengthText) return;
    
    let strength = 0;
    let strengthLabel = '';
    
    // Şifre uzunluğu kontrolü
    if (password.length >= 8) strength += 1;
    if (password.length >= 12) strength += 1;
    
    // Büyük harf kontrolü
    if (/[A-Z]/.test(password)) strength += 1;
    
    // Küçük harf kontrolü
    if (/[a-z]/.test(password)) strength += 1;
    
    // Rakam kontrolü
    if (/[0-9]/.test(password)) strength += 1;
    
    // Özel karakter kontrolü
    if (/[^A-Za-z0-9]/.test(password)) strength += 1;
    
    // Güç seviyesini belirle
    strengthFill.className = 'strength-fill';
    
    // Mevcut dil ayarını al (script.js'den)
    const currentLanguage = localStorage.getItem('language') || 'tr';
    
    if (password.length === 0) {
      strengthFill.style.width = '0%';
      strengthLabel = currentLanguage === 'tr' ? 'Şifre gücü' : 'Password strength';
    } else if (strength <= 2) {
      strengthFill.classList.add('weak');
      strengthFill.style.width = '25%';
      strengthLabel = currentLanguage === 'tr' ? 'Zayıf' : 'Weak';
    } else if (strength <= 3) {
      strengthFill.classList.add('fair');
      strengthFill.style.width = '50%';
      strengthLabel = currentLanguage === 'tr' ? 'Orta' : 'Fair';
    } else if (strength <= 4) {
      strengthFill.classList.add('good');
      strengthFill.style.width = '75%';
      strengthLabel = currentLanguage === 'tr' ? 'İyi' : 'Good';
    } else {
      strengthFill.classList.add('strong');
      strengthFill.style.width = '100%';
      strengthLabel = currentLanguage === 'tr' ? 'Güçlü' : 'Strong';
    }
    
    strengthText.textContent = strengthLabel;
  }
  
  // TC Kimlik numarası validasyonu
  function validateTcNumber(tcNumber) {
    if (!tcNumber || tcNumber.length !== 11) return false;
    
    // Tüm rakamlar aynı olamaz
    if (/^(\d)\1{10}$/.test(tcNumber)) return false;
    
    // İlk rakam 0 olamaz
    if (tcNumber[0] === '0') return false;
    
    // TC kimlik algoritması
    const digits = tcNumber.split('').map(Number);
    
    // İlk 10 rakamın toplamı
    const sum1 = digits.slice(0, 10).reduce((a, b) => a + b, 0);
    if (sum1 % 10 !== digits[10]) return false;
    
    // Tek ve çift pozisyonların toplamı
    const oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
    const evenSum = digits[1] + digits[3] + digits[5] + digits[7];
    
    if ((oddSum * 7 - evenSum) % 10 !== digits[9]) return false;
    
    return true;
  }
  
  // Kayıt formu işlemi
  function handleSignup(event) {
    event.preventDefault();
    
    const form = event.target;
    const formData = new FormData(form);
    const data = Object.fromEntries(formData);
    
    // Mevcut dil ayarını al
    const currentLanguage = localStorage.getItem('language') || 'tr';
    
    // TC Kimlik kontrolü (eğer TC vatandaşıysa)
    const notTurkishCitizen = data.notTurkishCitizen;
    if (!notTurkishCitizen && data.tcNumber) {
      if (!validateTcNumber(data.tcNumber)) {
        showAuthMessage('error', 
          currentLanguage === 'tr' ? 'Geçerli bir TC Kimlik numarası girin!' : 'Please enter a valid Turkish ID number!'
        );
        return;
      }
    }
    
    // Gerekli alanları kontrol et
    const requiredFields = ['firstName', 'lastName', 'email', 'phone', 'birthDate', 'gender', 'password', 'confirmPassword', 'kvkk', 'terms'];
    let isValid = true;
    
    for (let field of requiredFields) {
      if (!data[field] || data[field].trim() === '') {
        isValid = false;
        break;
      }
    }
    
    if (!isValid) {
      showAuthMessage('error', 
        currentLanguage === 'tr' ? 'Lütfen tüm zorunlu alanları doldurun!' : 'Please fill in all required fields!'
      );
      return;
    }
    
    // E-posta formatını kontrol et
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(data.email)) {
      showAuthMessage('error', 
        currentLanguage === 'tr' ? 'Geçerli bir e-posta adresi girin!' : 'Please enter a valid email address!'
      );
      return;
    }
    
    // Şifre kontrolü
    if (data.password.length < 8) {
      showAuthMessage('error', 
        currentLanguage === 'tr' ? 'Şifre en az 8 karakter olmalıdır!' : 'Password must be at least 8 characters!'
      );
      return;
    }
    
    // Şifre eşleşme kontrolü
    if (data.password !== data.confirmPassword) {
      showAuthMessage('error', 
        currentLanguage === 'tr' ? 'Şifreler eşleşmiyor!' : 'Passwords do not match!'
      );
      return;
    }
    
    console.log('Kayıt formu gönderildi:', data);
    
    showAuthMessage('success', 
      currentLanguage === 'tr' ? 'Kayıt işlemi başarıyla tamamlandı! Giriş yapabilirsiniz.' : 'Registration completed successfully! You can now sign in.'
    );
    
    // 2 saniye sonra login sayfasına yönlendir
    setTimeout(() => {
      window.location.href = '/Home/Login';
    }, 2000);
  }
  
  // Giriş formu işlemi
  function handleLogin(event) {
    event.preventDefault();
    
    const form = event.target;
    const formData = new FormData(form);
    const data = Object.fromEntries(formData);
    
    // Mevcut dil ayarını al
    const currentLanguage = localStorage.getItem('language') || 'tr';
    
    // Gerekli alanları kontrol et
    if (!data.email || !data.password) {
      showAuthMessage('error', 
        currentLanguage === 'tr' ? 'E-posta ve şifre alanları zorunludur!' : 'Email and password fields are required!'
      );
      return;
    }
    
    // E-posta formatını kontrol et
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(data.email)) {
      showAuthMessage('error', 
        currentLanguage === 'tr' ? 'Geçerli bir e-posta adresi girin!' : 'Please enter a valid email address!'
      );
      return;
    }
    
    console.log('Giriş formu gönderildi:', data);
    
    showAuthMessage('success', 
      currentLanguage === 'tr' ? 'Giriş yapılıyor... Ana sayfaya yönlendiriliyorsunuz.' : 'Signing in... Redirecting to homepage.'
    );
    
    // 2 saniye sonra ana sayfaya yönlendir
    setTimeout(() => {
      window.location.href = '/Home/Index';
    }, 2000);
  }
  
  // Auth mesajı gösterme fonksiyonu
  function showAuthMessage(type, message) {
    // Mevcut mesajları kaldır
    const existingMessages = document.querySelectorAll('.auth-message');
    existingMessages.forEach(msg => msg.remove());
    
    // Yeni mesaj oluştur
    const messageDiv = document.createElement('div');
    messageDiv.className = `auth-message ${type}`;
    messageDiv.innerHTML = `
      <i class="fa-solid ${type === 'success' ? 'fa-check-circle' : 'fa-exclamation-triangle'}"></i>
      <span>${message}</span>
    `;
    
    // Mesajı forma ekle
    const form = document.querySelector('.auth-form');
    if (form) {
      form.insertBefore(messageDiv, form.firstChild);
      messageDiv.style.display = 'flex';
      
      // Hata mesajları 5 saniye sonra kaybolur, başarı mesajları kalır
      if (type === 'error') {
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
  }
  
  // Sosyal medya giriş/kayıt
  function signupWithGoogle() {
    const currentLanguage = localStorage.getItem('language') || 'tr';
    console.log('Google ile kayıt');
    alert(currentLanguage === 'tr' ? 'Google ile kayıt özelliği yakında!' : 'Sign up with Google coming soon!');
  }
  
  function signupWithFacebook() {
    const currentLanguage = localStorage.getItem('language') || 'tr';
    console.log('Facebook ile kayıt');
    alert(currentLanguage === 'tr' ? 'Facebook ile kayıt özelliği yakında!' : 'Sign up with Facebook coming soon!');
  }
  
  function loginWithGoogle() {
    const currentLanguage = localStorage.getItem('language') || 'tr';
    console.log('Google ile giriş');
    alert(currentLanguage === 'tr' ? 'Google ile giriş özelliği yakında!' : 'Sign in with Google coming soon!');
  }
  
  function loginWithFacebook() {
    const currentLanguage = localStorage.getItem('language') || 'tr';
    console.log('Facebook ile giriş');
    alert(currentLanguage === 'tr' ? 'Facebook ile giriş özelliği yakında!' : 'Sign in with Facebook coming soon!');
  }
  
  // Şifremi unuttum modal
  function openForgotPasswordModal() {
    const modal = document.getElementById('forgotModal');
    if (modal) {
      modal.style.display = 'flex';
    }
  }
  
  function closeForgotPasswordModal() {
    const modal = document.getElementById('forgotModal');
    if (modal) {
      modal.style.display = 'none';
    }
  }
  
  function handleForgotPassword(event) {
    event.preventDefault();
    const forgotEmailEl = document.getElementById('forgotEmail');
    const email = forgotEmailEl ? forgotEmailEl.value : '';
    
    // Mevcut dil ayarını al
    const currentLanguage = localStorage.getItem('language') || 'tr';
    
    if (!email) {
      alert(currentLanguage === 'tr' ? 'Lütfen e-posta adresinizi girin!' : 'Please enter your email address!');
      return;
    }
    
    // E-posta formatını kontrol et
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
      alert(currentLanguage === 'tr' ? 'Geçerli bir e-posta adresi girin!' : 'Please enter a valid email address!');
      return;
    }
    
    console.log('Şifre sıfırlama e-postası gönderildi:', email);
    
    // Başarı mesajı
    alert(currentLanguage === 'tr' ? 
      'Şifre sıfırlama bağlantısı e-posta adresinize gönderildi! E-posta kutunuzu kontrol edin.' : 
      'Password reset link sent to your email! Please check your inbox.'
    );
    
    closeForgotPasswordModal();
  }
