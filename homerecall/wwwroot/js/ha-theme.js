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
        drawerText: '#212121',
        success: '#4caf50',
        error: '#f44336'
    };

    try {
        // Check if we are in an iframe (Home Assistant Ingress)
        const inIframe = window.parent && window.parent !== window;
        const targetDoc = inIframe ? window.parent.document.documentElement : document.documentElement;
        
        // If we can't access parent document (cross-origin), we can't sync theme
        if (!targetDoc) return { isDarkMode: false, ...defaults };

        const style = getComputedStyle(targetDoc);

        const getVal = (name) => {
            const val = style.getPropertyValue(name).trim();
            return val || null;
        };

        const isDarkMode = targetDoc.classList.contains('dark') || targetDoc.getAttribute('data-theme') === 'dark';
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
                appBarBackground: getVal('--app-header-background-color') || primaryColor || defaults.appBarBackground,
                appBarText: getVal('--app-header-text-color') || getVal('--text-primary-color') || defaults.appBarText,
                drawerBackground: getVal('--card-background-color') || defaults.drawerBackground,
                drawerText: getVal('--primary-text-color') || defaults.drawerText,
                success: getVal('--success-color') || defaults.success,
                error: getVal('--error-color') || defaults.error
            };
        }
        return { isDarkMode: isDarkMode, ...defaults };

    } catch (e) {
        console.debug('HomeRecall: Unable to read Home Assistant theme.', e);
        return { isDarkMode: false, ...defaults };
    }
};

window.observeHaThemeChange = async (dotNetHelper) => {
    try {
        const inIframe = window.parent && window.parent !== window;
        if (!inIframe) return false;

        const targetNode = window.parent.document.documentElement;
        
        // Initial sync
        const colors = await window.getHaColors();
        dotNetHelper.invokeMethodAsync('UpdateThemeFromJs', colors);

        // Fall back to MutationObserver for DOM changes on the parent document
        const config = { attributes: true, attributeFilter: ['style', 'class', 'data-theme'] };

        const callback = (mutationList, observer) => {
            clearTimeout(callback.timeout);
            callback.timeout = setTimeout(async () => {
                const newColors = await window.getHaColors();
                dotNetHelper.invokeMethodAsync('UpdateThemeFromJs', newColors);
            }, 200);
        };

        const observer = new MutationObserver(callback);
        observer.observe(targetNode, config);
        
        return true;
    } catch (e) {
        console.debug('HomeRecall: Unable to observe Home Assistant theme changes.', e);
        return false;
    }
};