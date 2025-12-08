// Havalimanları listesi ve ilgili fonksiyonlar

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

// Havalimanı listesini modal içine yazdır
function renderAirportList(list) {
  const airportListEl = document.getElementById("airportList");
  if (airportListEl) {
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
}

// Sayfa yüklenince listeyi göster
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', function() {
    renderAirportList(airports);
  });
} else {
  renderAirportList(airports);
}

