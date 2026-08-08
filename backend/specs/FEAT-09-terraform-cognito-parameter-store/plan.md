# FEAT-09: Plano Técnico — Cognito e Parameter Store sob Terraform

## Estratégia geral: híbrida (por tipo de recurso)

Dado que a spec autoriza tanto `import` quanto recriação, a estratégia
escolhida é diferente por tipo de recurso, com base em qual é mais
simples/segura de implementar para cada um:

| Recurso | Estratégia | Por quê |
|---|---|---|
| Parameter Store (3 parâmetros) | **Import** | Recurso trivial (nome + tipo + valor único, sem blocos aninhados). Importar é tão simples quanto recriar, e não há nenhum dado em risco — não faz sentido apagar e recriar algo sem custo/risco de manter. |
| Cognito User Pool | **Recriar** | `aws_cognito_user_pool` tem muita configuração aninhada (schema attributes padrão do OIDC, account recovery, password policy, tiers). Fazer `terraform import` exigiria replicar exatamente todos esses detalhes no HCL até `terraform plan` bater "No changes" — processo frágil e demorado. Recriar do zero com a config desejada é mais simples e mais barato de implementar. Usuário já autorizou a perda dos 3 usuários cadastrados. |
| Cognito App Client | **Recriar** | Decorre da recriação do User Pool (o client pertence ao pool; ao recriar o pool, o client também precisa ser recriado). |

DynamoDB fica **fora deste plano** — confirmado sem drift na spec, nenhuma
ação necessária.

## Camadas afetadas

- **Infrastructure (Terraform, `backend/infra/terraform/`)**: únicos
  arquivos alterados/criados. Nenhuma mudança em `GastosApp.Api`,
  `GastosApp.Application`, `GastosApp.Domain` ou `GastosApp.Infrastructure`
  (código .NET) — o backend já lê `Region`/`UserPoolId`/`ClientId`
  dinamicamente do Parameter Store (`AwsParameterStoreExtensions`,
  `CognitoOptions`), então uma troca de valores nos parâmetros não exige
  deploy de código novo.
- **Nenhuma mudança de contrato de API** — endpoints `/auth/*` e
  `/expenses/*` continuam iguais; o único efeito observável é que, após
  o cutover, tokens emitidos pelo User Pool antigo passam a ser
  inválidos (esperado, já tratado pela validação JWT existente → 401).

## Recursos AWS afetados

Novos arquivos Terraform em `backend/infra/terraform/`:

### `cognito.tf` (recursos novos, recriados do zero)

`aws_cognito_user_pool` "main":
- `name = "user-pool-gastos-app"` (confirmado pelo usuário, substitui o
  nome auto-gerado atual `"User pool - ue86ti"`)
- `username_attributes = ["email"]`
- `auto_verified_attributes = ["email"]`
- `password_policy`: `minimum_length=8`, `require_uppercase=true`,
  `require_lowercase=true`, `require_numbers=true`,
  `require_symbols=true`, `temporary_password_validity_days=7`
  (replica exatamente a política atual)
- `mfa_configuration = "OFF"`
- `deletion_protection = "ACTIVE"` (mesmo valor atual — evita exclusão
  acidental futura via `terraform destroy`)
- `account_recovery_setting`: `verified_email` (prioridade 1),
  `verified_phone_number` (prioridade 2) — mesmo default atual
- `schema` para o atributo `email` (`required=true`, `mutable=true`) —
  necessário porque é `username_attribute`; os demais atributos padrão
  do OIDC (profile, address, given_name, etc.) não precisam ser
  declarados: o Cognito os adiciona automaticamente com os defaults já
  observados no pool atual
- **Ponto a verificar na implementação**: confirmar se o provider
  `hashicorp/aws` `~> 5.0` (instalado: `5.100.0`) expõe algum argumento
  para pedir o tier `LITE` explicitamente. Se não expuser, aceitar o
  tier padrão que a API atribuir — ambos os tiers hoje disponíveis
  (`LITE`/`ESSENTIALS`) são gratuitos para o volume de uso deste
  projeto, então não viola o requisito de custo zero.

`aws_cognito_user_pool_client` "spa":
- `name = "controle-gastos-spa"` (mantém nome atual)
- `user_pool_id = aws_cognito_user_pool.main.id`
- `generate_secret = false` (client público, sem secret — igual ao atual)
- `explicit_auth_flows = ["ALLOW_REFRESH_TOKEN_AUTH", "ALLOW_USER_AUTH", "ALLOW_USER_PASSWORD_AUTH", "ALLOW_USER_SRP_AUTH"]`
- `supported_identity_providers = ["COGNITO"]`
- `allowed_oauth_flows = ["code"]`
- `allowed_oauth_scopes = ["email", "openid", "phone"]`
- `allowed_oauth_flows_user_pool_client = true`
- `callback_urls = ["https://d84l1y8p4kdic.cloudfront.net"]` — placeholder
  (domínio de exemplo da AWS, não é um recurso real do projeto).
  Confirmado pelo usuário: manter esse valor por enquanto, já que o
  frontend ainda não existe.
- `prevent_user_existence_errors = "ENABLED"`
- `enable_token_revocation = true`
- `access_token_validity=60`, `id_token_validity=60`,
  `refresh_token_validity=5`, `token_validity_units { access_token="minutes", id_token="minutes", refresh_token="days" }`
- `auth_session_validity = 3`

### `parameter-store.tf` (recursos importados, valores atualizados)

Três `aws_ssm_parameter`, tipo `String`, todos sob `/GastosApp/Cognito/`:
- `user_pool_id`: `name = "/GastosApp/Cognito/UserPoolId"`,
  `value = aws_cognito_user_pool.main.id`
- `client_id`: `name = "/GastosApp/Cognito/ClientId"`,
  `value = aws_cognito_user_pool_client.spa.id`
- `region`: `name = "/GastosApp/Cognito/Region"`,
  `value = var.aws_region`

Esses três recursos são trazidos por **`terraform import`** (ligando o
nome do parâmetro já existente ao novo bloco de recurso). Depois do
import, o `value` no state ainda será o valor manual atual — o
`terraform apply` seguinte atualiza o valor para apontar ao novo
Pool/Client (update in-place, sem recriação, já que `name`/`type` não
mudam).

## Ordem de execução

Passos 1–7 são executados por mim (Claude Code), cada um com
confirmação explícita antes de rodar qualquer comando que crie ou altere
recursos. **O passo 8 (exclusão do recurso antigo) é feito inteiramente
pelo usuário, fora de qualquer comando que eu rode — confirmado
explicitamente: "não exclua nada da AWS".**

1. `terraform plan` mostrando a criação do novo User Pool + App Client
   (nenhum recurso existente é tocado ainda) → **usuário aprova antes do
   `apply`**
2. `terraform apply` cria o novo User Pool + App Client — nesse ponto o
   User Pool antigo (`us-east-1_cvKHaKo0g`) continua existindo e
   funcionando normalmente, sem interrupção
3. `terraform import` dos 3 parâmetros existentes para o state (liga os
   nomes atuais aos novos blocos de recurso — ainda sem alterar valores)
4. `terraform plan` mostrando a atualização dos 3 valores para os novos
   IDs → **usuário aprova antes do `apply`**
5. `terraform apply` atualiza os 3 parâmetros com `UserPoolId`/`ClientId`
   do novo pool
6. Reiniciar a API local (`dotnet run`) para recarregar `CognitoOptions`
   — hoje o binding usa `IOptions<CognitoOptions>` (singleton resolvido
   uma vez), então mesmo com `ReloadAfter = 5min` no
   `AddSystemsManager`, um restart é necessário para os novos valores
   valerem de fato
7. Validar manualmente: registrar um usuário de teste no novo pool
   (`POST /auth/register` + `/auth/login`) e confirmar que autentica
   com sucesso

**A partir daqui, a execução sai do meu escopo:**

8. O **usuário**, quando quiser, exclui manualmente o User Pool antigo
   (`us-east-1_cvKHaKo0g`) — isso remove junto o App Client antigo e os
   3 usuários antigos (são filhos do pool). Nota importante para essa
   exclusão: o pool atual tem `DeletionProtection = ACTIVE`, então é
   preciso desativar essa proteção primeiro (console ou
   `aws cognito-idp update-user-pool --deletion-protection INACTIVE`)
   antes que `delete-user-pool` funcione.
9. Depois de o usuário confirmar que excluiu o pool antigo, um
   `terraform plan` final deve retornar "No changes", confirmando que
   Cognito + Parameter Store estão 100% sob Terraform e sem drift — este
   passo posso rodar a pedido do usuário, mas não antes disso.

Manter o pool antigo ativo indefinidamente não gera custo adicional
(mesmo tier gratuito, sem uso) — não há pressa nem necessidade de eu
sugerir quando excluí-lo.

## Documentação a atualizar

- `backend/infra/terraform/README.md`: adicionar seção descrevendo
  `cognito.tf`/`parameter-store.tf` e que Cognito/Parameter Store agora
  são geridos por Terraform
- `backend/infra/CLAUDE.md`: remover a frase que diz que Cognito e
  Parameter Store ficam fora do Terraform "até serem migrados
  explicitamente" — a migração é esta feature
- `backend/docs/constitution.md`: a regra imutável "Cognito e Parameter
  Store já existem, provisionados manualmente... permanecem fora do
  Terraform até serem migrados explicitamente" deixa de valer após esta
  feature — atualizar o texto

## Mapeamento de erros

Não aplicável — não há mudança de contrato de API nem novo fluxo de
erro. O único efeito é operacional: tokens do pool antigo, se ainda em
uso no momento da exclusão (passo 8), passam a falhar a validação JWT e
retornam 401 — comportamento já existente (`FEAT-01`), sem necessidade
de código novo.

## Decisões confirmadas pelo usuário

1. **Nome do User Pool**: `"user-pool-gastos-app"` (em vez do
   auto-gerado `"User pool - ue86ti"`)
2. **`callback_urls` do App Client**: mantém o placeholder
   `https://d84l1y8p4kdic.cloudfront.net` por enquanto
3. **Exclusão do recurso antigo**: fica **inteiramente a cargo do
   usuário**, fora de qualquer comando que eu rode — eu não excluo nada
   na AWS neste FEAT, em nenhum momento. Passos 1–7 (criação do novo
   pool/client, import e atualização dos parâmetros, validação) ainda
   exigem aprovação explícita antes de cada `apply`/`import`.