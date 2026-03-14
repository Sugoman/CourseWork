/**
 * Knowledge Tree — JS Interop for Blazor SVG visualization.
 * Handles: leaf scattering, smooth zoom/pan, cloud level-up animation.
 */
window.KnowledgeTreeInterop = {

    // ─── Leaf Scattering ───────────────────────────────────────────
    // Distributes leaf <g> elements within a bounding box using a seeded jitter algorithm.
    scatterLeaves: function (containerId, leaves, bbox) {
        const container = document.getElementById(containerId);
        if (!container) return;

        container.innerHTML = '';
        const { x, y, width, height } = bbox;
        const count = Math.min(leaves.length, 100);
        const cols = Math.ceil(Math.sqrt(count * (width / height)));
        const rows = Math.ceil(count / cols);
        const cellW = width / cols;
        const cellH = height / rows;

        for (let i = 0; i < count; i++) {
            const col = i % cols;
            const row = Math.floor(i / cols);
            // Jitter within the cell (30-70% range to avoid edges)
            const jx = x + col * cellW + cellW * (0.3 + Math.random() * 0.4);
            const jy = y + row * cellH + cellH * (0.3 + Math.random() * 0.4);
            const rotation = -20 + Math.random() * 40;
            const scale = 0.7 + Math.random() * 0.6;

            const leaf = leaves[i];
            const color = leaf.isLearned ? '#4caf50' : '#a5d6a7';

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.setAttribute('transform', `translate(${jx.toFixed(1)},${jy.toFixed(1)}) rotate(${rotation.toFixed(0)}) scale(${scale.toFixed(2)})`);
            g.setAttribute('class', 'tree-leaf');
            g.setAttribute('data-word-id', leaf.wordId);

            // Leaf shape: simple SVG path
            const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.setAttribute('d', 'M0,-6 C3,-6 6,-3 6,0 C6,3 3,6 0,8 C-3,6 -6,3 -6,0 C-6,-3 -3,-6 0,-6Z');
            path.setAttribute('fill', color);
            path.setAttribute('stroke', '#388e3c');
            path.setAttribute('stroke-width', '0.5');
            g.appendChild(path);

            // Tiny word label
            const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            text.setAttribute('y', '1');
            text.setAttribute('text-anchor', 'middle');
            text.setAttribute('font-size', '3');
            text.setAttribute('fill', '#1b5e20');
            text.setAttribute('pointer-events', 'none');
            text.textContent = (leaf.word || '').substring(0, 6);
            g.appendChild(text);

            // Tooltip on hover via <title>
            const title = document.createElementNS('http://www.w3.org/2000/svg', 'title');
            title.textContent = leaf.word || '';
            g.appendChild(title);

            container.appendChild(g);
        }
    },

    // ─── Smooth Zoom ───────────────────────────────────────────────
    // Applies CSS transform to #tree-viewport to zoom into a specific area.
    zoomTo: function (viewportId, targetX, targetY, scale, durationMs) {
        const vp = document.getElementById(viewportId);
        if (!vp) return;

        vp.style.transition = `transform ${durationMs}ms cubic-bezier(0.4, 0, 0.2, 1)`;
        vp.style.transformOrigin = `${targetX}px ${targetY}px`;
        vp.style.transform = `scale(${scale}) translate(${-targetX * (1 - 1 / scale)}px, ${-targetY * (1 - 1 / scale)}px)`;
    },

    resetZoom: function (viewportId, durationMs) {
        const vp = document.getElementById(viewportId);
        if (!vp) return;

        vp.style.transition = `transform ${durationMs}ms cubic-bezier(0.4, 0, 0.2, 1)`;
        vp.style.transform = 'scale(1) translate(0, 0)';
    },

    // ─── Cloud Level-Up Animation ──────────────────────────────────
    // Slides in clouds from left/right, swaps tree stage, slides out clouds.
    playCloudTransition: function (overlayId, callbackRef) {
        const overlay = document.getElementById(overlayId);
        if (!overlay) return;

        overlay.classList.remove('hidden');
        overlay.classList.add('clouds-in');

        // After clouds cover the tree (600ms), signal Blazor to swap the sprite
        setTimeout(function () {
            if (callbackRef) {
                callbackRef.invokeMethodAsync('OnCloudsCovered');
            }
        }, 600);

        // After swap, slide clouds away (start at 900ms)
        setTimeout(function () {
            overlay.classList.remove('clouds-in');
            overlay.classList.add('clouds-out');
        }, 900);

        // Cleanup after full animation (1500ms)
        setTimeout(function () {
            overlay.classList.remove('clouds-out');
            overlay.classList.add('hidden');
        }, 1500);
    }
};
