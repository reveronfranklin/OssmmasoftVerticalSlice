# Project Instructions

## Project Overview

This repository is an ASP.NET Core Web API using a vertical slice style. The project targets `net10.0`, enables nullable reference types and implicit usings, and uses Oracle stored procedures as the main persistence boundary.

Prefer the existing feature structure over introducing broad architectural layers. Each business capability should live under `Features/<FeatureName>/` with its request/command/query records, handler, controller, shared mapping helpers, SQL scripts, and frontend contract docs when applicable.

## Backend Architecture

- Keep slices self-contained under `Features/<FeatureName>/`.
- Follow the existing pattern of colocating controller and handler in the operation file, for example `OssUsuarioRolGetAll.cs` or `RhDocumentosCreate.cs`.
- Use `record` types for command/query/request DTOs and response DTOs when matching existing code.
- Use `ResultDto<T>` from `OssmmasoftVerticalSlice.Helpers` for API responses.
- Return `Ok(result)` from controllers unless an existing slice establishes a different convention.
- Prefer small feature-specific helper classes such as `OssUsuarioRolDb` or `RhDocumentosDb` for mapping, stored procedure output parsing, and DB value normalization.
- Do not introduce MediatR, repositories, ORM abstractions, AutoMapper, or new architectural frameworks unless explicitly requested.

## Database Access

- Use `ConnectionDB` from `OssmmasoftVerticalSlice.ContextDB` to obtain database connections.
- For features that need the company code, read it from `settings:EmpresaConfig` in configuration via `IConfiguration`; do not require `codigoEmpresa`/`CodigoEmpresa` in frontend request DTOs unless the user explicitly asks for that override.
- Select the connection by domain:
  - `GetSisConnection()` for SIS features.
  - `GetRhConnection()` for RH features.
  - `GetPresupuestoConnection()` for PRE/presupuesto features.
  - Existing SQL Server or generic Oracle helpers only when a slice already uses them or the feature clearly requires them.
- Use `Oracle.ManagedDataAccess.Client` and stored procedures for Oracle operations.
- Set `cmd.CommandType = CommandType.StoredProcedure` and `cmd.BindByName = true`.
- Use explicit Oracle parameter names that match the stored procedure contract.
- Convert optional/null values to `DBNull.Value` through local helper methods instead of ad hoc inline checks.
- Preserve existing database message behavior, including treating both `success` and the legacy `suscces` as success where current slices do so.

## API And Naming Conventions

- Keep namespace format as `OssmmasoftVerticalSlice.Features.<FeatureName>`.
- Use route format `api/<FeatureName>` and operation routes similar to existing slices, such as `GetAll`, `getById`, `create`, `update`, and `delete`.
- Match the naming style already used in the target feature, including Spanish business terminology.
- Keep request properties in PascalCase for C# DTOs.
- Preserve frontend contracts in `ContratoFrontend*.md` when adding or changing API behavior.

## Error Handling

- Catch connection open failures separately when existing slices do so, and return `ResultDto<T>` with `IsValid = false`.
- Use Spanish user-facing messages consistent with nearby code.
- Keep technical exception messages concise, usually `Error técnico: {ex.Message}` or a domain-specific connection message.
- Avoid throwing exceptions from handlers for expected database or validation failures; encode them in `ResultDto<T>`.

## SQL And Documentation Files

- Store stored procedure scripts inside the related feature folder.
- Name SQL files with the existing uppercase stored procedure style, for example `SP_OSS_USR_ROL_GET_ALL.sql`.
- Include installation or migration scripts in the feature folder when needed.
- Add or update feature README, requirement docs, examples, or frontend contract docs when the API surface changes.

## Frontend Context

This repo is the backend slice. The related frontend source is located at `/Users/freveron/Developer/Projects/MM/NextOssmasoft/src`. When asked to create or update frontend contracts, document endpoint route, method, request body, response shape, pagination fields, validation behavior, and example payloads.

## Verification

- For backend changes, run `dotnet build` when feasible.
- If tests are added later, prefer focused tests for the touched slice.
- Do not require a live Oracle database for ordinary compile verification unless the user explicitly asks for integration testing.

## Working Rules For Agents

- Read the closest existing feature before adding a new one, and copy the local style intentionally.
- Keep changes scoped to the requested feature.
- Do not revert or overwrite unrelated user changes.
- Avoid broad refactors unless they are required for the requested behavior.
- Keep generated code and docs in ASCII unless the edited file already uses non-ASCII text or Spanish accents are necessary for user-facing documentation.
