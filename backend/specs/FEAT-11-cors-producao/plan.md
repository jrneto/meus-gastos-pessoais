# Plan — FEAT-11: CORS para o frontend de produção

Referência: [`spec.md`](./spec.md). Segue `backend/docs/constitution.md`
e `backend/infra/CLAUDE.md`. Estilo de execução em etapas aprovadas
(plan → apply, uma decisão de cada vez) segue o mesmo padrão já usado em
`backend/specs/FEAT-09-terraform-cognito-parameter-store/plan.md`.

## Contexto técnico levantado (antes de qualquer decisão)

Hoje existem **duas** camadas de CORS independentes, e nenhuma das duas
está pronta para produção:

1. **Aplicação (`Program.cs`)** — já lê `Cors:AllowedOrigins` (array)
   via `IConfiguration`, populado por `appsettings.Development.json`
   (`["http://localhost:5173"]`) em dev e por nada em produção
   (`appsettings.json` tem `[]`, não existe
   `appsettings.Production.json`).
2. **API Gateway (Terraform, `api-gateway.tf`)** — `cors_configuration`
   do `aws_apigatewayv2_api`, hoje `allow_origins = [var.frontend_origin]`,
   uma variável **string única** com placeholder
   `http://localhost:4200` (sobra da suposição inicial de frontend
   Angular).

Uma requisição do browser só passa se **as duas** camadas permitirem a
origem — por isso as duas precisam mudar.

**Achado importante sobre o Parameter Store**: `AddAwsParameterStore()`
(`GastosApp.Infrastructure/Configuration/AwsParameterStoreExtensions.cs`)
já roda em **todo ambiente, inclusive dev local** (só pula em
`"Testing"`), lendo `/GastosApp/` inteiro e mapeando `/` → `:` nas
chaves — ou seja, um parâmetro `/GastosApp/Cors/AllowedOrigins/0`
cairia exatamente na mesma chave de configuração
(`Cors:AllowedOrigins:0`) já usada por `appsettings.Development.json`,
e por ordem de registro dos *configuration providers* o Parameter
Store **sobrescreveria** o valor de dev (`http://localhost:5173`) —
quebrando o dev local. Confirmado com o usuário: usar uma **chave nova**
(`Cors:ProductionOrigins`), alimentada só pelo Parameter Store, e somar
as duas listas no código, em vez de reaproveitar `Cors:AllowedOrigins`.

## Camadas afetadas

- **Api** (`Program.cs`): pequeno ajuste na leitura de origens do CORS
  (soma duas listas em vez de uma)
- **Infrastructure**: nenhuma mudança de código — `AddAwsParameterStore()`
  já é genérico, nenhuma classe nova necessária
- **Infra (Terraform)**: `variables.tf` (`frontend_origin` string →
  `frontend_origins` list), `api-gateway.tf` (usa a lista),
  `parameter-store.tf` (2 novos parâmetros)

## Contratos técnicos

### `src/GastosApp.Api/Program.cs` (ajuste)
```csharp
// Origens do frontend liberadas via configuração: "Cors:AllowedOrigins"
// (appsettings.Development.json em dev local) + "Cors:ProductionOrigins"
// (só Parameter Store, produção) — chaves separadas de propósito, para
// o valor de produção nunca sobrescrever o de dev local (as duas
// convivem no mesmo Parameter Store /GastosApp/, lido em todo
// ambiente).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var productionOrigins = builder.Configuration.GetSection("Cors:ProductionOrigins").Get<string[]>() ?? [];
var corsOrigins = allowedOrigins.Concat(productionOrigins).ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});
```
Nenhuma outra parte do `Program.cs` muda. `appsettings.json` e
`appsettings.Development.json` não precisam de alteração — `Cors:AllowedOrigins`
continua exatamente como está hoje.

### `backend/infra/terraform/parameter-store.tf` (acréscimo)
```hcl
resource "aws_ssm_parameter" "cors_production_origin_0" {
  name  = "/GastosApp/Cors/ProductionOrigins/0"
  type  = "String"
  value = "https://jrnexpenses.com"
}

resource "aws_ssm_parameter" "cors_production_origin_1" {
  name  = "/GastosApp/Cors/ProductionOrigins/1"
  type  = "String"
  value = "https://www.jrnexpenses.com"
}
```
Recursos **novos** (não existem parâmetros de CORS manuais hoje) —
diferente dos parâmetros do Cognito na FEAT-09, não precisa de
`terraform import`, é criação direta.

### `backend/infra/terraform/variables.tf` (ajuste)
```hcl
variable "frontend_origins" {
  description = "Origens (URLs) do frontend de produção permitidas no CORS do API Gateway."
  type        = list(string)
  default     = ["https://jrnexpenses.com", "https://www.jrnexpenses.com"]
}
```
Substitui a variável `frontend_origin` (string única, placeholder
`http://localhost:4200`) — grep confirma que não é usada em mais nenhum
lugar além de `api-gateway.tf`.

### `backend/infra/terraform/api-gateway.tf` (ajuste)
```hcl
cors_configuration {
  allow_origins = var.frontend_origins
  allow_methods = ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
  allow_headers = ["Authorization", "Content-Type"]
}
```
Só a linha `allow_origins` muda (de `[var.frontend_origin]` para
`var.frontend_origins`, já uma lista). Atributo de um recurso já
existente (`aws_apigatewayv2_api.main`) — o provider AWS atualiza
`cors_configuration` in-place, sem recriar a API Gateway.

## Decisões técnicas confirmadas

- **Chave de configuração separada (`Cors:ProductionOrigins`) para as
  origens de produção**, em vez de reaproveitar `Cors:AllowedOrigins`
  via Parameter Store — confirmado com o usuário, evita que o Parameter
  Store (que roda em todo ambiente, inclusive dev local) sobrescreva o
  `http://localhost:5173` de desenvolvimento. Custo: ~3 linhas a mais no
  `Program.cs`; benefício: zero risco de regressão no ambiente local, e
  zero acoplamento entre a lista de dev e a de produção.
- **Efeito colateral aceito, e é o esperado**: como o Parameter Store é
  compartilhado entre ambientes (mesmo padrão já usado pelo Cognito,
  sem separação por ambiente), o dev local também vai enxergar
  `Cors:ProductionOrigins` e permitir CORS de `jrnexpenses.com`/`www`
  além do `localhost:5173` — inofensivo (CORS é aplicado pelo navegador
  do cliente, não é um controle de acesso do servidor), mas registrado
  aqui para não ser uma surpresa.
- **`frontend_origins` já nasce com os 2 domínios reais como default**
  (não um placeholder a preencher depois) — mesmo padrão da FEAT-09,
  que já colocou os valores reais pretendidos direto no `.tf`, sujeitos
  a `terraform plan`/aprovação antes de qualquer `apply`.
- **Sem `terraform import`** — diferente do Cognito na FEAT-09, os
  parâmetros de CORS não existem hoje em lugar nenhum, então são
  criação direta via `aws_ssm_parameter`, sem estado prévio a conciliar.
- **Nenhuma mudança de IAM necessária** — a policy da Lambda já
  concede `ssm:GetParametersByPath` sobre `arn:...:parameter/GastosApp/*`
  (`lambda.tf`), que já cobre os 2 parâmetros novos por estarem sob o
  mesmo prefixo.

## Recursos AWS afetados

- **2 novos parâmetros no Parameter Store**: `/GastosApp/Cors/ProductionOrigins/0`
  e `/1` (tipo `String`, sem criptografia — mesma classificação dos
  parâmetros do Cognito, não são segredos)
- **1 recurso existente atualizado**: `cors_configuration` do
  `aws_apigatewayv2_api.main` (API Gateway já provisionado na FEAT-10)
  — atualização in-place, sem recriação
- Nenhuma tabela, índice, Lambda ou App Client novo

## Mapeamento de erros

Não aplicável — CORS não introduz nem altera nenhum status code ou
corpo de resposta de negócio. O único efeito observável é a presença ou
ausência do cabeçalho `Access-Control-Allow-Origin` (aplicado pelo
navegador do cliente, não pela API).

## Ordem de execução

Cada etapa que cria/altera algo na AWS é aprovada individualmente antes
de rodar — mesmo padrão da FEAT-09.

1. Implementar o ajuste em `Program.cs` (código puro, sem efeito até
   deploy) + rodar a suíte completa (`dotnet test`) para garantir que
   nada quebrou
2. `terraform plan` mostrando: criação dos 2 parâmetros SSM +
   atualização do `cors_configuration` do API Gateway (nenhum outro
   recurso tocado) → **usuário aprova antes do `apply`**
3. `terraform apply`
4. Rebuild + redeploy da Lambda (`./infra/lambda/build.sh` gera novo
   `function.zip`; `terraform apply` detecta o novo
   `source_code_hash` e atualiza o código da função) — **necessário**
   porque o ajuste do passo 1 só vale depois que a Lambda em produção
   rodar o binário novo; os parâmetros SSM sozinhos não têm efeito
   nenhum sem essa parte do código para lê-los → **usuário aprova antes
   do `apply` de redeploy**, mesmo sendo só atualização de código (não
   recria a função)
5. Validação manual: preflight (`curl -X OPTIONS` com
   `Origin: https://jrnexpenses.com` e `Origin: https://www.jrnexpenses.com`)
   contra a API em produção, confirmando `Access-Control-Allow-Origin`
   correto; e localmente (`dotnet run`), confirmar que
   `http://localhost:5173` continua funcionando sem regressão

## Documentação a atualizar

- `backend/docs/openapi.json`: **sem alteração** — CORS não é contrato
  de wire, já confirmado no `spec.md`, nenhuma regeneração necessária
- `backend/infra/terraform/README.md`: não precisa de nova seção
  (CORS já é coberto implicitamente pela seção de `api-gateway.tf`
  existente), mas vale acrescentar uma frase citando
  `parameter-store.tf` também guarda as origens de CORS de produção

## Pontos que precisam de confirmação antes do `/tasks`
Nenhum pendente — a estratégia de configuração (chave separada +
merge no código) já foi confirmada pelo usuário. As aprovações de
`terraform plan`/`apply` continuam acontecendo passo a passo durante a
execução (`/tasks`), não aqui.