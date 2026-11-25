// The page a shared profile link opens (getdisphub.com/p/?name=...&v=70). It shows the profile and opens it in
// DispHub through disphub://preset with the same parameters. The values are only shown here: DispHub checks and
// limits them again, and asks, before it adds the profile. Keep the limits in step with ColorSettings and
// SharedPreset in DispHub.Core.
(() => {
  const query = new URLSearchParams(location.search);
  const known = ["name", "b", "c", "g", "t", "v", "s"].some((key) => query.has(key));
  const $ = (id) => document.getElementById(id);

  if (!known) {
    $("profile-name").textContent = "This link has no profile.";
    $("profile-intro").textContent = "Ask for the link again, or get DispHub and make your own.";
    $("profile-open").remove();
    return;
  }

  const clamp = (value, min, max) => Math.min(max, Math.max(min, value));
  const int = (key, fallback) => {
    const value = Number.parseInt(query.get(key) ?? "", 10);
    return Number.isFinite(value) ? value : fallback;
  };
  const float = (key, fallback) => {
    const value = Number.parseFloat(query.get(key) ?? "");
    return Number.isFinite(value) ? value : fallback;
  };
  const signed = (value) => (value > 0 ? `+${value}` : `${value}`);

  const brightness = clamp(int("b", 0), -50, 50);
  const contrast = clamp(int("c", 0), -50, 50);
  const gamma = Math.round(clamp(float("g", 1), 0.3, 2.8) * 100) / 100;
  const temperature = clamp(int("t", 6500), 2000, 10000);
  const vibrance = clamp(int("v", 50), 0, 100);
  const shadows = clamp(int("s", 0), 0, 100);

  // the same summary as DispHub's profile cards
  const parts = [];
  if (temperature !== 6500) parts.push(`${temperature} K`);
  if (brightness !== 0) parts.push(`Brightness ${signed(brightness)}`);
  if (contrast !== 0) parts.push(`Contrast ${signed(contrast)}`);
  if (gamma !== 1) parts.push(`Gamma ${gamma.toFixed(2)}`);
  if (shadows !== 0) parts.push(`Shadows +${shadows}`);
  if (vibrance !== 50) parts.push(`Vibrance ${vibrance}%`);

  const name = (query.get("name") ?? "").replace(/[\u0000-\u001f\u007f-\u009f]/g, "").trim().slice(0, 40) || "Shared preset";
  $("profile-name").textContent = name;
  $("profile-values").textContent = parts.length ? parts.join(" · ") : "Original colours";
  $("profile-open").href = `disphub://preset${location.search}`;
  document.title = `${name} · DispHub profile`;
})();
