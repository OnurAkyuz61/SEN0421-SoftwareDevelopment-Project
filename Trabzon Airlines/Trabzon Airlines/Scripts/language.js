// Dil sistemi fonksiyonları

// Dil modal'ını aç
function openLangModal() {
  const modal = document.getElementById("langModal");
  if (modal) {
    // Mevcut ayarları modal'da göster
    const currencySelect = document.getElementById("currencySelect");
    const languageSelect = document.getElementById("languageSelect");
    
    if (currencySelect) {
      currencySelect.value = localStorage.getItem("currency") || "₺";
    }
    
    if (languageSelect) {
      languageSelect.value = localStorage.getItem("language") || "tr";
    }
    
    modal.classList.add("show");
    modal.style.display = "flex";
  }
}

// Dil modal'ını kapat ve ayarları uygula
function closeLangModal() {
  const modal = document.getElementById("langModal");
  if (modal) {
    modal.classList.remove("show");
    modal.style.display = "none";
  }
}

// Dil değişikliğini onayla ve uygula
function confirmLanguageChange() {
  const currencySelect = document.getElementById("currencySelect");
  const languageSelect = document.getElementById("languageSelect");
  
  if (currencySelect && languageSelect) {
    const selectedCurrency = currencySelect.value;
    const selectedLanguage = languageSelect.value;

    // Ayarları localStorage'a kaydet
    localStorage.setItem("currency", selectedCurrency);
    localStorage.setItem("language", selectedLanguage);
    
    // Dil kodunu güncelle
    if (typeof currentLanguage !== 'undefined') {
      currentLanguage = selectedLanguage;
    }
    
    console.log('Dil değiştirildi:', selectedLanguage);
    
    // Ayarları uygula
    applyLanguageSettings();
    updatePageLanguage();
  }
  
  closeLangModal();
}

// Dil ve para birimi ayarlarını uygula
function applyLanguageSettings() {
  const currency = localStorage.getItem("currency") || "₺";
  const language = localStorage.getItem("language") || "tr";
  
  // Global dil değişkenini güncelle
  if (typeof currentLanguage !== 'undefined') {
    currentLanguage = language;
  }
  
  // Header'daki dil ve para birimi göstergelerini güncelle
  const headerCurrency = document.getElementById("headerCurrency");
  const headerLang = document.getElementById("headerLang");
  const headerFlag = document.getElementById("headerFlag");
  
  if (headerCurrency) headerCurrency.textContent = currency;
  
  if (headerLang && headerFlag) {
    if (language === "tr") {
      headerLang.textContent = "Türkçe";
      headerFlag.src = "https://flagcdn.com/w20/tr.png";
    } else {
      headerLang.textContent = "English";
      headerFlag.src = "https://flagcdn.com/w20/us.png";
    }
  }
  
  // Auth sayfalarında da güncelle
  const authFlag = document.getElementById("authFlag");
  const authLang = document.getElementById("authLang");
  
  if (authFlag && authLang) {
    if (language === "tr") {
      authLang.textContent = "TR";
      authFlag.src = "https://flagcdn.com/w20/tr.png";
    } else {
      authLang.textContent = "EN";
      authFlag.src = "https://flagcdn.com/w20/us.png";
    }
  }
  
  // Modal select'leri de güncelle
  const currencySelect = document.getElementById("currencySelect");
  const languageSelect = document.getElementById("languageSelect");
  
  if (currencySelect) {
    currencySelect.value = currency;
  }
  
  if (languageSelect) {
    languageSelect.value = language;
  }
}

// Sayfadaki tüm metinleri güncelle
function updatePageLanguage() {
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  const elements = document.querySelectorAll('[data-tr][data-en]');
  
  elements.forEach(element => {
    const trText = element.getAttribute('data-tr');
    const enText = element.getAttribute('data-en');
    
    if (currentLang === 'tr' && trText) {
      element.textContent = trText;
    } else if (currentLang === 'en' && enText) {
      element.textContent = enText;
    }
  });
  
  // Placeholder'ları güncelle
  const inputElements = document.querySelectorAll('[data-placeholder-tr][data-placeholder-en]');
  
  inputElements.forEach(element => {
    const trPlaceholder = element.getAttribute('data-placeholder-tr');
    const enPlaceholder = element.getAttribute('data-placeholder-en');
    
    if (currentLang === 'tr' && trPlaceholder) {
      element.placeholder = trPlaceholder;
    } else if (currentLang === 'en' && enPlaceholder) {
      element.placeholder = enPlaceholder;
    }
  });
  
  // Sayfa başlığını güncelle
  const titleElement = document.querySelector('title[data-tr][data-en]');
  if (titleElement) {
    const trTitle = titleElement.getAttribute('data-tr');
    const enTitle = titleElement.getAttribute('data-en');
    
    if (currentLang === 'tr' && trTitle) {
      titleElement.textContent = trTitle;
    } else if (currentLang === 'en' && enTitle) {
      titleElement.textContent = enTitle;
    }
  }
  
  // Modal option'larını da güncelle
  updateModalOptions();
}

// Modal option'larını güncelle
function updateModalOptions() {
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  const modalOptions = document.querySelectorAll('#currencySelect option, #languageSelect option');
  
  modalOptions.forEach(option => {
    const trText = option.getAttribute('data-tr');
    const enText = option.getAttribute('data-en');
    
    if (currentLang === 'tr' && trText) {
      option.textContent = trText;
    } else if (currentLang === 'en' && enText) {
      option.textContent = enText;
    }
  });
}

// Test fonksiyonu - dil değiştirmeyi test etmek için
function testLanguageSwitch() {
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  const newLang = currentLang === 'tr' ? 'en' : 'tr';
  localStorage.setItem('language', newLang);
  if (typeof currentLanguage !== 'undefined') {
    currentLanguage = newLang;
  }
  applyLanguageSettings();
  updatePageLanguage();
  console.log('Test: Dil değiştirildi ->', newLang);
}

