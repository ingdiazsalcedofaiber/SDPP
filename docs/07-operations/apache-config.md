# Apache — configuración de borde

La configuración real vive en [`deploy/apache/sdpp.conf`](../../deploy/apache/sdpp.conf) — este
documento explica el porqué de cada decisión, no repite el archivo.

## Rol de Apache en la arquitectura

Apache es el **único** punto de entrada desde la intranet. No reemplaza nada que ya existía:

```
Navegador --https--> Apache :443 --> nginx :5090 (frontend ya existente, sin cambios)
                                 \--> Gateway/YARP :5080 (API ya existente, sin cambios)
```

nginx sigue sirviendo el SPA compilado con su propia configuración ya afinada (fallback de SPA,
corrección de MIME para el worker de pdf.js, cache de assets — ver `frontend/sdpp-web/nginx.conf`).
El Gateway sigue siendo dueño de CORS, rate limiting, CSRF y el ruteo hacia los 5 módulos. Apache no
duplica ninguna de esas responsabilidades — solo termina TLS y decide, por path, a cuál de los dos
reenviar.

## Por qué Apache y no un tercer nginx

El pedido original fue explícitamente Apache como servidor web/reverse proxy del lado de Windows
Server; no había ninguna razón técnica para reemplazar el nginx que ya sirve el frontend (funciona,
está probado, tiene sus propios ajustes finos) — así que Apache se agrega como capa adicional, nunca
en reemplazo.

## Módulos requeridos

`mod_proxy`, `mod_proxy_http`, `mod_ssl`, `mod_headers`, `mod_rewrite`, `mod_deflate`. En Apache para
Windows no existe `a2enmod`; se habilitan editando las líneas `LoadModule` de `httpd.conf`
directamente (o el `Include` que trae la instalación de Apache Lounge).

## Headers de seguridad — por qué se repiten en dos capas

El Gateway ya aplica `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
`Content-Security-Policy` y (fuera de Development) `Strict-Transport-Security` en cada respuesta que
emite (ver `SDPP.BuildingBlocks.Infrastructure.Security.UseSdppSecurityHeaders`, compartida también
por los 5 módulos backend). Apache los vuelve a fijar con `Header always set` (que sobrescribe, no
agrega) — no porque el Gateway esté mal, sino porque Apache es la capa donde TLS realmente termina;
si el día de mañana algo llega a Apache sin pasar por el Gateway (un error de configuración, un
nuevo path), igual sale con esos headers puestos. Nunca deberían verse dos valores del mismo header
en una respuesta real — si eso ocurre, es señal de que esta config y la del Gateway se desalinearon
y hay que revisarlas juntas.

## Límite de tamaño de request

`LimitRequestBody` en `sdpp.conf` debe mantenerse igual al `MaxRequestBodySize` que Documents.Api y
Gateway configuran en su propio Kestrel (ver sus `Program.cs` — hoy 200 MB). Si alguno de los tres
cambia sin que los otros dos se actualicen igual, el más pequeño gana silenciosamente y se reproduce
el mismo problema de "PDF grande rechazado" que motivó subir el límite de Kestrel originalmente.

## Archivos sensibles

`sdpp.conf` incluye un `LocationMatch` que niega acceso a `.env`, `.git` y `node_modules` por si
alguna vez quedan alcanzables por el path que Apache proxea — defensa en profundidad, ya que en
condiciones normales ninguno de esos paths existe dentro de lo que nginx/Gateway sirven.
