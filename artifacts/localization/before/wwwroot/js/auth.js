(() => {
    'use strict';
    document.querySelectorAll('[data-toggle-password]').forEach(button => button.addEventListener('click', () => {
        const input = document.getElementById(button.dataset.togglePassword);
        const visible = input.type === 'password';
        input.type = visible ? 'text' : 'password';
        button.setAttribute('aria-pressed', String(visible));
        button.setAttribute('aria-label', visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
    }));
    document.querySelectorAll('.auth-form').forEach(form => form.addEventListener('submit', () => {
        const button = form.querySelector('button[type="submit"]');
        if (button) { button.disabled = true; button.textContent = 'Đang xử lý…'; }
    }));
    document.querySelectorAll('.account-menu').forEach(menu => {
        document.addEventListener('click', event => { if (!menu.contains(event.target)) menu.open = false; });
        menu.addEventListener('keydown', event => { if (event.key === 'Escape') { menu.open = false; menu.querySelector('summary').focus(); } });
    });
})();
