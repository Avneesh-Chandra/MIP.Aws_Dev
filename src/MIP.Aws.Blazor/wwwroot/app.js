// Browser-side helpers invoked via IJSRuntime by Blazor components.

/// <summary>Returns IANA time zone id from the browser (e.g. Asia/Bahrain), or null if unavailable.</summary>
window.mipGetBrowserTimeZone = function () {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
    } catch (err) {
        console.error('mipGetBrowserTimeZone failed', err);
        return null;
    }
};

/// <summary>Horizontal scroll for single-row app bar navigation.</summary>
window.mipScrollAppBarNav = function (navId, delta) {
    const nav = document.getElementById(navId);
    if (!nav) {
        return;
    }
    nav.scrollBy({ left: delta, behavior: 'smooth' });
};

window.mipInitAppBarNavScroll = function (navId, leftBtnId, rightBtnId) {
    const nav = document.getElementById(navId);
    const left = document.getElementById(leftBtnId);
    const right = document.getElementById(rightBtnId);
    if (!nav) {
        return;
    }

    const updateButtons = () => {
        const maxScroll = Math.max(0, nav.scrollWidth - nav.clientWidth);
        const atStart = nav.scrollLeft <= 2;
        const atEnd = nav.scrollLeft >= maxScroll - 2;
        const overflow = maxScroll > 4;

        if (left) {
            left.disabled = !overflow || atStart;
            left.style.visibility = overflow ? 'visible' : 'hidden';
        }
        if (right) {
            right.disabled = !overflow || atEnd;
            right.style.visibility = overflow ? 'visible' : 'hidden';
        }
    };

    if (nav.dataset.miNavScrollBound !== '1') {
        nav.dataset.miNavScrollBound = '1';
        nav.addEventListener('scroll', updateButtons, { passive: true });
        window.addEventListener('resize', updateButtons);
    }

    updateButtons();
};

window.downloadTextFile = function (filename, content, mimeType) {
    try {
        const blob = new Blob([content], { type: mimeType || 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename || 'download.txt';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (err) {
        console.error('downloadTextFile failed', err);
    }
};
