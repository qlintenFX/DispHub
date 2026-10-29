// DispHub website: the live quick panel, entrance motion, the header material, the menu, the WinGet copy button and
// the profiles' before and after.
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

  // the profiles page: each profile before and after on a few pictures, drawn with the app's colour maths
  // (RampBuilder, Blackbody and the preview's vibrance), in the browser, so a picture of the visitor's own never
  // leaves their PC
  const compare = $("[data-compare]");
  if (compare) {
    const linear = (t) => {
      const t2 = t * t, t3 = t2 * t;
      const x = t <= 4000
        ? -0.2661239e9 / t3 - 0.2343589e6 / t2 + 0.8776956e3 / t + 0.17991
        : -3.0258469e9 / t3 + 2.1070379e6 / t2 + 0.2226347e3 / t + 0.24039;
      const x2 = x * x, x3 = x2 * x;
      const y = t <= 2222
        ? -1.1063814 * x3 - 1.3481102 * x2 + 2.18555832 * x - 0.20219683
        : t <= 4000
          ? -0.9549476 * x3 - 1.37418593 * x2 + 2.09137015 * x - 0.16748867
          : 3.081758 * x3 - 5.8733867 * x2 + 3.75112997 * x - 0.37001483;
      const X = x / y, Z = (1 - x - y) / y;
      return [
        Math.max(3.2404542 * X - 1.5371385 - 0.4985314 * Z, 0),
        Math.max(-0.969266 * X + 1.8760108 + 0.041556 * Z, 0),
        Math.max(0.0556434 * X - 0.2040259 + 1.0572252 * Z, 0),
      ];
    };
    const reference = linear(6500);
    const encode = (v) => (v <= 0.0031308 ? 12.92 * v : 1.055 * Math.pow(v, 1 / 2.4) - 0.055);
    const whitePoint = (k) => {
      if (k === 6500) return [1, 1, 1];
      const rgb = linear(Math.min(Math.max(k, 2000), 10000)).map((v, i) => v / reference[i]);
      const max = Math.max(...rgb);
      return rgb.map((v) => encode(v / max));
    };
    // one 256-entry table per channel, as Windows gets it, already in 8 bits
    const ramps = ({ b, c, g, t }) => {
      const white = whitePoint(t);
      const tables = [new Uint8Array(256), new Uint8Array(256), new Uint8Array(256)];
      const last = [0, 0, 0];
      for (let i = 0; i < 256; i++) {
        let y = Math.pow(i / 255, 1 / g);
        y = 0.5 + (y - 0.5) * (1 + c / 100);
        y = b >= 0 ? y + (b / 100) * 0.5 * (1 - y) : y * (1 + b / 100);
        y = Math.min(Math.max(y, 0), 1);
        for (let ch = 0; ch < 3; ch++) {
          const value = Math.max(Math.round(Math.min(Math.max(y * white[ch], 0), 1) * 65535), last[ch]);
          last[ch] = value;
          tables[ch][i] = value >> 8;
        }
      }
      return tables;
    };
    const lookOf = (tab) => {
      const q = new URLSearchParams(tab.dataset.look);
      const n = (key, fallback) => (q.has(key) ? Number(q.get(key)) : fallback);
      return { b: n("b", 0), c: n("c", 0), g: n("g", 1), t: n("t", 6500), v: n("v", 50) };
    };

    const before = $("[data-before]", compare);
    const after = $("[data-after]", compare);
    const W = before.width, H = before.height;
    const bctx = before.getContext("2d");
    const actx = after.getContext("2d");
    const tabs = $$(".segmented [role=tab]");
    const examples = $("[data-examples]");
    const own = $(".example.own", examples);
    const cache = new Map();
    let tab = tabs[0];
    let picture = null; // a site picture's name, or "own"
    let ownPixels = null;

    // cover the frame, keeping the middle of the picture
    const pixelsOf = (img) => {
      bctx.clearRect(0, 0, W, H);
      const scale = Math.max(W / img.width, H / img.height);
      const w = img.width * scale, h = img.height * scale;
      bctx.drawImage(img, (W - w) / 2, (H - h) / 2, w, h);
      return bctx.getImageData(0, 0, W, H);
    };
    const load = (name) => {
      if (!cache.has(name)) {
        cache.set(name, new Promise((resolve, reject) => {
          const img = new Image();
          img.onload = () => resolve(img);
          img.onerror = reject;
          img.src = `../assets/img/show/${name}.webp`;
        }));
      }
      return cache.get(name);
    };

    const render = (source) => {
      bctx.putImageData(source, 0, 0);
      const look = lookOf(tab);
      const [red, green, blue] = ramps(look);
      const saturation = look.v / 50;
      const out = actx.createImageData(W, H);
      const s = source.data, d = out.data;
      const clamp = (v) => Math.min(Math.max(Math.round(v), 0), 255);
      for (let i = 0; i < s.length; i += 4) {
        let r = s[i], g = s[i + 1], b = s[i + 2];
        if (saturation !== 1) {
          const luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
          r = luma + (r - luma) * saturation;
          g = luma + (g - luma) * saturation;
          b = luma + (b - luma) * saturation;
        }
        d[i] = red[clamp(r)];
        d[i + 1] = green[clamp(g)];
        d[i + 2] = blue[clamp(b)];
        d[i + 3] = 255;
      }
      actx.putImageData(out, 0, 0);
    };
    let drawn = 0;
    const show = async (name) => {
      picture = name;
      $$(".example", examples).forEach((ex) => ex.setAttribute("aria-checked", String(ex.dataset.picture === name)));
      const ticket = ++drawn;
      const source = name === "own" ? ownPixels : pixelsOf(await load(name));
      if (ticket === drawn && source) render(source);
    };

    // the three pictures that fit the chosen profile, then the visitor's own
    const fill = () => {
      $$(".example:not(.own)", examples).forEach((ex) => ex.remove());
      const alts = tab.dataset.alts.split("|");
      tab.dataset.pictures.split(" ").forEach((name, i) => {
        const ex = document.createElement("button");
        ex.type = "button";
        ex.className = "example";
        ex.dataset.picture = name;
        ex.setAttribute("role", "radio");
        ex.setAttribute("aria-label", alts[i]);
        ex.innerHTML = `<img src="../assets/img/show/${name}-thumb.webp" alt="" width="320" height="180">`;
        ex.addEventListener("click", () => show(name));
        examples.insertBefore(ex, own);
      });
    };
    const choose = (next) => {
      tab = next;
      tabs.forEach((t) => t.setAttribute("aria-selected", String(t === tab)));
      $("[data-show-line]").textContent = tab.dataset.line;
      $("[data-show-values]").textContent = tab.dataset.values;
      $("[data-after-name]", compare).textContent = tab.textContent;
      fill();
      show(picture === "own" ? "own" : tab.dataset.pictures.split(" ")[0]);
    };
    tabs.forEach((t) => t.addEventListener("click", () => choose(t)));
    // arrow keys move between the tabs, as in Windows
    $(".segmented").addEventListener("keydown", (e) => {
      const step = e.key === "ArrowRight" ? 1 : e.key === "ArrowLeft" ? -1 : 0;
      if (!step) return;
      const next = tabs[(tabs.indexOf(tab) + step + tabs.length) % tabs.length];
      choose(next);
      next.focus();
    });

    const split = $("[data-split]", compare);
    const move = () => compare.style.setProperty("--split", `${split.value}%`);
    split.addEventListener("input", move);
    move();

    const useFile = (file) => {
      if (!file || !file.type.startsWith("image/")) return;
      const url = URL.createObjectURL(file);
      const img = new Image();
      img.onload = () => {
        ownPixels = pixelsOf(img);
        // the tile shows the picture, small
        const thumb = document.createElement("canvas");
        thumb.width = 256;
        thumb.height = 144;
        thumb.getContext("2d").drawImage(before, 0, 0, W, H, 0, 0, 256, 144);
        own.style.backgroundImage = `url(${thumb.toDataURL("image/jpeg", 0.8)})`;
        own.classList.add("has-picture");
        URL.revokeObjectURL(url);
        own.dataset.picture = "own";
        show("own");
      };
      img.src = url;
    };
    const picker = $("[data-picture]", examples);
    picker.addEventListener("change", () => useFile(picker.files[0]));
    // a picture dropped anywhere on the page works too
    document.addEventListener("dragover", (e) => e.preventDefault());
    document.addEventListener("drop", (e) => {
      e.preventDefault();
      useFile(e.dataTransfer.files[0]);
    });

    choose(tab);
  }

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
