// Canvas drawing for Dynamics. C# sends batches of commands in logical pixel coordinates
// (the size of the diagram in Simulator); the canvas is scaled for the device pixel ratio.
//
// Command encoding (flat number array):
//   0                        clear (white)
//   1 x1 y1 x2 y2 color w    line
//   2 x y r color            filled circle
//   3 x y size color         point (filled circle of radius size; tiny points as squares)
//   4 dx                     shift the whole picture right by dx (chart recorder); the left strip is cleared
//   5 x y w h color width    rectangle outline
//   6 x y r color width      circle outline
//   7 x y size argb          point like 3, but with any color (ARGB as in System.Drawing.Color.ToArgb)
//   8 x y rx ry start sweep argb width   arc of an ellipse; start and sweep in degrees, clockwise on the screen (GDI DrawArc)
//   9 x1 y1 x2 y2 argb w     line with any color
//
// Pixel pictures (Julia and Mandelbrot sets) use a canvas set up with pixelExact = true:
// one canvas pixel per logical pixel, filled with putImage.

const palette = ['#ffffff', '#0000ff', '#ff0000', '#008000', '#7fffd4', '#9400d3', '#000000', '#ff00ff'];
const layers = new Map();

const argbToCss = argb => `rgb(${(argb >> 16) & 255},${(argb >> 8) & 255},${argb & 255})`;

export function setup(id, size, pixelExact = false) {
    const canvas = document.getElementById(id);
    if (!canvas) return;
    const dpr = pixelExact ? 1 : (window.devicePixelRatio || 1);
    canvas.width = Math.round(size * dpr);
    canvas.height = Math.round(size * dpr);
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    layers.set(id, { canvas, ctx, size });
}

export function draw(id, commands) {
    const layer = layers.get(id);
    if (!layer) return;
    const { ctx, size } = layer;
    let i = 0;
    while (i < commands.length) {
        switch (commands[i]) {
            case 0:
                ctx.clearRect(0, 0, size, size);
                i += 1;
                break;
            case 1:
                ctx.strokeStyle = palette[commands[i + 5]];
                ctx.lineWidth = commands[i + 6];
                ctx.beginPath();
                ctx.moveTo(commands[i + 1], commands[i + 2]);
                ctx.lineTo(commands[i + 3], commands[i + 4]);
                ctx.stroke();
                i += 7;
                break;
            case 2:
                ctx.fillStyle = palette[commands[i + 4]];
                ctx.beginPath();
                ctx.arc(commands[i + 1], commands[i + 2], Math.max(commands[i + 3], 0.5), 0, 2 * Math.PI);
                ctx.fill();
                i += 5;
                break;
            case 3: {
                const s = commands[i + 3];
                ctx.fillStyle = palette[commands[i + 4]];
                if (s < 2) {
                    ctx.fillRect(commands[i + 1] - s, commands[i + 2] - s, 2 * s, 2 * s);
                } else {
                    ctx.beginPath();
                    ctx.arc(commands[i + 1], commands[i + 2], s, 0, 2 * Math.PI);
                    ctx.fill();
                }
                i += 5;
                break;
            }
            case 4: {
                const dx = commands[i + 1];
                const copy = layer.copy ??= document.createElement('canvas');
                copy.width = layer.canvas.width;
                copy.height = layer.canvas.height;
                copy.getContext('2d').drawImage(layer.canvas, 0, 0);
                ctx.clearRect(0, 0, size, size);
                ctx.drawImage(copy, dx, 0, size, size);
                i += 2;
                break;
            }
            case 5:
                ctx.strokeStyle = palette[commands[i + 5]];
                ctx.lineWidth = commands[i + 6];
                ctx.strokeRect(commands[i + 1], commands[i + 2], commands[i + 3], commands[i + 4]);
                i += 7;
                break;
            case 6:
                ctx.strokeStyle = palette[commands[i + 4]];
                ctx.lineWidth = commands[i + 5];
                ctx.beginPath();
                ctx.arc(commands[i + 1], commands[i + 2], Math.max(commands[i + 3], 0.5), 0, 2 * Math.PI);
                ctx.stroke();
                i += 6;
                break;
            case 7: {
                const s = commands[i + 3];
                const argb = commands[i + 4];
                ctx.fillStyle = `rgb(${(argb >> 16) & 255},${(argb >> 8) & 255},${argb & 255})`;
                if (s < 2) {
                    ctx.fillRect(commands[i + 1] - s, commands[i + 2] - s, 2 * s, 2 * s);
                } else {
                    ctx.beginPath();
                    ctx.arc(commands[i + 1], commands[i + 2], s, 0, 2 * Math.PI);
                    ctx.fill();
                }
                i += 5;
                break;
            }
            case 8: {
                const toRad = Math.PI / 180;
                ctx.strokeStyle = argbToCss(commands[i + 7]);
                ctx.lineWidth = commands[i + 8];
                ctx.beginPath();
                ctx.ellipse(commands[i + 1], commands[i + 2], Math.abs(commands[i + 3]), Math.abs(commands[i + 4]), 0,
                    commands[i + 5] * toRad, (commands[i + 5] + commands[i + 6]) * toRad);
                ctx.stroke();
                i += 9;
                break;
            }
            case 9:
                ctx.strokeStyle = argbToCss(commands[i + 5]);
                ctx.lineWidth = commands[i + 6];
                ctx.beginPath();
                ctx.moveTo(commands[i + 1], commands[i + 2]);
                ctx.lineTo(commands[i + 3], commands[i + 4]);
                ctx.stroke();
                i += 7;
                break;
            default:
                console.error('Unknown canvas command', commands[i]);
                return;
        }
    }
}

// Copies RGBA bytes (w * h * 4) to the rectangle at (x, y) of a pixel-exact canvas.
export function putImage(id, x, y, w, h, bytes) {
    const layer = layers.get(id);
    if (!layer || w <= 0 || h <= 0) return;
    const data = new Uint8ClampedArray(bytes.buffer, bytes.byteOffset, w * h * 4);
    layer.ctx.putImageData(new ImageData(data, w, h), x, y);
}

// Pointer events in logical pixel coordinates of the canvas (CSS may scale it).
export function attachPointer(id, dotnetRef) {
    const layer = layers.get(id);
    if (!layer) return;
    const { canvas, size } = layer;
    const toLogical = e => {
        const rect = canvas.getBoundingClientRect();
        return [(e.clientX - rect.left) * size / rect.width, (e.clientY - rect.top) * size / rect.height];
    };
    let moveQueued = false;
    let lastMove = null;
    canvas.addEventListener('pointerdown', e => {
        canvas.setPointerCapture(e.pointerId);
        const [x, y] = toLogical(e);
        dotnetRef.invokeMethodAsync('OnPointer', 'down', x, y);
    });
    canvas.addEventListener('pointermove', e => {
        lastMove = toLogical(e);
        if (moveQueued) return;
        moveQueued = true;
        requestAnimationFrame(() => {
            moveQueued = false;
            dotnetRef.invokeMethodAsync('OnPointer', 'move', lastMove[0], lastMove[1]);
        });
    });
    const up = e => {
        const [x, y] = toLogical(e);
        dotnetRef.invokeMethodAsync('OnPointer', 'up', x, y);
    };
    canvas.addEventListener('pointerup', up);
    canvas.addEventListener('pointercancel', up);
}

export function nextFrame() {
    return new Promise(resolve => requestAnimationFrame(() => resolve()));
}
