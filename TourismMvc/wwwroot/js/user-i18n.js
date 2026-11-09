(() => {
    const english = JSON.parse(document.getElementById('user-translations')?.textContent || '{}');
    const language = document.documentElement.lang === 'vi' ? 'vi' : 'en';
    window.UserI18n = Object.freeze({
        language,
        locale: language === 'vi' ? 'vi-VN' : 'en-GB',
        t(source, ...args) {
            const key = source.trim().replace(/\s+/g, ' ');
            const text = language === 'en' ? english[key] || source : source;
            return text.replace(/\{(\d+)\}/g, (_, index) => String(args[Number(index)] ?? ''));
        }
    });
    document.querySelectorAll('.user-language-selector').forEach(form => form.addEventListener('submit', () => {
        form.elements.returnUrl.value = location.pathname + location.search + location.hash;
    }));
})();
