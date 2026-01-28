console.log("[HomeRecall Theme] Script loaded successfully");

window.getHaColors = async () => {
    console.log("[HomeRecall Theme] getHaColors called");
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
        console.log("[HomeRecall Theme] Parent window:", parentWindow === window ? "same (standalone)" : "different (Ingress)");
        
        // Check if hass object is available (HA 2024.1+)
        if (parentWindow.hass) {
            console.log("[HomeRecall Theme] hass object found");
            console.log("[HomeRecall Theme] hass.themes available:", !!parentWindow.hass.themes);
            
            if (parentWindow.hass.themes) {
                const themes = parentWindow.hass.themes;
                const isDarkMode = parentWindow.hass.themes.darkMode || false;
                const currentThemeName = parentWindow.hass.themes.theme || (isDarkMode ? 'dark' : 'light');
                const currentTheme = themes.themes && themes.themes[currentThemeName] ? themes.themes[currentThemeName] : {};
                
                console.log("[HomeRecall Theme] Using HA API:");
                console.log("[HomeRecall Theme]   isDarkMode:", isDarkMode);
                console.log("[HomeRecall Theme]   currentThemeName:", currentThemeName);
                console.log("[HomeRecall Theme]   currentTheme keys:", Object.keys(currentTheme).slice(0, 5));
                
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
        } else {
            console.log("[HomeRecall Theme] hass object NOT found, trying CSS variables");
        }
    } catch (e) {
        console.warn("[HomeRecall Theme] Error accessing HA API, falling back to CSS variables:", e);
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
        const primaryColor = getVal('--primary-color');
        
        console.log("[HomeRecall Theme] Using CSS Variables fallback:");
        console.log("[HomeRecall Theme]   isDarkMode (dark class):", isDarkMode);
        console.log("[HomeRecall Theme]   --primary-color found:", !!primaryColor);

        if (primaryColor) {
            console.log("[HomeRecall Theme]   Colors found in CSS variables");
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
        console.log("[HomeRecall Theme]   No CSS variables found, using defaults");
        return { isDarkMode: isDarkMode, ...defaults };

    } catch (e) {
        console.warn("[HomeRecall Theme] Error reading CSS variables, using hardcoded defaults:", e);
        return { isDarkMode: false, ...defaults };
    }
};

window.observeHaThemeChange = (dotNetHelper) => {
    console.log("[HomeRecall Theme] observeHaThemeChange called");
    try {
        const parentWindow = (window.parent && window.parent !== window) ? window.parent : window;
        
        // If HA exposes a way to listen for theme changes, use it
        if (parentWindow.hass && parentWindow.addEventListener) {
            console.log("[HomeRecall Theme] Setting up HA theme-changed event listener");
            // Listen for custom HA theme change events
            parentWindow.addEventListener('theme-changed', async () => {
                console.log("[HomeRecall Theme] theme-changed event received");
                const colors = await window.getHaColors();
                console.log("[HomeRecall Theme] Calling UpdateThemeFromJs with colors");
                dotNetHelper.invokeMethodAsync('UpdateThemeFromJs', colors);
            });
        } else {
            console.log("[HomeRecall Theme] HA theme-changed listener not available");
        }
        
        // Also fall back to MutationObserver for DOM changes
        const targetNode = (window.parent && window.parent !== window) ? window.parent.document.documentElement : document.documentElement;
        const config = { attributes: true, attributeFilter: ['style', 'class'] };

        let mutationCount = 0;
        const callback = (mutationList, observer) => {
            clearTimeout(callback.timeout);
            callback.timeout = setTimeout(async () => {
                mutationCount++;
                console.log(`[HomeRecall Theme] Mutation detected (${mutationCount}), updating colors`);
                const colors = await window.getHaColors();
                console.log("[HomeRecall Theme] Calling UpdateThemeFromJs from MutationObserver");
                dotNetHelper.invokeMethodAsync('UpdateThemeFromJs', colors);
            }, 100);
        };

        const observer = new MutationObserver(callback);
        observer.observe(targetNode, config);
        console.log("[HomeRecall Theme] MutationObserver set up successfully");
        
        return true;
    } catch (e) {
        console.error("[HomeRecall Theme] Error in observeHaThemeChange:", e);
        return false;
    }
};