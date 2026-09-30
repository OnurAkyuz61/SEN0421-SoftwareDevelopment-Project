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
  if (typeof restorePassengerCounts === 'function') {
    restorePassengerCounts();
  }
  
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
    if (typeof flightSegments !== 'undefined' && flightSegments.length === 0) {
      if (typeof initializeMultiCityFlights === 'function') {
        initializeMultiCityFlights();
      }
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
  if (typeof updatePassengerButtonText === 'function') {
    updatePassengerButtonText();
  }
}

// Yolcu butonunun metnini güncelle
function updatePassengerButtonText() {
  if (typeof passengerCounts === 'undefined') return;
  
  const totalPassengers = passengerCounts.adult + passengerCounts.child + passengerCounts.infant;
  const passengerButton = document.querySelector('button[onclick="openPassengerModal()"]');
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  if (passengerButton) {
    if (currentLang === 'tr') {
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
  if (typeof passengerCounts !== 'undefined') {
    localStorage.setItem('passengerCounts', JSON.stringify(passengerCounts));
  }
}

// Yolcu sayılarını geri yükle
function restorePassengerCounts() {
  if (typeof passengerCounts === 'undefined') return;
  
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
      passengerCounts.adult = 1;
      passengerCounts.child = 0;
      passengerCounts.infant = 0;
    }
  }
}

