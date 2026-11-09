(() => {
    'use strict';
    const form = document.getElementById('booking-order-form');
    if (!form) return;
    const list = document.getElementById('passenger-list');
    const submit = document.getElementById('submit-booking');
    const canBook = !submit.disabled;
    const slots = Number(form.dataset.slots);
    const departure = form.dataset.departureDate.split('-').map(Number);
    const counters = [...document.querySelectorAll('[data-counter]')];
    const money = value => new Intl.NumberFormat('vi-VN').format(value) + ' ₫';
    const cards = () => [...list.querySelectorAll('[data-passenger]')];
    const count = type => cards().filter(card => card.dataset.passenger === type).length;
    const error = message => { const box = document.getElementById('booking-client-error'); box.textContent = message; box.hidden = !message; };
    const validateBirth = card => {
        const input = card.querySelector('[data-field="DateOfBirth"]');
        input.setCustomValidity('');
        if (!input.value) return;
        const birth = input.value.split('-').map(Number);
        const age = departure[0] - birth[0] - (departure[1] < birth[1] || departure[1] === birth[1] && departure[2] < birth[2] ? 1 : 0);
        const type = card.dataset.passenger;
        if (age < 0 || age > 120 || type === 'ADULT' && age < 12 || type === 'CHILD' && (age < 2 || age >= 12) || type === 'INFANT' && age >= 2)
            input.setCustomValidity('Ngày sinh chưa phù hợp với loại vé. Tuổi được tính vào ngày khởi hành.');
    };
    const update = () => {
        const numbers = {};
        cards().forEach((card, index) => {
            const type = card.dataset.passenger;
            numbers[type] = (numbers[type] || 0) + 1;
            const counter = counters.find(c => c.dataset.counter === type);
            card.querySelector('[data-passenger-label]').textContent = `${counter?.dataset.label || 'Hành khách'} ${numbers[type]}`;
            card.querySelectorAll('input, select').forEach(input => {
                const field = input.dataset.field || input.name.split('.').at(-1);
                input.dataset.field = field; input.name = `Input.Passengers[${index}].${field}`;
                input.id = `booking-person-${index}-${field}`;
                input.closest('.formGroup')?.querySelector('label')?.setAttribute('for', input.id);
            });
            const remove = card.querySelector('[data-remove-passenger]');
            remove.type = 'button'; remove.disabled = type === 'ADULT' && count('ADULT') <= 1;
            remove.setAttribute('aria-label', `Xoá ${counter?.dataset.label || 'hành khách'} ${numbers[type]}`);
            validateBirth(card);
        });
        let total = 0;
        const seats = count('ADULT') + count('CHILD');
        counters.forEach(counter => {
            const type = counter.dataset.counter;
            const value = count(type);
            const price = counter.dataset.price === '' ? 0 : Number(counter.dataset.price);
            total += value * price;
            counter.querySelector('[data-count]').textContent = value;
            const minus = counter.querySelector('[data-remove-type]'); minus.type = 'button';
            const plus = counter.querySelector('[data-add-type]'); plus.type = 'button';
            minus.disabled = value === 0 || type === 'ADULT' && value <= 1;
            plus.disabled = !canBook || counter.dataset.price === '' || cards().length >= 50 || (type === 'INFANT' ? value >= count('ADULT') : seats >= slots);
            document.querySelector(`[data-summary-count="${type}"]`).textContent = value;
            document.querySelector(`[data-summary-price="${type}"]`).textContent = money(value * price);
        });
        document.getElementById('selected-seats').textContent = seats;
        document.getElementById('booking-total').textContent = money(total);
    };
    const removeCard = card => {
        if (!card || card.dataset.passenger === 'ADULT' && count('ADULT') <= 1) return;
        if (card.dataset.passenger === 'ADULT' && count('INFANT') > count('ADULT') - 1) { error('Vui lòng giảm số em bé trước. Mỗi người lớn chỉ đi kèm tối đa một em bé.'); return; }
        card.remove(); error(''); update();
    };
    form.addEventListener('click', event => {
        const add = event.target.closest('[data-add-type]');
        const minus = event.target.closest('[data-remove-type]');
        const remove = event.target.closest('[data-remove-passenger]');
        if (add) {
            event.preventDefault();
            if (add.disabled) return;
            const type = add.dataset.addType;
            const template = document.getElementById('passenger-template').content.cloneNode(true);
            const card = template.querySelector('[data-passenger]'); card.dataset.passenger = type;
            card.querySelector('[data-field="PassengerType"]').value = type;
            list.append(template); error(''); update();
        } else if (minus) { event.preventDefault(); removeCard(cards().filter(card => card.dataset.passenger === minus.dataset.removeType).at(-1)); }
        else if (remove) { event.preventDefault(); removeCard(remove.closest('[data-passenger]')); }
    });
    form.addEventListener('change', event => { if (event.target.matches('[data-field="DateOfBirth"]')) validateBirth(event.target.closest('[data-passenger]')); });
    form.addEventListener('submit', event => {
        cards().forEach(validateBirth);
        if (!form.checkValidity()) { event.preventDefault(); form.reportValidity(); return; }
        if (count('ADULT') < 1 || count('INFANT') > count('ADULT') || count('ADULT') + count('CHILD') > slots || cards().length > 50) {
            event.preventDefault(); error('Số lượng hành khách chưa hợp lệ hoặc vượt quá số chỗ còn lại.'); return;
        }
        submit.disabled = true; submit.textContent = 'Đang gửi yêu cầu…';
    });
    window.addEventListener('pageshow', () => { submit.disabled = !canBook; submit.textContent = 'Đặt tour và thanh toán'; });
    update();
})();
