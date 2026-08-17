document.addEventListener('click', event => {
    const button = event.target.closest('[data-password-toggle]');
    if (!button) return;

    const field = button.closest('.password-field');
    const input = field?.querySelector('input');
    if (!input) return;

    const showing = input.type === 'text';
    input.type = showing ? 'password' : 'text';
    button.classList.toggle('showing', !showing);
    button.setAttribute('aria-label', showing ? 'Show password' : 'Hide password');
    button.setAttribute('title', showing ? 'Show password' : 'Hide password');
});

window.employeeImageProcessing = {
    cropAndClean(source, type, zoom, panX, panY) {
        return new Promise((resolve, reject) => {
            const image = new Image();
            image.onload = () => {
                const aspect = type === 'signature' ? 3 : 1;
                let sourceWidth = image.width;
                let sourceHeight = sourceWidth / aspect;
                if (sourceHeight > image.height) { sourceHeight = image.height; sourceWidth = sourceHeight * aspect; }
                sourceWidth /= zoom; sourceHeight /= zoom;
                const sx = (image.width - sourceWidth) / 2 + ((image.width - sourceWidth) / 2) * (panX / 100);
                const sy = (image.height - sourceHeight) / 2 + ((image.height - sourceHeight) / 2) * (panY / 100);
                const canvas = document.createElement('canvas');
                canvas.width = type === 'signature' ? 900 : 600;
                canvas.height = Math.round(canvas.width / aspect);
                const context = canvas.getContext('2d');
                context.drawImage(image, sx, sy, sourceWidth, sourceHeight, 0, 0, canvas.width, canvas.height);
                if (type === 'signature') {
                    const frame = context.getImageData(0, 0, canvas.width, canvas.height);
                    for (let i = 0; i < frame.data.length; i += 4) {
                        const gray = frame.data[i] * .299 + frame.data[i + 1] * .587 + frame.data[i + 2] * .114;
                        const ink = Math.max(0, Math.min(255, (185 - gray) * 3.6));
                        const value = 255 - ink;
                        frame.data[i] = value; frame.data[i + 1] = value; frame.data[i + 2] = value; frame.data[i + 3] = 255;
                    }
                    context.putImageData(frame, 0, 0);
                }
                resolve(canvas.toDataURL('image/webp', .95));
            };
            image.onerror = () => reject(new Error('Could not process image.'));
            image.src = source;
        });
    }
};

window.employeeImageProcessing.cropFromFrame = function (source, type) {
    return new Promise((resolve, reject) => {
        const workspace = document.getElementById('crop-workspace');
        const displayedImage = workspace?.querySelector('img');
        const frame = workspace?.querySelector('[data-crop-frame]');
        if (!workspace || !displayedImage || !frame) {
            reject(new Error('Cropper is not ready.'));
            return;
        }
        const image = new Image();
        image.onload = () => {
            const imageRect = displayedImage.getBoundingClientRect();
            const frameRect = frame.getBoundingClientRect();
            if (!imageRect.width || !imageRect.height) {
                reject(new Error('Image could not be positioned for cropping.'));
                return;
            }
            let sx = ((frameRect.left - imageRect.left) / imageRect.width) * image.naturalWidth;
            let sy = ((frameRect.top - imageRect.top) / imageRect.height) * image.naturalHeight;
            let sw = (frameRect.width / imageRect.width) * image.naturalWidth;
            let sh = (frameRect.height / imageRect.height) * image.naturalHeight;
            sx = Math.max(0, Math.min(image.naturalWidth - 1, sx));
            sy = Math.max(0, Math.min(image.naturalHeight - 1, sy));
            sw = Math.max(1, Math.min(image.naturalWidth - sx, sw));
            sh = Math.max(1, Math.min(image.naturalHeight - sy, sh));
            const canvas = document.createElement('canvas');
            canvas.width = type === 'signature' ? 900 : 600;
            canvas.height = type === 'signature' ? 300 : 750;
            const context = canvas.getContext('2d');
            context.drawImage(image, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);
            if (type === 'signature') {
                const pixels = context.getImageData(0, 0, canvas.width, canvas.height);
                for (let i = 0; i < pixels.data.length; i += 4) {
                    const gray = pixels.data[i] * .299 + pixels.data[i + 1] * .587 + pixels.data[i + 2] * .114;
                    const ink = Math.max(0, Math.min(255, (185 - gray) * 3.6));
                    const value = 255 - ink;
                    pixels.data[i] = value; pixels.data[i + 1] = value; pixels.data[i + 2] = value; pixels.data[i + 3] = 255;
                }
                context.putImageData(pixels, 0, 0);
            }
            resolve(canvas.toDataURL('image/webp', .95));
        };
        image.onerror = () => reject(new Error('Could not process image.'));
        image.src = source;
    });
};

(() => {
    let drag;
    document.addEventListener('pointerdown', event => {
        const frame = event.target.closest('[data-crop-frame]');
        if (!frame) return;
        const workspace = frame.closest('.sds-crop-workspace');
        const frameRect = frame.getBoundingClientRect();
        drag = { frame, workspace, offsetX: event.clientX - (frameRect.left + frameRect.width / 2), offsetY: event.clientY - (frameRect.top + frameRect.height / 2) };
        frame.setPointerCapture?.(event.pointerId);
        event.preventDefault();
    });
    document.addEventListener('pointermove', event => {
        if (!drag) return;
        const workspaceRect = drag.workspace.getBoundingClientRect();
        const frameRect = drag.frame.getBoundingClientRect();
        const x = Math.max(frameRect.width / 2, Math.min(workspaceRect.width - frameRect.width / 2, event.clientX - workspaceRect.left - drag.offsetX));
        const y = Math.max(frameRect.height / 2, Math.min(workspaceRect.height - frameRect.height / 2, event.clientY - workspaceRect.top - drag.offsetY));
        drag.frame.style.left = `${x}px`;
        drag.frame.style.top = `${y}px`;
        drag.frame.style.transform = 'translate(-50%, -50%)';
    });
    document.addEventListener('pointerup', () => { drag = undefined; });
    document.addEventListener('pointercancel', () => { drag = undefined; });
})();

const newPassword = document.querySelector('[data-new-password]');
const confirmPassword = document.querySelector('[data-confirm-password]');
const passwordMessage = document.querySelector('[data-password-message]');

const validatePasswordMatch = () => {
    if (!newPassword || !confirmPassword || !passwordMessage) return;
    newPassword.classList.remove('password-match', 'password-mismatch');
    confirmPassword.classList.remove('password-match', 'password-mismatch');

    if (!newPassword.value && !confirmPassword.value) {
        passwordMessage.textContent = '';
        passwordMessage.className = 'password-match-message';
        return;
    }

    const matches = newPassword.value === confirmPassword.value && confirmPassword.value.length > 0;
    const state = matches ? 'password-match' : 'password-mismatch';
    newPassword.classList.add(state);
    confirmPassword.classList.add(state);
    passwordMessage.textContent = matches ? 'Passwords match.' : 'Passwords do not match.';
    passwordMessage.className = `password-match-message ${state}`;
};

newPassword?.addEventListener('input', validatePasswordMatch);
confirmPassword?.addEventListener('input', validatePasswordMatch);
validatePasswordMatch();
