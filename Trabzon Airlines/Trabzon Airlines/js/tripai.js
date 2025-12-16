function generateTripPlan() {
    // HTML'deki formlardan değerleri al 

    const city = document.getElementById("destinationSelect").value;
    const duration = document.getElementById("durationSelect").value;
    const budget = document.getElementById("budgetSelect").value;
    const interest = document.getElementById("interestSelect").value;

    const resultBox = document.getElementById("tripResult");
    const status = document.getElementById("tripaiStatus");

    // Mevcut dil ayarını al
    const currentLang = localStorage.getItem('language') || 'tr';

    if (!city) {
        const noCityMsg = currentLang === 'en' 
            ? "Please select a city." 
            : "Lütfen şehir seçiniz.";
        resultBox.innerHTML = `<div class='tripai-message ai'><div class='bubble'><p>${noCityMsg}</p></div></div>`;
        return;
    }

    // Hazırladığım planlar - Türkçe ve İngilizce
    const cityPlans = {
        antalya: {
            tr: `
                <div class="tripai-message ai">
                    <div class="bubble">
                        <h3>🌴 Antalya Seyahat Planı</h3>
                        <p><strong>Önerilen Yerler:</strong></p>
                        <ul>
                            <li>🏖️ Konyaaltı Plajı - Şehir merkezine yakın, temiz kum plajı</li>
                            <li>🏛️ Kaleiçi - Tarihi Osmanlı evleri ve dar sokaklar</li>
                            <li>🚠 Olympos Teleferik - 2365 metre yükseklikte muhteşem manzara</li>
                            <li>🌅 Lara Plajı - Gün batımı için ideal nokta</li>
                            <li>🏛️ Antalya Müzesi - Türkiye'nin en önemli arkeoloji müzelerinden biri</li>
                        </ul>
                        <p><strong>💡 İpucu:</strong> Gün batımını mutlaka izleyin! Kaleiçi'nde akşam yemeği yiyebilirsiniz.</p>
                    </div>
                </div>
            `,
            en: `
                <div class="tripai-message ai">
                    <div class="bubble">
                        <h3>🌴 Antalya Travel Plan</h3>
                        <p><strong>Recommended Places:</strong></p>
                        <ul>
                            <li>🏖️ Konyaaltı Beach - Close to city center, clean sandy beach</li>
                            <li>🏛️ Kaleiçi - Historic Ottoman houses and narrow streets</li>
                            <li>🚠 Olympos Cable Car - Spectacular view at 2365 meters altitude</li>
                            <li>🌅 Lara Beach - Ideal spot for sunset</li>
                            <li>🏛️ Antalya Museum - One of Turkey's most important archaeology museums</li>
                        </ul>
                        <p><strong>💡 Tip:</strong> Don't miss the sunset! You can have dinner in Kaleiçi.</p>
                    </div>
                </div>
            `
        },
        istanbul: {
            tr: `
                <div class="tripai-message ai">
                    <div class="bubble">
                        <h3>🏛️ İstanbul Seyahat Planı</h3>
                        <p><strong>Önerilen Yerler:</strong></p>
                        <ul>
                            <li>🕌 Tarihi Yarımada - Ayasofya, Sultanahmet Camii, Topkapı Sarayı</li>
                            <li>🚢 Boğaz Turu - İstanbul Boğazı'nın eşsiz manzarası</li>
                            <li>🏰 Galata Kulesi - Şehrin panoramik manzarası</li>
                            <li>🛍️ Kapalıçarşı - Geleneksel Türk çarşısı ve el sanatları</li>
                            <li>🌉 Galata Köprüsü - Balık ekmek ve boğaz manzarası</li>
                        </ul>
                        <p><strong>💡 İpucu:</strong> Mutlaka Kapalıçarşı'ya gidin ve Türk lokumu, baharat alın!</p>
                    </div>
                </div>
            `,
            en: `
                <div class="tripai-message ai">
                    <div class="bubble">
                        <h3>🏛️ Istanbul Travel Plan</h3>
                        <p><strong>Recommended Places:</strong></p>
                        <ul>
                            <li>🕌 Historic Peninsula - Hagia Sophia, Sultanahmet Mosque, Topkapi Palace</li>
                            <li>🚢 Bosphorus Tour - Unique view of Istanbul Bosphorus</li>
                            <li>🏰 Galata Tower - Panoramic view of the city</li>
                            <li>🛍️ Grand Bazaar - Traditional Turkish bazaar and handicrafts</li>
                            <li>🌉 Galata Bridge - Fish sandwich and Bosphorus view</li>
                        </ul>
                        <p><strong>💡 Tip:</strong> Don't miss the Grand Bazaar! Buy Turkish delight and spices!</p>
                    </div>
                </div>
            `
        },
        izmir: {
            tr: `
                <div class="tripai-message ai">
                    <div class="bubble">
                        <h3>🌊 İzmir Seyahat Planı</h3>
                        <p><strong>Önerilen Yerler:</strong></p>
                        <ul>
                            <li>🌅 Kordon Boyu - Uzun sahil yürüyüş yolu ve kafeler</li>
                            <li>🏛️ Alsancak - Modern şehir merkezi ve alışveriş</li>
                            <li>🏛️ Efes Antik Kenti - Dünyaca ünlü antik Roma şehri</li>
                            <li>🏖️ Alaçatı - Rüzgar sörfü ve şirin taş evler</li>
                            <li>🏛️ Agora Açık Hava Müzesi - Antik dönem kalıntıları</li>
                        </ul>
                        <p><strong>💡 İpucu:</strong> Alaçatı'da bir gezi turu yapın ve yerel şarapları deneyin!</p>
                    </div>
                </div>
            `,
            en: `
                <div class="tripai-message ai">
                    <div class="bubble">
                        <h3>🌊 Izmir Travel Plan</h3>
                        <p><strong>Recommended Places:</strong></p>
                        <ul>
                            <li>🌅 Kordon Promenade - Long coastal walking path and cafes</li>
                            <li>🏛️ Alsancak - Modern city center and shopping</li>
                            <li>🏛️ Ephesus Ancient City - World-famous ancient Roman city</li>
                            <li>🏖️ Alaçatı - Wind surfing and charming stone houses</li>
                            <li>🏛️ Agora Open Air Museum - Ancient period ruins</li>
                        </ul>
                        <p><strong>💡 Tip:</strong> Take a tour in Alaçatı and try local wines!</p>
                    </div>
                </div>
            `
        },
        trabzon: {
            tr: `
                <div class="tripai-message ai">
                    <div class="bubble">
                        <h3>🏔️ Trabzon Seyahat Planı</h3>
                        <p><strong>Önerilen Yerler:</strong></p>
                        <ul>
                            <li>🏞️ Uzungöl - Doğa harikası göl ve yürüyüş parkurları</li>
                            <li>⛪ Sümela Manastırı - Tarihi manastır ve muhteşem manzara</li>
                            <li>🏛️ Ayasofya Müzesi - Bizans dönemi kilise</li>
                            <li>🌲 Çal Mağarası - Doğal mağara ve yürüyüş alanları</li>
                            <li>🌊 Boztepe - Şehir manzarası ve çay bahçeleri</li>
                        </ul>
                        <p><strong>💡 İpucu:</strong> Doğa yürüyüşleri için harika yerler! Hamsi ve kuymak mutlaka deneyin.</p>
                    </div>
                </div>
            `,
            en: `
                <div class="tripai-message ai">
                    <div class="bubble">
                        <h3>🏔️ Trabzon Travel Plan</h3>
                        <p><strong>Recommended Places:</strong></p>
                        <ul>
                            <li>🏞️ Uzungöl - Natural wonder lake and hiking trails</li>
                            <li>⛪ Sumela Monastery - Historic monastery and spectacular view</li>
                            <li>🏛️ Hagia Sophia Museum - Byzantine period church</li>
                            <li>🌲 Çal Cave - Natural cave and hiking areas</li>
                            <li>🌊 Boztepe - City view and tea gardens</li>
                        </ul>
                        <p><strong>💡 Tip:</strong> Great places for nature walks! Don't miss anchovy and kuymak.</p>
                    </div>
                </div>
            `
        }
    };

    const interestAddons = {
        tarih: {
            tr: "<p><strong>📚 Tarih & Kültür Odaklı:</strong> Tarihi mekan ziyaretleri ağırlıkta olacaktır. Müzeler, antik kentler ve tarihi yapılar plana dahil edildi.</p>",
            en: "<p><strong>📚 History & Culture Focused:</strong> Historical site visits will be emphasized. Museums, ancient cities and historical structures are included in the plan.</p>"
        },
        deniz: {
            tr: "<p><strong>🌊 Deniz & Plaj Odaklı:</strong> Deniz ve sahil planları ön planda. Plaj aktiviteleri, su sporları ve sahil yürüyüşleri eklendi.</p>",
            en: "<p><strong>🌊 Sea & Beach Focused:</strong> Sea and coastal plans are prioritized. Beach activities, water sports and coastal walks are added.</p>"
        },
        dogal: {
            tr: "<p><strong>🌲 Doğa & Manzara Odaklı:</strong> Doğa ve manzara odaklı duraklar eklendi. Yürüyüş parkurları, doğal güzellikler ve açık hava aktiviteleri planlandı.</p>",
            en: "<p><strong>🌲 Nature & Landscape Focused:</strong> Nature and landscape-focused stops are added. Hiking trails, natural beauties and outdoor activities are planned.</p>"
        },
        alisveris: {
            tr: "<p><strong>🛍️ Alışveriş & Şehir Odaklı:</strong> Alışveriş bölgeleri ve şehir içi gezi rotaları eklendi. Modern mağazalar, çarşılar ve şehir merkezi keşifleri planlandı.</p>",
            en: "<p><strong>🛍️ Shopping & City Focused:</strong> Shopping areas and city tour routes are added. Modern stores, bazaars and city center explorations are planned.</p>"
        }
    };

    const budgetNotes = {
        ekonomik: {
            tr: "<p><strong>💰 Ekonomik Bütçe:</strong> Ekonomik bütçe için uygun fiyatlı restoranlar, konaklama seçenekleri ve ücretsiz aktiviteler seçildi.</p>",
            en: "<p><strong>💰 Economy Budget:</strong> Budget-friendly restaurants, accommodation options and free activities are selected for economy budget.</p>"
        },
        orta: {
            tr: "<p><strong>💵 Orta Bütçe:</strong> Orta seviye bütçeye göre dengeli bir plan hazırlandı. Hem ekonomik hem de kaliteli seçenekler karıştırıldı.</p>",
            en: "<p><strong>💵 Medium Budget:</strong> A balanced plan is prepared according to medium budget. Both economical and quality options are mixed.</p>"
        },
        premium: {
            tr: "<p><strong>💎 Premium Bütçe:</strong> Premium bütçe için lüks seçenekler dahil edildi. 5 yıldızlı oteller, fine dining restoranlar ve özel turlar planlandı.</p>",
            en: "<p><strong>💎 Premium Budget:</strong> Luxury options are included for premium budget. 5-star hotels, fine dining restaurants and private tours are planned.</p>"
        }
    };

    const durationNotes = {
        "2": {
            tr: "<p><strong>⏱️ 2 Günlük Plan:</strong> Kısa süreli bir gezi için en önemli noktalar seçildi. Yoğun ama keyifli bir program hazırlandı.</p>",
            en: "<p><strong>⏱️ 2-Day Plan:</strong> The most important points are selected for a short trip. An intensive but enjoyable program is prepared.</p>"
        },
        "3": {
            tr: "<p><strong>⏱️ 3 Günlük Plan:</strong> Orta uzunlukta bir gezi için dengeli bir program. Hem dinlenme hem de keşif zamanı var.</p>",
            en: "<p><strong>⏱️ 3-Day Plan:</strong> A balanced program for a medium-length trip. There is time for both rest and exploration.</p>"
        },
        "5": {
            tr: "<p><strong>⏱️ 5 Günlük Plan:</strong> Detaylı bir keşif için ideal süre. Tüm önemli yerler ve aktiviteler rahatça yapılabilir.</p>",
            en: "<p><strong>⏱️ 5-Day Plan:</strong> Ideal duration for a detailed exploration. All important places and activities can be done comfortably.</p>"
        },
        "7": {
            tr: "<p><strong>⏱️ 7 Günlük Plan:</strong> Kapsamlı bir tatil planı. Hem şehir keşfi hem de çevre gezileri için yeterli süre.</p>",
            en: "<p><strong>⏱️ 7-Day Plan:</strong> A comprehensive vacation plan. Sufficient time for both city exploration and surrounding trips.</p>"
        }
    };

    // Temel planı al (dil ayarına göre, yoksa Türkçe'ye fallback)
    let finalPlan = '';
    if (cityPlans[city]) {
        finalPlan = cityPlans[city][currentLang] || cityPlans[city]['tr'] || '';
    }
    
    // Süre bilgisini ekle
    if (duration && durationNotes[duration]) {
        const durationNote = durationNotes[duration][currentLang] || durationNotes[duration]['tr'] || '';
        finalPlan += durationNote;
    }
    
    // İlgi alanı bilgisini ekle
    if (interest && interestAddons[interest]) {
        const interestNote = interestAddons[interest][currentLang] || interestAddons[interest]['tr'] || '';
        finalPlan += interestNote;
    }
    
    // Bütçe bilgisini ekle
    if (budget && budgetNotes[budget]) {
        const budgetNote = budgetNotes[budget][currentLang] || budgetNotes[budget]['tr'] || '';
        finalPlan += budgetNote;
    }

    // Sonucu yaz
    resultBox.innerHTML = finalPlan;
    status.innerText = currentLang === 'en' ? "Ready" : "Hazır";
}

