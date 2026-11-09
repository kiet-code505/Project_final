(() => {
    'use strict';
    window.lucide?.createIcons();
    const header = document.getElementById('user-header');
    const logo = document.getElementById('user-logo');
    const updateHeader = () => {
        const scrolled = document.body.dataset.solidHeader === 'true' || window.scrollY > 50;
        header?.classList.toggle('scrolled', scrolled);
        const source = `/images/home/future-logo-${scrolled ? 'dark' : 'light'}.svg`;
        if (logo && logo.getAttribute('src') !== source) logo.src = source;
    };
    window.addEventListener('scroll', updateHeader, { passive: true });
    updateHeader();
    document.querySelector('.menu-toggle')?.addEventListener('click', event => {
        const open = document.getElementById('user-navigation').classList.toggle('open');
        event.currentTarget.setAttribute('aria-expanded', String(open));
    });
    document.querySelectorAll('#user-navigation a').forEach(link => link.addEventListener('click', () => {
        document.getElementById('user-navigation').classList.remove('open');
        document.querySelector('.menu-toggle')?.setAttribute('aria-expanded', 'false');
    }));
    document.querySelectorAll('[data-carousel]').forEach(button => {
        const track = document.getElementById(button.dataset.carousel);
        if (!track) return;
        const update = () => {
            const end = track.scrollWidth - track.clientWidth;
            button.disabled = Number(button.dataset.direction) < 0 ? track.scrollLeft <= 1 : track.scrollLeft >= end - 1;
        };
        button.addEventListener('click', () => {
            const card = track.firstElementChild?.firstElementChild;
            track.scrollBy({ left: Number(button.dataset.direction) * ((card?.getBoundingClientRect().width || 280) + 20), behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' });
        });
        track.addEventListener('scroll', update, { passive: true });
        window.addEventListener('resize', update);
        update();
    });
    const tabs = [...document.querySelectorAll('[data-region]')];
    const selectRegion = tab => {
        tabs.forEach(item => {
            const active = item === tab;
            item.classList.toggle('activeTab', active);
            item.setAttribute('aria-selected', String(active));
            item.tabIndex = active ? 0 : -1;
            item.querySelector('.activeIndicator').hidden = !active;
            document.getElementById(`region-${item.dataset.region}`).hidden = !active;
        });
    };
    tabs.forEach((tab, index) => {
        tab.tabIndex = tab.getAttribute('aria-selected') === 'true' ? 0 : -1;
        tab.addEventListener('click', () => selectRegion(tab));
        tab.addEventListener('keydown', event => {
            if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
            event.preventDefault();
            const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
            selectRegion(tabs[next]); tabs[next].focus();
        });
    });
    document.querySelectorAll('[data-tour-dialog]').forEach(button => button.addEventListener('click', () => document.getElementById(button.dataset.tourDialog)?.showModal()));
    document.querySelectorAll('.home-tour-dialog').forEach(dialog => dialog.addEventListener('click', event => {
        const rect = dialog.getBoundingClientRect();
        if (event.target === dialog && (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom)) dialog.close();
    }));
    document.querySelectorAll('.user-home img').forEach(img => img.addEventListener('error', () => {
        if (!img.dataset.fallback) { img.dataset.fallback = 'true'; img.src = '/images/home/ryan-le-8-hEg1OuFn8-unsplash.jpg'; }
    }));
    const photos = [...document.querySelectorAll('#travel-gallery .coverCard')];
    let active = 0;
    const showPhoto = index => {
        active = index;
        photos.forEach((photo, i) => {
            let offset = (i - active + photos.length) % photos.length;
            if (offset > Math.floor(photos.length / 2)) offset -= photos.length;
            photo.style.transform = `translate3d(${offset * 52}%,0,0) rotateY(${offset * -22}deg) scale(${offset === 0 ? 1 : .72})`;
            photo.style.zIndex = String(10 - Math.abs(offset));
            photo.style.filter = `brightness(${offset === 0 ? .7 : .3})`;
            photo.classList.toggle('coverCardActive', offset === 0);
        });
    };
    photos.forEach((photo, i) => photo.addEventListener('click', () => showPhoto(i)));
    if (photos.length) {
        showPhoto(0);
        if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches)
            window.setInterval(() => { if (!document.hidden && !document.getElementById('travel-gallery').matches(':hover, :focus-within')) showPhoto((active + 1) % photos.length); }, 3000);
    }
})();
