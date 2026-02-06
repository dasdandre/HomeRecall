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
   

    // Fallback to CSS variables if API not available
    try {
        const targetDoc = (window.parent && window.parent !== window) ? window.parent.document.documentElement : document.documentElement;
        const style = getComputedStyle(targetDoc);

        const getVal = (name) => {
            const val = style.getPropertyValue(name).trim();
            return val || null;
        };

        const isDarkMode = targetDoc.classList.contains('dark');
        const primaryColor = getVal('--primary-color');
        
        if (primaryColor) {
            return {
                isDarkMode: isDarkMode,
                primary: primaryColor || defaults.primary,
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

        let mutationCount = 0;
        const callback = (mutationList, observer) => {
            clearTimeout(callback.timeout);
            callback.timeout = setTimeout(async () => {
                mutationCount++;
                const colors = await window.getHaColors();
                dotNetHelper.invokeMethodAsync('UpdateThemeFromJs', colors);
            }, 100);
        };

        const observer = new MutationObserver(callback);
        observer.observe(targetNode, config);
        
        return true;
    } catch (e) {
        return false;
    }
};