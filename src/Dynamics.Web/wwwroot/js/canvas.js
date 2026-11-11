// Canvas drawing for Dynamics. C# sends batches of commands in logical pixel coordinates
// (the size of the diagram in Simulator); the canvas is scaled for the device pixel ratio.
//
// Command encoding (flat number array):
//   0                        clear (white)
//   1 x1 y1 x2 y2 color w    line
//   2 x y r color            filled circle
//   3 x y size color         point (filled square of 2*size, like a small FillEllipse)

const palette = ['#ffffff', '#0000ff', '#ff0000', '#008000', '#7fffd4', '#9400d3', '#000000'];
const layers = new Map();

export function setup(id, size) {
    const canvas = document.getElementById(id);
    if (!canvas) return;
    const dpr = window.devicePixelRatio || 1;
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
                ctx.fillRect(commands[i + 1] - s, commands[i + 2] - s, 2 * s, 2 * s);
                i += 5;
                break;
            }
            default:
                console.error('Unknown canvas command', commands[i]);
                return;
        }
    }
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
