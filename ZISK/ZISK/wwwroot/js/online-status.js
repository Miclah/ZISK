window.onlineStatus = {
    _ref: null,
    _onlineHandler: null,
    _offlineHandler: null,

    initialize(dotNetRef) {
        this._ref = dotNetRef;
        this._onlineHandler = () => this._ref.invokeMethodAsync('UpdateOnlineStatus', true);
        this._offlineHandler = () => this._ref.invokeMethodAsync('UpdateOnlineStatus', false);
        window.addEventListener('online', this._onlineHandler);
        window.addEventListener('offline', this._offlineHandler);
        return navigator.onLine;
    },

    dispose() {
        if (this._onlineHandler) window.removeEventListener('online', this._onlineHandler);
        if (this._offlineHandler) window.removeEventListener('offline', this._offlineHandler);
        this._ref = null;
    }
};
