/* ================== TRABZON AIRLINES JAVASCRIPT ================== */
/* COM5005 Web Programming - Grup 20 */
/* Çift dil desteği, sidebar kontrolü ve form işlemleri */

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
  airportItem.onclick = () => selectAirport(airport);
  
  const name = currentLanguage === 'tr' ? airport.name : airport.enName;
  const fullName = currentLanguage === 'tr' ? airport.fullName : airport.enFullName;
  
  airportItem.innerHTML = `
    <span class="airport-code-modal">${airport.code}</span>
    <strong>${name}</strong><br>
    <small>${fullName}</small>
  `;
  
  return airportItem;
}

// Havalimanı seç
function selectAirport(airport) {
  if (!currentAirportSelection) return;
  
  const codeElement = document.getElementById(currentAirportSelection + 'Code');
  const nameElement = document.getElementById(currentAirportSelection + 'Name');
  const fullNameElement = document.getElementById(currentAirportSelection + 'FullName');
  
  if (codeElement && nameElement && fullNameElement) {
    codeElement.textContent = airport.code;
    
    if (currentLanguage === 'tr') {
      nameElement.textContent = airport.name;
      fullNameElement.textContent = airport.fullName;
    } else {
      nameElement.textContent = airport.enName;
      fullNameElement.textContent = airport.enFullName;
    }
  }
  
  closeAirportModal();
}

// Havalimanları değiştir
function swapAirports() {
  const departureCode = document.getElementById('departureCode').textContent;
  const departureName = document.getElementById('departureName').textContent;
  const departureFullName = document.getElementById('departureFullName').textContent;
  
  const destinationCode = document.getElementById('destinationCode').textContent;
  const destinationName = document.getElementById('destinationName').textContent;
  const destinationFullName = document.getElementById('destinationFullName').textContent;
  
  // Sadece her ikisi de seçilmişse değiştir
  if (departureCode && destinationCode && destinationCode !== '?') {
    document.getElementById('departureCode').textContent = destinationCode;
    document.getElementById('departureName').textContent = destinationName;
    document.getElementById('departureFullName').textContent = destinationFullName;
    
    document.getElementById('destinationCode').textContent = departureCode;
    document.getElementById('destinationName').textContent = departureName;
    document.getElementById('destinationFullName').textContent = departureFullName;
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
  updatePassengerCountsInModal();
}

// Modal'daki yolcu sayılarını güncelle
function updatePassengerCountsInModal() {
  document.getElementById('adultCount').textContent = passengerCounts.adult;
  document.getElementById('childCount').textContent = passengerCounts.child;
  document.getElementById('infantCount').textContent = passengerCounts.infant;
  
  // Buton durumlarını güncelle
  const adultMinusBtn = document.querySelector('[onclick="changePassengerCount(\'adult\', -1)"]');
  const infantPlusBtn = document.querySelector('[onclick="changePassengerCount(\'infant\', 1)"]');
  
  if (adultMinusBtn) {
    adultMinusBtn.disabled = passengerCounts.adult <= 1;
  }
  
  if (infantPlusBtn) {
    infantPlusBtn.disabled = passengerCounts.infant >= passengerCounts.adult;
  }
}

// Yolcu seçimini onayla
function confirmPassengerSelection() {
  const totalPassengers = passengerCounts.adult + passengerCounts.child + passengerCounts.infant;
  const cabinClass = document.querySelector('input[name="cabinClass"]:checked').value;
  selectedCabinClass = cabinClass;
  
  // Yolcu özetini güncelle
  const passengerSummary = document.getElementById('passengerSummary');
  const passengerDetails = document.getElementById('passengerDetails');
  
  if (currentLanguage === 'tr') {
    passengerSummary.textContent = `${totalPassengers} Yolcu`;
    passengerDetails.textContent = cabinClass === 'economy' ? 'ECO' : 'BUS';
  } else {
    passengerSummary.textContent = `${totalPassengers} Passenger${totalPassengers > 1 ? 's' : ''}`;
    passengerDetails.textContent = cabinClass === 'economy' ? 'ECO' : 'BUS';
  }
  
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

// Yolcu sayısını değiştir
function changePassengerCount(change) {
  const countElement = document.getElementById("adultCount");
  const passengerElement = document.getElementById("passengerCount");
  
  if (countElement && passengerElement) {
    let currentCount = parseInt(countElement.textContent);
    let newCount = Math.max(1, Math.min(10, currentCount + change));
    
    countElement.textContent = newCount;
    
    // Dile göre metni güncelle
    if (currentLanguage === 'tr') {
      passengerElement.textContent = `${newCount} Yetişkin`;
    } else {
      passengerElement.textContent = `${newCount} Adult${newCount > 1 ? 's' : ''}`;
    }
  }
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
  
  if (!departureDate) {
    alert(currentLanguage === 'tr' ? 'Lütfen gidiş tarihi seçin!' : 'Please select departure date!');
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
  
  // Havalimanı arama fonksiyonunu başlat
  setupAirportSearch();
  
  // Seyahat türü değişikliklerini dinle
  handleTripTypeChange();
  
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
  
  // ESC tuşu ile modal'ları kapat
  document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
      closeAirportModal();
      closePassengerModal();
      closeMobileSidebar();
    }
  });
  
  console.log('Trabzon Airlines sistemi yüklendi!');
});