// T142 — Buscador de artículos (RF-034, RF-034a, RF-034b, RF-034c).
//
// Componente encapsulado y único: la pantalla sólo incluye la partial y marca sus campos de
// Código. Todo lo demás vive acá.
//
// Dos decisiones sostienen RF-034b, que exige que elegir un Código desde la ventana dispare
// "exactamente las mismas operaciones" que tipearlo a mano:
//
//   1. La ventana no hace nada especial al aceptar un registro: escribe el Código en el campo y
//      emite el mismo evento `change` que produce el usuario al tipear. No hay una segunda ruta.
//   2. La resolución del Código —una sola consulta, la de `Articulo/Buscar?codigo=`— se hace acá y
//      su resultado se publica como el evento `articulo:resuelto`. Quien necesite algo más del
//      artículo (por ejemplo, sugerir el Precio Unitario) lo escucha en vez de volver a consultar,
//      de modo que lo que se muestra y lo que se sugiere no puedan salir de fuentes distintas.
(function () {
    'use strict';

    var RUTA = '/Articulos/Buscar';

    // Campo al que irá el Código que se elija en la ventana.
    var destino = null;

    function consultar(parametros) {
        return fetch(RUTA + '?' + parametros, {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' }
        }).then(function (respuesta) {
            // Sin sesión, el servidor redirige al login y la respuesta no es JSON: no hay nada que
            // mostrar y tampoco es un error que valga interrumpir la carga.
            return respuesta.ok && respuesta.headers.get('content-type')
                && respuesta.headers.get('content-type').indexOf('application/json') >= 0
                ? respuesta.json()
                : null;
        }).catch(function () {
            return null;
        });
    }

    /// Único punto por el que un Código entra a un campo, venga de la ventana o del teclado.
    function establecerCodigo(campo, codigo) {
        campo.value = codigo;
        campo.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function descripcionDe(campo) {
        return document.querySelector('[data-descripcion-de="' + campo.id + '"]');
    }

    /// Resuelve el Código vigente del campo y avisa a quien lo esté esperando.
    function resolver(campo) {
        var codigo = (campo.value || '').trim();
        var rotulo = descripcionDe(campo);

        function publicar(articulo) {
            if (rotulo) {
                rotulo.textContent = articulo ? articulo.descripcion : '';
            }

            campo.dispatchEvent(new CustomEvent('articulo:resuelto', {
                bubbles: true,
                detail: { campo: campo, articulo: articulo }
            }));
        }

        if (!codigo) {
            publicar(null);
            return;
        }

        consultar('codigo=' + encodeURIComponent(codigo)).then(function (datos) {
            publicar(datos && datos.filas && datos.filas.length ? datos.filas[0] : null);
        });
    }

    function pintarResultados(datos) {
        var cuerpo = document.getElementById('buscadorArticulosResultados');
        var aviso = document.getElementById('buscadorArticulosAviso');

        cuerpo.innerHTML = '';
        aviso.classList.toggle('d-none', !(datos && datos.truncado));

        if (!datos) {
            return;
        }

        datos.filas.forEach(function (fila) {
            var tr = document.createElement('tr');
            tr.style.cursor = 'pointer';

            [fila.codigo, fila.descripcion].forEach(function (texto) {
                var td = document.createElement('td');
                td.textContent = texto;
                tr.appendChild(td);
            });

            tr.addEventListener('click', function () {
                if (destino) {
                    establecerCodigo(destino, fila.codigo);
                }

                cerrar();
            });

            cuerpo.appendChild(tr);
        });
    }

    function ventana() {
        return document.getElementById('buscadorArticulos');
    }

    function abrir(campo) {
        destino = campo;

        var modal = bootstrap.Modal.getOrCreateInstance(ventana());
        modal.show();
    }

    function cerrar() {
        var modal = bootstrap.Modal.getInstance(ventana());

        if (modal) {
            modal.hide();
        }
    }

    function buscar() {
        var descripcion = document.getElementById('buscadorArticulosDescripcion').value || '';

        // Una Descripción vacía no acota, pero tampoco libera del tope: el servidor devuelve como
        // máximo 10.000 filas y avisa del recorte (RF-034a).
        consultar('descripcion=' + encodeURIComponent(descripcion)).then(pintarResultados);
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.addEventListener('click', function (evento) {
            var boton = evento.target.closest('[data-buscador-destino]');

            if (!boton) {
                return;
            }

            var campo = document.getElementById(boton.getAttribute('data-buscador-destino'));

            if (campo) {
                abrir(campo);
            }
        });

        var botonBuscar = document.getElementById('buscadorArticulosBuscar');

        if (botonBuscar) {
            botonBuscar.addEventListener('click', buscar);
        }

        // El campo se observa por delegación: las líneas de detalle pueden agregarse después de
        // cargada la página y tienen que comportarse igual que las que ya estaban.
        document.addEventListener('change', function (evento) {
            if (evento.target.matches('[data-articulo-codigo]')) {
                resolver(evento.target);
            }
        });

        // Descripción del Código que ya viene cargado (una edición, o un formulario devuelto con
        // su rechazo): la pantalla tiene que mostrarla desde el vamos.
        document.querySelectorAll('[data-articulo-codigo]').forEach(function (campo) {
            if ((campo.value || '').trim()) {
                resolver(campo);
            }
        });
    });
})();
