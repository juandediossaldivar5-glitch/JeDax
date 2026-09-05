(function () {
    let scanner = null;
    let targetId = null;
    let targetLabel = '';

    window.openScanner = function (fieldId, label) {
        targetId = fieldId;
        targetLabel = label;

        const modal = document.getElementById('scan-modal');
        const result = document.getElementById('scan-result');

        document.getElementById('scan-camera').innerHTML = '';
        result.style.display = 'none';
        document.getElementById('scan-value').textContent = '';
        document.getElementById('scan-label').textContent = 'Escaneando: ' + label;
        modal.style.display = 'flex';

        scanner = new Html5Qrcode('scan-camera');
        scanner.start(
            { facingMode: 'environment' },
            { fps: 10, qrbox: { width: 230, height: 230 } },
            function (text) {
                document.getElementById('scan-value').textContent = text;
                result.style.display = 'block';
                scanner.stop().catch(function () {});
            },
            function () {}
        ).catch(function (err) {
            alert('No se pudo acceder a la cámara.\n' + err);
            closeScanner();
        });
    };

    window.closeScanner = function () {
        if (scanner) {
            scanner.stop().catch(function () {}).finally(function () {
                if (scanner) { scanner.clear(); scanner = null; }
            });
        }
        document.getElementById('scan-modal').style.display = 'none';
    };

    window.confirmScan = function () {
        var value = document.getElementById('scan-value').textContent.trim();
        var field = document.getElementById(targetId);
        if (field && value) {
            field.value = value;
            field.dispatchEvent(new Event('input', { bubbles: true }));
            field.dispatchEvent(new Event('change', { bubbles: true }));
        }
        closeScanner();
    };

    window.retryScanner = function () {
        openScanner(targetId, targetLabel);
    };
})();
