// DispHub website: entrance motion, the header material, the menu and the WinGet copy button.
(() => {
  const $ = (sel, root = document) => root.querySelector(sel);
  const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

  // header turns into acrylic once the page scrolls
  const header = $(".header");
  if (header) {
    const onScroll = () => header.classList.toggle("scrolled", window.scrollY > 8);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
  }

  // small-screen menu
  const menuBtn = $(".menu-btn");
  const nav = $("#nav");
  if (menuBtn && nav) {
    const setOpen = (open) => {
      nav.classList.toggle("open", open);
      menuBtn.setAttribute("aria-expanded", String(open));
    };
    menuBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      setOpen(!nav.classList.contains("open"));
    });
    document.addEventListener("click", (e) => {
      if (!nav.contains(e.target)) setOpen(false);
    });
    $$("a", nav).forEach((a) => a.addEventListener("click", () => setOpen(false)));
  }

  // entrance motion
  const reveal = $$("[data-reveal]");
  if ("IntersectionObserver" in window) {
    const io = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("in");
          io.unobserve(entry.target);
        }
      });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.08 });
    reveal.forEach((el) => io.observe(el));
  } else {
    reveal.forEach((el) => el.classList.add("in"));
  }

  // a small message at the bottom of the page
  let toastTimer = 0;
  const toast = (html, ms = 3200) => {
    let el = $(".toast");
    if (!el) {
      el = document.createElement("div");
      el.className = "toast";
      el.setAttribute("role", "status");
      document.body.append(el);
    }
    el.innerHTML = html;
    el.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.remove("show"), ms);
    return el;
  };

  // WinGet: copy the install command, and say so
  $$("[data-copy]").forEach((btn) => btn.addEventListener("click", async () => {
    const text = btn.dataset.copy;
    let copied = true;
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      copied = false;
    }
    const el = toast(copied ? "Copied. Paste it into Terminal: <code></code>" : "Run this in Terminal: <code></code>");
    $("code", el).textContent = text;
  }));
})();
