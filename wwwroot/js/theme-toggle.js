(function () {
    const themeToggle = document.getElementById('theme-toggle');
    const htmlElement = document.documentElement;
    const themeIcon = document.getElementById('theme-icon');

    // 1. Initial Load: Check localStorage and system preference
    const savedTheme = localStorage.getItem('theme');
    const systemPrefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    
    const initialTheme = savedTheme || (systemPrefersDark ? 'dark' : 'light');
    applyTheme(initialTheme);

    // 2. Toggle Event
    if (themeToggle) {
        themeToggle.addEventListener('click', () => {
            const currentTheme = htmlElement.getAttribute('data-theme') || 'light';
            const newTheme = currentTheme === 'light' ? 'dark' : 'light';
            applyTheme(newTheme);
            localStorage.setItem('theme', newTheme);
        });
    }

    function applyTheme(theme) {
        if (theme === 'dark') {
            htmlElement.setAttribute('data-theme', 'dark');
            if (themeIcon) {
                themeIcon.classList.remove('bi-sun');
                themeIcon.classList.add('bi-moon-stars');
            }
        } else {
            htmlElement.removeAttribute('data-theme');
            if (themeIcon) {
                themeIcon.classList.remove('bi-moon-stars');
                themeIcon.classList.add('bi-sun');
            }
        }
    }
})();
