(() => {
    'use strict';
    // Rich content is encoded by Razor. Rebuild only the allowed formatting nodes.
    const allowedTags = new Set(['P', 'BR', 'DIV', 'SPAN', 'STRONG', 'B', 'EM', 'I', 'U', 'S', 'UL', 'OL', 'LI', 'H2', 'H3', 'H4', 'H5', 'BLOCKQUOTE', 'TABLE', 'THEAD', 'TBODY', 'TR', 'TH', 'TD', 'A', 'IMG', 'HR']);
    const blockedTags = new Set(['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'EMBED', 'SVG', 'MATH', 'LINK', 'META', 'BASE', 'FORM', 'INPUT', 'BUTTON', 'TEXTAREA', 'TEMPLATE']);
    const safeUrl = (value, image = false) => {
        try {
            const url = new URL(value, location.origin);
            return ['http:', 'https:', ...(image ? [] : ['tel:', 'mailto:'])].includes(url.protocol) ? url.href : null;
        } catch { return null; }
    };
    const clean = node => {
        if (node.nodeType === Node.TEXT_NODE) return document.createTextNode(node.textContent);
        const result = document.createDocumentFragment();
        if (node.nodeType !== Node.ELEMENT_NODE || blockedTags.has(node.tagName)) return result;
        if (!allowedTags.has(node.tagName)) { node.childNodes.forEach(child => result.append(clean(child))); return result; }
        const element = document.createElement(node.tagName.toLowerCase());
        if (node.tagName === 'A') {
            const href = safeUrl(node.getAttribute('href') || '');
            if (href) { element.setAttribute('href', href); element.setAttribute('rel', 'noopener noreferrer'); }
        }
        if (node.tagName === 'IMG') {
            const src = safeUrl(node.getAttribute('src') || '', true);
            if (!src) return result;
            element.src = src; element.alt = node.getAttribute('alt') || ''; element.loading = 'lazy';
        }
        node.childNodes.forEach(child => element.append(clean(child)));
        return element;
    };
    document.querySelectorAll('[data-rich-text]').forEach(container => {
        const text = container.textContent;
        if (!/<[a-z][\s\S]*>/i.test(text)) { container.classList.add('plain-text'); return; }
        const template = document.createElement('template'); template.innerHTML = text;
        const content = document.createDocumentFragment();
        template.content.childNodes.forEach(child => content.append(clean(child)));
        container.replaceChildren(content);
    });

    const calendar = document.getElementById('departures');
    const monthPanels = [...document.querySelectorAll('[data-month-panel]')];
    let activeMonth = calendar?.dataset.initialMonth;
    const showMonth = key => {
        activeMonth = key;
        monthPanels.forEach(panel => { panel.hidden = panel.dataset.monthPanel !== key; });
        document.querySelectorAll('[data-month-select]').forEach(link => {
            const active = link.dataset.monthSelect === key;
            link.classList.toggle('active', active);
            active ? link.setAttribute('aria-current', 'true') : link.removeAttribute('aria-current');
        });
        const index = monthPanels.findIndex(panel => panel.dataset.monthPanel === key);
        document.querySelectorAll('[data-month-step]').forEach(button => {
            button.disabled = Number(button.dataset.monthStep) < 0 ? index <= 0 : index >= monthPanels.length - 1;
        });
    };
    if (monthPanels.length) showMonth(activeMonth || monthPanels[0].dataset.monthPanel);
    document.querySelectorAll('[data-month-select]').forEach(link => link.addEventListener('click', event => { event.preventDefault(); showMonth(link.dataset.monthSelect); }));
    document.querySelectorAll('[data-month-step]').forEach(button => button.addEventListener('click', () => {
        const index = monthPanels.findIndex(panel => panel.dataset.monthPanel === activeMonth);
        const next = monthPanels[index + Number(button.dataset.monthStep)]; if (next) showMonth(next.dataset.monthPanel);
    }));
    document.querySelectorAll('[data-select-departure]').forEach(link => link.addEventListener('click', event => {
        event.preventDefault(); const id = link.dataset.selectDeparture;
        document.querySelectorAll('[data-departure-panel]').forEach(panel => { panel.hidden = panel.dataset.departurePanel !== id; });
        document.querySelectorAll('[data-sidebar-departure]').forEach(panel => { panel.hidden = panel.dataset.sidebarDeparture !== id; });
        document.querySelectorAll('[data-departure-policy]').forEach(panel => { panel.hidden = panel.dataset.departurePolicy !== id; });
        document.querySelectorAll('[data-default-sidebar], [data-default-policy]').forEach(panel => { panel.hidden = true; });
        document.querySelectorAll('[data-select-departure]').forEach(item => {
            item.classList.toggle('selected', item === link);
            item === link ? item.setAttribute('aria-current', 'true') : item.removeAttribute('aria-current');
        });
        const url = new URL(location.href); url.searchParams.set('departureId', id); url.searchParams.delete('departureDate');
        history.replaceState(null, '', url.pathname + url.search);
        calendar.dataset.selectedDeparture = id;
        const bookLink = document.getElementById('book-tour-link');
        if (bookLink) {
            const available = link.dataset.bookable === 'true';
            const bookingUrl = new URL('/order-booking', location.origin);
            bookingUrl.searchParams.set('tourCode', bookLink.dataset.tourCode);
            bookingUrl.searchParams.set('departureId', id);
            bookLink.href = available ? bookingUrl.pathname + bookingUrl.search : '#departures';
            bookLink.textContent = available ? 'Đặt tour' : 'Chọn lịch còn chỗ';
        }
        document.getElementById('departure-detail')?.scrollIntoView({behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth', block:'start'});
    }));

    const thumbs = [...document.querySelectorAll('[data-gallery-index]')];
    const mainImage = document.getElementById('tour-main-image');
    const dialog = document.getElementById('tour-gallery-dialog');
    const fullImage = document.getElementById('tour-gallery-image');
    let activeImage = 0;
    const showImage = index => {
        if (!thumbs.length) return;
        activeImage = (index + thumbs.length) % thumbs.length;
        const thumbnail = thumbs[activeImage].querySelector('img');
        mainImage.src = thumbnail.src; fullImage.src = thumbnail.src;
        thumbs.forEach((thumb, i) => { thumb.classList.toggle('active', i === activeImage); thumb.setAttribute('aria-pressed', String(i === activeImage)); });
        document.getElementById('gallery-counter').textContent = `${activeImage + 1} / ${thumbs.length}`;
    };
    thumbs.forEach((thumb, index) => thumb.addEventListener('click', () => showImage(index)));
    document.getElementById('gallery-open')?.addEventListener('click', () => dialog.showModal());
    document.querySelectorAll('[data-gallery-step]').forEach(button => button.addEventListener('click', () => showImage(activeImage + Number(button.dataset.galleryStep))));
    dialog?.addEventListener('keydown', event => {
        if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); showImage(activeImage + (event.key === 'ArrowRight' ? 1 : -1)); }
    });
    dialog?.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
})();
