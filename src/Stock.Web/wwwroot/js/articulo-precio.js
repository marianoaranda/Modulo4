// T145 — Recálculo interactivo del Precio de Venta (RF-016a).
//
// Muestra, mientras el usuario edita el Precio de Costo o el Margen, el resultado de la fórmula de
// RF-016: Costo × (1 + Margen / 100). Es **sólo una previsualización**: el campo sigue siendo de
// sólo lectura y no se envía, así que lo que queda grabado lo calcula siempre el servidor. Un
// cliente que no ejecute este script no puede alterar el precio, y uno que lo ejecute tampoco.
(function () {
    'use strict';

    function numero(campo) {
        var valor = parseFloat((campo.value || '').replace(',', '.'));

        return isNaN(valor) ? 0 : valor;
    }

    document.addEventListener('DOMContentLoaded', function () {
        var costo = document.querySelector('[data-precio-costo]');
        var margen = document.querySelector('[data-margen]');
        var venta = document.querySelector('[data-precio-venta]');

        if (!costo || !margen || !venta) {
            return;
        }

        function recalcular() {
            venta.value = (numero(costo) * (1 + numero(margen) / 100)).toFixed(2);
        }

        // `input` y no `change`: el requisito pide que se vea *a medida que* se edita, sin esperar
        // a que el campo pierda el foco.
        costo.addEventListener('input', recalcular);
        margen.addEventListener('input', recalcular);
    });
})();
