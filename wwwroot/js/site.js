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

window.sdsCropper = {
    canvas: null,
    ctx: null,
    img: null,
    target: 'photo', // 'photo' | 'signature'
    baseScale: 1,
    scale: 1,
    panX: 0,
    panY: 0,
    frameW: 300,
    frameH: 375,
    isDragging: false,
    dragStartX: 0,
    dragStartY: 0,
    startPanX: 0,
    startPanY: 0,

    init(canvasId, source, target) {
        this.canvas = typeof canvasId === 'string' ? document.getElementById(canvasId) : canvasId;
        if (!this.canvas) return;
        this.ctx = this.canvas.getContext('2d');
        this.target = target || 'photo';

        const parent = this.canvas.parentElement;
        const cw = parent.clientWidth || 560;
        const ch = parent.clientHeight || 450;
        this.canvas.width = cw;
        this.canvas.height = ch;

        // Static frame sizes from D:\SDS-PAYROLL
        if (this.target === 'signature') {
            this.frameW = Math.min(cw - 40, 450);
            this.frameH = 150;
        } else {
            this.frameW = 300;
            this.frameH = 375;
        }

        this.scale = 1;
        this.panX = 0;
        this.panY = 0;
        this.isDragging = false;

        this.img = new Image();
        this.img.onload = () => {
            // Calculate base scale to fill the frame
            const scaleX = this.frameW / this.img.width;
            const scaleY = this.frameH / this.img.height;
            this.baseScale = Math.max(scaleX, scaleY);
            this.scale = 1;
            this.panX = 0;
            this.panY = 0;

            this.initEvents();
            this.render();
            this.updateZoomLabel();
        };
        this.img.src = source;
    },

    initEvents() {
        if (this._eventsInit) return;
        this._eventsInit = true;

        const c = this.canvas;
        c.addEventListener('pointerdown', e => {
            this.isDragging = true;
            this.dragStartX = e.clientX;
            this.dragStartY = e.clientY;
            this.startPanX = this.panX;
            this.startPanY = this.panY;
            c.setPointerCapture?.(e.pointerId);
            c.style.cursor = 'grabbing';
            e.preventDefault();
        });

        window.addEventListener('pointermove', e => {
            if (!this.isDragging) return;
            const dx = e.clientX - this.dragStartX;
            const dy = e.clientY - this.dragStartY;
            this.panX = this.startPanX + dx;
            this.panY = this.startPanY + dy;
            this.render();
        });

        window.addEventListener('pointerup', () => {
            this.isDragging = false;
            if (this.canvas) this.canvas.style.cursor = 'grab';
        });

        window.addEventListener('pointercancel', () => {
            this.isDragging = false;
            if (this.canvas) this.canvas.style.cursor = 'grab';
        });

        c.addEventListener('wheel', e => {
            e.preventDefault();
            const delta = e.deltaY < 0 ? 0.1 : -0.1;
            this.scale = Math.max(0.1, Math.min(5.0, this.scale + delta));
            this.render();
            this.updateZoomLabel();
        }, { passive: false });
    },

    zoomIn() {
        this.scale = Math.min(5.0, this.scale + 0.1);
        this.render();
        this.updateZoomLabel();
    },

    zoomOut() {
        this.scale = Math.max(0.1, this.scale - 0.1);
        this.render();
        this.updateZoomLabel();
    },

    updateZoomLabel() {
        const label = document.getElementById('sds-zoom-label');
        if (label) label.textContent = `${(this.scale * 100).toFixed(0)}%`;
    },

    render() {
        if (!this.ctx || !this.img || !this.img.complete) return;
        const ctx = this.ctx;
        const cw = this.canvas.width;
        const ch = this.canvas.height;
        const fw = this.frameW;
        const fh = this.frameH;
        const fx = (cw - fw) / 2;
        const fy = (ch - fh) / 2;

        ctx.clearRect(0, 0, cw, ch);

        // 1. Draw Image centered at frame + pan
        const drawScale = this.baseScale * this.scale;
        const dw = this.img.width * drawScale;
        const dh = this.img.height * drawScale;
        const imgX = (cw / 2) + this.panX - (dw / 2);
        const imgY = (ch / 2) + this.panY - (dh / 2);

        ctx.drawImage(this.img, imgX, imgY, dw, dh);

        // 2. Dark Backdrop outside frame
        ctx.fillStyle = 'rgba(0, 0, 0, 0.65)';
        ctx.fillRect(0, 0, cw, fy);
        ctx.fillRect(0, fy + fh, cw, ch - (fy + fh));
        ctx.fillRect(0, fy, fx, fh);
        ctx.fillRect(fx + fw, fy, cw - (fx + fw), fh);

        // 3. Frame Border (SDS Purple/Blue Accent)
        ctx.strokeStyle = '#6366f1';
        ctx.lineWidth = 2;
        ctx.strokeRect(fx, fy, fw, fh);
    },

    cleanSignatureBackground(canvas) {
        const context = canvas.getContext('2d');
        if (!context) return canvas;

        const frame = context.getImageData(0, 0, canvas.width, canvas.height);
        const pixels = frame.data;
        const width = canvas.width;
        const height = canvas.height;
        const pixelCount = width * height;

        // Convert to grayscale
        const gray = new Uint8ClampedArray(pixelCount);
        for (let i = 0, p = 0; i < pixelCount; i++, p += 4) {
            const red = pixels[p];
            const green = pixels[p + 1];
            const blue = pixels[p + 2];
            gray[i] = Math.round((0.299 * red) + (0.587 * green) + (0.114 * blue));
        }

        // Build integral image for fast local mean calculation
        const stride = width + 1;
        const integral = new Float64Array((width + 1) * (height + 1));
        for (let y = 1; y <= height; y++) {
            let rowSum = 0;
            for (let x = 1; x <= width; x++) {
                rowSum += gray[(y - 1) * width + (x - 1)];
                integral[y * stride + x] = integral[(y - 1) * stride + x] + rowSum;
            }
        }

        // Adaptive threshold to handle uneven lighting/shadows on paper
        const radius = Math.max(8, Math.round(Math.min(width, height) * 0.08));
        const offset = 14;
        const inkMask = new Uint8Array(pixelCount);

        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const x1 = Math.max(0, x - radius);
                const y1 = Math.max(0, y - radius);
                const x2 = Math.min(width - 1, x + radius);
                const y2 = Math.min(height - 1, y + radius);

                const area = (x2 - x1 + 1) * (y2 - y1 + 1);
                const sum =
                    integral[(y2 + 1) * stride + (x2 + 1)] -
                    integral[y1 * stride + (x2 + 1)] -
                    integral[(y2 + 1) * stride + x1] +
                    integral[y1 * stride + x1];

                const localMean = sum / area;
                const index = y * width + x;
                inkMask[index] = gray[index] < (localMean - offset) ? 1 : 0;
            }
        }

        // Remove isolated noise pixels
        for (let y = 1; y < height - 1; y++) {
            for (let x = 1; x < width - 1; x++) {
                const index = y * width + x;
                if (inkMask[index] === 0) continue;

                let neighbors = 0;
                for (let yy = -1; yy <= 1; yy++) {
                    for (let xx = -1; xx <= 1; xx++) {
                        if (xx === 0 && yy === 0) continue;
                        neighbors += inkMask[(y + yy) * width + (x + xx)];
                    }
                }

                if (neighbors <= 1) {
                    inkMask[index] = 0;
                }
            }
        }

        // Render clean black ink on white background
        for (let i = 0, p = 0; i < pixelCount; i++, p += 4) {
            if (inkMask[i] === 1) {
                pixels[p] = 35;
                pixels[p + 1] = 35;
                pixels[p + 2] = 35;
                pixels[p + 3] = 255;
            } else {
                pixels[p] = 255;
                pixels[p + 1] = 255;
                pixels[p + 2] = 255;
                pixels[p + 3] = 255;
            }
        }

        context.putImageData(frame, 0, 0);
        return canvas;
    },

    getCroppedResult() {
        if (!this.img || !this.img.complete) return null;
        const cw = this.canvas.width;
        const ch = this.canvas.height;
        const fw = this.frameW;
        const fh = this.frameH;
        const fx = (cw - fw) / 2;
        const fy = (ch - fh) / 2;

        const outW = this.target === 'photo' ? 400 : 600;
        const outH = this.target === 'photo' ? 500 : 200;

        const exportCanvas = document.createElement('canvas');
        exportCanvas.width = outW;
        exportCanvas.height = outH;
        const ectx = exportCanvas.getContext('2d', { willReadFrequently: true });

        const scaleRatio = outW / fw;
        const drawScale = this.baseScale * this.scale * scaleRatio;
        const dw = this.img.width * drawScale;
        const dh = this.img.height * drawScale;

        // Image relative to frame top-left
        const imgX = (this.panX + fw / 2) * scaleRatio - (dw / 2);
        const imgY = (this.panY + fh / 2) * scaleRatio - (dh / 2);

        ectx.fillStyle = '#ffffff';
        ectx.fillRect(0, 0, outW, outH);
        ectx.drawImage(this.img, imgX, imgY, dw, dh);

        if (this.target === 'signature') {
            this.cleanSignatureBackground(exportCanvas);
        }

        return exportCanvas.toDataURL('image/webp', 0.92);
    }
};

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

window.downloadFileFromBase64 = (fileName, contentType, base64Data) => {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = `data:${contentType};base64,${base64Data}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
