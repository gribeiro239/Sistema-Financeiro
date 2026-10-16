# Tecnologias do Nexo Financeiro

A tabela completa, os pré-requisitos e as instruções de execução estão no [README principal](README.md#stacks-utilizadas).

- **Frontend:** React 19.2, TypeScript 6, Vite 8, HTML5, CSS3, Lucide React e QRCode.
- **Backend:** C#, .NET 10 e ASP.NET Core Web API, organizado em API, Application, Domain e Infrastructure.
- **Persistência:** SQLite e Entity Framework Core 10 com migrations.
- **Autenticação:** cookies HTTP-only, PBKDF2, TOTP e ASP.NET Core Data Protection.
- **Operações:** SMTP, fila de e-mails persistente e backups AES-256-GCM.
- **API:** REST, JSON e OpenAPI em desenvolvimento.
- **Qualidade:** Oxlint, compilador TypeScript, testes do Node.js e verificações de integração em C#.
- **Ferramentas:** Node.js, npm, .NET CLI, EF Core CLI, Git e GitHub Actions.

Versões e dependências exatas são registradas em `frontend/package-lock.json` e nos arquivos `.csproj` do backend.
