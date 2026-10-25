// DispHub website: the live quick panel, entrance motion, the header material, the menu and the WinGet copy button.
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

  // WinGet: copy the install command, and say so in a small toast
  $$("[data-copy]").forEach((btn) => btn.addEventListener("click", async () => {
    const text = btn.dataset.copy;
    let toast = $(".toast");
    if (!toast) {
      toast = document.createElement("div");
      toast.className = "toast";
      toast.setAttribute("role", "status");
      document.body.append(toast);
    }
    try {
      await navigator.clipboard.writeText(text);
      toast.innerHTML = "Copied. Paste it into Terminal: <code></code>";
    } catch {
      toast.innerHTML = "Run this in Terminal: <code></code>";
    }
    $("code", toast).textContent = text;
    toast.classList.add("show");
    clearTimeout(btn._t);
    btn._t = setTimeout(() => toast.classList.remove("show"), 3200);
  }));

  // the live quick panel: the photo uses the looks the app's colour ramps produce
  const quick = $(".quick");
  if (!quick) return;
  const PROFILES = {
    standard: { name: "Standard", b: 0, w: 6500 },
    gaming: { name: "Gaming", b: 5, w: 6500 },
    night: { name: "Night", b: -10, w: 3600 },
    dim: { name: "Dim", b: -38, w: 6500 },
  };
  const ORDER = ["standard", "gaming", "night", "dim"];
  const looks = $$(".screen .look");
  const warm = $('.tune[data-tune="warm"]');
  const cool = $('.tune[data-tune="cool"]');
  const osd = $(".osd");
  const osdName = $("[data-osd-name]");
  const state = $("[data-state]", quick);
  const adjusting = $("[data-adjusting]", quick);
  const toggle = $(".toggle", quick);
  const sliders = { b: $('[data-slider="b"]', quick), w: $('[data-slider="w"]', quick) };
  const outs = { b: $('[data-out="b"]', quick), w: $('[data-out="w"]', quick) };
  const glow = $(".demo-glow");
  let current = "standard";
  let on = true;
  let osdTimer = 0;

  const fill = (input) => {
    const p = ((input.value - input.min) / (input.max - input.min)) * 100;
    input.style.setProperty("--fill", `${p}%`);
  };

  // the sliders tune the shown look: brightness through a CSS filter, warmth with a soft colour wash
  const applyTune = () => {
    const p = PROFILES[current];
    const db = on ? Number(sliders.b.value) - p.b : 0;
    const dw = on ? Number(sliders.w.value) - p.w : 0;
    looks.forEach((img) => { img.style.filter = db ? `brightness(${(1 + db * 0.009).toFixed(3)})` : ""; });
    warm.style.opacity = dw < 0 ? Math.min(0.55, (-dw / 4600) * 0.62).toFixed(3) : "0";
    cool.style.opacity = dw > 0 ? Math.min(0.4, (dw / 2900) * 0.4).toFixed(3) : "0";
    outs.b.textContent = (Number(sliders.b.value) > 0 ? "+" : "") + sliders.b.value;
    outs.w.textContent = `${sliders.w.value} K`;
    fill(sliders.b);
    fill(sliders.w);
  };

  // the on-screen message shows just the profile's name, as the app does
  const showOsd = (name) => {
    osdName.textContent = name;
    osd.classList.add("show");
    clearTimeout(osdTimer);
    osdTimer = setTimeout(() => osd.classList.remove("show"), 1800);
  };

  const render = () => {
    const shown = on ? current : "standard";
    looks.forEach((img) => img.classList.toggle("on", img.dataset.look === shown));
    $$(".tile", quick).forEach((t) => t.setAttribute("aria-pressed", String(t.dataset.profile === current)));
    quick.classList.toggle("off", !on);
    toggle.setAttribute("aria-checked", String(on));
    toggle.setAttribute("aria-label", on ? "DispHub on" : "DispHub off");
    const p = PROFILES[current];
    state.textContent = on ? `${p.name} · Selected` : "Off · Original colours";
    adjusting.textContent = `Adjusting ${p.name}`;
    if (glow) glow.style.opacity = on && current !== "standard" ? "0.3" : "0.22";
    applyTune();
  };

  const select = (key, announce = true) => {
    current = key;
    on = true;
    sliders.b.value = PROFILES[key].b;
    sliders.w.value = PROFILES[key].w;
    render();
    if (announce) showOsd(PROFILES[key].name);
  };

  $$(".tile", quick).forEach((t) => t.addEventListener("click", () => select(t.dataset.profile)));
  toggle.addEventListener("click", () => {
    on = !on;
    render();
    showOsd(on ? PROFILES[current].name : "Off");
  });
  Object.values(sliders).forEach((s) => s.addEventListener("input", applyTune));

  // Ctrl+Alt+1-4, as in the app. Matched on the key's position, so AltGr layouts work too.
  document.addEventListener("keydown", (e) => {
    if (!e.ctrlKey || !e.altKey || e.shiftKey || e.metaKey) return;
    const m = /^(?:Digit|Numpad)([1-4])$/.exec(e.code);
    if (!m) return;
    e.preventDefault();
    select(ORDER[Number(m[1]) - 1]);
  });

  // load the other looks once the page is idle, so the first switch is instant
  const warmUp = () => looks.forEach((img) => { img.loading = "eager"; });
  if ("requestIdleCallback" in window) requestIdleCallback(warmUp, { timeout: 2500 });
  else setTimeout(warmUp, 1500);

  select("standard", false);
})();
