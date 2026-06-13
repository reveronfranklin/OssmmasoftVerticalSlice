namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntBancoGetAllQuery(int UsuarioId, string SearchText = "");

public record CntBancoResponse(int CodigoBanco, string Nombre, string CodigoInterbancario, int? CodigoEmpresa);
