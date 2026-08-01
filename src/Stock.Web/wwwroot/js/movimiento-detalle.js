// T153 — Carga asistida del detalle de movimientos (RF-020g, RF-020h, RF-020i).
//
// Hace tres cosas, todas de comodidad y ninguna de negocio: sugiere el Precio Unitario según el
// Tipo del movimiento, muestra el Precio Total de cada línea y suma el Total General. Lo que se
// graba lo decide siempre el servidor.
//
// No consulta al catálogo por su cuenta: escucha el evento `articulo:resuelto` que publica
// `buscador-articulos.js` al resolver un Código. Así hay **una sola consulta por Código** (RF-020g)
// y la Descripción que se muestra y el precio que se sugiere no pueden salir de fuentes distintas.
(function () {
    'use strict';

    function numero(valor) {
        var convertido = parseFloat((valor || '').toString().replace(',', '.'));

        return isNaN(convertido) ? 0 : convertido;
    }

    function esCompra() {
        var tipo = document.querySelector('[data-tipo-movimiento]');

        // El `select` lleva el nombre del valor del enum, no su número.
        return !tipo || (tipo.value || '').toLowerCase().indexOf('compra') === 0;
    }

    function filaDe(elemento) {
        return elemento.closest('tr');
    }

    function recalcularFila(fila) {
        var cantidad = fila.querySelector('[data-cantidad]');
        var precio = fila.querySelector('[data-precio-unitario]');
        var total = fila.querySelector('[data-precio-total]');

        if (!cantidad || !precio || !total) {
            return;
        }

        // RF-020c: Cantidad × Precio Unitario. Es la misma fórmula del servidor, que sigue siendo
        // la fuente de verdad: acá sólo se muestra mientras se carga.
        total.textContent = (numero(cantidad.value) * numero(precio.value)).toFixed(2);
    }

    function recalcularTotalGeneral() {
        var total = document.querySelector('[data-total-general]');

        if (!total) {
            return;
        }

        var suma = 0;

        document.querySelectorAll('[data-precio-total]').forEach(function (celda) {
            suma += numero(celda.textContent);
        });

        total.textContent = suma.toFixed(2);
    }

    function recalcularTodo() {
        document.querySelectorAll('#detalle tbody tr').forEach(recalcularFila);
        recalcularTotalGeneral();
    }

    document.addEventListener('DOMContentLoaded', function () {
        // Sugerencia del Precio Unitario: se dispara **sólo** cuando cambia el Código de la línea
        // (RF-020g). Cambiar el Tipo después no reescribe los precios ya cargados: pueden haber
        // sido editados a mano y pisarlos perdería lo que el usuario puso.
        document.addEventListener('articulo:resuelto', function (evento) {
            var fila = filaDe(evento.detail.campo);
            var articulo = evento.detail.articulo;

            if (!fila) {
                return;
            }

            var precio = fila.querySelector('[data-precio-unitario]');

            // Sin artículo no hay sugerencia: el Precio Unitario queda como estaba y el rechazo
            // llega recién al grabar, con el 404 de RF-020e.
            if (precio && articulo) {
                precio.value = (esCompra() ? articulo.precioCosto : articulo.precioVenta).toFixed(2);
            }

            recalcularFila(fila);
            recalcularTotalGeneral();
        });

        // Por delegación, para que una línea agregada después se comporte igual que las que ya
        // estaban. `input` y no `change`: el total acompaña lo que se está tipeando.
        document.addEventListener('input', function (evento) {
            if (evento.target.matches('[data-cantidad], [data-precio-unitario]')) {
                recalcularFila(filaDe(evento.target));
                recalcularTotalGeneral();
            }
        });

        // RF-020j: líneas a demanda. La pantalla abre con una sola y el usuario agrega las que
        // necesite. La línea clonada no necesita cableado propio —la búsqueda, la Descripción y la
        // sugerencia trabajan por delegación—, pero sí una numeración secuencial: el binding del
        // modelo corta la lista en el primer índice que falte.
        var boton = document.querySelector('[data-agregar-linea]');
        var plantilla = document.getElementById('plantillaLineaDetalle');

        if (boton && plantilla) {
            boton.addEventListener('click', function () {
                var cuerpo = document.querySelector('#detalle tbody');
                var indice = cuerpo.querySelectorAll('tr').length;

                cuerpo.insertAdjacentHTML(
                    'beforeend', plantilla.innerHTML.split('__i__').join(indice));

                recalcularTotalGeneral();
            });
        }

        recalcularTodo();
    });
})();
