/* =========================================================
   Trabzon Asistan - AI Chat Box
   Sohbet geçmişi sessionStorage'da tutulur (sekme kapanınca silinir)
   ========================================================= */
(function () {
  "use strict";

  var root = document.getElementById("taChat");
  if (!root) return;

  var userId = root.getAttribute("data-user-id") || "";
  var userName = root.getAttribute("data-user-name") || "";
  // Geçmiş kullanıcıya özel tutulur: misafir sohbeti ile hesap sohbeti karışmaz
  var STORE_KEY = "taChatHistory_" + (userId || "guest");
  var OPEN_KEY = "taChatOpen";
  var endpoint = root.getAttribute("data-endpoint");
  var unreadUrl = root.getAttribute("data-unread");
  var messagesUrl = root.getAttribute("data-messages") || "/Messages";

  var panel = document.getElementById("taChatPanel");
  var body = document.getElementById("taChatBody");
  var form = document.getElementById("taChatForm");
  var input = document.getElementById("taChatText");
  var sendBtn = document.getElementById("taChatSend");
  var launcher = document.getElementById("taChatLauncher");
  var closeBtn = document.getElementById("taChatClose");
  var resetBtn = document.getElementById("taChatReset");
  var suggestions = document.getElementById("taChatSuggestions");

  var history = load();
  var busy = false;

  function lang() {
    try { return localStorage.getItem("language") || "tr"; } catch (e) { return "tr"; }
  }
  function t(tr, en) { return lang() === "en" ? en : tr; }

  function load() {
    try { return JSON.parse(sessionStorage.getItem(STORE_KEY)) || []; } catch (e) { return []; }
  }
  function save() {
    try { sessionStorage.setItem(STORE_KEY, JSON.stringify(history.slice(-30))); } catch (e) { }
  }

  function token() {
    var el = document.querySelector('#taChatToken input[name="__RequestVerificationToken"]');
    return el ? el.value : "";
  }

  // ---------- Basit ve güvenli markdown ----------
  function escapeHtml(s) {
    return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
  }
  function inline(s) {
    return s
      // [metin](/yol) veya [metin](https://...)
      .replace(/\[([^\]]+)\]\(((?:https?:\/\/|\/)[^\s)]*)\)/g, '<a href="$2">$1</a>')
      // Yalın site yolları: /CheckIn, /Flights/Status
      .replace(/(^|[\s(])(\/(?:Home|CheckIn|Flights|Account|Booking)(?:\/[A-Za-z]+)?)(?=[\s).,!?]|$)/g, '$1<a href="$2">$2</a>')
      .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
      .replace(/(^|[^*])\*([^*\n]+)\*(?!\*)/g, "$1<em>$2</em>");
  }
  function render(text) {
    var lines = escapeHtml(text).split(/\r?\n/);
    var html = "", inList = false, para = [];
    function flush() { if (para.length) { html += "<p>" + inline(para.join("<br>")) + "</p>"; para = []; } }
    lines.forEach(function (line) {
      var m = line.match(/^\s*(?:[-*•]|\d+\.)\s+(.*)$/);
      if (m) {
        flush();
        if (!inList) { html += "<ul>"; inList = true; }
        html += "<li>" + inline(m[1]) + "</li>";
      } else {
        if (inList) { html += "</ul>"; inList = false; }
        if (line.trim() === "") flush(); else para.push(line.replace(/^#+\s*/, ""));
      }
    });
    flush();
    if (inList) html += "</ul>";
    return html;
  }

  // ---------- UI ----------
  function addBubble(role, text, extraClass) {
    var div = document.createElement("div");
    div.className = "ta-msg " + (role === "user" ? "user" : "bot") + (extraClass ? " " + extraClass : "");
    if (role === "user") div.textContent = text; else div.innerHTML = render(text);
    body.appendChild(div);
    body.scrollTop = body.scrollHeight;
    return div;
  }

  function greeting() {
    if (userId) {
      var n = escapeHtml(userName);
      return t(
        "Merhaba " + n + "! 👋 Rezervasyonların ve baktığın uçuşlar elimde. Ne konuda yardımcı olayım?",
        "Hi " + n + "! 👋 I can see your bookings and the flights you've been looking at. How can I help?"
      );
    }
    return t(
      "Merhaba! 👋 Ben Trabzon Asistan. Uçuşlar, check-in, bagaj veya rezervasyonla ilgili sorularınızda yardımcı olabilirim.",
      "Hi! 👋 I'm Trabzon Assistant. I can help with flights, check-in, baggage or bookings."
    );
  }

  function renderAll() {
    body.innerHTML = "";
    addBubble("assistant", greeting());
    history.forEach(function (m) { addBubble(m.role, m.content); });
    suggestions.classList.toggle("hidden", history.length > 0);
  }

  function showTyping() {
    var d = document.createElement("div");
    d.className = "ta-msg bot ta-typing";
    d.id = "taTyping";
    d.innerHTML = "<span></span><span></span><span></span>";
    body.appendChild(d);
    body.scrollTop = body.scrollHeight;
  }
  function hideTyping() {
    var d = document.getElementById("taTyping");
    if (d) d.remove();
  }

  function setOpen(open) {
    root.classList.toggle("open", open);
    panel.setAttribute("aria-hidden", open ? "false" : "true");
    launcher.setAttribute("aria-expanded", open ? "true" : "false");
    try { sessionStorage.setItem(OPEN_KEY, open ? "1" : "0"); } catch (e) { }
    if (open) setTimeout(function () { input.focus(); body.scrollTop = body.scrollHeight; }, 150);
  }

  function autoGrow() {
    input.style.height = "auto";
    input.style.height = Math.min(input.scrollHeight, 110) + "px";
    sendBtn.disabled = busy || input.value.trim() === "";
  }

  // ---------- Gönder ----------
  function send(text) {
    text = (text || "").trim();
    if (!text || busy) return;

    busy = true;
    suggestions.classList.add("hidden");
    addBubble("user", text);
    input.value = "";
    autoGrow();
    showTyping();

    var params = new URLSearchParams();
    params.append("message", text);
    params.append("history", JSON.stringify(history));
    params.append("lang", lang());
    params.append("__RequestVerificationToken", token());

    fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8", "X-Requested-With": "XMLHttpRequest" },
      body: params.toString(),
      credentials: "same-origin"
    })
      .then(function (r) {
        if (!r.ok) throw new Error("HTTP " + r.status);
        return r.json();
      })
      .then(function (data) {
        hideTyping();
        if (data && data.success) {
          history.push({ role: "user", content: text });
          history.push({ role: "assistant", content: data.reply });
          save();
          addBubble("assistant", data.reply);
        } else {
          addBubble("assistant", (data && data.message) || t("Bir hata oluştu.", "Something went wrong."), "error");
        }
      })
      .catch(function () {
        hideTyping();
        addBubble("assistant", t("Bağlantı hatası. Lütfen tekrar deneyin.", "Connection error. Please try again."), "error");
      })
      .then(function () {
        busy = false;
        autoGrow();
        input.focus();
      });
  }

  // ---------- Olaylar ----------
  launcher.addEventListener("click", function () { setOpen(!root.classList.contains("open")); });
  closeBtn.addEventListener("click", function () { setOpen(false); });
  resetBtn.addEventListener("click", function () {
    history = [];
    save();
    renderAll();
    input.focus();
  });

  form.addEventListener("submit", function (e) { e.preventDefault(); send(input.value); });
  input.addEventListener("input", autoGrow);
  input.addEventListener("keydown", function (e) {
    if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(input.value); }
  });

  suggestions.addEventListener("click", function (e) {
    var b = e.target.closest("button");
    if (b) send(b.textContent);
  });

  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && root.classList.contains("open")) setOpen(false);
  });

  // ---------- Mesaj rozeti + fiyat düştü bildirimi ----------
  window.taSetMsgBadge = function (n) {
    var b = document.getElementById("headerMsgBadge");
    if (!b) return;
    n = parseInt(n, 10) || 0;
    b.textContent = n > 9 ? "9+" : String(n);
    b.classList.toggle("show", n > 0);
  };

  function showPriceNotice(latest) {
    var d = document.createElement("div");
    d.className = "ta-msg bot notice" + (latest.type === "WeatherAlert" ? " warn" : "");
    var title = lang() === "en" ? (latest.titleEn || latest.titleTr) : latest.titleTr;
    var text = lang() === "en" ? (latest.bodyEn || latest.bodyTr) : latest.bodyTr;
    var icon = latest.type === "WeatherAlert" ? "⛈️ " : "🔔 ";
    d.innerHTML = "<p><strong>" + icon + escapeHtml(title) + "</strong></p><p>" + escapeHtml(text) + "</p>" +
      '<p><a href="' + messagesUrl + '">' + t("Mesajlarıma git →", "Go to my messages →") + "</a></p>";
    // Karşılama mesajının hemen altına ekle
    var first = body.firstChild;
    if (first && first.nextSibling) body.insertBefore(d, first.nextSibling); else body.appendChild(d);
  }

  function checkMessages() {
    if (!userId || !unreadUrl) return;
    fetch(unreadUrl, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } })
      .then(function (r) { return r.json(); })
      .then(function (data) {
        if (!data || !data.loggedIn) return;
        window.taSetMsgBadge(data.count);
        if (data.latest) {
          showPriceNotice(data.latest);
          var key = "taNotified_" + data.latest.id;
          var seen = false;
          try { seen = sessionStorage.getItem(key) === "1"; sessionStorage.setItem(key, "1"); } catch (e) { }
          if (!seen && !root.classList.contains("open")) {
            var dot = document.getElementById("taLaunchDot");
            if (dot) dot.classList.add("show");
          }
        }
      })
      .catch(function () { });
  }

  // İlk yükleme
  renderAll();
  checkMessages();
  try { if (sessionStorage.getItem(OPEN_KEY) === "1") setOpen(true); } catch (e) { }
})();
