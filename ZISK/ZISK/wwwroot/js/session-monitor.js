var SessionMonitor = (function () {
    var warningTime = 0.2 * 60 * 1000; 
    var logoutTime = 10 * 1000;         
    
    var warningTimer = null;
    var logoutTimer = null;
    var warningShown = false;

    function formatTime(milliseconds) {
        var totalSeconds = Math.floor(milliseconds / 1000);
        var minutes = Math.floor(totalSeconds / 60);
        var seconds = totalSeconds % 60;
        
        if (minutes > 0) {
            return minutes + " minút";
        }
        return seconds + " sekúnd";
    }

    function resetTimers() {
        if (warningShown) {
            return;
        }

        if (warningTimer) {
            clearTimeout(warningTimer);
        }
        if (logoutTimer) {
            clearTimeout(logoutTimer);
        }

        warningTimer = setTimeout(showWarning, warningTime);
    }

    function showWarning() {
        warningShown = true;

        logoutTimer = setTimeout(performLogout, logoutTime);

        var inactivityTime = formatTime(warningTime);
        var timeoutTime = formatTime(logoutTime);

        var modalHTML = 
            '<div id="session-modal" style="position:fixed;top:0;left:0;width:100%;height:100%;' +
            'background:rgba(0,0,0,0.7);display:flex;align-items:center;justify-content:center;z-index:9999;">' +
            '<div style="background:white;padding:25px;border-radius:5px;max-width:350px;text-align:center;">' +
            '<h3 style="margin-top:0;color:#d32f2f;">Upozornenie</h3>' +
            '<p>Boli ste neaktívni ' + inactivityTime + '.</p>' +
            '<p>Budete automaticky odhlásený za ' + timeoutTime + '.</p>' +
            '<button onclick="SessionMonitor.continueSession()" style="background:#4caf50;color:white;border:none;' +
            'padding:10px 20px;margin:5px;border-radius:4px;cursor:pointer;">Pokračovať</button>' +
            '<button onclick="SessionMonitor.cancelSession()" style="background:#757575;color:white;border:none;' +
            'padding:10px 20px;margin:5px;border-radius:4px;cursor:pointer;">Zrušiť</button>' +
            '</div></div>';
        
        document.body.insertAdjacentHTML('beforeend', modalHTML);
    }

    function continueSession() {
        warningShown = false;
        clearTimeout(logoutTimer);
        
        var modal = document.getElementById("session-modal");
        if (modal) {
            modal.remove();
        }
        
        resetTimers();
    }

    function cancelSession() {
        performLogout();
    }

    function performLogout() {
        window.location.href = "/logout";
    }

    function init() {
        document.addEventListener("mousemove", resetTimers);
        document.addEventListener("mousedown", resetTimers);
        document.addEventListener("keypress", resetTimers);
        document.addEventListener("touchstart", resetTimers);
        document.addEventListener("scroll", resetTimers);

        resetTimers();
        
        console.log("Session monitor inicializovaný - neaktivita: " + formatTime(warningTime));
    }

    return {
        init: init,
        continueSession: continueSession,
        cancelSession: cancelSession
    };
})();
