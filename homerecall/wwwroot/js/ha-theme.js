window.getHaColors = async () => {
    const defaults = {
        primary: '#03a9f4',
        secondary: '#ff9800',
        background: '#fafafa',
        surface: '#ffffff',
        textPrimary: '#212121',
        textSecondary: '#727272',
        appBarBackground: '#03a9f4',
        appBarText: '#ffffff',
        drawerBackground: '#ffffff',
        drawerText: '#212121'
    };

    try {
        // Try to get the parent window (Home Assistant Ingress context)
        const parentWindow = (window.parent && window.parent !== window) ? window.parent : window;
        
        // Check if hass object is available (HA 2024.1+)
        if (parentWindow.hass && parentWindow.hass.themes) {
            const themes = parentWindow.hass.themes;
            const isDarkMode = parentWindow.hass.themes.darkMode || false;
            const currentThemeName = parentWindow.hass.themes.theme || (isDarkMode ? 'dark' : 'light');
            const currentTheme = themes.themes && themes.themes[currentThemeName] ? themes.themes[currentThemeName] : {};
            
            return {
                isDarkMode: isDarkMode,
                primary: currentTheme['--primary-color'] || defaults.primary,
                secondary: currentTheme['--accent-color'] || defaults.secondary,
                background: currentTheme['--primary-background-color'] || defaults.background,
                surface: currentTheme['--card-background-color'] || defaults.surface,
                textPrimary: currentTheme['--primary-text-color'] || defaults.textPrimary,
                textSecondary: currentTheme['--secondary-text-color'] || defaults.textSecondary,
                appBarBackground: currentTheme['--app-header-background-color'] || defaults.appBarBackground,
                appBarText: currentTheme['--app-header-text-color'] || defaults.appBarText,
                drawerBackground: currentTheme['--card-background-color'] || defaults.drawerBackground,
                drawerText: currentTheme['--primary-text-color'] || defaults.drawerText
            };
        }
    } catch (e) {
        console.warn("Could not read from HA API, falling back to CSS variables", e);
    }

    // Fallback to CSS variables if API not available
    try {
        const targetDoc = (window.parent && window.parent !== window) ? window.parent.document.documentElement : document.documentElement;
        const style = getComputedStyle(targetDoc);

        const getVal = (name) => {
            const val = style.getPropertyValue(name).trim();
            return val || null;
        };

        const isDarkMode = targetDoc.classList.contains('dark');

        if (getVal('--primary-color')) {
            return {
                isDarkMode: isDarkMode,
                primary: getVal('--primary-color') || defaults.primary,
                secondary: getVal('--accent-color') || defaults.secondary,
                background: getVal('--primary-background-color') || defaults.background,
                surface: getVal('--card-background-color') || defaults.surface,
                textPrimary: getVal('--primary-text-color') || defaults.textPrimary,
                textSecondary: getVal('--secondary-text-color') || defaults.textSecondary,
                appBarBackground: getVal('--app-header-background-color') || defaults.appBarBackground,
                appBarText: getVal('--app-header-text-color') || defaults.appBarText,
                drawerBackground: getVal('--card-background-color') || defaults.drawerBackground,
                drawerText: getVal('--primary-text-color') || defaults.drawerText
            };
        }
        return { isDarkMode: isDarkMode, ...defaults };

    } catch (e) {
        console.warn("Could not read colors, using defaults", e);
        return { isDarkMode: false, ...defaults };
    }
};

window.observeHaThemeChange = (dotNetHelper) => {
    try {
        const parentWindow = (window.parent && window.parent !== window) ? window.parent : window;
        
        // If HA exposes a way to listen for theme changes, use it
        if (parentWindow.hass && parentWindow.addEventListener) {
            // Listen for custom HA theme change events
            parentWindow.addEventListener('theme-changed', async () => {
                const colors = await window.getHaColors();
                dotNetHelper.invokeMethodAsync('UpdateThemeFromJs', colors);
            });
        }
        
        // Also fall back to MutationObserver for DOM changes
        const targetNode = (window.parent && window.parent !== window) ? window.parent.document.documentElement : document.documentElement;
        const config = { attributes: true, attributeFilter: ['style', 'class'] };

        const callback = (mutationList, observer) => {
            clearTimeout(callback.timeout);
            callback.timeout = setTimeout(async () => {
                const colors = await window.getHaColors();
                dotNetHelper.invokeMethodAsync('UpdateThemeFromJs', colors);
            }, 100);
        };

        const observer = new MutationObserver(callback);
        observer.observe(targetNode, config);
        
        return true;
    } catch (e) {
        console.warn("Could not setup HA theme observer", e);
        return false;
    }
};