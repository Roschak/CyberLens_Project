// CyberLens client helpers: theme, clock, scrolling, uploads.
window.cyberlens = {
  initTheme() {
    const saved = localStorage.getItem('cl-theme') || 'light';
    document.documentElement.setAttribute('data-theme', saved);
    return saved;
  },
  toggleTheme() {
    const cur = document.documentElement.getAttribute('data-theme') || 'light';
    const next = cur === 'light' ? 'dark' : 'light';
    document.documentElement.setAttribute('data-theme', next);
    localStorage.setItem('cl-theme', next);
    window.dispatchEvent(new CustomEvent('cl-theme-changed', { detail: next }));
    return next;
  },
  currentTheme() { return document.documentElement.getAttribute('data-theme') || 'light'; },
  startClock(el) {
    if (!el) return;
    const tick = () => {
      const d = new Date();
      const wib = new Date(d.getTime() + (7 * 60 + d.getTimezoneOffset()) * 60000);
      el.textContent = wib.toISOString().slice(0, 19).replace('T', ' ') + ' WIB';
    };
    tick();
    setInterval(tick, 1000);
  },
  scrollToBottom(el) { if (el) el.scrollTop = el.scrollHeight; },
  focusEl(el) { if (el) el.focus(); }
};

document.addEventListener('DOMContentLoaded', () => window.cyberlens.initTheme());
window.cyberlens.initTheme();
