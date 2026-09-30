// Sidebar kontrol fonksiyonları

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

// Mobil drawer açma/kapama
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

// Ekran desktop'a dönünce mobil menüyü kapat
function autoSidebar() {
  if (window.innerWidth > 992) closeMobileSidebar();
}

// Escape ile kapatma
document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") closeMobileSidebar();
});

window.addEventListener("resize", autoSidebar);

