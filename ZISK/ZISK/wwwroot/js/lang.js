// Backs LanguageService (WASM). A single zisk_lang cookie (not localStorage) is the one
// persistence mechanism, deliberately: Static SSR pages (Login, /demo) read the same cookie
// server-side via HttpContext.Request.Cookies, so a language choice made in the interactive
// app is still honored on the next fully server-rendered page - no second store to keep in sync.
window.ziskLang = {
    getCookie: function () {
        const match = document.cookie.match(/(?:^|; )zisk_lang=([^;]*)/);
        return match ? decodeURIComponent(match[1]) : null;
    },
    setCookie: function (value) {
        document.cookie = `zisk_lang=${value}; path=/; max-age=2592000; samesite=lax`;
    }
};
