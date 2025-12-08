function generateTripPlan() {
    // HTmldeki formlardan değerleri al 

    const city = document.getElementById("destinationSelect").value;
    const duration = document.getElementById("durationSelect").value;
    const budget = document.getElementById("budgetSelect").value;
    const interest = document.getElementById("interestSelect").value;

    const resultBox = document.getElementById("tripResult");
    const status = document.getElementById("tripaiStatus");

    if (!city) {
        resultBox.innerHTML = "<p>Lütfen şehir seçiniz.</p>";
        return;
    }

    //Hazırladığım planlar 
    const cityPlans = {
        antalya: `
            <h3>Antalya seyahaet Planı</h3>
            <p> KonyaAltı Plajı, Kaleiçi ve Teleferik Turu.</p>
            <p> Gün batımını mutlaka izle!</p>
        `,
        istanbul: `
            <h3>İstanbul seyahet Plano</h3>
            <p> Tarihi yarımada, Boğaz TUru ve Galata.</p>
            <p> Mutlaka kapalıçarşıya gidin</p>
        `,
        izmir: `
            <h3>İzmir seyahaet Planı</h3>
            <p> Kordon, Alsancak veEFes antik Kent.</p>
            <p> Alaçatıda bir gezi turu.</p>
        `,
        trabzon: `
            <h3>Trabzon Seyahat Planı</h3>
            <p> Uuzungöl ve Sümela Manastır.</p>
            <p> Doğa yürüyüşleri için harika yerler</p>
        `
    };

    const interestAddons = {
        tarih: "Tarihi mekan ziyaretleri ağırlıkta olacaktır.",
        deniz: "Deniz ve sahil planları.",
        dogal: "Doğa ve manzara odaklı durakları eklendi.",
        alisveris: "Alışveriş bölgeleri ve şehiriçi gezi eklendi."
    };

    const budgetNotes = {
        ekonomik: " Ekonomik btçe için uygun fiyatlı restoranlar ve oteller seçildi.",
        orta: " Orta seviye bütçeye göre dengeli bir plan hazırlandı.",
        premium: " Premium bütçe için lüks seçenekler dahil edildi."
    };

    let finalPlan = cityPlans[city];

    // Sonucu yaz
    resultBox.innerHTML = finalPlan;
    status.innerText = "Hazır";
}

