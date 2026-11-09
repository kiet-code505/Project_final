// Trang danh sách địa điểm: tìm kiếm debounce, chọn nhiều, xoá, xuất CSV
(function () {
    const form = document.getElementById('filterForm');
    const search = document.getElementById('searchInput');
    const pageSize = document.getElementById('pageSizeSelect');

    // ── Tìm kiếm: tự submit sau 450ms ngừng gõ ──
    let timer;
    search?.addEventListener('input', () => {
        clearTimeout(timer);
        timer = setTimeout(() => form.submit(), 450);
    });
    // Giữ con trỏ ở cuối ô tìm kiếm sau khi reload
    if (search && search.value) { search.focus(); search.setSelectionRange(search.value.length, search.value.length); }
    pageSize?.addEventListener('change', () => form.submit());

    // ── Chọn nhiều ──
    const checkAll = document.getElementById('checkAll');
    const checks = () => [...document.querySelectorAll('.row-check')];
    const bulkBar = document.getElementById('bulkBar');
    const bulkCount = document.getElementById('bulkCount');

    function refreshBulk() {
        const sel = checks().filter(c => c.checked);
        sel.length ? bulkBar.removeAttribute('hidden') : bulkBar.setAttribute('hidden', '');
        bulkCount.textContent = sel.length;
        if (checkAll) {
            checkAll.checked = sel.length > 0 && sel.length === checks().length;
            checkAll.indeterminate = sel.length > 0 && sel.length < checks().length;
        }
        checks().forEach(c => c.closest('tr').classList.toggle('row-sel', c.checked));
    }
    checkAll?.addEventListener('change', () => { checks().forEach(c => c.checked = checkAll.checked); refreshBulk(); });
    document.addEventListener('change', e => { if (e.target.classList.contains('row-check')) refreshBulk(); });
    document.getElementById('bulkClear')?.addEventListener('click', () => { checks().forEach(c => c.checked = false); refreshBulk(); });

    // ── Modal xác nhận xoá ──
    const modal = document.getElementById('confirmModal');
    const idsBox = document.getElementById('deleteIds');
    const title = document.getElementById('confirmTitle');
    const message = document.getElementById('confirmMessage');

    function openDelete(items) {
        idsBox.innerHTML = items.map(i => `<input type="hidden" name="ids" value="${i.id}">`).join('');
        const inUse = items.filter(i => i.inUse > 0);
        if (items.length === 1) {
            title.textContent = UserI18n.t("Xoá \"{0}\"?" ,items[0].name);
            message.textContent = inUse.length
                ? UserI18n.t("Địa điểm này đang được {0} tour sử dụng nên hệ thống sẽ từ chối xoá. Hãy đổi điểm đi/đến của các tour trước." ,items[0].inUse)
                : UserI18n.t("Hành động này không thể hoàn tác.");
        } else {
            title.textContent = UserI18n.t("Xoá {0} địa điểm?" ,items.length);
            message.textContent = UserI18n.t("Hành động này không thể hoàn tác.") +
                (inUse.length ? ' ' + UserI18n.t("{0} địa điểm đang được tour sử dụng sẽ bị bỏ qua." ,inUse.length) : '');
        }
        modal.removeAttribute('hidden');
        modal.querySelector('[data-close]').focus();
    }
    const closeModal = () => modal.setAttribute('hidden', '');

    document.querySelectorAll('.js-delete-one').forEach(btn => btn.addEventListener('click', () =>
        openDelete([{ id: btn.dataset.id, name: btn.dataset.name, inUse: +btn.dataset.inuse }])));

    document.getElementById('bulkDelete')?.addEventListener('click', () => {
        const items = checks().filter(c => c.checked).map(c => {
            const b = c.closest('tr').querySelector('.js-delete-one');
            return { id: b.dataset.id, name: b.dataset.name, inUse: +b.dataset.inuse };
        });
        if (items.length) openDelete(items);
    });

    modal?.addEventListener('click', e => { if (e.target === modal || e.target.closest('[data-close]')) closeModal(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && modal && !modal.hidden) closeModal(); });
    document.getElementById('deleteForm')?.addEventListener('submit', () => {
        const b = document.getElementById('confirmDeleteBtn');
        b.disabled = true; b.textContent = UserI18n.t("Đang xử lý…");
    });

    // ── Xuất CSV (trang hiện tại) ──
    document.getElementById('exportCsv')?.addEventListener('click', () => {
        const rows = [...document.querySelectorAll('#locationsTable tbody tr')];
        if (!rows.length) { adminToast(UserI18n.t("Không có dữ liệu để xuất"), 'error'); return; }
        const data = [['ID', UserI18n.t("Tên"), 'Slug', UserI18n.t("Khu vực"), UserI18n.t("Mã sân bay"), UserI18n.t("Tên sân bay"), UserI18n.t("Tours đi"), UserI18n.t("Tours đến"), UserI18n.t("Mô tả")],
            ...rows.map(r => { const d = r.dataset; return [d.id, d.name, d.slug, d.region, d.airportCode, d.airportName, d.start, d.end, (d.desc || '').replace(/\n/g, ' ')]; })];
        const csv = data.map(r => r.map(c => `"${String(c ?? '').replace(/"/g, '""')}"`).join(',')).join('\n');
        const url = URL.createObjectURL(new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' }));
        const a = Object.assign(document.createElement('a'), { href: url, download: `locations-${new Date().toISOString().slice(0, 10)}.csv` });
        a.click(); URL.revokeObjectURL(url);
        adminToast(UserI18n.t("Đã xuất CSV"));
    });

    // ── Hiệu ứng đếm số ở stat cards ──
    document.querySelectorAll('.stat-value[data-count]').forEach(el => {
        const target = +el.dataset.count; if (!target) return;
        const t0 = performance.now(), dur = 700;
        const step = now => {
            const p = Math.min(1, (now - t0) / dur);
            el.textContent = Math.round(target * (1 - Math.pow(1 - p, 3))).toLocaleString(UserI18n.locale);
            if (p < 1) requestAnimationFrame(step);
        };
        requestAnimationFrame(step);
    });
})();
