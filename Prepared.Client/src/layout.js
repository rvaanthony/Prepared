// Global layout scripts
// This file runs on all pages

console.log('Prepared Client - Layout scripts loaded');

// Global utilities
window.Prepared = window.Prepared || {};

// Helper to get CSRF token
window.Prepared.getCsrfToken = function() {
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    return token ? token.value : '';
};

// Helper to format timestamps
window.Prepared.formatTimestamp = function(date) {
    if (!date) return '';
    const d = new Date(date);
    return d.toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });
};

// Helper to format relative time
window.Prepared.formatRelativeTime = function(date) {
    if (!date) return '';
    const d = new Date(date);
    const now = new Date();
    const diffMs = now - d;
    const diffSecs = Math.floor(diffMs / 1000);
    const diffMins = Math.floor(diffSecs / 60);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffSecs < 60) return 'just now';
    if (diffMins < 60) return `${diffMins} minute${diffMins !== 1 ? 's' : ''} ago`;
    if (diffHours < 24) return `${diffHours} hour${diffHours !== 1 ? 's' : ''} ago`;
    if (diffDays < 7) return `${diffDays} day${diffDays !== 1 ? 's' : ''} ago`;
    return window.Prepared.formatTimestamp(date);
};

