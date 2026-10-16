# Frontend — Nexo Financeiro

Interface em português desenvolvida com React 19, TypeScript 6, Vite 8, CSS próprio, Lucide React e QRCode.

## Executar

A partir desta pasta, execute:

```powershell
npm ci
npm run dev
```

Acesse http://localhost:5173. A API deve estar em execução na porta 5090; o Vite encaminha as chamadas `/api`.

## Comandos

| Comando | Finalidade |
| --- | --- |
| `npm run dev` | Servidor de desenvolvimento |
| `npm run lint` | Análise estática com Oxlint |
| `npm test` | Testes do cliente HTTP com o runner do Node.js |
| `npm run typecheck` | Verificação de tipos |
| `npm run build` | Verificação de tipos e build em `dist/` |
| `npm run preview` | Conferência do build estático; requer encaminhar `/api` para testar a integração |

No Windows, use `npm.cmd` quando necessário. Consulte o [README principal](../README.md) para instalação, arquitetura, configuração e testes completos.
