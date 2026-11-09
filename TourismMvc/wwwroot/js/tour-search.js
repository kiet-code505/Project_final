(() => {
    'use strict';
    const toggle = document.getElementById('filter-toggle');
    const panel = document.getElementById('tour-filter-panel');
    toggle?.addEventListener('click', () => {
        panel.hidden = !panel.hidden;
        toggle.setAttribute('aria-expanded', String(!panel.hidden));
        toggle.classList.toggle('filterToggleActive', !panel.hidden);
    });
    document.querySelectorAll('[data-tour-url]').forEach(card => card.addEventListener('click', event => {
        if (!event.target.closest('button, a')) window.location.assign(card.dataset.tourUrl);
    }));
})();
