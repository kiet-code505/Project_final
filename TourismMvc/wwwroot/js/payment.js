(() => {
    const tr = (...args) => window.UserI18n?.t(...args) ?? args[0];
    const receipt = document.querySelector('[data-booking-code]');
    if (!receipt) return;
    const countdown = document.querySelector('[data-payment-seconds]');
    const until = Date.now() + Number(countdown?.dataset.paymentSeconds || 0) * 1000;
    const tick = () => {
        if (!countdown) return;
        const left = Math.max(0, Math.ceil((until - Date.now()) / 1000));
        const label = document.getElementById('payment-countdown');
        if (label) label.textContent = `${String(Math.floor(left / 60)).padStart(2, '0')}:${String(left % 60).padStart(2, '0')}`;
        if (left <= 60) {
            const button = document.getElementById('pay-vnpay');
            if (button) { button.disabled = true; button.textContent = tr('Hết thời gian bắt đầu thanh toán'); }
        }
    };
    tick(); if (countdown) setInterval(tick, 1000);
    document.querySelectorAll('#vnpay-payment-form,.payment-refresh-form').forEach(form => form.addEventListener('submit', () => {
        const button = form.querySelector('button'); if (button) { button.disabled = true; button.textContent = tr('Đang xử lý…'); }
    }));
    window.addEventListener('pageshow', e => { if (e.persisted) location.reload(); });
    if (receipt.dataset.bookingStatus === 'PENDING_PAYMENT' || receipt.dataset.bookingStatus === 'EXPIRED') {
        setInterval(async () => {
            if (document.hidden) return;
            try {
                const response = await fetch(`/booking/complete/${encodeURIComponent(receipt.dataset.bookingCode)}/status`, { cache: 'no-store' });
                if (!response.ok) return;
                const data = await response.json();
                if (data.status !== receipt.dataset.bookingStatus || String(data.needsReview) !== receipt.dataset.needsReview) location.reload();
            } catch { /* Payment can also be checked using the form. */ }
        }, 10000);
    }
})();
