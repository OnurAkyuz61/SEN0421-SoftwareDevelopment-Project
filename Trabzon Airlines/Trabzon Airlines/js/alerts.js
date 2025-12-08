// Arama fonksiyonu
function toggleSearch() {
  showComingSoonAlert('search');
}

// Yakında eklenecek özellikler için uyarı fonksiyonu
function showComingSoonAlert(feature) {
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  let title, message;
  
  // Özellik türüne göre mesajları belirle
  switch(feature) {
    case 'login':
      title = currentLang === 'tr' ? '🔐 Giriş Yap' : '🔐 Login';
      message = currentLang === 'tr' ? 
        'Giriş yapma özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Login feature coming soon!\n\nCurrently under development.';
      break;
    case 'signup':
      title = currentLang === 'tr' ? '📝 Kayıt Ol' : '📝 Sign Up';
      message = currentLang === 'tr' ? 
        'Kayıt olma özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Sign up feature coming soon!\n\nCurrently under development.';
      break;
    case 'search':
      title = currentLang === 'tr' ? '✈️ Uçuş Arama' : '✈️ Flight Search';
      message = currentLang === 'tr' ? 
        'Uçuş arama özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Flight search feature coming soon!\n\nCurrently under development.';
      break;
    case 'booking':
      title = currentLang === 'tr' ? '🎫 Rezervasyon' : '🎫 Booking';
      message = currentLang === 'tr' ? 
        'Rezervasyon özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Booking feature coming soon!\n\nCurrently under development.';
      break;
    case 'packages':
      title = currentLang === 'tr' ? '🧳 Paketler' : '🧳 Packages';
      message = currentLang === 'tr' ? 
        'Paket turlar özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Package tours feature coming soon!\n\nCurrently under development.';
      break;
    case 'deals':
      title = currentLang === 'tr' ? '🏷️ Fırsatlar' : '🏷️ Deals';
      message = currentLang === 'tr' ? 
        'Özel fırsatlar özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Special deals feature coming soon!\n\nCurrently under development.';
      break;
    case 'trips':
      title = currentLang === 'tr' ? '🗺️ Seyahatler' : '🗺️ Trips';
      message = currentLang === 'tr' ? 
        'Seyahat planları özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Trip planning feature coming soon!\n\nCurrently under development.';
      break;
    case 'checkin':
      title = currentLang === 'tr' ? '✅ Check-in' : '✅ Check-in';
      message = currentLang === 'tr' ? 
        'Online check-in özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Online check-in feature coming soon!\n\nCurrently under development.';
      break;
    case 'status':
      title = currentLang === 'tr' ? '📊 Uçuş Durumu' : '📊 Flight Status';
      message = currentLang === 'tr' ? 
        'Uçuş durumu sorgulama özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Flight status feature coming soon!\n\nCurrently under development.';
      break;
    case 'support':
      title = currentLang === 'tr' ? '🎧 Destek' : '🎧 Support';
      message = currentLang === 'tr' ? 
        'Müşteri destek özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Customer support feature coming soon!\n\nCurrently under development.';
      break;
    case 'tripai':
      title = currentLang === 'tr' ? '🤖 Trip AI' : '🤖 Trip AI';
      message = currentLang === 'tr' ? 
        'AI destekli seyahat planlama özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'AI-powered trip planning feature coming soon!\n\nCurrently under development.';
      break;
    case 'more':
      title = currentLang === 'tr' ? '➕ Daha Fazla' : '➕ More';
      message = currentLang === 'tr' ? 
        'Ek özellikler yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Additional features coming soon!\n\nCurrently under development.';
      break;
    case 'privacy':
      title = currentLang === 'tr' ? '🔒 Gizlilik Politikası' : '🔒 Privacy Policy';
      message = currentLang === 'tr' ? 
        'Gizlilik politikası sayfası yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Privacy policy page coming soon!\n\nCurrently under development.';
      break;
    case 'contact':
      title = currentLang === 'tr' ? '📧 İletişim Formu' : '📧 Contact Form';
      message = currentLang === 'tr' ? 
        'İletişim formu gönderme özelliği yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'Contact form submission feature coming soon!\n\nCurrently under development.';
      break;
    default:
      title = currentLang === 'tr' ? '🚧 Yakında' : '🚧 Coming Soon';
      message = currentLang === 'tr' ? 
        'Bu özellik yakında eklenecek!\n\nŞu anda geliştirme aşamasında.' : 
        'This feature coming soon!\n\nCurrently under development.';
  }
  
  // Güzel bir alert göster
  alert(title + '\n\n' + message);
}

