(function () {
    let scanner = null;
    let targetId = null;
    let targetLabel = '';

    window.openScanner = function (fieldId, label) {
        targetId = fieldId;
        targetLabel = label;

        document.getElementById('scan-camera').innerHTML = '';
        document.getElementById('scan-result').style.display = 'none';
        document.getElementById('scan-value').textContent = '';
        document.getElementById('scan-label').textContent = 'Escaneando: ' + label;
        document.getElementById('scan-modal').style.display = 'flex';

        scanner = new Html5Qrcode('scan-camera', {
            formatsToSupport: [
                Html5QrcodeSupportedFormats.QR_CODE,
                Html5QrcodeSupportedFormats.CODE_128,
                Html5QrcodeSupportedFormats.CODE_39,
                Html5QrcodeSupportedFormats.CODE_39_MOD_43,
                Html5QrcodeSupportedFormats.EAN_13,
                Html5QrcodeSupportedFormats.EAN_8,
                Html5QrcodeSupportedFormats.UPC_A,
                Html5QrcodeSupportedFormats.UPC_E,
                Html5QrcodeSupportedFormats.ITF,
                Html5QrcodeSupportedFormats.DATA_MATRIX,
                Html5QrcodeSupportedFormats.AZTEC,
            ]
        });

        scanner.start(
            { facingMode: 'environment' },
            { fps: 10, qrbox: { width: 230, height: 230 } },
            function (text) {
                document.getElementById('scan-value').textContent = text;
                document.getElementById('scan-result').style.display = 'block';
                // Pause processing (keeps camera stream alive for retry)
                try { scanner.pause(); } catch (e) {}
            },
            function () {}
        ).catch(function (err) {
            alert('No se pudo acceder a la cámara.\n' + err);
            _reset();
        });
    };

    function _reset() {
        var s = scanner;
        scanner = null;
        document.getElementById('scan-modal').style.display = 'none';
        if (s) {
            s.stop().catch(function () {}).finally(function () {
                try { s.clear(); } catch (e) {}
            });
        }
    }

    window.confirmScan = function () {
        var value = document.getElementById('scan-value').textContent.trim();
        var field = document.getElementById(targetId);
        if (field && value) {
            field.value = value;
            field.dispatchEvent(new Event('input', { bubbles: true }));
            field.dispatchEvent(new Event('change', { bubbles: true }));
        }
        _reset();
    };

    window.retryScanner = function () {
        var s = scanner;
        scanner = null;
        var id = targetId, lbl = targetLabel;
        if (s) {
            s.stop().catch(function () {}).finally(function () {
                try { s.clear(); } catch (e) {}
                openScanner(id, lbl);
            });
        } else {
            openScanner(id, lbl);
        }
    };

    window.closeScanner = _reset;
})();
