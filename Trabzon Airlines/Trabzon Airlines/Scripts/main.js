// Ana initialization ve global değişkenler

// Mevcut dil ayarı
let currentLanguage = localStorage.getItem('language') || 'tr';

// Sayfa yüklenmeden önce dil ayarlarını uygula
document.addEventListener('DOMContentLoaded', function() {
  // Dil ayarlarını hemen uygula
  if (typeof applyLanguageSettings === 'function') {
    applyLanguageSettings();
  }
  if (typeof updatePageLanguage === 'function') {
    updatePageLanguage();
  }
  
  console.log('Dil sistemi yüklendi. Mevcut dil:', currentLanguage);
});

// Sayfa yüklendiğinde çalışacak fonksiyonlar
document.addEventListener('DOMContentLoaded', function() {
  // Dil ayarlarını yükle
  currentLanguage = localStorage.getItem('language') || 'tr';
  if (typeof applyLanguageSettings === 'function') {
    applyLanguageSettings();
  }
  if (typeof updatePageLanguage === 'function') {
    updatePageLanguage();
  }
  
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
  if (typeof restoreTripTypeState === 'function') {
    restoreTripTypeState();
  }
  
  // Havalimanı arama fonksiyonunu başlat
  if (typeof setupAirportSearch === 'function') {
    setupAirportSearch();
  }
  
  // Havalimanı listesi event listener'ını ekle
  const airportList = document.getElementById('airportList');
  if (airportList) {
    airportList.addEventListener('click', function(e) {
      const airportItem = e.target.closest('.airport-item');
      if (airportItem) {
        // Airport nesnesini bul
        const codeElement = airportItem.querySelector('.airport-code-modal');
        const code = codeElement ? codeElement.textContent : null;
        if (typeof airports !== 'undefined') {
          const airport = airports.find(a => a.code === code);
          if (airport && typeof selectAirport === 'function') {
            selectAirport(airport);
          }
        }
      }
    });
  }
  
  // Seyahat türü değişikliklerini dinle
  if (typeof handleTripTypeChange === 'function') {
    handleTripTypeChange();
  }
  
  // Tarih validasyonu event listener'larını ekle
  if (typeof setupDateValidation === 'function') {
    setupDateValidation();
  }
  
  // Modal'ları kapatmak için dış tıklama
  document.addEventListener('click', function(event) {
    // Havalimanı modal'ı
    const airportModal = document.getElementById('airportModal');
    if (airportModal && event.target === airportModal) {
      if (typeof closeAirportModal === 'function') {
        closeAirportModal();
      }
    }
    
    // Yolcu modal'ı
    const passengerModal = document.getElementById('passengerModal');
    if (passengerModal && event.target === passengerModal) {
      if (typeof closePassengerModal === 'function') {
        closePassengerModal();
      }
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
      if (typeof saveTripTypeState === 'function') {
        saveTripTypeState(type);
      }

      // SEYAHAT TÜRÜNE GÖRE DAVRANIŞ
      const normalSection = document.getElementById("normalFlightSection");
      const multiCitySection = document.getElementById("multiCitySection");
      
      if (type === "multicity") {
        // Çoklu uçuş → normal bölümü gizle, çoklu uçuş bölümünü göster
        if (normalSection) normalSection.style.display = "none";
        if (multiCitySection) multiCitySection.style.display = "block";
        
        // İlk iki uçuş segmentini oluştur
        if (typeof initializeMultiCityFlights === 'function') {
          initializeMultiCityFlights();
        }
      } else {
        // Normal uçuş (tek yön veya gidiş-dönüş)
        if (normalSection) normalSection.style.display = "block";
        if (multiCitySection) multiCitySection.style.display = "none";
        
        if (type === "oneway") {
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
    });
  });

  // ESC tuşu ile modal'ları kapat
  document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
      if (typeof closeAirportModal === 'function') {
        closeAirportModal();
      }
      if (typeof closePassengerModal === 'function') {
        closePassengerModal();
      }
      if (typeof closeMobileSidebar === 'function') {
        closeMobileSidebar();
      }
    }
  });
  
  console.log('Trabzon Airlines sistemi yüklendi!');
});

