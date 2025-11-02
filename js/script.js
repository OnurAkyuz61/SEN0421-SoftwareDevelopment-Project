// Mevcut dil ayarı
let currentLanguage = localStorage.getItem('language') || 'tr';

/* ================== SIDEBAR CONTROL ================== */
// Desktop sidebar daraltma/genişletme
function toggleSidebar() {
  const sidebar = document.getElementById("sidebar");
  const icon = document.getElementById("toggleIcon");
  const body = document.body;

  // Sidebar durumunu değiştir
  sidebar.classList.toggle("collapsed");
  body.classList.toggle("sidebar-collapsed");

  // İkon yönünü güncelle
  if (sidebar.classList.contains("collapsed")) {
    icon.classList.remove("fa-angles-left");
    icon.classList.add("fa-angles-right");
  } else {
    icon.classList.remove("fa-angles-right");
    icon.classList.add("fa-angles-left");
  }

  // Durumu localStorage'a kaydet
  localStorage.setItem('sidebarCollapsed', sidebar.classList.contains("collapsed"));
}

/* ===================== MOBILE DRAWER ===================== */
function toggleSidebarMobile() {
  const sidebar = document.getElementById("sidebar");
  const overlay = document.getElementById("drawerOverlay");
  const bottomNav = document.querySelector(".mobile-bottom-nav");

  sidebar.classList.toggle("is-open");
  overlay.classList.toggle("show");
  bottomNav.classList.toggle("hide");

  document.body.style.overflow = sidebar.classList.contains("is-open") ? "hidden" : "";
}

function closeMobileSidebar() {
  const sidebar = document.getElementById("sidebar");
  const overlay = document.getElementById("drawerOverlay");
  const bottomNav = document.querySelector(".mobile-bottom-nav");

  sidebar.classList.remove("is-open");
  overlay.classList.remove("show");
  bottomNav.classList.remove("hide");
  document.body.style.overflow = "";
}

/* Ekran desktop'a dönünce mobil menüyü kapat */
function autoSidebar() {
  if (window.innerWidth > 992) closeMobileSidebar();
}

/* Escape ile kapatma */
document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") closeMobileSidebar();
});

window.addEventListener("resize", autoSidebar);

/* ================== LANGUAGE SYSTEM ================== */
// Çift dil desteği sistemi

// Dil modal'ını aç
function openLangModal() {
  const modal = document.getElementById("langModal");
  if (modal) {
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
    currentLanguage = selectedLanguage;
    
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
}

// Sayfadaki tüm metinleri güncelle
function updatePageLanguage() {
  const elements = document.querySelectorAll('[data-tr][data-en]');
  
  elements.forEach(element => {
    const trText = element.getAttribute('data-tr');
    const enText = element.getAttribute('data-en');
    
    if (currentLanguage === 'tr' && trText) {
      element.textContent = trText;
    } else if (currentLanguage === 'en' && enText) {
      element.textContent = enText;
    }
  });
  
  // Placeholder'ları güncelle
  const inputElements = document.querySelectorAll('[data-placeholder-tr][data-placeholder-en]');
  
  inputElements.forEach(element => {
    const trPlaceholder = element.getAttribute('data-placeholder-tr');
    const enPlaceholder = element.getAttribute('data-placeholder-en');
    
    if (currentLanguage === 'tr' && trPlaceholder) {
      element.placeholder = trPlaceholder;
    } else if (currentLanguage === 'en' && enPlaceholder) {
      element.placeholder = enPlaceholder;
    }
  });
  
  // Sayfa başlığını güncelle
  const titleElement = document.querySelector('title[data-tr][data-en]');
  if (titleElement) {
    const trTitle = titleElement.getAttribute('data-tr');
    const enTitle = titleElement.getAttribute('data-en');
    
    if (currentLanguage === 'tr' && trTitle) {
      titleElement.textContent = trTitle;
    } else if (currentLanguage === 'en' && enTitle) {
      titleElement.textContent = enTitle;
    }
  }
}

/* ================== FLIGHT SEARCH FUNCTIONS ================== */
// Uçuş arama formu fonksiyonları

// Türkiye'deki tüm havalimanları listesi
const airports = [
  // Uluslararası Havalimanları
  { code: 'ADA', name: 'Adana', fullName: 'Şakirpaşa Havalimanı', enName: 'Adana', enFullName: 'Sakirpasa Airport', category: 'international' },
  { code: 'ESB', name: 'Ankara', fullName: 'Ankara Esenboğa Havalimanı', enName: 'Ankara', enFullName: 'Ankara Esenboga Airport', category: 'international' },
  { code: 'AYT', name: 'Antalya', fullName: 'Antalya Havalimanı', enName: 'Antalya', enFullName: 'Antalya Airport', category: 'international' },
  { code: 'GZP', name: 'Alanya', fullName: 'Gazipaşa Havalimanı', enName: 'Alanya', enFullName: 'Gazipasa Airport', category: 'international' },
  { code: 'EDO', name: 'Balıkesir', fullName: 'Balıkesir Koca Seyit Havalimanı', enName: 'Balikesir', enFullName: 'Balikesir Koca Seyit Airport', category: 'international' },
  { code: 'YEI', name: 'Bursa', fullName: 'Bursa Yenişehir Havalimanı', enName: 'Bursa', enFullName: 'Bursa Yenisehir Airport', category: 'international' },
  { code: 'CKZ', name: 'Çanakkale', fullName: 'Çanakkale Havalimanı', enName: 'Canakkale', enFullName: 'Canakkale Airport', category: 'international' },
  { code: 'DNZ', name: 'Denizli', fullName: 'Denizli Çardak Havalimanı', enName: 'Denizli', enFullName: 'Denizli Cardak Airport', category: 'international' },
  { code: 'DIY', name: 'Diyarbakır', fullName: 'Diyarbakır Havalimanı', enName: 'Diyarbakir', enFullName: 'Diyarbakir Airport', category: 'international' },
  { code: 'EZS', name: 'Elazığ', fullName: 'Elazığ Havalimanı', enName: 'Elazig', enFullName: 'Elazig Airport', category: 'international' },
  { code: 'ERZ', name: 'Erzurum', fullName: 'Erzurum Havalimanı', enName: 'Erzurum', enFullName: 'Erzurum Airport', category: 'international' },
  { code: 'AOE', name: 'Eskişehir', fullName: 'Hasan Polatkan Havalimanı', enName: 'Eskisehir', enFullName: 'Hasan Polatkan Airport', category: 'international' },
  { code: 'GZT', name: 'Gaziantep', fullName: 'Gaziantep Havalimanı', enName: 'Gaziantep', enFullName: 'Gaziantep Airport', category: 'international' },
  { code: 'HTY', name: 'Hatay', fullName: 'Hatay Havalimanı', enName: 'Hatay', enFullName: 'Hatay Airport', category: 'international' },
  { code: 'ISE', name: 'Isparta', fullName: 'Isparta Süleyman Demirel Havalimanı', enName: 'Isparta', enFullName: 'Isparta Suleyman Demirel Airport', category: 'international' },
  { code: 'IST', name: 'İstanbul', fullName: 'İstanbul Havalimanı', enName: 'Istanbul', enFullName: 'Istanbul Airport', category: 'international' },
  { code: 'SAW', name: 'İstanbul Sabiha', fullName: 'Sabiha Gökçen Havalimanı', enName: 'Istanbul Sabiha', enFullName: 'Sabiha Gokcen Airport', category: 'international' },
  { code: 'ADB', name: 'İzmir', fullName: 'Adnan Menderes Havalimanı', enName: 'Izmir', enFullName: 'Adnan Menderes Airport', category: 'international' },
  { code: 'KSY', name: 'Kars', fullName: 'Kars Harakani Havalimanı', enName: 'Kars', enFullName: 'Kars Harakani Airport', category: 'international' },
  { code: 'ASR', name: 'Kayseri', fullName: 'Erkilet Havalimanı', enName: 'Kayseri', enFullName: 'Erkilet Airport', category: 'international' },
  { code: 'KCO', name: 'Kocaeli', fullName: 'Cengiz Topel Havalimanı', enName: 'Kocaeli', enFullName: 'Cengiz Topel Airport', category: 'international' },
  { code: 'KYA', name: 'Konya', fullName: 'Konya Havalimanı', enName: 'Konya', enFullName: 'Konya Airport', category: 'international' },
  { code: 'KZR', name: 'Kütahya', fullName: 'Zafer Havalimanı', enName: 'Kutahya', enFullName: 'Zafer Airport', category: 'international' },
  { code: 'MLX', name: 'Malatya', fullName: 'Malatya Havalimanı', enName: 'Malatya', enFullName: 'Malatya Airport', category: 'international' },
  { code: 'DLM', name: 'Muğla Dalaman', fullName: 'Dalaman Havalimanı', enName: 'Mugla Dalaman', enFullName: 'Dalaman Airport', category: 'international' },
  { code: 'BJV', name: 'Muğla Bodrum', fullName: 'Milas-Bodrum Havalimanı', enName: 'Mugla Bodrum', enFullName: 'Milas-Bodrum Airport', category: 'international' },
  { code: 'COV', name: 'Mersin', fullName: 'Çukurova Uluslararası Havalimanı', enName: 'Mersin', enFullName: 'Cukurova International Airport', category: 'international' },
  { code: 'NAV', name: 'Nevşehir', fullName: 'Nevşehir Kapadokya Havalimanı', enName: 'Nevsehir', enFullName: 'Nevsehir Cappadocia Airport', category: 'international' },
  { code: 'OGU', name: 'Ordu-Giresun', fullName: 'Ordu-Giresun Havalimanı', enName: 'Ordu-Giresun', enFullName: 'Ordu-Giresun Airport', category: 'international' },
  { code: 'RZV', name: 'Rize-Artvin', fullName: 'Rize-Artvin Havalimanı', enName: 'Rize-Artvin', enFullName: 'Rize-Artvin Airport', category: 'international' },
  { code: 'SZF', name: 'Samsun', fullName: 'Samsun Çarşamba Havalimanı', enName: 'Samsun', enFullName: 'Samsun Carsamba Airport', category: 'international' },
  { code: 'NOP', name: 'Sinop', fullName: 'Sinop Havalimanı', enName: 'Sinop', enFullName: 'Sinop Airport', category: 'international' },
  { code: 'VAS', name: 'Sivas', fullName: 'Sivas Nuri Demirağ Havalimanı', enName: 'Sivas', enFullName: 'Sivas Nuri Demirag Airport', category: 'international' },
  { code: 'GNY', name: 'Şanlıurfa', fullName: 'Şanlıurfa GAP Havalimanı', enName: 'Sanliurfa', enFullName: 'Sanliurfa GAP Airport', category: 'international' },
  { code: 'TEQ', name: 'Tekirdağ', fullName: 'Tekirdağ Çorlu Havalimanı', enName: 'Tekirdag', enFullName: 'Tekirdag Corlu Airport', category: 'international' },
  { code: 'TZX', name: 'Trabzon', fullName: 'Trabzon Havalimanı', enName: 'Trabzon', enFullName: 'Trabzon Airport', category: 'international' },
  { code: 'USQ', name: 'Uşak', fullName: 'Uşak Havalimanı', enName: 'Usak', enFullName: 'Usak Airport', category: 'international' },
  { code: 'VAN', name: 'Van', fullName: 'Van Ferit Melen Havalimanı', enName: 'Van', enFullName: 'Van Ferit Melen Airport', category: 'international' },
  { code: 'ONQ', name: 'Zonguldak', fullName: 'Zonguldak Havalimanı', enName: 'Zonguldak', enFullName: 'Zonguldak Airport', category: 'international' },
  
  // Yerel Havalimanları
  { code: 'ADF', name: 'Adıyaman', fullName: 'Adıyaman Havalimanı', enName: 'Adiyaman', enFullName: 'Adiyaman Airport', category: 'domestic' },
  { code: 'AJI', name: 'Ağrı', fullName: 'Ağrı Ahmed-i Hani Havalimanı', enName: 'Agri', enFullName: 'Agri Ahmed-i Hani Airport', category: 'domestic' },
  { code: 'MZH', name: 'Amasya', fullName: 'Amasya Merzifon Havalimanı', enName: 'Amasya', enFullName: 'Amasya Merzifon Airport', category: 'domestic' },
  { code: 'CII', name: 'Aydın', fullName: 'Aydın Çıldır Havalimanı', enName: 'Aydin', enFullName: 'Aydin Cildir Airport', category: 'domestic' },
  { code: 'BZI', name: 'Balıkesir Merkez', fullName: 'Balıkesir Merkez Havalimanı', enName: 'Balikesir Center', enFullName: 'Balikesir Center Airport', category: 'domestic' },
  { code: 'BAL', name: 'Batman', fullName: 'Batman Havalimanı', enName: 'Batman', enFullName: 'Batman Airport', category: 'domestic' },
  { code: 'BGG', name: 'Bingöl', fullName: 'Bingöl Havalimanı', enName: 'Bingol', enFullName: 'Bingol Airport', category: 'domestic' },
  { code: 'BTZ', name: 'Bursa Yunuseli', fullName: 'Bursa Yunuseli Havalimanı', enName: 'Bursa Yunuseli', enFullName: 'Bursa Yunuseli Airport', category: 'domestic' },
  { code: 'GKD', name: 'Gökçeada', fullName: 'Gökçeada Havalimanı', enName: 'Gokceada', enFullName: 'Gokceada Airport', category: 'domestic' },
  { code: 'ERC', name: 'Erzincan', fullName: 'Erzincan Yıldırım Akbulut Havalimanı', enName: 'Erzincan', enFullName: 'Erzincan Yildirim Akbulut Airport', category: 'domestic' },
  { code: 'YKO', name: 'Hakkâri', fullName: 'Hakkâri Yüksekova Selahaddin Eyyubi Havalimanı', enName: 'Hakkari', enFullName: 'Hakkari Yuksekova Selahaddin Eyyubi Airport', category: 'domestic' },
  { code: 'IGD', name: 'Iğdır', fullName: 'Iğdır Şehit Bülent Aydın Havalimanı', enName: 'Igdir', enFullName: 'Igdir Sehit Bulent Aydin Airport', category: 'domestic' },
  { code: 'IZM', name: 'İzmir Selçuk', fullName: 'Selçuk-Efes Havalimanı', enName: 'Izmir Selcuk', enFullName: 'Selcuk-Efes Airport', category: 'domestic' },
  { code: 'KCM', name: 'Kahramanmaraş', fullName: 'Kahramanmaraş Havalimanı', enName: 'Kahramanmaras', enFullName: 'Kahramanmaras Airport', category: 'domestic' },
  { code: 'KFS', name: 'Kastamonu', fullName: 'Kastamonu Havalimanı', enName: 'Kastamonu', enFullName: 'Kastamonu Airport', category: 'domestic' },
  { code: 'MQM', name: 'Mardin', fullName: 'Mardin Prof. Dr. Aziz Sancar Havalimanı', enName: 'Mardin', enFullName: 'Mardin Prof. Dr. Aziz Sancar Airport', category: 'domestic' },
  { code: 'MSR', name: 'Muş', fullName: 'Muş Havalimanı', enName: 'Mus', enFullName: 'Mus Airport', category: 'domestic' },
  { code: 'SXZ', name: 'Siirt', fullName: 'Siirt Havalimanı', enName: 'Siirt', enFullName: 'Siirt Airport', category: 'domestic' },
  { code: 'NKT', name: 'Şırnak', fullName: 'Şırnak Şerafettin Elçi Havalimanı', enName: 'Sirnak', enFullName: 'Sirnak Serafettin Elci Airport', category: 'domestic' },
  { code: 'TJK', name: 'Tokat', fullName: 'Tokat Havalimanı', enName: 'Tokat', enFullName: 'Tokat Airport', category: 'domestic' }
];

const airportListEl = document.getElementById("airportList");

// Listeyi modal içine yazdır
function renderAirportList(list) {
  airportListEl.innerHTML = list.map(a => `
    <div class="airport-item" 
         data-code="${a.code}" 
         data-city="${a.name}" 
         data-full="${a.fullName}">
      <span class="airport-code-modal">${a.code}</span>
      <div class="airport-info">
        <span class="airport-city-modal">${a.name}</span>
        <small class="airport-full-modal">${a.fullName}</small>
      </div>
    </div>
  `).join("");
}

// Sayfa yüklenince listeyi göster
renderAirportList(airports);



// Yolcu sayıları
let passengerCounts = {
  adult: 1,
  child: 0,
  infant: 0
};

let selectedCabinClass = 'economy';
let currentAirportSelection = null; // 'departure' veya 'destination'

// Havalimanı modal'ını aç
function openAirportModal(type) {
  currentAirportSelection = type;
  const modal = document.getElementById('airportModal');
  const title = document.getElementById('airportModalTitle');
  
  // Önceki hata mesajlarını temizle
  clearAirportSelectionError();
  
  if (currentLanguage === 'tr') {
    title.textContent = type === 'departure' ? 'Kalkış Havalimanı Seçin' : 'Varış Havalimanı Seçin';
  } else {
    title.textContent = type === 'departure' ? 'Select Departure Airport' : 'Select Destination Airport';
  }
  
  loadAirportList();
  modal.classList.add('show');
  modal.style.display = 'flex';
}

// Havalimanı modal'ını kapat
function closeAirportModal() {
  const modal = document.getElementById('airportModal');
  modal.classList.remove('show');
  modal.style.display = 'none';
  currentAirportSelection = null;
}

// Havalimanı listesini yükle
function loadAirportList(searchTerm = '') {
  const airportList = document.getElementById('airportList');
  airportList.innerHTML = '';
  
  const filteredAirports = airports.filter(airport => {
    const searchLower = searchTerm.toLowerCase();
    const name = currentLanguage === 'tr' ? airport.name : airport.enName;
    const fullName = currentLanguage === 'tr' ? airport.fullName : airport.enFullName;
    
    return airport.code.toLowerCase().includes(searchLower) ||
           name.toLowerCase().includes(searchLower) ||
           fullName.toLowerCase().includes(searchLower);
  });
  
  // Kategorilere göre grupla
  const internationalAirports = filteredAirports.filter(airport => airport.category === 'international');
  const domesticAirports = filteredAirports.filter(airport => airport.category === 'domestic');
  
  // Uluslararası havalimanları
  if (internationalAirports.length > 0) {
    const internationalHeader = document.createElement('div');
    internationalHeader.className = 'airport-category-header';
    internationalHeader.innerHTML = `<strong>${currentLanguage === 'tr' ? 'Uluslararası Havalimanları' : 'International Airports'}</strong>`;
    airportList.appendChild(internationalHeader);
    
    internationalAirports.forEach(airport => {
      const airportItem = createAirportItem(airport);
      airportList.appendChild(airportItem);
    });
  }
  
  // Yerel havalimanları
  if (domesticAirports.length > 0) {
    const domesticHeader = document.createElement('div');
    domesticHeader.className = 'airport-category-header';
    domesticHeader.innerHTML = `<strong>${currentLanguage === 'tr' ? 'Yerel Havalimanları' : 'Domestic Airports'}</strong>`;
    airportList.appendChild(domesticHeader);
    
    domesticAirports.forEach(airport => {
      const airportItem = createAirportItem(airport);
      airportList.appendChild(airportItem);
    });
  }
  
  // Sonuç bulunamadı
  if (filteredAirports.length === 0) {
    const noResults = document.createElement('div');
    noResults.className = 'no-results';
    noResults.innerHTML = `<p>${currentLanguage === 'tr' ? 'Havalimanı bulunamadı' : 'No airports found'}</p>`;
    airportList.appendChild(noResults);
  }
}

// Havalimanı item'ı oluştur
function createAirportItem(airport) {
  const airportItem = document.createElement('div');
  airportItem.className = 'airport-item';
  
  // Şu anda seçili havalimanlarını kontrol et
  const departureCode = document.getElementById('departureCode')?.textContent;
  const destinationCode = document.getElementById('destinationCode')?.textContent;
  
  const isCurrentlySelected = airport.code === departureCode || airport.code === destinationCode;
  const isOtherSelection = (currentAirportSelection === 'departure' && airport.code === destinationCode) ||
                          (currentAirportSelection === 'destination' && airport.code === departureCode);
  
  if (isOtherSelection) {
    // Diğer seçimde kullanılan havalimanını devre dışı bırak
    airportItem.className += ' disabled';
    airportItem.style.cssText = `
      opacity: 0.5;
      cursor: not-allowed;
      background-color: #f8f9fa;
      pointer-events: none;
    `;
  } else {
    airportItem.onclick = () => selectAirport(airport);
  }
  
  const name = currentLanguage === 'tr' ? airport.name : airport.enName;
  const fullName = currentLanguage === 'tr' ? airport.fullName : airport.enFullName;
  
  let statusIcon = '';
  if (isOtherSelection) {
    statusIcon = '<i class="fa-solid fa-ban" style="color: #dc3545; margin-left: 10px;"></i>';
  } else if (isCurrentlySelected) {
    statusIcon = '<i class="fa-solid fa-check" style="color: #28a745; margin-left: 10px;"></i>';
  }
  
  airportItem.innerHTML = `
    <span class="airport-code-modal">${airport.code}</span>
    <strong>${name}</strong>${statusIcon}<br>
    <small>${fullName}</small>
  `;
  
  return airportItem;
}

// Havalimanı seç
function selectAirport(airport) {
  if (!currentAirportSelection) return;
  
  // Multi-city segment kontrolü
  if (currentAirportSelection.startsWith('segment-')) {
    handleSegmentAirportSelection(airport);
    return;
  }
  
  // Normal uçuş için aynı havalimanının seçilmesini engelle
  const otherSelection = currentAirportSelection === 'departure' ? 'destination' : 'departure';
  const otherCodeElement = document.getElementById(otherSelection + 'Code');
  
  if (otherCodeElement && otherCodeElement.textContent === airport.code) {
    // Hata mesajı göster
    const errorMessage = currentLanguage === 'tr' ? 
      'Gidiş ve dönüş için aynı havalimanını seçemezsiniz!' : 
      'You cannot select the same airport for departure and destination!';
    
    showAirportSelectionError(errorMessage);
    return;
  }
  
  const codeElement = document.getElementById(currentAirportSelection + 'Code');
  const nameElement = document.getElementById(currentAirportSelection + 'Name');
  
  if (codeElement && nameElement) {
    codeElement.textContent = airport.code;
    
    if (currentLanguage === 'tr') {
      nameElement.textContent = airport.name;
    } else {
      nameElement.textContent = airport.enName;
    }
  }
  
  closeAirportModal();
}

// Multi-city segment havalimanı seçimi
function handleSegmentAirportSelection(airport) {
  const parts = currentAirportSelection.split('-');
  const segmentId = parts[1];
  const type = parts[2]; // 'departure' veya 'destination'
  
  const codeElement = document.getElementById(`segment-${segmentId}-${type === 'departure' ? 'dep' : 'dest'}-code`);
  const nameElement = document.getElementById(`segment-${segmentId}-${type === 'departure' ? 'dep' : 'dest'}-name`);
  
  if (codeElement && nameElement) {
    codeElement.textContent = airport.code;
    
    if (currentLanguage === 'tr') {
      nameElement.textContent = airport.name;
    } else {
      nameElement.textContent = airport.enName;
    }
    
    // Eğer bir segment'in varış havalimanı seçildiyse, sonraki segment'in kalkış havalimanını otomatik güncelle
    if (type === 'destination') {
      updateNextSegmentDeparture(segmentId, airport);
    }
  }
  
  closeAirportModal();
}

// Sonraki segment'in kalkış havalimanını güncelle
function updateNextSegmentDeparture(currentSegmentId, selectedAirport) {
  const currentSegmentIndex = flightSegments.findIndex(seg => seg.id == currentSegmentId);
  const nextSegmentIndex = currentSegmentIndex + 1;
  
  if (nextSegmentIndex < flightSegments.length) {
    const nextSegment = flightSegments[nextSegmentIndex];
    const nextDepCode = document.getElementById(`segment-${nextSegment.id}-dep-code`);
    const nextDepName = document.getElementById(`segment-${nextSegment.id}-dep-name`);
    
    if (nextDepCode && nextDepName) {
      nextDepCode.textContent = selectedAirport.code;
      
      if (currentLanguage === 'tr') {
        nextDepName.textContent = selectedAirport.name;
      } else {
        nextDepName.textContent = selectedAirport.enName;
      }
    }
  }
}

// Havalimanı seçim hatası gösterme fonksiyonu
function showAirportSelectionError(message) {
  // Mevcut hata mesajını kaldır
  clearAirportSelectionError();
  
  // Yeni hata mesajı oluştur
  const errorDiv = document.createElement('div');
  errorDiv.className = 'airport-error-message';
  errorDiv.style.cssText = `
    color: #dc3545;
    font-size: 14px;
    margin: 10px 15px;
    padding: 12px 15px;
    background-color: #f8d7da;
    border: 1px solid #f5c6cb;
    border-radius: 6px;
    display: flex;
    align-items: center;
    gap: 10px;
    animation: slideDown 0.3s ease-out;
  `;
  errorDiv.innerHTML = `
    <i class="fa-solid fa-exclamation-triangle"></i>
    <span>${message}</span>
  `;
  
  // Modal body'ye ekle
  const modalBody = document.querySelector('#airportModal .modal-body');
  if (modalBody) {
    modalBody.insertBefore(errorDiv, modalBody.firstChild);
  }
  
  // 4 saniye sonra otomatik kaldır
  setTimeout(() => {
    clearAirportSelectionError();
  }, 4000);
}

// Havalimanı seçim hatası temizleme fonksiyonu
function clearAirportSelectionError() {
  const existingError = document.querySelector('.airport-error-message');
  if (existingError) {
    existingError.style.opacity = '0';
    setTimeout(() => {
      if (existingError.parentNode) {
        existingError.remove();
      }
    }, 300);
  }
}

// Havalimanları değiştir
function swapAirports() {
  const departureCode = document.getElementById('departureCode').textContent;
  const departureName = document.getElementById('departureName').textContent;
  
  const destinationCode = document.getElementById('destinationCode').textContent;
  const destinationName = document.getElementById('destinationName').textContent;
  
  // Sadece her ikisi de seçilmişse değiştir
  if (departureCode && destinationCode && destinationCode !== '?') {
    document.getElementById('departureCode').textContent = destinationCode;
    document.getElementById('departureName').textContent = destinationName;
    
    document.getElementById('destinationCode').textContent = departureCode;
    document.getElementById('destinationName').textContent = departureName;
  }
}

// Yolcu modal'ını aç
function openPassengerModal() {
  const modal = document.getElementById('passengerModal');
  updatePassengerCountsInModal();
  modal.classList.add('show');
  modal.style.display = 'flex';
}

// Yolcu modal'ını kapat
function closePassengerModal() {
  const modal = document.getElementById('passengerModal');
  modal.classList.remove('show');
  modal.style.display = 'none';
}

// Yolcu sayısını değiştir (yeni versiyon)
function changePassengerCount(type, change) {
  console.log(`Changing ${type} count by ${change}`); // Debug log
  
  if (!passengerCounts || typeof passengerCounts[type] === 'undefined') {
    console.error('passengerCounts not properly initialized');
    return;
  }
  
  const currentCount = passengerCounts[type];
  let newCount = currentCount + change;
  
  // Minimum ve maksimum kontrolleri
  if (type === 'adult') {
    newCount = Math.max(1, Math.min(9, newCount));
  } else {
    newCount = Math.max(0, Math.min(9, newCount));
  }
  
  // Bebek sayısı yetişkin sayısını geçemez
  if (type === 'infant' && newCount > passengerCounts.adult) {
    newCount = passengerCounts.adult;
  }
  
  passengerCounts[type] = newCount;
  console.log('Updated passengerCounts:', passengerCounts); // Debug log
  
  // Yolcu sayılarını kaydet
  savePassengerCounts();
  
  updatePassengerCountsInModal();
}

// Modal'daki yolcu sayılarını güncelle
function updatePassengerCountsInModal() {
  const adultCountEl = document.getElementById('adultCount');
  const childCountEl = document.getElementById('childCount');
  const infantCountEl = document.getElementById('infantCount');
  
  if (adultCountEl) adultCountEl.textContent = passengerCounts.adult;
  if (childCountEl) childCountEl.textContent = passengerCounts.child;
  if (infantCountEl) infantCountEl.textContent = passengerCounts.infant;
  
  // Buton durumlarını güncelle - daha güvenilir selector kullan
  const modal = document.getElementById('passengerModal');
  if (modal) {
    // Yetişkin minus butonu - en az 1 olmalı
    const adultMinusBtn = modal.querySelector('.counter-controls button[onclick*="adult\', -1"]');
    if (adultMinusBtn) {
      adultMinusBtn.disabled = passengerCounts.adult <= 1;
    }
    
    // Çocuk minus butonu
    const childMinusBtn = modal.querySelector('.counter-controls button[onclick*="child\', -1"]');
    if (childMinusBtn) {
      childMinusBtn.disabled = passengerCounts.child <= 0;
    }
    
    // Bebek minus butonu
    const infantMinusBtn = modal.querySelector('.counter-controls button[onclick*="infant\', -1"]');
    if (infantMinusBtn) {
      infantMinusBtn.disabled = passengerCounts.infant <= 0;
    }
    
    // Bebek plus butonu - yetişkin sayısını geçemez
    const infantPlusBtn = modal.querySelector('.counter-controls button[onclick*="infant\', 1"]');
    if (infantPlusBtn) {
      infantPlusBtn.disabled = passengerCounts.infant >= passengerCounts.adult;
    }
  }
}

// Yolcu seçimini onayla
function confirmPassengerSelection() {
  const totalPassengers = passengerCounts.adult + passengerCounts.child + passengerCounts.infant;
  const cabinClass = document.querySelector('input[name="cabinClass"]:checked').value;
  selectedCabinClass = cabinClass;
  
  // Yolcu sayılarını kaydet
  savePassengerCounts();
  
  // Yolcu butonunu güncelle
  updatePassengerButtonText();
  
  closePassengerModal();
}

// Gelişmiş seçenekleri aç/kapat
function toggleAdvancedOptions() {
  const advancedContent = document.getElementById('advancedOptions');
  const toggleBtn = document.querySelector('.advanced-toggle i');
  
  if (advancedContent.classList.contains('show')) {
    advancedContent.classList.remove('show');
    toggleBtn.style.transform = 'rotate(0deg)';
  } else {
    advancedContent.classList.add('show');
    toggleBtn.style.transform = 'rotate(180deg)';
  }
}

// Havalimanı arama fonksiyonu
function setupAirportSearch() {
  const searchInput = document.getElementById('airportSearch');
  if (searchInput) {
    searchInput.addEventListener('input', function(e) {
      loadAirportList(e.target.value);
    });
  }
}

// Tarih validasyonu fonksiyonu
function setupDateValidation() {
  const departureInput = document.getElementById('departureDate');
  const returnInput = document.getElementById('returnDate');
  
  if (departureInput && returnInput) {
    // Gidiş tarihi değiştiğinde
    departureInput.addEventListener('change', function() {
      validateDates();
      // Dönüş tarihi minimum değerini güncelle
      returnInput.min = this.value;
    });
    
    // Dönüş tarihi değiştiğinde
    returnInput.addEventListener('change', function() {
      validateDates();
    });
  }
}

// Tarih validasyonu kontrol fonksiyonu
function validateDates() {
  const departureInput = document.getElementById('departureDate');
  const returnInput = document.getElementById('returnDate');
  
  if (!departureInput || !returnInput) return;
  
  const departureDate = departureInput.value;
  const returnDate = returnInput.value;
  
  // Sadece her iki tarih de seçiliyse kontrol et
  if (departureDate && returnDate) {
    const depDate = new Date(departureDate);
    const retDate = new Date(returnDate);
    
    if (retDate < depDate) {
      // Hata durumunda dönüş tarihini temizle ve uyarı göster
      returnInput.value = '';
      showDateError(currentLanguage === 'tr' ? 
        'Dönüş tarihi gidiş tarihinden önce olamaz!' : 
        'Return date cannot be earlier than departure date!');
    } else {
      // Hata yoksa error mesajını temizle
      clearDateError();
    }
  }
}

// Tarih hatası gösterme fonksiyonu
function showDateError(message) {
  // Mevcut hata mesajını kaldır
  clearDateError();
  
  // Yeni hata mesajı oluştur
  const errorDiv = document.createElement('div');
  errorDiv.className = 'date-error-message';
  errorDiv.style.cssText = `
    color: #dc3545;
    font-size: 14px;
    margin-top: 5px;
    padding: 8px 12px;
    background-color: #f8d7da;
    border: 1px solid #f5c6cb;
    border-radius: 4px;
    display: flex;
    align-items: center;
    gap: 8px;
  `;
  errorDiv.innerHTML = `
    <i class="fa-solid fa-exclamation-triangle"></i>
    <span>${message}</span>
  `;
  
  // Tarih alanlarının parent container'ına ekle
  const dateRow = document.querySelector('.date-row');
  if (dateRow) {
    dateRow.appendChild(errorDiv);
  }
}

// Tarih hatası temizleme fonksiyonu
function clearDateError() {
  const existingError = document.querySelector('.date-error-message');
  if (existingError) {
    existingError.remove();
  }
}

// Seyahat türü değişikliğini dinle
function handleTripTypeChange() {
  const tripTypeRadios = document.querySelectorAll('input[name="tripType"]');
  const returnDateGroup = document.querySelector('.return-date');
  
  tripTypeRadios.forEach(radio => {
    radio.addEventListener('change', function() {
      if (returnDateGroup) {
        if (this.value === 'oneway') {
          returnDateGroup.style.display = 'none';
        } else {
          returnDateGroup.style.display = 'block';
        }
      }
    });
  });
}

// Seyahat türünü ayarla (eski fonksiyon - uyumluluk için)
function setTripType(trText, enText) {
  // Bu fonksiyon artık radio button'lar tarafından kontrol ediliyor
  console.log('Trip type changed:', trText, enText);
}


// Dropdown'ları aç/kapat
function toggleTripDropdown() {
  const dropdown = document.getElementById("tripDropdown");
  if (dropdown) {
    dropdown.classList.toggle("show");
  }
}

function togglePassengerDropdown() {
  const dropdown = document.getElementById("passengerDropdown");
  if (dropdown) {
    dropdown.classList.toggle("show");
  }
}

// Lokasyon değiştirme
function swapLocations() {
  // Animasyon efekti için
  const swapBtn = document.querySelector('.swap-btn');
  if (swapBtn) {
    swapBtn.style.transform = 'rotate(180deg)';
    setTimeout(() => {
      swapBtn.style.transform = '';
    }, 300);
  }
  
  console.log('Lokasyonlar değiştirildi');
}

// Kalkış noktası seçimi
function selectDeparture() {
  console.log('Kalkış noktası seçimi açılacak');
}

// Varış noktası seçimi
function selectDestination() {
  console.log('Varış noktası seçimi açılacak');
}

// Uçuş arama
function searchFlights() {
  const departureDate = document.getElementById('departureDate')?.value;
  const returnDate = document.getElementById('returnDate')?.value;
  const promoCode = document.getElementById('promoCode')?.value;
  
  // Havalimanı seçimlerini kontrol et
  const departureCode = document.getElementById('departureCode')?.textContent;
  const destinationCode = document.getElementById('destinationCode')?.textContent;
  
  if (!departureCode || departureCode === 'TZX') {
    // Varsayılan olarak Trabzon seçili, bu normal
  }
  
  if (!destinationCode || destinationCode === '?') {
    alert(currentLanguage === 'tr' ? 'Lütfen varış havalimanı seçin!' : 'Please select destination airport!');
    return;
  }
  
  // Aynı havalimanı kontrolü
  if (departureCode === destinationCode) {
    alert(currentLanguage === 'tr' ? 
      'Gidiş ve dönüş için aynı havalimanını seçemezsiniz!' : 
      'You cannot select the same airport for departure and destination!');
    return;
  }
  
  if (!departureDate) {
    alert(currentLanguage === 'tr' ? 'Lütfen gidiş tarihi seçin!' : 'Please select departure date!');
    return;
  }
  
  // Dönüş tarihi kontrolü - sadece gidiş-dönüş seçiliyse
  const activeButton = document.querySelector('.top-opt.active');
  const isRoundTrip = activeButton && activeButton.dataset.type === 'roundtrip';
  
  if (isRoundTrip && returnDate) {
    const depDate = new Date(departureDate);
    const retDate = new Date(returnDate);
    
    if (retDate < depDate) {
      alert(currentLanguage === 'tr' ? 
        'Dönüş tarihi gidiş tarihinden önce olamaz!' : 
        'Return date cannot be earlier than departure date!');
      return;
    }
  }
  
  // Dönüş tarihi zorunlu kontrolü (gidiş-dönüş için)
  if (isRoundTrip && !returnDate) {
    alert(currentLanguage === 'tr' ? 
      'Gidiş-dönüş seçimi için dönüş tarihi gereklidir!' : 
      'Return date is required for round trip!');
    return;
  }
  
  console.log('Uçuş aranıyor...', {
    departureDate,
    returnDate,
    promoCode
  });
  
  // Burada normalde API çağrısı yapılacak
  alert(currentLanguage === 'tr' ? 'Uçuşlar aranıyor...' : 'Searching flights...');
}

// Teklif rezervasyonu
function bookFlight(destination) {
  console.log(`${destination} için rezervasyon yapılacak`);
  alert(currentLanguage === 'tr' ? `${destination} uçuşu için rezervasyon sayfasına yönlendiriliyorsunuz...` : `Redirecting to booking page for ${destination}...`);
}

/* ================== FORM FUNCTIONS ================== */
// Form işlemleri ve validasyon

// İletişim formu gönderimi
function submitContactForm(event) {
  event.preventDefault();
  
  const form = event.target;
  const formData = new FormData(form);
  const data = Object.fromEntries(formData);
  
  // Gerekli alanları kontrol et
  const requiredFields = ['fullName', 'email', 'subject', 'message', 'kvkk'];
  let isValid = true;
  
  for (let field of requiredFields) {
    if (!data[field] || data[field].trim() === '') {
      isValid = false;
      break;
    }
  }
  
  if (!isValid) {
    showFormMessage('error', 
      currentLanguage === 'tr' ? 'Lütfen tüm zorunlu alanları doldurun!' : 'Please fill in all required fields!'
    );
    return;
  }
  
  // E-posta formatını kontrol et
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(data.email)) {
    showFormMessage('error', 
      currentLanguage === 'tr' ? 'Geçerli bir e-posta adresi girin!' : 'Please enter a valid email address!'
    );
    return;
  }
  
  console.log('İletişim formu gönderildi:', data);
  
  // Başarı mesajı göster
  showFormMessage('success', 
    currentLanguage === 'tr' ? 'Mesajınız başarıyla gönderildi! En kısa sürede size dönüş yapacağız.' : 'Your message has been sent successfully! We will get back to you soon.'
  );
  
  // Formu temizle
  form.reset();
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
  const faqQuestion = faqAnswer?.previousElementSibling;
  
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
      const icon = faqQuestion?.querySelector('i');
      if (icon) {
        icon.classList.remove('fa-chevron-down');
        icon.classList.add('fa-chevron-up');
      }
    }
  }
}

/* ================== AUTH FUNCTIONS ================== */
// Kimlik doğrulama fonksiyonları

// Şifre göster/gizle
function togglePassword(inputId) {
  const input = document.getElementById(inputId);
  const button = input?.nextElementSibling?.querySelector('i');
  
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
  const password = document.getElementById('signupPassword')?.value || '';
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
    window.location.href = 'login.html';
  }, 2000);
}

// Giriş formu işlemi
function handleLogin(event) {
  event.preventDefault();
  
  const form = event.target;
  const formData = new FormData(form);
  const data = Object.fromEntries(formData);
  
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
    window.location.href = '../index.html';
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
  console.log('Google ile kayıt');
  alert(currentLanguage === 'tr' ? 'Google ile kayıt özelliği yakında!' : 'Sign up with Google coming soon!');
}

function signupWithFacebook() {
  console.log('Facebook ile kayıt');
  alert(currentLanguage === 'tr' ? 'Facebook ile kayıt özelliği yakında!' : 'Sign up with Facebook coming soon!');
}

function loginWithGoogle() {
  console.log('Google ile giriş');
  alert(currentLanguage === 'tr' ? 'Google ile giriş özelliği yakında!' : 'Sign in with Google coming soon!');
}

function loginWithFacebook() {
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
  const email = document.getElementById('forgotEmail')?.value;
  
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

/* ================== MULTI-CITY FLIGHTS ================== */
// Çoklu uçuş fonksiyonları

let flightSegments = [];
let segmentCounter = 0;

// Çoklu uçuş sistemini başlat
function initializeMultiCityFlights() {
  const segmentsContainer = document.getElementById('flightSegments');
  segmentsContainer.innerHTML = '';
  flightSegments = [];
  segmentCounter = 0;
  
  // Önceki hata mesajlarını temizle
  clearAllSegmentDateErrors();
  
  // İlk iki segment'i oluştur
  addFlightSegment(); // İlk uçuş
  addFlightSegment(); // İkinci uçuş
}

// Yeni uçuş segmenti ekle
function addFlightSegment() {
  const segmentId = segmentCounter++;
  const segmentsContainer = document.getElementById('flightSegments');
  
  // Önceki segmentin varış havalimanını al
  let defaultDepartureCode = 'TZX';
  let defaultDepartureName = 'Trabzon';
  
  if (flightSegments.length > 0) {
    const lastSegment = flightSegments[flightSegments.length - 1];
    const lastDestCode = document.getElementById(`segment-${lastSegment.id}-dest-code`);
    const lastDestName = document.getElementById(`segment-${lastSegment.id}-dest-name`);
    
    if (lastDestCode && lastDestName && lastDestCode.textContent !== '?') {
      defaultDepartureCode = lastDestCode.textContent;
      defaultDepartureName = lastDestName.textContent;
    }
  }
  
  // Segment nesnesini oluştur
  const segment = {
    id: segmentId,
    departureCode: defaultDepartureCode,
    departureName: defaultDepartureName,
    destinationCode: '?',
    destinationName: currentLanguage === 'tr' ? 'Nereye' : 'Where to'
  };
  
  flightSegments.push(segment);
  
  // HTML oluştur
  const segmentHtml = createFlightSegmentHtml(segment, flightSegments.length);
  segmentsContainer.insertAdjacentHTML('beforeend', segmentHtml);
  
  // Segment 3'ten fazlaysa silme butonlarını göster
  updateDeleteButtons();
  
  // Yeni segment için dil çevirisini uygula
  updatePageLanguage();
}

// Uçuş segmenti HTML'i oluştur
function createFlightSegmentHtml(segment, segmentNumber) {
  const isRemovable = flightSegments.length > 2;
  
  // Dile göre başlık metni
  const headerText = currentLanguage === 'tr' ? `Uçuş ${segmentNumber}` : `Flight ${segmentNumber}`;
  
  return `
    <div class="flight-segment" data-segment-id="${segment.id}">
      <div class="segment-header">
        <h4 data-tr="Uçuş ${segmentNumber}" data-en="Flight ${segmentNumber}">${headerText}</h4>
        ${isRemovable ? `<button type="button" class="remove-segment-btn" onclick="removeFlightSegment(${segment.id})">
          <i class="fa-solid fa-trash"></i>
        </button>` : ''}
      </div>
      
      <div class="segment-locations">
        <div class="loc-box" onclick="openAirportModal('segment-${segment.id}-departure')">
          <span class="loc-code" id="segment-${segment.id}-dep-code">${segment.departureCode}</span>
          <span class="loc-city" id="segment-${segment.id}-dep-name">${segment.departureName}</span>
        </div>
        
        <div class="flight-arrow">
          <i class="fa-solid fa-arrow-right"></i>
        </div>
        
        <div class="loc-box" onclick="openAirportModal('segment-${segment.id}-destination')">
          <span class="loc-code" id="segment-${segment.id}-dest-code">${segment.destinationCode}</span>
          <span class="loc-city" id="segment-${segment.id}-dest-name">${segment.destinationName}</span>
        </div>
      </div>
      
      <div class="segment-date">
        <label data-tr="Tarih" data-en="Date">Tarih</label>
        <input type="date" id="segment-${segment.id}-date" class="segment-date-input" onchange="validateSegmentDates(${segment.id})">
      </div>
    </div>
  `;
}

// Uçuş segmentini kaldır
function removeFlightSegment(segmentId) {
  if (flightSegments.length <= 2) return; // En az 2 segment olmalı
  
  // Segment'i DOM'dan kaldır
  const segmentElement = document.querySelector(`[data-segment-id="${segmentId}"]`);
  if (segmentElement) {
    segmentElement.remove();
  }
  
  // Array'den kaldır
  flightSegments = flightSegments.filter(segment => segment.id !== segmentId);
  
  // Segment numaralarını güncelle
  updateSegmentNumbers();
  updateDeleteButtons();
}

// Segment numaralarını güncelle
function updateSegmentNumbers() {
  const segmentElements = document.querySelectorAll('.flight-segment');
  segmentElements.forEach((element, index) => {
    const header = element.querySelector('.segment-header h4');
    if (header) {
      const segmentNumber = index + 1;
      if (currentLanguage === 'tr') {
        header.textContent = `Uçuş ${segmentNumber}`;
        header.setAttribute('data-tr', `Uçuş ${segmentNumber}`);
      } else {
        header.textContent = `Flight ${segmentNumber}`;
        header.setAttribute('data-en', `Flight ${segmentNumber}`);
      }
    }
  });
}

// Silme butonlarını güncelle
function updateDeleteButtons() {
  const segmentElements = document.querySelectorAll('.flight-segment');
  segmentElements.forEach((element, index) => {
    const removeBtn = element.querySelector('.remove-segment-btn');
    if (flightSegments.length > 2) {
      if (!removeBtn) {
        // Silme butonu yoksa ekle
        const header = element.querySelector('.segment-header');
        const segmentId = element.getAttribute('data-segment-id');
        header.insertAdjacentHTML('beforeend', `
          <button type="button" class="remove-segment-btn" onclick="removeFlightSegment(${segmentId})">
            <i class="fa-solid fa-trash"></i>
          </button>
        `);
      }
    } else {
      // 2 veya daha az segment varsa silme butonunu kaldır
      if (removeBtn) {
        removeBtn.remove();
      }
    }
  });
}

// Multi-city tarih validasyonu
function validateSegmentDates(changedSegmentId) {
  const changedSegment = flightSegments.find(seg => seg.id == changedSegmentId);
  if (!changedSegment) return;
  
  const changedSegmentIndex = flightSegments.findIndex(seg => seg.id == changedSegmentId);
  const changedDateInput = document.getElementById(`segment-${changedSegmentId}-date`);
  const changedDate = new Date(changedDateInput.value);
  
  if (!changedDateInput.value) return;
  
  // Önceki segment'in tarihini kontrol et
  if (changedSegmentIndex > 0) {
    const previousSegment = flightSegments[changedSegmentIndex - 1];
    const previousDateInput = document.getElementById(`segment-${previousSegment.id}-date`);
    
    if (previousDateInput.value) {
      const previousDate = new Date(previousDateInput.value);
      
      if (changedDate < previousDate) {
        // Hata: Bu uçuş önceki uçuştan önce olamaz
        showSegmentDateError(changedSegmentId, currentLanguage === 'tr' ? 
          `Uçuş ${changedSegmentIndex + 1} tarihi, Uçuş ${changedSegmentIndex} tarihinden önce olamaz!` :
          `Flight ${changedSegmentIndex + 1} date cannot be earlier than Flight ${changedSegmentIndex} date!`);
        changedDateInput.value = '';
        return;
      }
    }
  }
  
  // Sonraki segment'lerin minimum tarihini güncelle
  updateSubsequentSegmentMinDates(changedSegmentIndex, changedDate);
  
  // Sonraki segment'lerin tarihlerini kontrol et
  validateSubsequentSegments(changedSegmentIndex, changedDate);
  
  // Hata mesajını temizle
  clearSegmentDateError(changedSegmentId);
}

// Sonraki segment'lerin minimum tarihlerini güncelle
function updateSubsequentSegmentMinDates(fromIndex, minDate) {
  const minDateStr = minDate.toISOString().split('T')[0];
  
  for (let i = fromIndex + 1; i < flightSegments.length; i++) {
    const segment = flightSegments[i];
    const dateInput = document.getElementById(`segment-${segment.id}-date`);
    if (dateInput) {
      dateInput.min = minDateStr;
    }
  }
}

// Sonraki segment'lerin tarihlerini kontrol et ve gerekirse temizle
function validateSubsequentSegments(fromIndex, minDate) {
  for (let i = fromIndex + 1; i < flightSegments.length; i++) {
    const segment = flightSegments[i];
    const dateInput = document.getElementById(`segment-${segment.id}-date`);
    
    if (dateInput && dateInput.value) {
      const segmentDate = new Date(dateInput.value);
      
      if (segmentDate < minDate) {
        // Bu segment'in tarihi geçersiz, temizle
        dateInput.value = '';
        showSegmentDateError(segment.id, currentLanguage === 'tr' ? 
          `Uçuş ${i + 1} tarihi güncellendi. Lütfen yeni bir tarih seçin.` :
          `Flight ${i + 1} date updated. Please select a new date.`);
      }
    }
  }
}

// Segment tarih hatası göster
function showSegmentDateError(segmentId, message) {
  // Mevcut hata mesajını temizle
  clearSegmentDateError(segmentId);
  
  // Hata mesajı oluştur
  const errorDiv = document.createElement('div');
  errorDiv.className = `segment-date-error segment-${segmentId}-error`;
  errorDiv.style.cssText = `
    color: #dc3545;
    font-size: 12px;
    margin-top: 5px;
    padding: 6px 10px;
    background-color: #f8d7da;
    border: 1px solid #f5c6cb;
    border-radius: 4px;
    display: flex;
    align-items: center;
    gap: 6px;
  `;
  errorDiv.innerHTML = `
    <i class="fa-solid fa-exclamation-triangle"></i>
    <span>${message}</span>
  `;
  
  // Segment'in tarih alanının altına ekle
  const segmentDateDiv = document.getElementById(`segment-${segmentId}-date`).parentElement;
  segmentDateDiv.appendChild(errorDiv);
  
  // 5 saniye sonra otomatik kaldır
  setTimeout(() => {
    clearSegmentDateError(segmentId);
  }, 5000);
}

// Segment tarih hatasını temizle
function clearSegmentDateError(segmentId) {
  const existingError = document.querySelector(`.segment-${segmentId}-error`);
  if (existingError) {
    existingError.remove();
  }
}

// Tüm segment tarih hatalarını temizle
function clearAllSegmentDateErrors() {
  const existingErrors = document.querySelectorAll('.segment-date-error');
  existingErrors.forEach(error => error.remove());
}

/* ================== TRIP TYPE STATE MANAGEMENT ================== */
// Trip type durumu yönetimi

// Trip type durumunu kaydet
function saveTripTypeState(tripType) {
  localStorage.setItem('selectedTripType', tripType);
}

// Trip type durumunu geri yükle
function restoreTripTypeState() {
  const savedTripType = localStorage.getItem('selectedTripType') || 'roundtrip';
  const tripButtons = document.querySelectorAll('.top-opt[data-type]');
  
  // Yolcu sayılarını geri yükle
  restorePassengerCounts();
  
  // Tüm butonlardan active sınıfını kaldır
  tripButtons.forEach(btn => btn.classList.remove('active'));
  
  // Kaydedilen trip type'ı aktif yap
  const targetButton = document.querySelector(`[data-type="${savedTripType}"]`);
  if (targetButton) {
    targetButton.classList.add('active');
    
    // İlgili bölümleri göster/gizle
    applyTripTypeSettings(savedTripType);
  }
}

// Trip type ayarlarını uygula
function applyTripTypeSettings(tripType) {
  const normalSection = document.getElementById("normalFlightSection");
  const multiCitySection = document.getElementById("multiCitySection");
  const returnDateField = document.getElementById("returnDate");
  
  if (tripType === "multicity") {
    // Çoklu uçuş → normal bölümü gizle, çoklu uçuş bölümünü göster
    if (normalSection) normalSection.style.display = "none";
    if (multiCitySection) multiCitySection.style.display = "block";
    
    // Eğer segment'ler yoksa oluştur
    if (flightSegments.length === 0) {
      initializeMultiCityFlights();
    }
  } else {
    // Normal uçuş (tek yön veya gidiş-dönüş)
    if (normalSection) normalSection.style.display = "block";
    if (multiCitySection) multiCitySection.style.display = "none";
    
    if (tripType === "oneway") {
      // Tek yön → dönüş tarihini gizle
      if (returnDateField && returnDateField.parentElement) {
        returnDateField.parentElement.style.display = "none";
        returnDateField.value = "";
      }
    } else {
      // Roundtrip → dönüş tarihi görünür
      if (returnDateField && returnDateField.parentElement) {
        returnDateField.parentElement.style.display = "block";
      }
    }
  }
  
  // Yolcu butonunu güncelle
  updatePassengerButtonText();
}

// Yolcu butonunun metnini güncelle
function updatePassengerButtonText() {
  const totalPassengers = passengerCounts.adult + passengerCounts.child + passengerCounts.infant;
  const passengerButton = document.querySelector('button[onclick="openPassengerModal()"]');
  
  if (passengerButton) {
    if (currentLanguage === 'tr') {
      passengerButton.textContent = `${totalPassengers} Yolcu`;
      passengerButton.setAttribute('data-tr', `${totalPassengers} Yolcu`);
    } else {
      const passengerText = totalPassengers === 1 ? '1 Passenger' : `${totalPassengers} Passengers`;
      passengerButton.textContent = passengerText;
      passengerButton.setAttribute('data-en', passengerText);
    }
  }
}

// Yolcu sayılarını kaydet
function savePassengerCounts() {
  localStorage.setItem('passengerCounts', JSON.stringify(passengerCounts));
}

// Yolcu sayılarını geri yükle
function restorePassengerCounts() {
  const savedCounts = localStorage.getItem('passengerCounts');
  if (savedCounts) {
    try {
      const parsedCounts = JSON.parse(savedCounts);
      passengerCounts.adult = parsedCounts.adult || 1;
      passengerCounts.child = parsedCounts.child || 0;
      passengerCounts.infant = parsedCounts.infant || 0;
    } catch (e) {
      console.error('Error parsing saved passenger counts:', e);
      // Varsayılan değerleri kullan
      passengerCounts = { adult: 1, child: 0, infant: 0 };
    }
  }
}

/* ================== INITIALIZATION ================== */
// Sayfa yüklendiğinde çalışacak fonksiyonlar

document.addEventListener('DOMContentLoaded', function() {
  // Dil ayarlarını yükle
  currentLanguage = localStorage.getItem('language') || 'tr';
  applyLanguageSettings();
  updatePageLanguage();
  
  // Sidebar durumunu yükle
  const sidebarCollapsed = localStorage.getItem('sidebarCollapsed') === 'true';
  if (sidebarCollapsed) {
    const sidebar = document.getElementById('sidebar');
    const body = document.body;
    if (sidebar) {
      sidebar.classList.add('collapsed');
      body.classList.add('sidebar-collapsed');
    }
  }
  
  // Trip type durumunu yükle
  restoreTripTypeState();
  
  // Havalimanı arama fonksiyonunu başlat
  setupAirportSearch();
  
  // Havalimanı listesi event listener'ını ekle
  const airportList = document.getElementById('airportList');
  if (airportList) {
    airportList.addEventListener('click', function(e) {
      const airportItem = e.target.closest('.airport-item');
      if (airportItem) {
        // Airport nesnesini bul
        const code = airportItem.querySelector('.airport-code-modal').textContent;
        const airport = airports.find(a => a.code === code);
        if (airport) {
          selectAirport(airport);
        }
      }
    });
  }
  
  // Seyahat türü değişikliklerini dinle
  handleTripTypeChange();
  
  // Tarih validasyonu event listener'larını ekle
  setupDateValidation();
  
  // Modal'ları kapatmak için dış tıklama
  document.addEventListener('click', function(event) {
    // Havalimanı modal'ı
    const airportModal = document.getElementById('airportModal');
    if (airportModal && event.target === airportModal) {
      closeAirportModal();
    }
    
    // Yolcu modal'ı
    const passengerModal = document.getElementById('passengerModal');
    if (passengerModal && event.target === passengerModal) {
      closePassengerModal();
    }
    
    // Dropdown'ları kapat
    const dropdowns = document.querySelectorAll('.dropdown-menu');
    dropdowns.forEach(dropdown => {
      if (!dropdown.parentElement.contains(event.target)) {
        dropdown.classList.remove('show');
      }
    });
  });


  // Gidiş Dönüş 

  const tripButtons = document.querySelectorAll(".top-opt");
const returnDateField = document.getElementById("returnDate");

  tripButtons.forEach(btn => {
  btn.addEventListener("click", () => {

    // Tüm butonlardan active sınıfını kaldır
    tripButtons.forEach(b => b.classList.remove("active"));

    // Tıklanan butona active ekle
    btn.classList.add("active");

    const type = btn.dataset.type;
    
    // Trip type'ı kaydet
    saveTripTypeState(type);

    // SEYAHAT TÜRÜNE GÖRE DAVRANIŞ
    const normalSection = document.getElementById("normalFlightSection");
    const multiCitySection = document.getElementById("multiCitySection");
    
    if (type === "multicity") {
      // Çoklu uçuş → normal bölümü gizle, çoklu uçuş bölümünü göster
      normalSection.style.display = "none";
      multiCitySection.style.display = "block";
      
      // İlk iki uçuş segmentini oluştur
      initializeMultiCityFlights();
    } else {
      // Normal uçuş (tek yön veya gidiş-dönüş)
      normalSection.style.display = "block";
      multiCitySection.style.display = "none";
      
      if (type === "oneway") {
        // Tek yön → dönüş tarihini gizle
        returnDateField.parentElement.style.display = "none";
        returnDateField.value = "";
      } else {
        // Roundtrip → dönüş tarihi görünür
        returnDateField.parentElement.style.display = "block";
      }
    }

    });
  });
  



  // ESC tuşu ile modal'ları kapat
  document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
      closeAirportModal();
      closePassengerModal();
      closeMobileSidebar();
    }
  });
  
  // Sayfa yüklendiğinde dil ayarlarını uygula
  applyLanguageSettings();
  updatePageLanguage();
  
  console.log('Trabzon Airlines sistemi yüklendi!');
  });