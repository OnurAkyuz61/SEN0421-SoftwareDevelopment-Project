// ServiceDesk — giriş ve OTP ekranlarının betikleri (_AuthLayout)
// jQuery gerektirmez. Sunucu tarafı doğrulama her zaman çalışır; buradaki
// kontroller yalnızca kullanıcıya anında geri bildirim vermek içindir.
(function () {
    'use strict';
    document.documentElement.classList.add('js');

    const $ = (sel, root = document) => root.querySelector(sel);
    const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

    // ───── Şifreyi göster / gizle ─────────────────────────────────────────
    $$('[data-reveal]').forEach(btn => {
        const input = $(btn.dataset.reveal);
        if (!input) return;
        btn.addEventListener('click', () => {
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.querySelector('i').className = show ? 'bi bi-eye-slash' : 'bi bi-eye';
            btn.setAttribute('aria-label', show ? 'Şifreyi gizle' : 'Şifreyi göster');
            btn.title = btn.getAttribute('aria-label');
            input.focus();
        });
    });

    // ───── Caps Lock uyarısı ──────────────────────────────────────────────
    $$('[data-caps-target]').forEach(input => {
        const hint = $(input.dataset.capsTarget);
        if (!hint) return;
        const check = e => {
            if (typeof e.getModifierState === 'function')
                hint.classList.toggle('show', e.getModifierState('CapsLock'));
        };
        input.addEventListener('keydown', check);
        input.addEventListener('keyup', check);
        input.addEventListener('blur', () => hint.classList.remove('show'));
    });

    // ───── Form doğrulama + gönderim durumu ───────────────────────────────
    // Model'deki [Required], [EmailAddress], [StringLength], [RegularExpression]
    // nitelikleri Razor tarafından data-val-* olarak yazılır; burada okunur.
    const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    function validateInput(input) {
        const d = input.dataset;
        const v = input.value.trim();
        if (d.val !== 'true') return null;
        if (d.valRequired !== undefined && !v) return d.valRequired;
        if (!v) return null;
        if (d.valEmail !== undefined && !EMAIL_RE.test(v)) return d.valEmail;
        if (d.valLengthMin && v.length < +d.valLengthMin) return d.valLength;
        if (d.valLengthMax && v.length > +d.valLengthMax) return d.valLength;
        if (d.valRegexPattern && !new RegExp('^(?:' + d.valRegexPattern + ')$').test(v)) return d.valRegex;
        return null;
    }

    function showError(input, msg) {
        const span = $(`[data-valmsg-for="${input.name}"]`);
        const box = input.closest('.auth-input');
        if (span) span.textContent = msg || '';
        if (box) box.classList.toggle('invalid', !!msg);
        input.setAttribute('aria-invalid', msg ? 'true' : 'false');
    }

    function setLoading(form, on) {
        $$('.auth-btn', form).forEach(b => {
            b.classList.toggle('loading', on);
            b.toggleAttribute('aria-busy', on);
        });
    }

    $$('form[data-auth-form]').forEach(form => {
        const fields = $$('input[data-val="true"]', form);

        fields.forEach(input => {
            input.addEventListener('input', () => {
                if (input.getAttribute('aria-invalid') === 'true') showError(input, validateInput(input));
            });
            input.addEventListener('blur', () => {
                if (input.value) showError(input, validateInput(input));
            });
        });

        form.addEventListener('submit', e => {
            if (form.dataset.submitting === '1') { e.preventDefault(); return; }

            let firstBad = null;
            fields.forEach(input => {
                if (input.offsetParent === null) return; // görünmeyen alan (OTP tek kutu)
                const msg = validateInput(input);
                showError(input, msg);
                if (msg && !firstBad) firstBad = input;
            });
            if (firstBad) { e.preventDefault(); firstBad.focus(); return; }
            if (e.defaultPrevented) return;

            form.dataset.submitting = '1';
            setLoading(form, true);
        });
    });

    // Tarayıcı "geri" ile sayfayı önbellekten açarsa butonlar takılı kalmasın
    window.addEventListener('pageshow', e => {
        if (!e.persisted) return;
        $$('form[data-auth-form]').forEach(f => { f.dataset.submitting = ''; setLoading(f, false); });
    });

    // ───── 6 haneli OTP kutuları ──────────────────────────────────────────
    const otpForm = $('#otpForm');
    const boxWrap = $('#otpBoxes');
    if (otpForm && boxWrap) {
        const boxes = $$('.otp-digit', boxWrap);
        const hidden = $('#Code', otpForm);
        const submitBtn = $('.auth-btn', otpForm);

        const code = () => boxes.map(b => b.value).join('');
        const sync = () => {
            hidden.value = code();
            boxes.forEach(b => b.classList.toggle('filled', !!b.value));
        };
        const clearError = () => {
            boxWrap.classList.remove('invalid', 'shake');
            const alert = $('.auth-alert.error', otpForm);
            if (alert) alert.remove();
        };
        const trySubmit = () => {
            if (code().length !== boxes.length || submitBtn.disabled) return;
            boxWrap.classList.add('success');
            if (otpForm.requestSubmit) otpForm.requestSubmit(); else otpForm.submit();
        };

        // Rakamları verilen kutudan itibaren dağıtır (yapıştırma, otomatik doldurma)
        const spread = (digits, from) => {
            digits = digits.replace(/\D/g, '').slice(0, boxes.length - from);
            [...digits].forEach((ch, i) => { boxes[from + i].value = ch; });
            sync();
            const next = Math.min(from + digits.length, boxes.length - 1);
            boxes[next].focus();
            trySubmit();
        };

        boxes.forEach((box, idx) => {
            box.addEventListener('focus', () => box.select());

            box.addEventListener('input', () => {
                clearError();
                const v = box.value.replace(/\D/g, '');
                if (v.length > 1) { box.value = ''; spread(v, idx); return; } // iOS SMS otomatik doldurma
                box.value = v;
                sync();
                if (v && idx < boxes.length - 1) boxes[idx + 1].focus();
                trySubmit();
            });

            box.addEventListener('keydown', e => {
                if (e.key === 'Backspace' && !box.value && idx > 0) {
                    boxes[idx - 1].value = '';
                    boxes[idx - 1].focus();
                    sync();
                    e.preventDefault();
                } else if (e.key === 'ArrowLeft' && idx > 0) {
                    boxes[idx - 1].focus(); e.preventDefault();
                } else if (e.key === 'ArrowRight' && idx < boxes.length - 1) {
                    boxes[idx + 1].focus(); e.preventDefault();
                } else if (e.key.length === 1 && !/\d/.test(e.key) && !e.ctrlKey && !e.metaKey) {
                    e.preventDefault(); // rakam dışı tuşlar
                }
            });

            box.addEventListener('paste', e => {
                e.preventDefault();
                clearError();
                spread((e.clipboardData || window.clipboardData).getData('text'), idx);
            });
        });

        // Eksik kodla gönderilmesin
        otpForm.addEventListener('submit', e => {
            sync();
            if (code().length !== boxes.length) {
                e.preventDefault();
                e.stopImmediatePropagation();
                boxWrap.classList.remove('shake');
                void boxWrap.offsetWidth; // animasyonu yeniden başlat
                boxWrap.classList.add('invalid', 'shake');
                (boxes.find(b => !b.value) || boxes[0]).focus();
            }
        }, true);

        boxes[0].focus();

        // Geliştirme otomatik doldurması ve testler için
        window.SdOtp = {
            fill(digits) {
                let i = 0;
                const step = () => {
                    if (i >= digits.length) return;
                    boxes[i].value = digits[i];
                    boxes[i].dispatchEvent(new Event('input', { bubbles: true }));
                    i++;
                    setTimeout(step, 90);
                };
                step();
            }
        };
    }

    // ───── Kod süresi geri sayımı ─────────────────────────────────────────
    const timer = $('#otpTimer');
    if (timer) {
        const total = +timer.dataset.lifetime || 300;
        const end = Date.now() + (+timer.dataset.secondsLeft) * 1000;
        const text = $('[data-timer-text]', timer);
        const ring = $('.ring .fg', timer);
        const C = 50.27; // 2πr, r = 8
        const submitBtn = $('#otpForm .auth-btn');

        const render = () => {
            const left = Math.max(0, Math.round((end - Date.now()) / 1000));
            ring.style.strokeDashoffset = String(C * (1 - left / total));
            timer.classList.toggle('low', left > 0 && left <= 60);
            timer.classList.toggle('over', left === 0);
            if (left === 0) {
                text.textContent = 'Kodun süresi doldu';
                if (submitBtn) submitBtn.disabled = true;
                return false;
            }
            text.innerHTML = `Kalan süre <b>${Math.floor(left / 60)}:${String(left % 60).padStart(2, '0')}</b>`;
            return true;
        };
        if (render()) {
            const id = setInterval(() => { if (!render()) clearInterval(id); }, 1000);
        }
    }

    // ───── "Tekrar gönder" bekleme süresi ─────────────────────────────────
    const resend = $('#resendBtn');
    if (resend) {
        let left = +resend.dataset.cooldown || 0;
        const render = () => {
            if (left > 0) {
                resend.disabled = true;
                resend.textContent = `Tekrar gönder (${left} sn)`;
            } else {
                resend.disabled = false;
                resend.textContent = 'Kodu tekrar gönder';
            }
        };
        render();
        if (left > 0) {
            const id = setInterval(() => { left--; render(); if (left <= 0) clearInterval(id); }, 1000);
        }
        resend.form.addEventListener('submit', () => {
            resend.disabled = true;
            resend.textContent = 'Gönderiliyor…';
        });
    }
})();
