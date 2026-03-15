/**
 * Knowledge Tree — JS Interop for Blazor SVG skill-tree visualization.
 * Handles: cloud level-up transition animation, wheel zoom.
 */
window.KnowledgeTreeInterop = {

    // ─── Cloud Level-Up Animation ──────────────────────────────────
    // Slides in clouds from left/right, signals Blazor, then slides out.
    playCloudTransition: function (overlayId, callbackRef) {
        const overlay = document.getElementById(overlayId);
        if (!overlay) return;

        overlay.classList.remove('hidden');
        overlay.classList.add('clouds-in');

        // After clouds cover (500ms), signal Blazor to swap content
        setTimeout(function () {
            if (callbackRef) {
                callbackRef.invokeMethodAsync('OnCloudsCovered');
            }
        }, 500);

        // After swap, slide clouds away (start at 800ms)
        setTimeout(function () {
            overlay.classList.remove('clouds-in');
            overlay.classList.add('clouds-out');
        }, 800);

        // Cleanup after full animation (1400ms)
        setTimeout(function () {
            overlay.classList.remove('clouds-out');
            overlay.classList.add('hidden');
        }, 1400);
    }
};
