using System.Security.Cryptography;
using System.Text;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T7.4 - el identificador NO ADIVINABLE con el que un tercero sin cuenta consulta
// su documento. Art. 18.10, y el Art. 18.9 por la via del enlace.
//
// EL CODIGO SE DERIVA, NO SE GUARDA. Es `HMAC-SHA256(secreto, "tipo:id")`
// truncado, pegado al id: `d129.AbC...`. El id viaja a la vista para que la
// consulta sea directa, y la firma es lo que impide fabricarlo.
//
// POR QUE DERIVADO Y NO UNA COLUMNA. Tres razones concretas:
//
//  1. `FED_DOCUMENTO` no admite UPDATE (D-20). Una columna de token habria que
//     escribirla en la emision, y eso obliga a tocar los tres caminos de emision
//     -factura, nota y guia- mas el del comprobante, que ni siquiera vive en esa
//     tabla.
//  2. Funciona hacia atras. Los documentos ya emitidos tienen enlace desde el
//     primer despliegue, sin migracion.
//  3. Es estable. El Art. 18.7 obliga a conservar diez anos; un enlace impreso en
//     un correo de 2026 tiene que seguir abriendo en 2036, y un token guardado es
//     un token que alguien puede borrar por error.
//
// LO QUE SE PIERDE, dicho: un codigo derivado NO SE PUEDE REVOCAR uno por uno.
// Rotar el secreto invalida todos. Para un documento fiscal que la norma manda a
// conservar y a poner a disposicion del receptor, la revocacion individual no es
// una funcion deseable: el documento no deja de existir.
//
// ENUMERAR ES IMPOSIBLE SIN EL SECRETO, que es lo que pide la tarea: conocer el
// id 129 no permite construir el codigo de 130. Y la comparacion es en tiempo
// constante, para no filtrar el prefijo correcto por diferencia de tiempos.
public static class EnlacePublico
{
    // El secreto vive en configuracion, como los datos de la imprenta. Vacio por
    // defecto: sin el, la superficie publica se apaga entera en vez de emitir
    // codigos que cualquiera pueda reproducir.
    public const string ClaveSecreto = "settings:FedEnlaceSecreto";

    // La base publica del portal, para armar la URL que va en el correo.
    public const string ClaveBaseUrl = "settings:FedPortalUrl";

    public const string TipoDocumento = "d";
    public const string TipoRetencion = "r";

    private const int LargoFirma = 22;

    public static bool Configurado(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config[ClaveSecreto]);

    public static string Codigo(IConfiguration config, string tipo, long id)
    {
        string secreto = config[ClaveSecreto] ?? string.Empty;

        return $"{tipo}{id}.{Firmar(secreto, tipo, id)}";
    }

    // La URL que se le manda al usuario final. Si no hay portal configurado
    // devuelve solo el codigo: el correo dira como usarlo en vez de mentir con un
    // enlace que no lleva a ninguna parte.
    public static string Url(IConfiguration config, string tipo, long id)
    {
        string baseUrl = (config[ClaveBaseUrl] ?? string.Empty).Trim().TrimEnd('/');
        string codigo = Codigo(config, tipo, id);

        return baseUrl.Length > 0 ? $"{baseUrl}/fed/consulta?c={codigo}" : codigo;
    }

    // Devuelve el tipo y el id solo si la firma es valida. Cualquier codigo mal
    // formado, con firma incorrecta o de un secreto viejo cae en false, y quien
    // llama responde lo mismo que ante un documento inexistente: no se distingue
    // "no existe" de "no tenes permiso", que es lo que evita el sondeo.
    public static bool Verificar(IConfiguration config, string? codigo, out string tipo, out long id)
    {
        tipo = string.Empty;
        id = 0;

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return false;
        }

        string secreto = config[ClaveSecreto] ?? string.Empty;

        if (secreto.Length == 0)
        {
            return false;
        }

        string limpio = codigo.Trim();
        int punto = limpio.IndexOf('.');

        if (punto < 2 || punto == limpio.Length - 1)
        {
            return false;
        }

        string cabeza = limpio[..punto];
        string firma = limpio[(punto + 1)..];
        string tipoLeido = cabeza[..1];

        if (tipoLeido != TipoDocumento && tipoLeido != TipoRetencion)
        {
            return false;
        }

        if (!long.TryParse(cabeza[1..], out long idLeido) || idLeido <= 0)
        {
            return false;
        }

        // Comparacion en tiempo constante: comparar con == cortaria en el primer
        // caracter distinto y filtraria cuanto prefijo acerto quien prueba.
        var esperada = Encoding.UTF8.GetBytes(Firmar(secreto, tipoLeido, idLeido));
        var recibida = Encoding.UTF8.GetBytes(firma);

        if (recibida.Length != esperada.Length || !CryptographicOperations.FixedTimeEquals(recibida, esperada))
        {
            return false;
        }

        tipo = tipoLeido;
        id = idLeido;

        return true;
    }

    private static string Firmar(string secreto, string tipo, long id)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        byte[] firma = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{tipo}:{id}"));

        // base64url: sin '+', '/' ni '=' , para que el codigo entre en una URL y
        // en un correo sin escaparse.
        return Convert.ToBase64String(firma)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=')[..LargoFirma];
    }
}
