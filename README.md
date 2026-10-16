# Nexo Financeiro

Sistema de gestão financeira com interface em português, desenvolvido com **React, TypeScript e ASP.NET Core**. Organiza contas, receitas, despesas, conciliação bancária e planejamento em espaços pessoais ou empresariais.

## Funcionalidades

- Dashboard com saldos, indicadores, alertas e visão do resultado mensal.
- Contas financeiras, movimentações, transferências e estorno com histórico.
- Contas a pagar e receber, baixas parciais, parcelamento e recorrência.
- Conciliação por arquivos OFX ou CSV, revisão antes da importação, sugestões e controle de duplicidades.
- Categorias, regras de categorização, centros de custo e rateio de lançamentos.
- Orçamentos mensais, metas, fluxo de caixa, DRE e comparativo orçado × realizado.
- Clientes e fornecedores, anexos e trilha de auditoria.
- Cadastro, login, recuperação de senha e autenticação em dois fatores (TOTP).
- Espaços isolados, convites de equipe e permissões por usuário.
- Central de ajuda, chamados e administração da plataforma.
- Backups criptografados e fila persistente de e-mails com tentativas de reenvio.

## Stacks utilizadas

| Camada | Tecnologias | Uso no projeto |
| --- | --- | --- |
| Frontend | React 19.2, TypeScript 6, Vite 8 | Interface, tipagem e desenvolvimento/build |
| Estilização | HTML5 e CSS3 | Layout responsivo e estilos próprios |
| Interface | Lucide React e QRCode | Ícones e QR Codes para configurar o segundo fator |
| Backend | C#, .NET 10 e ASP.NET Core Web API | API REST com controllers e regras de aplicação |
| Persistência | Entity Framework Core 10 e SQLite | ORM, migrations e banco de dados local |
| Autenticação | Cookies HTTP-only, PBKDF2, TOTP e ASP.NET Core Data Protection | Sessões, senhas e proteção de segredos |
| Operações | SMTP, fila de e-mails e AES-256-GCM | Notificações e backups criptografados |
| Documentação da API | Microsoft.AspNetCore.OpenApi | Especificação OpenAPI em desenvolvimento |
| Qualidade | Oxlint, TypeScript e verificações de integração em C# | Análise estática, compilação e testes |
| Ferramentas | Node.js, npm, .NET CLI, Git e GitHub Actions | Dependências, versionamento e integração contínua |

As versões completas das dependências estão em [frontend/package.json](frontend/package.json) e nos arquivos de projeto do [backend](backend/src). O frontend usa CSS próprio.

## Pré-requisitos

- **Node.js 24 LTS** recomendado; o projeto aceita Node.js a partir de 22.12.
- **npm**, incluído no Node.js.
- **SDK .NET 10**. O arquivo `global.json` aceita as versões de atualização da série 10.0.
- **Git** para clonar e versionar.

O SQLite é utilizado pela aplicação; não é necessário instalar um servidor de banco de dados.

## Instalação

```powershell
git clone https://github.com/gribeiro239/Sistema-Financeiro.git
cd Sistema-Financeiro
dotnet restore backend/Financeiro.slnx
npm --prefix frontend ci
```

No Windows, use `npm.cmd` se o PowerShell bloquear a execução de `npm.ps1`.

## Executar localmente

Na raiz do projeto, inicie a API:

```powershell
dotnet run --project backend/src/Financeiro.Api --launch-profile http
```

Em outro terminal, também na raiz, inicie a interface:

```powershell
npm --prefix frontend run dev
```

| Serviço | Endereço |
| --- | --- |
| Interface | http://localhost:5173 |
| API / verificação de saúde | http://localhost:5090/api/health |
| OpenAPI (desenvolvimento) | http://localhost:5090/openapi/v1.json |

O Vite encaminha as chamadas `/api` à porta 5090. A porta da interface fica fixada em 5173 para corresponder à configuração local da API.

### Primeiro acesso e dados

Na primeira execução, a API aplica as migrations e cria `backend/src/Financeiro.Api/App_Data/finance.db`, com dados de demonstração do espaço **Studio Norte**. Cadastre sua conta pela interface: o primeiro usuário da instalação recebe o papel **SuperAdmin** e a propriedade do espaço inicial. Recursos privilegiados exigem configurar a autenticação em dois fatores.

Os dados permanecem no SQLite após reiniciar. Bancos locais, anexos, chaves e backups são ignorados pelo Git e não são enviados ao repositório.

## Estrutura

```text
Sistema-Financeiro/
├── .github/workflows/ci.yml        # Validação automática no GitHub
├── backend/
│   ├── .config/dotnet-tools.json   # Ferramenta local do EF Core
│   ├── Financeiro.slnx             # Solução com aplicação e testes
│   ├── src/
│   │   ├── Financeiro.Api/         # HTTP, autenticação e serviços operacionais
│   │   ├── Financeiro.Application/ # Casos de uso, contratos e regras de aplicação
│   │   ├── Financeiro.Domain/      # Entidades e regras de domínio
│   │   └── Financeiro.Infrastructure/ # EF Core, SQLite e migrations
│   └── tests/Financeiro.Operations.Tests/
├── frontend/
│   ├── public/                    # Arquivos públicos
│   ├── src/
│   │   ├── hooks/                 # Consultas assíncronas com cancelamento
│   │   └── lib/                   # Cliente HTTP compartilhado
│   └── package.json
├── docs/API.md                    # Endpoints e regras de funcionamento
├── OPERATIONS.md                  # SMTP, backup e restauração
└── PRODUCT_VISION.md              # Visão e evolução do produto
```

## Validação

Execute na raiz:

```powershell
npm --prefix frontend run lint
npm --prefix frontend test
npm --prefix frontend run build
dotnet build backend/Financeiro.slnx --configuration Release
dotnet run --project backend/tests/Financeiro.Operations.Tests --configuration Release --no-build
```

Os testes do backend são um **executável de integração**, iniciado com `dotnet run`. Eles verificam regras financeiras, arredondamento, saldos, transferências, baixas, rateios, migrations, conciliação, isolamento entre espaços, permissões HTTP, e-mails e backup/restauração. Utilizam dados isolados em `TestData` e um servidor SMTP local, sem enviar e-mails externos.

O GitHub Actions executa lint, testes e build em cada envio à `main` e em pull requests.

## Migrations

A inicialização da API aplica as migrations existentes. Para criar uma alteração de esquema, execute a partir de `backend`:

```powershell
cd backend
dotnet tool restore
dotnet ef migrations add NomeDaMigration --project src/Financeiro.Infrastructure --startup-project src/Financeiro.Api
```

Revise as alterações de esquema e faça backup antes de aplicá-las a uma instalação com dados reais.

## Configuração e publicação

As opções padrão estão em [appsettings.json](backend/src/Financeiro.Api/appsettings.json). Configure credenciais por variáveis de ambiente ou por um gerenciador de segredos; não grave senhas no código.

O envio SMTP começa desativado. Backups automáticos usam uma chave local que deve ser preservada separadamente dos arquivos de backup. Consulte [OPERATIONS.md](OPERATIONS.md) para configurar e-mails, retenção, verificação e restauração.

O comando `npm --prefix frontend run build` gera a interface em `frontend/dist`. O proxy do Vite funciona no servidor de desenvolvimento. Na hospedagem, sirva esses arquivos e encaminhe `/api` para a API sob a mesma origem, configurando HTTPS e `Email__PublicUrl` conforme o ambiente. O envio deste código ao GitHub não realiza a implantação da aplicação.

## Documentação complementar

- [API e regras de funcionamento](docs/API.md)
- [Operações: e-mails, backup e restauração](OPERATIONS.md)
- [Resumo das tecnologias](STACKS.md)
- [Visão do produto](PRODUCT_VISION.md)
