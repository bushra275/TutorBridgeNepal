// A reusable, on-brand confirmation dialog to replace the browser's native
// confirm() everywhere in the app.
//
// Two ways to use it:
//
// A) Zero-JS, on any <form>:
//    <form ... data-tb-confirm="Suspend this account?">
//    (optional: data-tb-confirm-title="...", data-tb-confirm-label="Suspend",
//     data-tb-confirm-danger="true" for the red/danger styling)
//
// B) Programmatically, anywhere in a script block:
//    const ok = await tbConfirm({ title: "...", message: "...", danger: true });
//    if (ok) { ... }
(function () {
    function ensureModal() {
        if (document.getElementById('tbConfirmOverlay')) return;

        var overlay = document.createElement('div');
        overlay.className = 'tb-confirm-overlay';
        overlay.id = 'tbConfirmOverlay';
        overlay.innerHTML =
            '<div class="tb-confirm-box">' +
            '<div class="tb-confirm-icon" id="tbConfirmIcon">!</div>' +
            '<h3 class="tb-confirm-title" id="tbConfirmTitle"></h3>' +
            '<p class="tb-confirm-message" id="tbConfirmMessage"></p>' +
            '<div class="tb-confirm-actions">' +
            '<button type="button" class="tb-btn-outline" id="tbConfirmCancel"></button>' +
            '<button type="button" class="tb-btn" id="tbConfirmOk"></button>' +
            '</div>' +
            '</div>';
        document.body.appendChild(overlay);
    }

    window.tbConfirm = function (options) {
        ensureModal();

        var opts = Object.assign({
            title: 'Are you sure?',
            message: '',
            confirmLabel: 'Confirm',
            cancelLabel: 'Cancel',
            danger: false
        }, options || {});

        var overlay = document.getElementById('tbConfirmOverlay');
        var box = overlay.querySelector('.tb-confirm-box');
        var icon = document.getElementById('tbConfirmIcon');
        var titleEl = document.getElementById('tbConfirmTitle');
        var msgEl = document.getElementById('tbConfirmMessage');
        var okBtn = document.getElementById('tbConfirmOk');
        var cancelBtn = document.getElementById('tbConfirmCancel');

        titleEl.textContent = opts.title;
        msgEl.textContent = opts.message;
        okBtn.textContent = opts.confirmLabel;
        cancelBtn.textContent = opts.cancelLabel;
        icon.textContent = opts.danger ? '!' : '?';
        box.classList.toggle('tb-confirm-danger', !!opts.danger);
        okBtn.classList.toggle('tb-btn-danger', !!opts.danger);

        overlay.classList.add('open');
        document.body.style.overflow = 'hidden';

        return new Promise(function (resolve) {
            function cleanup(result) {
                overlay.classList.remove('open');
                document.body.style.overflow = '';
                okBtn.removeEventListener('click', onOk);
                cancelBtn.removeEventListener('click', onCancel);
                overlay.removeEventListener('click', onOverlay);
                document.removeEventListener('keydown', onKey);
                resolve(result);
            }
            function onOk() { cleanup(true); }
            function onCancel() { cleanup(false); }
            function onOverlay(e) { if (e.target === overlay) cleanup(false); }
            function onKey(e) { if (e.key === 'Escape') cleanup(false); }

            okBtn.addEventListener('click', onOk);
            cancelBtn.addEventListener('click', onCancel);
            overlay.addEventListener('click', onOverlay);
            document.addEventListener('keydown', onKey);
            okBtn.focus();
        });
    };

    // Auto-wire any form carrying data-tb-confirm - most call sites in the
    // app just need this attribute, no JS of their own.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (!form.hasAttribute('data-tb-confirm') || form.dataset.tbConfirmed === 'true') return;

        e.preventDefault();
        tbConfirm({
            message: form.dataset.tbConfirm,
            title: form.dataset.tbConfirmTitle || 'Are you sure?',
            confirmLabel: form.dataset.tbConfirmLabel || 'Confirm',
            danger: form.dataset.tbConfirmDanger === 'true'
        }).then(function (ok) {
            if (ok) {
                form.dataset.tbConfirmed = 'true';
                form.submit();
            }
        });
    }, true);
})();