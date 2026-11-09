(() => {
    'use strict';
    const tr = (...args) => window.UserI18n?.t(...args) ?? args[0];
    document.querySelectorAll('[data-toggle-password]').forEach(button => button.addEventListener('click', () => {
        const input = document.getElementById(button.dataset.togglePassword);
        const visible = input.type === 'password';
        input.type = visible ? 'text' : 'password';
        button.setAttribute('aria-pressed', String(visible));
        button.setAttribute('aria-label', visible ? tr('Ẩn mật khẩu') : tr('Hiện mật khẩu'));
    }));
    document.querySelectorAll('.auth-form').forEach(form => form.addEventListener('submit', () => {
        const button = form.querySelector('button[type="submit"]');
        if (button) { button.disabled = true; button.textContent = tr('Đang xử lý…'); }
    }));
    document.querySelectorAll('.account-menu').forEach(menu => {
        document.addEventListener('click', event => { if (!menu.contains(event.target)) menu.open = false; });
        menu.addEventListener('keydown', event => { if (event.key === 'Escape') { menu.open = false; menu.querySelector('summary').focus(); } });
    });
})();
