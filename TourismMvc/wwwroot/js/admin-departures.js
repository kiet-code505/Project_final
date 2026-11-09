(() => {
    'use strict';
    document.querySelectorAll('[data-expand-departures]').forEach(button => button.addEventListener('click', () => {
        document.querySelectorAll('.departure-group').forEach(group => { group.open = button.dataset.expandDepartures === 'true'; });
    }));
    const selected = [...document.querySelectorAll('[data-departure-id]')];
    const all = document.querySelector('[data-select-departures]');
    const updateSelection = () => {
        const count = selected.filter(input => input.checked).length;
        const label = document.querySelector('[data-selected-count]');
        if (label) label.textContent = UserI18n.t("Đã chọn {0} lịch" ,count);
        const button = document.querySelector('[data-delete-selected]');
        if (button) button.disabled = count === 0;
        if (all) { all.checked = count > 0 && count === selected.length; all.indeterminate = count > 0 && count < selected.length; }
    };
    all?.addEventListener('change', () => { selected.forEach(input => { input.checked = all.checked; }); updateSelection(); });
    selected.forEach(input => input.addEventListener('change', updateSelection));
    updateSelection();

    const dialog = document.querySelector('#departure-delete-dialog');
    let pending = null;
    document.querySelectorAll('[data-departure-delete]').forEach(form => form.addEventListener('submit', event => {
        const count = form.id === 'departure-bulk-delete' ? selected.filter(input => input.checked).length : 1;
        if (!count) { event.preventDefault(); return; }
        if (!dialog?.showModal) { if (!window.confirm(UserI18n.t("Xóa {0} lịch khởi hành?" ,count))) event.preventDefault(); return; }
        event.preventDefault(); pending = form;
        dialog.querySelector('[data-delete-description]').textContent = UserI18n.t("Bạn sắp xóa {0} lịch cùng bảng giá và thông tin vận chuyển. Thao tác này không thể hoàn tác." ,count);
        dialog.returnValue = ''; dialog.showModal();
    }));
    dialog?.addEventListener('close', () => {
        if (dialog.returnValue === 'confirm' && pending) { const form = pending; pending = null; HTMLFormElement.prototype.submit.call(form); }
        else pending = null;
    });

    document.querySelectorAll('[data-transport-toggle]').forEach(toggle => {
        const fields = toggle.closest('.departure-transport-editor').querySelector('.departure-transport-fields');
        const sync = () => { fields.hidden = !toggle.checked; };
        toggle.addEventListener('change', sync); sync();
    });
    document.querySelector('[data-date-range]')?.addEventListener('change', event => {
        const value = event.target.value; if (!value) return;
        const form = event.target.form, today = new Date(); today.setHours(0, 0, 0, 0);
        let start = new Date(today), end = new Date(today);
        if (value === 'week') { start.setDate(today.getDate() - today.getDay()); end = new Date(start); end.setDate(start.getDate() + 6); }
        if (value === 'month') { start = new Date(today.getFullYear(), today.getMonth(), 1); end = new Date(today.getFullYear(), today.getMonth() + 1, 0); }
        if (value === 'next-month') { start = new Date(today.getFullYear(), today.getMonth() + 1, 1); end = new Date(today.getFullYear(), today.getMonth() + 2, 0); }
        const format = date => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
        form.elements.startDate.value = value === 'all' ? '' : format(start);
        form.elements.endDate.value = value === 'all' ? '' : format(end);
        form.requestSubmit();
    });
})();
