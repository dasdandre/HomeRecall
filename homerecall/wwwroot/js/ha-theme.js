window.getHaColors = () => {
    // Standard MudBlazor-ish Defaults but HA Style
    const defaults = {
        primary: '#03a9f4',      // HA Blue
        secondary: '#ff9800',    // HA Orange
        background: '#fafafa',   // HA Light Grey
        surface: '#ffffff',      // Card White
        textPrimary: '#212121',  // Dark Text
        textSecondary: '#727272',// Grey Text
        appBarBackground: '#03a9f4',
        appBarText: '#ffffff',
        drawerBackground: '#ffffff',
        drawerText: '#212121'
    };

    try {
        // Try to access the parent window (Home Assistant)
        const targetDoc = (window.parent && window.parent !== window) ? window.parent.document.documentElement : document.documentElement;
        const style = getComputedStyle(targetDoc);

        const getVal = (name) => {
            const val = style.getPropertyValue(name).trim();
            return val || null;
        };

        if (getVal('--primary-color')) {
            return {
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
        return defaults;

    } catch (e) {
        // console.warn("Could not read colors from parent window (HA), using defaults.", e);
        return defaults;
    }
};

window.observeHaThemeChange = (dotNetHelper) => {
    try {
        const targetNode = (window.parent && window.parent !== window) ? window.parent.document.documentElement : document.documentElement;
        
        // Configuration of the observer:
        const config = { attributes: true, attributeFilter: ['style', 'class'] };

        // Callback function to execute when mutations are observed
        const callback = (mutationList, observer) => {
            // We throttle slightly or just notify
            // Ideally we check if relevant vars changed, but simpler to just re-fetch
            const colors = window.getHaColors();
            dotNetHelper.invokeMethodAsync('UpdateThemeFromJs', colors);
        };

        // Create an observer instance linked to the callback function
        const observer = new MutationObserver(callback);

        // Start observing the target node for configured mutations
        observer.observe(targetNode, config);
        
        // Return a cleanup function (not directly usable by Blazor, but good practice)
        return true;
    } catch (e) {
        console.warn("Could not setup HA theme observer", e);
        return false;
    }
};