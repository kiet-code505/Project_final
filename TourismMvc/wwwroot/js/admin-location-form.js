// Form tạo/sửa địa điểm: tự sinh slug, tự điền sân bay, xem trước ảnh, đếm ký tự
(function () {
    const $ = id => document.getElementById(id);
    const name = $('nameInput'), slug = $('slugInput');
    const code = $('airportCodeInput'), airport = $('airportNameInput');
    const desc = $('descInput'), count = $('descCount');
    const note = $('autofillNote');

    // Giống generateSlug() trong LocationManager.jsx
    const toSlug = s => s.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
        .replace(/đ/g, 'd').replace(/[^a-z0-9\s-]/g, '').trim()
        .replace(/\s+/g, '-').replace(/-+/g, '-');

    // Nếu slug đang khớp với tên (hoặc trống) thì tiếp tục tự sinh; người dùng sửa tay thì dừng
    let slugAuto = !slug.value || slug.value === toSlug(name.value);
    slug.addEventListener('input', () => { slugAuto = slug.value === '' ; });

    const norm = s => s.toLowerCase().trim().replace(/\s+/g, ' ')
        .replace('tp.', '').replace('thành phố', '').replace('tỉnh', '').trim();

    name.addEventListener('input', () => {
        if (slugAuto) slug.value = toSlug(name.value);
        const a = (window.AIRPORTS || {})[norm(name.value)];
        if (a && (!code.value || !airport.value)) {
            if (!code.value) code.value = a.code;
            if (!airport.value) airport.value = a.name;
            note.removeAttribute('hidden');
            setTimeout(() => note.setAttribute('hidden', ''), 2500);
        }
    });
    code.addEventListener('input', () => { code.value = code.value.toUpperCase(); });

    // Đếm ký tự mô tả
    const updateCount = () => {
        count.textContent = desc.value.length;
        count.parentElement.classList.toggle('warn', desc.value.length > 450);
    };
    desc.addEventListener('input', updateCount); updateCount();

    // Ảnh: xem trước + kéo thả
    const area = $('uploadArea'), input = $('imageInput'), preview = $('imagePreview');
    input.addEventListener('change', () => {
        const f = input.files[0];
        if (!f) return;
        if (!f.type.startsWith('image/')) { adminToast(UserI18n.t("Vui lòng chọn file ảnh"), 'error'); input.value = ''; return; }
        if (f.size > 5 * 1024 * 1024) { adminToast(UserI18n.t("Kích thước ảnh tối đa 5MB"), 'error'); input.value = ''; return; }
        const r = new FileReader();
        r.onload = () => { preview.src = r.result; preview.hidden = false; area.classList.add('has-image'); };
        r.readAsDataURL(f);
    });
    ['dragenter', 'dragover'].forEach(ev => area.addEventListener(ev, () => area.classList.add('dragging')));
    ['dragleave', 'drop'].forEach(ev => area.addEventListener(ev, () => area.classList.remove('dragging')));

    // Chống bấm submit 2 lần
    $('locationForm').addEventListener('submit', () => {
        const b = $('btnSubmit'); b.disabled = true; b.innerHTML = UserI18n.t("Đang lưu…");
    });
})();
