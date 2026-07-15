# FEAT-09: Cognito e Parameter Store sob Terraform

## Objetivo

Eliminar o descasamento entre a infraestrutura declarada em código
(Terraform) e a infraestrutura real da conta AWS, trazendo para dentro
do Terraform os recursos que hoje existem apenas de forma manual
(Cognito User Pool, App Client e parâmetros no Parameter Store), sem
custo adicional e sem nenhuma alteração aplicada à conta AWS sem
autorização prévia explícita do usuário.

## Contexto

Levantamento feito diretamente na conta AWS (`648443184523`,
`us-east-1`) antes de escrever esta spec:

- **DynamoDB**: já está 100% coberto pelo Terraform. `terraform plan`
  contra o state remoto (bucket `gastosapp-terraform-state-648443184523`)
  não acusa nenhuma diferença — a tabela `GastosApp`, o `GSI1` e o `GSI2`
  batem exatamente com `backend/infra/terraform/dynamodb.tf`. **Não há
  drift no DynamoDB** — a hipótese inicial de descasamento aqui não se
  confirmou; nenhuma ação é necessária nesta parte.
- **Cognito**: existe um User Pool (`us-east-1_cvKHaKo0g`, tier LITE) e
  um único App Client (`controle-gastos-spa`, público, sem client
  secret), ambos criados manualmente. O User Pool tem usuários reais
  cadastrados (3, conforme `EstimatedNumberOfUsers`), mas o usuário
  autorizou explicitamente excluí-los manualmente e recriar o pool do
  zero via Terraform, caso essa abordagem seja mais simples/barata do
  que importar o recurso existente — perda desses usuários (exigindo
  novo cadastro) é aceitável.
- **Parameter Store**: existem 3 parâmetros manuais sob `/GastosApp/`,
  todos tipo `String` (não `SecureString`): `/GastosApp/Cognito/ClientId`,
  `/GastosApp/Cognito/Region`, `/GastosApp/Cognito/UserPoolId`.
- **DynamoDB (dado, não schema)**: hoje há menos de 10 registros de
  despesas na tabela `GastosApp`. O usuário autorizou excluí-los caso
  isso facilite algum ajuste necessário — não é uma restrição a
  preservar dados aqui.
- Hoje `backend/infra/terraform/README.md` e `backend/infra/CLAUDE.md`
  documentam explicitamente que Cognito e Parameter Store ficam **fora**
  do Terraform "até serem migrados explicitamente" — esta feature é essa
  migração explícita, pedida pelo usuário.

## Requisitos de negócio / restrições

- **Custo zero**: a estratégia não pode introduzir nenhum recurso ou
  configuração que gere cobrança. Cognito User Pool tier LITE e
  parâmetros Standard do Parameter Store já estão dentro do free tier
  permanente e devem continuar assim.
- **Nenhuma ação na conta AWS sem autorização prévia explícita do
  usuário** — isso vale tanto para o desenho da estratégia quanto para
  qualquer execução futura (`terraform import`, `terraform apply`,
  exclusão manual de recursos). Nenhum `apply` roda sem confirmação
  explícita, comando por comando.
- **Import ou recriação são ambos aceitáveis** — a escolha entre
  importar o User Pool/App Client/parâmetros existentes ou excluí-los
  manualmente e recriá-los do zero via Terraform é uma decisão técnica
  (fica a cargo do `plan.md`, com base no que for mais simples), não uma
  restrição de negócio. O usuário já autorizou a perda dos 3 usuários
  cadastrados e dos registros de despesa (menos de 10) caso a
  recriação seja o caminho escolhido.
- Ainda assim, a ordem exata dos passos, o que será excluído
  manualmente e o ponto de não-retorno devem ficar explícitos no
  `plan.md` antes da execução — a autorização geral não dispensa
  confirmação explícita de cada comando destrutivo no momento de
  executá-lo.
- Nenhuma mudança de comportamento observável pela API (`/auth/*`,
  `/expenses/*`) — a autenticação continua funcionando com o mesmo User
  Pool/App Client, apenas passando a ser gerenciada via código.

## User Stories

**US1 — Cognito User Pool gerenciado pelo Terraform**
- Given o User Pool `us-east-1_cvKHaKo0g` já existe na conta, criado
  manualmente
- When a estratégia de reconciliação é aplicada (via `import` ou via
  exclusão manual + recriação, o que for mais simples)
- Then o Terraform passa a gerenciar um User Pool equivalente (mesma
  configuração observável: política de senha, atributos, MFA off,
  auto-verificação de email), com os usuários existentes preservados
  (se `import`) ou recriados do zero (se recriação, exigindo novo
  cadastro)

**US2 — App Client gerenciado pelo Terraform**
- Given o App Client `controle-gastos-spa` já existe, vinculado ao User
  Pool
- When a estratégia de reconciliação é aplicada
- Then o Terraform passa a gerenciar um App Client equivalente (mesmos
  fluxos de auth habilitados, sem client secret); se o `ClientId` mudar
  por causa de recriação, os parâmetros do Parameter Store e a
  configuração do backend (`CognitoOptions`) são atualizados juntos

**US3 — Parâmetros do Parameter Store gerenciados pelo Terraform**
- Given os 3 parâmetros `/GastosApp/Cognito/*` já existem
- When a estratégia de reconciliação é aplicada
- Then o Terraform passa a gerenciar esses mesmos parâmetros, com os
  mesmos valores atuais (nomes e valores inalterados)

**US4 — Nenhuma diferença após a reconciliação**
- Given todos os recursos acima já foram trazidos para o Terraform
- When se roda `terraform plan`
- Then o resultado é "No changes" — o código Terraform reflete
  exatamente o que existe na conta AWS

**US5 — Nenhuma execução sem aprovação explícita**
- Given qualquer comando que crie, altere ou destrua um recurso AWS
  (`terraform apply`, `terraform import`, exclusão manual via console/CLI)
- When esse comando está prestes a ser executado
- Then o usuário é consultado e precisa aprovar explicitamente antes da
  execução — nenhum comando desse tipo roda de forma autônoma

**US6 — Sem custo adicional**
- Given a estratégia final escolhida
- When ela é aplicada
- Then nenhum recurso novo gera cobrança — Cognito continua tier LITE,
  parâmetros continuam Standard, nenhum recurso pago (ex.: WAF, MFA SMS,
  domínio customizado) é introduzido

## Critérios de aceite

- [x] `terraform state list` (config principal, `backend/infra/terraform/`)
      passa a incluir o User Pool, o App Client e os 3 parâmetros do
      Parameter Store
- [x] `terraform plan` após a reconciliação retorna "No changes" contra
      a conta AWS real
- [x] Estratégia final: Cognito **recriado** (`us-east-1_yCZfxCLZY`,
      App Client `66m7eic6ef1imufkasiu3vrlir`), Parameter Store
      **importado**. Os 3 parâmetros foram atualizados com os novos
      `ClientId`/`UserPoolId`; validado manualmente que
      `POST /auth/register` → `POST /auth/login` → `GET /auth/me`
      funcionam de ponta a ponta contra o novo pool, sem nenhuma
      mudança no código .NET (`CognitoOptions` lido dinamicamente do
      Parameter Store)
- [x] `backend/infra/CLAUDE.md`, `backend/infra/terraform/README.md` e
      `backend/docs/constitution.md` atualizados para refletir que
      Cognito e Parameter Store agora são geridos por Terraform
- [x] Nenhum comando de `import`/`apply` foi executado sem aprovação
      explícita do usuário no momento da execução
- [x] Exclusão do User Pool antigo (`us-east-1_cvKHaKo0g`) — feita
      manualmente pelo usuário

## Status

Implementado conforme `plan.md`. `backend/infra/terraform/cognito.tf`
(User Pool `user-pool-gastos-app` + App Client `controle-gastos-spa`,
recriados do zero) e `parameter-store.tf` (3 parâmetros importados e
atualizados) criados e aplicados na conta AWS real. `terraform state
list` inclui os 6 recursos (DynamoDB + Cognito + Parameter Store) e
`terraform plan` final retorna "No changes". Validado manualmente:
registro, login e `/auth/me` funcionando de ponta a ponta contra o novo
User Pool (`us-east-1_yCZfxCLZY`).

O User Pool antigo (`us-east-1_cvKHaKo0g`) foi excluído manualmente pelo
usuário — Cognito e Parameter Store estão 100% sob Terraform, sem
recursos remanescentes fora do IaC.

**Achado fora do escopo desta feature**: durante a validação manual,
`POST /auth/login` de um usuário recém-registrado e ainda não confirmado
retornou 500 (erro interno) em vez de um erro de negócio mapeado — a
`UserNotConfirmedException` do Cognito não está sendo tratada em
`CognitoAuthService.LoginAsync` (`FEAT-01`). Não corrigido aqui por ser
um bug pré-existente de tratamento de erro, sem relação com a
reconciliação de infraestrutura desta feature.

## Fora do escopo deste FEAT

- Qualquer mudança no **schema/definição** da tabela DynamoDB (já sem
  drift, confirmado nesta spec) — a exclusão dos poucos registros de
  dados existentes (despesas), se necessária, é operacional e não altera
  a definição da tabela
- Novas funcionalidades de Cognito (MFA, domínio customizado, novos
  atributos de schema, novos App Clients)
- Mudança de tier do Cognito ou de tipo dos parâmetros (`SecureString`,
  `Advanced`)
- Infraestrutura de frontend
- Pipeline de CI/CD para aplicar Terraform automaticamente — execução
  continua manual, a partir da máquina do usuário, com aprovação passo a
  passo