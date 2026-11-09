// Dùng chung cho mọi trang admin
(function () {
    if (window.lucide) lucide.createIcons();

    // Toast tự ẩn
    document.querySelectorAll('#toastStack .toast').forEach((t, i) => {
        setTimeout(() => { t.classList.add('hide'); setTimeout(() => t.remove(), 400); }, 4500 + i * 600);
        t.addEventListener('click', () => t.remove());
    });

    // Sidebar mobile
    document.getElementById('sidebarToggle')?.addEventListener('click', () => document.body.classList.toggle('sidebar-open'));

    // Toast phía client
    window.adminToast = (msg, type = 'success') => {
        const el = document.createElement('div');
        el.className = `toast toast-${type}`;
        el.innerHTML = `<i data-lucide="${type === 'success' ? 'check-circle-2' : 'alert-triangle'}"></i><span></span>`;
        el.querySelector('span').textContent = msg;
        document.getElementById('toastStack').appendChild(el);
        lucide.createIcons();
        setTimeout(() => { el.classList.add('hide'); setTimeout(() => el.remove(), 400); }, 3500);
    };
})();
