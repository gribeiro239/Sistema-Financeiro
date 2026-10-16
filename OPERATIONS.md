# Backups e envio de e-mails

## Backup automático

A API cria um backup na primeira execução sem backup recente e depois a cada 24 horas enquanto estiver em execução. Verifica o agendamento a cada cinco minutos. Mantém os 14 arquivos mais recentes. Essas opções ficam em `Backups` no `appsettings.json` ou em variáveis de ambiente `Backups__IntervalHours`, `Backups__RetentionCount` e `Backups__Directory`.

O backup inclui um snapshot consistente do SQLite, os anexos referenciados pelo banco e as chaves Data Protection usadas para autenticação e segundo fator. Arquivos são conferidos por SHA-256, o SQLite passa por `integrity_check` e `foreign_key_check`, e os anexos são conferidos por existência e tamanho. O arquivo só é publicado e a retenção só é aplicada depois da verificação.

Os arquivos `.nexobak` usam AES-256-GCM. Na primeira criação, uma chave aleatória de 32 bytes é gravada em `App_Data/Backup-Key.key`. **Preserve uma cópia segura dessa chave fora do servidor e separada dos backups. Sem ela, os backups não podem ser restaurados.** O arquivo de chave não entra no backup. Como alternativa, configure uma chave de 32 bytes em Base64 pela variável `Backups__EncryptionKey`; use a mesma chave para verificar e restaurar. Não altere a chave enquanto depender dos arquivos anteriores.

O diretório padrão é local. Copie os backups para armazenamento externo para proteger contra perda do servidor. Restrinja o acesso do sistema operacional à conta que executa a API tanto para a chave quanto para os arquivos de trabalho. O limite padrão do arquivo compactado é 512 MB; a criptografia usa memória proporcional ao tamanho. Para bases maiores, dimensione memória ou substitua esse mecanismo por um processo de backup em streaming.

Superadministradores com segundo fator encontram o painel **Backups e e-mails** na Administração. Podem criar, baixar e verificar arquivos. Essas operações são auditadas. O backup contém dados de todos os espaços; suporte e desenvolvedores não têm acesso a ele.

## Verificação e restauração

Execute na raiz `sistema-financeiro`. Os comandos offline não aplicam migrations nem dados de demonstração ao banco existente:

```powershell
dotnet run --project backend/src/Financeiro.Api --no-launch-profile -- --contentRoot "$PWD/backend/src/Financeiro.Api" --backup-create
dotnet run --project backend/src/Financeiro.Api --no-launch-profile -- --contentRoot "$PWD/backend/src/Financeiro.Api" --backup-verify "C:/caminho/nexo-arquivo.nexobak"
dotnet run --project backend/src/Financeiro.Api --no-launch-profile -- --contentRoot "$PWD/backend/src/Financeiro.Api" --backup-restore "C:/caminho/nexo-arquivo.nexobak" --target "C:/caminho/instalacao-restaurada"
```

A restauração exige um destino que ainda não exista. Ela valida o conteúdo em uma pasta temporária e só publica a pasta final depois que todas as verificações passam. Nunca restaura por cima da instalação ativa.

O destino contém `App_Data/finance.db`, `App_Data/Attachments` e `App_Data/DataProtection-Keys`. Para uma recuperação operacional:

1. Restaure em uma pasta nova usando a chave original de backup.
2. Configure uma instância isolada da mesma versão da API para usar o destino como `--contentRoot` e `Database__File=App_Data/finance.db`. Mantenha `Email__Enabled=false` nessa conferência para não reenviar avisos restaurados.
3. Confira saldos, usuários, anexos e autenticação em dois fatores.
4. Pare a instalação original antes de promover o diretório restaurado. Preserve o diretório antigo para reversão e mantenha o segredo de backup disponível na instalação recuperada.
5. Reative e-mails após revisar a fila: SMTP tem entrega no mínimo uma vez, e restaurar uma fila antiga pode repetir mensagens que o servidor já recebeu. Tokens expirados não são enviados.

Não há endpoint HTTP para sobrescrever/restaurar a instalação ativa.

## SMTP

O envio externo começa desativado. Recuperações e convites criam mensagens na fila persistente, junto com seus respectivos registros, no mesmo commit. O conteúdo da fila é protegido por Data Protection. Falhas têm até seis tentativas com intervalo crescente; mensagens expiradas não são enviadas. Após o envio, o conteúdo sensível é removido. Metadados antigos são limpos após sete dias da expiração.

Configure as variáveis na conta/processo que inicia a API, sem gravar senhas no repositório:

```powershell
$env:Email__Enabled = 'true'
$env:Email__Host = 'smtp.seu-provedor.com'
$env:Email__Port = '587'
$env:Email__StartTls = 'true'
$env:Email__FromAddress = 'financeiro@seu-dominio.com'
$env:Email__FromName = 'Nexo Financeiro'
$env:Email__Username = 'usuario-smtp'
# Defina Email__Password pelo gerenciador de segredos do ambiente.
$env:Email__PublicUrl = 'https://financeiro.seu-dominio.com'
```

Reinicie a API depois de configurar. O adaptador usa SMTP com STARTTLS, normalmente na porta 587; não suporta TLS implícito na porta 465 nem OAuth SMTP. Use credenciais SMTP compatíveis com seu provedor. Produção exige HTTPS na URL pública e TLS/credenciais no envio. Mesmo com SMTP desativado, configure `Email__PublicUrl` com o endereço HTTPS correto em produção.

O e-mail de recuperação inclui um link e um código válidos por 20 minutos. O token fica no fragmento da URL e é removido do endereço quando a tela de recuperação abre. A resposta pública de produção não revela token, prazo ou existência da conta. Desenvolvimento conserva o código na interface para testes locais. Convites exigem login com o mesmo e-mail e aceitação em Equipe e acessos; a mensagem não concede acesso por si só.

O painel mostra SMTP ativado/desativado e mensagens pendentes, enviadas, expiradas ou com tentativas esgotadas. Aceitação pelo SMTP não garante entrega na caixa de entrada; consulte também o provedor.

## Validação

```powershell
dotnet build backend/Financeiro.slnx --configuration Release
dotnet run --project backend/tests/Financeiro.Operations.Tests --configuration Release --no-build
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend test
npm.cmd --prefix frontend run build
```

Os testes usam um banco e pastas isolados em `backend/tests/Financeiro.Operations.Tests/TestData`, sem alterar o banco usado pela aplicação. Cobrem restauração financeira, anexos, chaves, retenção, arquivo adulterado, chave incorreta, dados ausentes, fila transacional, privacidade de tokens, falhas/reenvio e um receptor SMTP local. Não enviam e-mails externos.
