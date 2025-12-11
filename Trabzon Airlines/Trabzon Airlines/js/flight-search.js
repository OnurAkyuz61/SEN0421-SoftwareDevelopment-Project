// Uçuş arama formu fonksiyonları

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
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  if (currentLang === 'tr') {
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
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  const filteredAirports = airports.filter(airport => {
    const searchLower = searchTerm.toLowerCase();
    const name = currentLang === 'tr' ? airport.name : airport.enName;
    const fullName = currentLang === 'tr' ? airport.fullName : airport.enFullName;
    
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
    internationalHeader.innerHTML = `<strong>${currentLang === 'tr' ? 'Uluslararası Havalimanları' : 'International Airports'}</strong>`;
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
    domesticHeader.innerHTML = `<strong>${currentLang === 'tr' ? 'Yerel Havalimanları' : 'Domestic Airports'}</strong>`;
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
    noResults.innerHTML = `<p>${currentLang === 'tr' ? 'Havalimanı bulunamadı' : 'No airports found'}</p>`;
    airportList.appendChild(noResults);
  }
}

// Havalimanı item'ı oluştur
function createAirportItem(airport) {
  const airportItem = document.createElement('div');
  airportItem.className = 'airport-item';
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  // Şu anda seçili havalimanlarını kontrol et
  const departureCodeEl = document.getElementById('departureCode');
  const destinationCodeEl = document.getElementById('destinationCode');
  const departureCode = departureCodeEl ? departureCodeEl.textContent : null;
  const destinationCode = destinationCodeEl ? destinationCodeEl.textContent : null;
  
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
  
  const name = currentLang === 'tr' ? airport.name : airport.enName;
  const fullName = currentLang === 'tr' ? airport.fullName : airport.enFullName;
  
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
    if (typeof handleSegmentAirportSelection === 'function') {
      handleSegmentAirportSelection(airport);
    }
    return;
  }
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  // Normal uçuş için aynı havalimanının seçilmesini engelle
  const otherSelection = currentAirportSelection === 'departure' ? 'destination' : 'departure';
  const otherCodeElement = document.getElementById(otherSelection + 'Code');
  
  if (otherCodeElement && otherCodeElement.textContent === airport.code) {
    // Hata mesajı göster
    const errorMessage = currentLang === 'tr' ? 
      'Gidiş ve dönüş için aynı havalimanını seçemezsiniz!' : 
      'You cannot select the same airport for departure and destination!';
    
    showAirportSelectionError(errorMessage);
    return;
  }
  
  const codeElement = document.getElementById(currentAirportSelection + 'Code');
  const nameElement = document.getElementById(currentAirportSelection + 'Name');
  
  if (codeElement && nameElement) {
    codeElement.textContent = airport.code;
    
    if (currentLang === 'tr') {
      nameElement.textContent = airport.name;
    } else {
      nameElement.textContent = airport.enName;
    }
  }
  
  closeAirportModal();
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
  const departureCodeEl = document.getElementById('departureCode');
  const departureNameEl = document.getElementById('departureName');
  const departureCode = departureCodeEl ? departureCodeEl.textContent : null;
  const departureName = departureNameEl ? departureNameEl.textContent : null;
  
  const destinationCodeEl = document.getElementById('destinationCode');
  const destinationNameEl = document.getElementById('destinationName');
  const destinationCode = destinationCodeEl ? destinationCodeEl.textContent : null;
  const destinationName = destinationNameEl ? destinationNameEl.textContent : null;
  
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
  
  // Passenger butonundan active class'ını kaldır (mavi olmamalı)
  // Remove active class from passenger button (should not be blue)
  const passengerButton = document.querySelector('button[onclick="openPassengerModal()"]');
  if (passengerButton) {
    passengerButton.classList.remove('active');
  }
}

// Yolcu modal'ını kapat
function closePassengerModal() {
  const modal = document.getElementById('passengerModal');
  modal.classList.remove('show');
  modal.style.display = 'none';
}

// Yolcu sayısını değiştir
function changePassengerCount(type, change) {
  console.log(`Changing ${type} count by ${change}`);
  
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
  console.log('Updated passengerCounts:', passengerCounts);
  
  // Yolcu sayılarını kaydet
  if (typeof savePassengerCounts === 'function') {
    savePassengerCounts();
  }
  
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
  
  // Buton durumlarını güncelle
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
  const cabinClassInput = document.querySelector('input[name="cabinClass"]:checked');
  const cabinClass = cabinClassInput ? cabinClassInput.value : null;
  if (cabinClass) {
    selectedCabinClass = cabinClass;
    // Kabin sınıfını localStorage'a kaydet
    // Save cabin class to localStorage
    localStorage.setItem('selectedCabinClass', selectedCabinClass);
    
    // Custom event trigger et (aynı sekmede değişiklik için)
    // Trigger custom event (for same tab changes)
    const event = new CustomEvent('cabinClassChanged', {
      detail: { cabinClass: selectedCabinClass }
    });
    window.dispatchEvent(event);
    console.log('✅ Kabin sınıfı değişti ve event tetiklendi:', selectedCabinClass);
  }
  
  // Yolcu sayılarını kaydet
  if (typeof savePassengerCounts === 'function') {
    savePassengerCounts();
  }
  
  // Yolcu butonunu güncelle
  if (typeof updatePassengerButtonText === 'function') {
    updatePassengerButtonText();
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
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  // Sadece her iki tarih de seçiliyse kontrol et
  if (departureDate && returnDate) {
    const depDate = new Date(departureDate);
    const retDate = new Date(returnDate);
    
    if (retDate < depDate) {
      // Hata durumunda dönüş tarihini temizle ve uyarı göster
      returnInput.value = '';
      showDateError(currentLang === 'tr' ? 
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
  const departureDateEl = document.getElementById('departureDate');
  const returnDateEl = document.getElementById('returnDate');
  const promoCodeEl = document.getElementById('promoCode');
  const departureDate = departureDateEl ? departureDateEl.value : null;
  const returnDate = returnDateEl ? returnDateEl.value : null;
  const promoCode = promoCodeEl ? promoCodeEl.value : null;
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  // Havalimanı seçimlerini kontrol et
  const departureCodeEl = document.getElementById('departureCode');
  const destinationCodeEl = document.getElementById('destinationCode');
  const departureCode = departureCodeEl ? departureCodeEl.textContent : null;
  const destinationCode = destinationCodeEl ? destinationCodeEl.textContent : null;
  
  if (!departureCode || departureCode === 'TZX') {
    // Varsayılan olarak Trabzon seçili, bu normal
  }
  
  if (!destinationCode || destinationCode === '?') {
    alert(currentLang === 'tr' ? 'Lütfen varış havalimanı seçin!' : 'Please select destination airport!');
    return;
  }
  
  // Aynı havalimanı kontrolü
  if (departureCode === destinationCode) {
    alert(currentLang === 'tr' ? 
      'Gidiş ve dönüş için aynı havalimanını seçemezsiniz!' : 
      'You cannot select the same airport for departure and destination!');
    return;
  }
  
  if (!departureDate) {
    alert(currentLang === 'tr' ? 'Lütfen gidiş tarihi seçin!' : 'Please select departure date!');
    return;
  }
  
  // Dönüş tarihi kontrolü - sadece gidiş-dönüş seçiliyse
  const activeButton = document.querySelector('.top-opt.active');
  const isRoundTrip = activeButton && activeButton.dataset.type === 'roundtrip';
  const tripType = activeButton ? activeButton.dataset.type : 'roundtrip';
  
  if (isRoundTrip && returnDate) {
    const depDate = new Date(departureDate);
    const retDate = new Date(returnDate);
    
    if (retDate < depDate) {
      alert(currentLang === 'tr' ? 
        'Dönüş tarihi gidiş tarihinden önce olamaz!' : 
        'Return date cannot be earlier than departure date!');
      return;
    }
  }
  
  // Dönüş tarihi zorunlu kontrolü (gidiş-dönüş için)
  if (isRoundTrip && !returnDate) {
    alert(currentLang === 'tr' ? 
      'Gidiş-dönüş seçimi için dönüş tarihi gereklidir!' : 
      'Return date is required for round trip!');
    return;
  }
  
  // Arama butonunu devre dışı bırak ve loading göster
  const searchBtn = document.querySelector('.search-btn');
  const originalBtnText = searchBtn.innerHTML;
  searchBtn.disabled = true;
  searchBtn.innerHTML = currentLang === 'tr' ? 
    '<span>Arıyor...</span> <i class="fa-solid fa-spinner fa-spin"></i>' : 
    '<span>Searching...</span> <i class="fa-solid fa-spinner fa-spin"></i>';
  
  // AJAX isteği ile backend'e bağlan
  $.ajax({
    url: '/Flights/Search',
    type: 'POST',
    data: {
      fromCode: departureCode,
      toCode: destinationCode,
      departureDate: departureDate,
      returnDate: returnDate || null,
      tripType: tripType
    },
    success: function(response) {
      // Butonu tekrar aktif et
      searchBtn.disabled = false;
      searchBtn.innerHTML = originalBtnText;
      
      if (response.success) {
        if (response.hasResults) {
          // Uçuş bulundu - sonuçları göster
          // Uçuş sonuçlarını sessionStorage'a kaydet
          sessionStorage.setItem('searchResults', JSON.stringify(response));
          sessionStorage.setItem('searchParams', JSON.stringify({
            fromCode: departureCode,
            toCode: destinationCode,
            departureDate: departureDate,
            returnDate: returnDate,
            tripType: tripType
          }));
          
          // Uçuş sonuçları sayfasına yönlendir
          window.location.href = '/Flights/SearchResults';
        } else {
          // Uçuş bulunamadı - popup göster
          showNoFlightModal();
        }
      } else {
        // Hata durumu
        alert(response.message || (currentLang === 'tr' ? 'Bir hata oluştu!' : 'An error occurred!'));
      }
    },
    error: function(xhr, status, error) {
      // Butonu tekrar aktif et
      searchBtn.disabled = false;
      searchBtn.innerHTML = originalBtnText;
      
      // Hata mesajı göster
      alert(currentLang === 'tr' ? 
        'Arama sırasında bir hata oluştu. Lütfen tekrar deneyin.' : 
        'An error occurred during search. Please try again.');
      console.error('Search error:', error);
    }
  });
}

// Uçuş bulunamadı modal'ını göster
function showNoFlightModal() {
  const modal = document.getElementById('noFlightModal');
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  // Modal içeriğini dile göre güncelle
  const title = document.getElementById('noFlightModalTitle');
  const message = document.getElementById('noFlightMessage');
  const subMessage = document.getElementById('noFlightSubMessage');
  
  if (currentLang === 'tr') {
    if (title) title.textContent = 'Uçuş Bulunamadı';
    if (message) message.textContent = 'Seçtiğiniz kriterlere uygun uçuş bulunamadı.';
    if (subMessage) subMessage.textContent = 'Lütfen farklı tarih veya havalimanı seçerek tekrar deneyin.';
  } else {
    if (title) title.textContent = 'No Flights Found';
    if (message) message.textContent = 'No flights found matching your search criteria.';
    if (subMessage) subMessage.textContent = 'Please try again with different dates or airports.';
  }
  
  if (modal) {
    modal.classList.add('show');
    modal.style.display = 'flex';
  }
}

// Uçuş bulunamadı modal'ını kapat
function closeNoFlightModal() {
  const modal = document.getElementById('noFlightModal');
  if (modal) {
    modal.classList.remove('show');
    modal.style.display = 'none';
  }
}

// Modal dışına tıklandığında kapat
document.addEventListener('DOMContentLoaded', function() {
  const noFlightModal = document.getElementById('noFlightModal');
  if (noFlightModal) {
    noFlightModal.addEventListener('click', function(e) {
      if (e.target === noFlightModal) {
        closeNoFlightModal();
      }
    });
  }
});

// Teklif rezervasyonu
function bookFlight(destination) {
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  console.log(`${destination} için rezervasyon yapılacak`);
  alert(currentLang === 'tr' ? `${destination} uçuşu için rezervasyon sayfasına yönlendiriliyorsunuz...` : `Redirecting to booking page for ${destination}...`);
}

