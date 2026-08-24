# Contrato frontend - Historial de multas

- `POST /api/CatastroMultas/getByInmueble`
- Request: `{ "codigoInmueble": 123, "codigoContribuyente": 456 }`
- Devuelve multas ya registradas; no asigna numero, no inserta y no hace commit.

La emision queda fuera de este contrato. Los RDF legados mutan la base de datos durante la generacion, usan `CAT_S_MULTAS.NEXTVAL`, insertan en `CAT_MULTAS` y contienen textos legales distintos por variante. Esa operacion requerira un comando transaccional idempotente, seleccion explicita de fundamento legal y aprobacion funcional/juridica.
