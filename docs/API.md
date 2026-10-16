# API e regras de funcionamento

Consulte o [README](../README.md) para instalar e executar o projeto.

## Endpoints atuais

- `GET /api/health`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/login/2fa`
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`
- `GET /api/auth/me`
- `POST /api/auth/logout`
- `PUT /api/auth/profile`
- `PUT /api/auth/password`
- `POST /api/auth/2fa/setup`
- `POST /api/auth/2fa/enable`
- `DELETE /api/auth/2fa`
- `GET|POST /api/workspaces`
- `PUT /api/workspaces/{workspaceId}`
- `GET /api/workspaces/{workspaceId}/members`
- `GET|POST /api/workspaces/{workspaceId}/invitations`
- `DELETE /api/workspaces/{workspaceId}/invitations/{invitationId}`
- `PUT /api/workspaces/{workspaceId}/members/{memberId}/role`
- `DELETE /api/workspaces/{workspaceId}/members/{memberId}`
- `POST /api/workspaces/{workspaceId}/transfer-ownership`
- `GET /api/workspace-invitations`
- `POST /api/workspace-invitations/{invitationId}/accept`
- `GET /api/dashboard`
- `GET /api/accounts?includeInactive={opcional}`
- `POST /api/accounts`
- `PUT /api/accounts/{accountId}`
- `POST /api/accounts/{accountId}/active`
- `GET /api/transactions`
- `GET /api/cash-flow?days=30`
- `GET /api/commitments`
- `POST /api/transactions`
- `POST /api/transfers`
- `PUT /api/transactions/{transactionId}`
- `PUT /api/transfers/{transferId}`
- `DELETE /api/transactions/{transactionId}` (estorno lógico)
- `POST /api/commitments`
- `PUT /api/commitments/{commitmentId}`
- `POST /api/commitments/{commitmentId}/settlements`
- `POST /api/commitments/{commitmentId}/cancel`
- `GET /api/reconciliation?accountId={accountId}`
- `POST /api/reconciliation/import/preview` (`multipart/form-data`, valida OFX ou CSV sem gravar)
- `POST /api/reconciliation/import/confirm` (`multipart/form-data`, aplica as decisões revisadas em lote)
- `POST /api/reconciliation/import` (`multipart/form-data`, OFX ou CSV)
- `GET /api/reconciliation/history?accountId={accountId}`
- `POST /api/reconciliation/{entryId}/undo` (mantém ou estorna a movimentação criada)
- `POST /api/reconciliation/{entryId}/match`
- `POST /api/reconciliation/{entryId}/create`
- `POST /api/reconciliation/{entryId}/ignore`
- `POST /api/reconciliation/{entryId}/reopen`
- `GET|POST /api/categories`
- `POST /api/categories/{categoryId}/active`
- `GET|POST /api/categorization-rules`
- `PUT|DELETE /api/categorization-rules/{ruleId}`
- `GET /api/categorization-rules/{ruleId}/preview`
- `POST /api/categorization-rules/{ruleId}/apply`
- `GET|POST /api/attachments`
- `GET /api/attachments/counts?entityType={Transaction|Commitment}`
- `GET /api/attachments/{attachmentId}/download`
- `DELETE /api/attachments/{attachmentId}`
- `GET|POST /api/cost-centers`
- `POST /api/cost-centers/{costCenterId}/active`
- `GET /api/allocations?transactionId={transactionId}`
- `PUT /api/transactions/{transactionId}/allocations`
- `GET /api/budgets?year={ano}&month={mes}`
- `PUT /api/budgets`
- `DELETE /api/budgets/{budgetId}`
- `GET /api/reports/dre?year={ano}&month={mes}&costCenterId={opcional}`
- `GET /api/reports/budget-variance?year={ano}&month={mes}&months=6`
- `GET|POST /api/goals`
- `PUT /api/goals/{goalId}`
- `POST /api/goals/{goalId}/active`
- `POST /api/goals/{goalId}/contributions`
- `GET /api/contacts?includeInactive={opcional}`
- `POST /api/contacts`
- `PUT /api/contacts/{contactId}`
- `POST /api/contacts/{contactId}/active`
- `GET /api/audit`
- `GET /api/help/articles?query={opcional}&module={opcional}`
- `GET /api/help/tickets?scope={mine|assigned}`
- `POST /api/help/tickets`
- `POST /api/help/tickets/{ticketId}/messages`
- `POST /api/help/tickets/{ticketId}/status`
- `GET /api/platform/overview` (SuperAdmin ou Support)
- `GET /api/platform/users` (SuperAdmin ou Support)
- `GET /api/platform/workspaces` (SuperAdmin ou Support)
- `PUT /api/platform/users/{userId}/role` (SuperAdmin)
- `POST /api/platform/users/{userId}/active` (SuperAdmin)
- `POST /api/platform/users/{userId}/password-reset` (SuperAdmin ou Support)
- `GET /api/platform/audit` (SuperAdmin ou Support)
- `GET /api/platform/diagnostics` (SuperAdmin ou Developer)
- `GET|PUT /api/platform/settings/{key}` (SuperAdmin ou Developer)

As rotas financeiras aceitam o cabeçalho `X-Workspace-Id`. Quando ele não é informado, a API usa o espaço inicial `Studio Norte`. Cada espaço mantém contas, movimentações, compromissos, orçamentos, metas e relatórios isolados.

Na primeira execução após habilitar a autenticação, crie uma conta pela tela inicial. O primeiro usuário cadastrado se torna proprietário do espaço `Studio Norte` e recebe acesso aos dados já existentes. As senhas são armazenadas com hash PBKDF2 e a sessão usa cookie HTTP-only.

A persistência usa SQLite com Entity Framework Core, chaves estrangeiras e índices. Evoluções de esquema devem usar migrations antes da implantação em produção.

O orçamento mensal aceita limites por categoria de despesa ou centro de custo. O realizado é calculado com os lançamentos do mês; para categorias, a projeção também inclui contas a pagar em aberto no período. Cada limite pode disparar um alerta percentual configurável.

A DRE gerencial consolida os lançamentos confirmados por categoria, compara o período ao mês anterior e permite filtrar pelos valores rateados em cada centro de custo. Categorias configuradas como `Both` são tratadas como transferências internas e ficam fora do resultado.

O comparativo orçado × realizado apresenta valores planejados, confirmados, comprometidos e projetados por categoria ou centro de custo, além do desvio financeiro e da evolução dos últimos 3, 6 ou 12 meses.

As metas financeiras podem acompanhar faturamento, limite de despesas e saldo de caixa diretamente pelos lançamentos, ou receber aportes manuais quando forem do tipo reserva de emergência ou personalizada. O sistema calcula progresso, situação de risco e previsão de conclusão.

Para importar CSV na conciliação, use cabeçalhos `Data`, `Descrição` e `Valor`. A coluna `Id` é opcional, mas melhora a identificação de duplicidades. Valores negativos representam saídas e positivos representam entradas. A interface sempre analisa o arquivo antes da confirmação, mostra linhas inválidas ou duplicadas e limita cada arquivo a 5.000 lançamentos e 10 MB. Na revisão, o sistema pontua possíveis correspondências por conta, valor, data e descrição, aplica regras de categorização e permite conciliar, criar, ignorar ou deixar cada linha pendente.

A Central de Pendências consolida todas as contas, permite filtrar por texto, motivo, período e faixa de valor, identifica possíveis duplicidades e oferece ações em lote para conciliar sugestões, criar movimentações ou ignorar registros. A quantidade pendente também aparece no menu lateral.

O histórico da conciliação registra cada decisão por lançamento, incluindo usuário, data, movimentação vinculada ou criada, categoria, centro de custo e regra aplicada. Conciliações podem ser desfeitas mantendo a movimentação financeira ou, quando ela foi criada pelo importador, estornando seu efeito com preservação da auditoria.

Transferências são registradas por `POST /api/transfers` como duas movimentações atômicas vinculadas pelo mesmo `TransferId`. Elas alteram os saldos das contas de origem e destino, mas não entram como receita, despesa, orçamento, DRE ou progresso de metas.

Compromissos podem ser recorrentes com `recurrenceFrequency` igual a `Weekly`, `Monthly` ou `Yearly` e `recurrenceCount` entre 2 e 60. O valor informado é repetido em cada ocorrência; parcelamento e recorrência não podem ser combinados.

Clientes e fornecedores possuem cadastro próprio e podem ser vinculados aos compromissos. Contatos arquivados continuam preservados no histórico, mas deixam de aparecer para novos vínculos.

A exclusão de uma movimentação realiza um estorno lógico: o registro deixa de compor saldos, relatórios e listagens operacionais, sem ser apagado do banco. Criações, alterações, baixas, cancelamentos, estornos e mudanças de contatos são registradas na trilha de auditoria do espaço.

A recuperação pública de senha usa tokens aleatórios, armazenados apenas como hash, válidos por 20 minutos e de uso único. Em desenvolvimento, o token é devolvido à interface para permitir testes locais. Em produção, deve ser entregue por um provedor de e-mail e não é incluído na resposta pública da API; a emissão administrativa permanece restrita e auditada.

## Central de ajuda e chamados

A Central de ajuda reúne artigos pesquisáveis e chamados com conversa em histórico. Ao abrir um chamado, o usuário escolhe assunto, categoria, prioridade e o destino: administração do próprio espaço, suporte da plataforma ou desenvolvimento.

Chamados destinados à administração ficam visíveis somente para proprietários e administradores do espaço. Chamados de suporte ficam disponíveis aos papéis globais `Support` e `SuperAdmin`; os de desenvolvimento, a `Developer` e `SuperAdmin`. O acesso às filas globais exige autenticação em dois fatores. Usuários acompanham apenas os próprios chamados no espaço selecionado.

Para suporte ou desenvolvimento, o usuário pode autorizar explicitamente o compartilhamento de informações adicionais do espaço para diagnóstico. A autorização é registrada no chamado, e mensagens, mudanças de situação e abertura ficam preservadas na auditoria. As situações disponíveis são aberto, em andamento, aguardando solicitante, resolvido e fechado.

## Administração da plataforma

Os papéis globais `User`, `Support`, `Developer` e `SuperAdmin` são independentes dos papéis `Owner`, `Administrator` e `Member` de cada espaço. O primeiro usuário da instalação recebe `SuperAdmin`; os demais começam como `User`.

O painel Administração permite ao superadministrador gerenciar papéis, bloquear ou reativar contas, iniciar redefinições de senha e consultar usuários, espaços e auditoria privilegiada. O suporte pode consultar esses dados e iniciar redefinições apenas para usuários sem privilégios. Senhas nunca são exibidas ou armazenadas em texto puro.

O painel Área técnica disponibiliza diagnóstico da API, situação das migrations e configurações técnicas permitidas. O papel `Developer` não concede acesso aos dados financeiros de outros espaços nem às funções administrativas de usuários.

Bloqueios, mudanças de papel e redefinições de senha invalidam as sessões existentes por meio de um selo de segurança. A plataforma impede o autobloqueio, a autoalteração de papel e a remoção do último superadministrador ativo. Todas as ações privilegiadas registram ator, data, endereço IP e detalhes da alteração.

Contas `SuperAdmin`, `Support` e `Developer` precisam ativar autenticação em dois fatores antes de acessar recursos privilegiados. O sistema utiliza códigos TOTP compatíveis com aplicativos autenticadores, gera dez códigos de recuperação de uso único e bloqueia o segundo fator por 15 minutos após cinco tentativas inválidas. O segredo TOTP é protegido pelo Data Protection e os códigos de recuperação são armazenados apenas como hash.

## Equipe e permissões do espaço

Proprietários e administradores podem convidar usuários pelo e-mail cadastrado. Convites expiram em sete dias e precisam ser aceitos pelo usuário autenticado com o mesmo endereço. O proprietário pode promover administradores, rebaixar membros, remover acessos e transferir a propriedade; administradores podem convidar e remover apenas membros.

A matriz padrão é:

- `Owner`: controle financeiro e administrativo completo, incluindo transferência de propriedade.
- `Administrator`: criação e alteração de dados financeiros e gestão de membros comuns.
- `Member`: consulta dos dados financeiros em modo somente leitura.

Convites, entradas, remoções, mudanças de papel e transferências de propriedade são registrados na auditoria do espaço.
