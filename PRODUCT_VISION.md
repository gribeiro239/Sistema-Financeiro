# Visão de produto — Sistema Financeiro

Pesquisa e proposta inicial, revisadas em 10 de setembro de 2026.

## 1. Direção recomendada

Construir uma plataforma financeira modular para MEIs, profissionais autônomos e pequenas empresas brasileiras. A promessa central é simples:

> Saber quanto tenho, quanto posso gastar e como estará meu caixa — sem depender de planilhas.

O produto deve começar como uma camada de controle e inteligência, sem movimentar dinheiro. Isso reduz risco regulatório e permite validar o valor do produto antes de integrar pagamentos, cartões ou emissão fiscal.

O diferencial sugerido é trabalhar com **espaços financeiros**. Uma mesma pessoa pode ter um espaço Pessoal, um espaço Empresa e, no futuro, espaços para projetos ou clientes. Cada espaço mantém contas, categorias, orçamento e relatórios separados; a visão consolidada só aparece mediante permissão explícita.

## 2. Referências fortes de mercado

| Referência | O que faz muito bem | O que aproveitar como princípio |
| --- | --- | --- |
| [Conta Azul](https://contaazul.com/funcionalidades/gestao-financeira/) | Pagar/receber, cobrança, conciliação, fluxo de caixa, DRE e conexão com contador | Uma visão operacional clara do que venceu, vence hoje e ainda vence no mês |
| [Omie](https://www.omie.com.br/funcionalidades/controle-financeiro-empresarial/) | Integra financeiro, vendas, estoque, fiscal, conta PJ e automações | Arquitetura de módulos ligados por eventos do negócio |
| [QuickBooks](https://quickbooks.intuit.com/oidam/intuit/sbseg/en_au/quickbooks-online/web/content/QuickBooks_SMB_user_manual-2022.pdf) | Dashboard, banco, despesas, faturamento, caixa, projetos, impostos e apps | Navegação orientada a tarefas e ecossistema extensível |
| [Xero](https://www.xero.com/my/accounting-software/) | Importação bancária, conciliação, faturamento e relatórios em tempo real | Conciliação como rotina principal, não como ferramenta escondida |
| [Ramp](https://ramp.com/products) | Controles de gasto, despesas, fornecedores, aprovações e automação contábil | Políticas e aprovações antes do gasto; automação de comprovantes depois dele |
| [YNAB](https://www.ynab.com/features) | Planejamento baseado em prioridades, metas e quitação de dívidas | Mostrar ao usuário o que o dinheiro precisa fazer, não apenas onde foi gasto |
| [Monarch Money](https://www.monarchmoney.com/features/tracking) | Patrimônio, transações, recorrências, investimentos e dashboard personalizável | Visão consolidada e widgets que cada pessoa pode organizar |
| [Open Finance — Banco Central](https://www.bcb.gov.br/estabilidadefinanceira/openfinance/documentacao) | Compartilhamento padronizado e consentido de dados financeiros | Conexão bancária transparente, revogável e com escopo explícito |

Não devemos reproduzir identidade visual, textos ou fluxos proprietários. As referências servem para extrair padrões de produto já validados.

## 3. O produto como conjunto de aplicações

### Núcleo compartilhado

- Autenticação, organizações, espaços financeiros e membros.
- Contas: banco, caixa, cartão, carteira e conta de investimento.
- Categorias, tags, centros de custo e contatos.
- Livro de transações com histórico de alterações.
- Anexos, comentários, notificações e trilha de auditoria.
- Importadores CSV e OFX; integração bancária entra depois por um adaptador.

### Aplicações iniciais

1. **Hoje** — saldo disponível, entradas e saídas do mês, contas vencidas, próximos sete dias e alertas acionáveis.
2. **Movimentações** — busca, filtros, divisão de lançamento, transferência entre contas, anexos e categorização em lote.
3. **Caixa** — realizado versus previsto e projeções de 30, 60 e 90 dias.
4. **Pagar e receber** — parcelas, recorrência, baixa parcial, atraso, lembretes e visão em calendário.
5. **Orçamento e metas** — limites por categoria, progresso, reservas e metas com data.
6. **Relatórios** — receitas e despesas, resultado gerencial, categorias, centros de custo e comparações entre períodos.

### Aplicações posteriores

- Clientes, propostas, faturamento e cobrança.
- Fornecedores, despesas, reembolsos e aprovações.
- Assinaturas e detecção de cobranças recorrentes.
- Patrimônio, investimentos e dívidas.
- Estoque, compras e vendas para quem realmente precisar de ERP.
- Portal do contador/consultor com acesso delegado.
- Assistente financeiro que explica números, aponta anomalias e simula cenários; nunca toma decisões ou movimenta dinheiro sem confirmação explícita.

## 4. MVP viável

O primeiro lançamento deve provar três resultados:

- O usuário enxerga sua posição financeira em menos de cinco minutos.
- Consegue saber se faltará caixa nos próximos 30 dias.
- Mantém pagar/receber atualizado com menos esforço que em uma planilha.

Escopo sugerido:

- Um usuário e múltiplos espaços financeiros.
- Dashboard responsivo com dados de demonstração e estados vazios úteis.
- Cadastro de contas e saldos iniciais.
- Receitas, despesas, transferências, parcelas e recorrências.
- Importação CSV/OFX com prevenção de duplicidade.
- Categorias e centros de custo.
- Pagar/receber com estados previsto, vencido, pago/recebido e cancelado.
- Fluxo de caixa realizado e projetado.
- Orçamento mensal simples.
- Relatórios básicos e exportação CSV.
- Auditoria dos eventos financeiros essenciais.

Ficam fora do MVP: Pix, movimentação bancária, cartão próprio, crédito, emissão fiscal, folha, contabilidade oficial, recomendação de investimento e marketplace financeiro.

## 5. Experiência principal

### Dashboard “Hoje”

A hierarquia deve responder, nesta ordem:

1. **Posição:** saldo total e saldo disponível.
2. **Urgência:** vencidos e vencendo hoje.
3. **Futuro:** menor saldo projetado nos próximos 30 dias.
4. **Desempenho:** receitas, despesas e resultado do mês.
5. **Ação:** conciliar, cobrar, pagar ou revisar lançamentos.

Em vez de muitos gráficos decorativos, usar poucos componentes acionáveis:

- cartões de números com comparação ao mês anterior;
- linha de caixa realizado/projetado;
- lista curta de pendências priorizadas;
- distribuição de despesas com acesso ao detalhe;
- botão global “Novo” para receita, despesa e transferência.

### Princípios de interface

- Português do Brasil e valores em `pt-BR` desde o início.
- Verde significa entrada/saúde; vermelho é reservado para problema ou saída crítica.
- Toda métrica deve abrir os lançamentos que a compõem.
- Mostrar origem, atualização e competência dos dados.
- Ocultar valores com um clique e não vazar números em notificações.
- Uma ação financeira destrutiva sempre pede confirmação e informa o impacto.
- Desktop eficiente para operação; mobile focado em consulta e registro rápido.

## 6. Modelo de domínio inicial

```text
User ──< Membership >── Workspace
                         ├── Account ──< Transaction
                         ├── Category
                         ├── CostCenter
                         ├── Contact
                         ├── PayableReceivable
                         ├── RecurrenceRule
                         ├── Budget
                         └── Goal
```

Decisões importantes:

- Dinheiro deve ser armazenado como decimal e moeda, nunca `float`.
- Transferência é um único fato de negócio com duas pernas vinculadas.
- Saldo é derivado de saldo inicial mais transações confirmadas; snapshots podem otimizar leitura.
- Data de competência, vencimento e liquidação são conceitos separados.
- Uma importação precisa de chave de idempotência para impedir duplicação.
- Exclusão financeira deve preferir cancelamento/estorno e preservar auditoria.
- Todas as entidades pertencem a um `WorkspaceId`; autorização sempre valida esse limite.

## 7. Arquitetura compatível com o repositório atual

O projeto já possui React/Vite no frontend e uma solução .NET 10 dividida em API, Application, Domain e Infrastructure. A direção mais segura é um **monólito modular**, não microserviços neste estágio.

- `Financeiro.Domain`: entidades, value objects, regras e eventos de domínio.
- `Financeiro.Application`: casos de uso, comandos, consultas, validação e portas.
- `Financeiro.Infrastructure`: banco de dados, autenticação, arquivos e provedores externos.
- `Financeiro.Api`: HTTP, autorização, OpenAPI, tratamento de erros e observabilidade.
- `frontend`: shell da plataforma e módulos carregados por rota.

Criar contratos como `IBankDataProvider` permite começar com dados manuais/OFX e incluir um agregador de Open Finance depois sem contaminar o domínio.

## 8. Open Finance e segurança

O Banco Central informa que o compartilhamento ocorre somente com autorização do cliente, para a instituição escolhida, e pode ser cancelado. Portanto, a tela de conexões deverá mostrar instituição, escopo, data de expiração, última sincronização e ação de revogação. Para acelerar a validação, um agregador com sandbox e webhooks pode ser usado; a [Belvo documenta](https://developers.belvo.com/pt-br/products/aggregation_brazil/aggregation-brazil-integration-widget) esse modelo e recomenda iniciar em sandbox.

Requisitos mínimos desde a primeira versão:

- criptografia em trânsito e em repouso;
- MFA opcional no MVP e obrigatório para perfis administrativos depois;
- sessões revogáveis, rate limiting e proteção contra enumeração;
- segregação por espaço e autorização por função;
- log de auditoria imutável para mudanças financeiras e permissões;
- backup testado e procedimento de restauração;
- coleta mínima, retenção definida e exportação/exclusão de dados;
- política de privacidade e consentimentos versionados.

A LGPD exige finalidade, necessidade, transparência, segurança, prevenção e prestação de contas, conforme a [síntese oficial dos princípios](https://www.gov.br/saude/pt-br/acesso-a-informacao/lgpd/principios). Se o produto futuramente armazenar ou processar dados de cartão, o escopo deve ser desenhado com base no [PCI DSS](https://www.pcisecuritystandards.org/standards/pci-dss/) — preferencialmente terceirizando a captura para não armazenar dados sensíveis de cartão.

## 9. Sequência de entrega

### Fase 0 — fundação e protótipo

- Definir nome provisório, público inicial e linguagem visual.
- Construir o shell navegável e o dashboard com dados realistas.
- Validar o protótipo com 5 a 8 usuários do público-alvo.

### Fase 1 — controle financeiro

- Espaços, contas, categorias e movimentações.
- Pagar/receber, recorrência e projeção de caixa.
- Importação CSV/OFX e relatórios básicos.

### Fase 2 — colaboração e automação

- Membros, papéis, aprovações e auditoria completa.
- Regras de categorização, anexos e lembretes.
- Conexão Open Finance em sandbox e depois em produção.

### Fase 3 — expansão por demanda

- Clientes/cobranças ou despesas/aprovações, escolhendo apenas o módulo mais solicitado.
- Investimentos e patrimônio para espaços pessoais.
- Integrações, API pública e marketplace de módulos somente após uso recorrente do núcleo.

## 10. Métricas para decidir se vale expandir

- Tempo até cadastrar/importar a primeira conta.
- Percentual de usuários que conclui a organização inicial.
- Percentual de transações categorizadas e conciliadas.
- Usuários que consultam a projeção de caixa semanalmente.
- Contas vencidas resolvidas a partir de um alerta.
- Retenção semanal e mensal por tipo de espaço.
- Número de correções manuais após importação.
- Módulo mais solicitado por usuários ativos, não apenas por visitantes.

## 11. Próxima decisão

Antes de desenvolver todo o backend, criar um protótipo funcional do shell, Dashboard Hoje e Movimentações com dados simulados. Ele permitirá validar posicionamento, densidade de informação e navegação. O primeiro fluxo completo deve ser:

> criar espaço → cadastrar/importar conta → revisar transações → ver projeção → resolver uma pendência.
