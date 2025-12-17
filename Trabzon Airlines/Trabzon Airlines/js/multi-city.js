// Çoklu uçuş fonksiyonları

let flightSegments = [];
let segmentCounter = 0;

// Çoklu uçuş sistemini başlat
function initializeMultiCityFlights() {
  const segmentsContainer = document.getElementById('flightSegments');
  if (!segmentsContainer) return;
  
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
  if (!segmentsContainer) return;
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');

  // Önceki segmentin varış havalimanını al
  let defaultDepartureCode = 'TZX';
  let defaultDepartureName = currentLang === 'tr' ? 'Trabzon' : 'Trabzon';
  
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
    destinationName: currentLang === 'tr' ? 'Nereye' : 'Where to'
  };
  
  flightSegments.push(segment);
  
  // HTML oluştur
  const segmentHtml = createFlightSegmentHtml(segment, flightSegments.length);
  segmentsContainer.insertAdjacentHTML('beforeend', segmentHtml);
  
  // Segment 3'ten fazlaysa silme butonlarını göster
  updateDeleteButtons();
  
  // Yeni segment için dil çevirisini uygula
  if (typeof updatePageLanguage === 'function') {
    updatePageLanguage();
  }
}

// Uçuş segmenti HTML'i oluştur
function createFlightSegmentHtml(segment, segmentNumber) {
  const isRemovable = flightSegments.length > 2;
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  // Dile göre başlık metni
  const headerText = currentLang === 'tr' ? `Uçuş ${segmentNumber}` : `Flight ${segmentNumber}`;
  
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
        <input type="date" id="segment-${segment.id}-date" class="segment-date-input" min="${new Date().toISOString().split('T')[0]}" onchange="validateSegmentDates(${segment.id})">
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
  flightSegments = flightSegments.filter(segment => segment.id != segmentId);
  
  // Segment numaralarını güncelle
  updateSegmentNumbers();
  updateDeleteButtons();
}

// Segment numaralarını güncelle
function updateSegmentNumbers() {
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  const segmentElements = document.querySelectorAll('.flight-segment');
  segmentElements.forEach((element, index) => {
    const header = element.querySelector('.segment-header h4');
    if (header) {
      const segmentNumber = index + 1;
      if (currentLang === 'tr') {
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

// Multi-city segment havalimanı seçimi
function handleSegmentAirportSelection(airport) {
  const parts = currentAirportSelection.split('-');
  const segmentId = parts[1];
  const type = parts[2]; // 'departure' veya 'destination'
  
  const codeElement = document.getElementById(`segment-${segmentId}-${type === 'departure' ? 'dep' : 'dest'}-code`);
  const nameElement = document.getElementById(`segment-${segmentId}-${type === 'departure' ? 'dep' : 'dest'}-name`);
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  if (codeElement && nameElement) {
    codeElement.textContent = airport.code;
    
    if (currentLang === 'tr') {
      nameElement.textContent = airport.name;
    } else {
      nameElement.textContent = airport.enName;
    }
    
    // Eğer bir segment'in varış havalimanı seçildiyse, sonraki segment'in kalkış havalimanını otomatik güncelle
    if (type === 'destination') {
      updateNextSegmentDeparture(segmentId, airport);
    }
  }
  
  if (typeof closeAirportModal === 'function') {
    closeAirportModal();
  }
}

// Sonraki segment'in kalkış havalimanını güncelle
function updateNextSegmentDeparture(currentSegmentId, selectedAirport) {
  const currentSegmentIndex = flightSegments.findIndex(seg => seg.id == currentSegmentId);
  const nextSegmentIndex = currentSegmentIndex + 1;
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  if (nextSegmentIndex < flightSegments.length) {
    const nextSegment = flightSegments[nextSegmentIndex];
    const nextDepCode = document.getElementById(`segment-${nextSegment.id}-dep-code`);
    const nextDepName = document.getElementById(`segment-${nextSegment.id}-dep-name`);
    
    if (nextDepCode && nextDepName) {
      nextDepCode.textContent = selectedAirport.code;
      
      if (currentLang === 'tr') {
        nextDepName.textContent = selectedAirport.name;
      } else {
        nextDepName.textContent = selectedAirport.enName;
      }
    }
  }
}

// Multi-city tarih validasyonu
function validateSegmentDates(changedSegmentId) {
  const changedSegment = flightSegments.find(seg => seg.id == changedSegmentId);
  if (!changedSegment) return;
  
  const changedSegmentIndex = flightSegments.findIndex(seg => seg.id == changedSegmentId);
  const changedDateInput = document.getElementById(`segment-${changedSegmentId}-date`);
  const changedDate = new Date(changedDateInput.value);
  
  if (!changedDateInput.value) return;
  
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  // Önceki segment'in tarihini kontrol et
  if (changedSegmentIndex > 0) {
    const previousSegment = flightSegments[changedSegmentIndex - 1];
    const previousDateInput = document.getElementById(`segment-${previousSegment.id}-date`);
    
    if (previousDateInput.value) {
      const previousDate = new Date(previousDateInput.value);
      
      if (changedDate < previousDate) {
        // Hata: Bu uçuş önceki uçuştan önce olamaz
        showSegmentDateError(changedSegmentId, currentLang === 'tr' ? 
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
  const currentLang = typeof currentLanguage !== 'undefined' ? currentLanguage : (localStorage.getItem('language') || 'tr');
  
  for (let i = fromIndex + 1; i < flightSegments.length; i++) {
    const segment = flightSegments[i];
    const dateInput = document.getElementById(`segment-${segment.id}-date`);
    
    if (dateInput && dateInput.value) {
      const segmentDate = new Date(dateInput.value);
      
      if (segmentDate < minDate) {
        // Bu segment'in tarihi geçersiz, temizle
        dateInput.value = '';
        showSegmentDateError(segment.id, currentLang === 'tr' ? 
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
  const dateInput = document.getElementById(`segment-${segmentId}-date`);
  if (dateInput && dateInput.parentElement) {
    dateInput.parentElement.appendChild(errorDiv);
  }
  
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

