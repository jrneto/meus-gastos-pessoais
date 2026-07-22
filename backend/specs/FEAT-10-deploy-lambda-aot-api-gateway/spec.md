# FEAT-10: Deploy da API em Lambda (Native AOT) + API Gateway

## Objetivo

Publicar a API do GastosApp na AWS como uma função Lambda compilada em
Native AOT (.NET 10), exposta publicamente via API Gateway (HTTP API),
provisionada via Terraform — encerrando a fase "só desenvolvimento
local" e colocando o backend acessível na internet para uso real (você
e sua esposa), como pré-requisito para o futuro frontend Angular também
ser publicado.

## Contexto

Hoje a API só roda localmente (`dotnet run`), conectada aos recursos
reais da AWS (Cognito, DynamoDB, Parameter Store — FEAT-09). O projeto
já tem a dependência `Amazon.Lambda.AspNetCoreServer.Hosting` no
`GastosApp.Api.csproj` e a flag `PublishAot` comentada — ou seja, o
suporte a Lambda foi previsto desde o início (`backend/docs/architecture.md`,
seção "Infraestrutura de produção"), mas nunca foi ligado nem
implantado.

Decisões já tomadas com o usuário para esta feature:
- **API Gateway HTTP API** (não REST API) — mais barato (~US$1/milhão
  de requisições vs. ~US$3,50/milhão) e suficiente para um proxy simples
  para a Lambda, com autenticação JWT nativa
- **Build Native AOT via Docker** (já instalado localmente) — necessário
  porque o binário precisa ser compilado para Linux (runtime Lambda),
  diferente do Windows usado em desenvolvimento
- **Segurança via autenticação, não por IP/WAF** — a API continua
  publicamente alcançável (como qualquer API HTTP GW/Function URL); a
  proteção real é o JWT do Cognito já exigido em `/expenses/*` (FEAT-01).
  Sem AWS WAF (tem custo fixo mensal, contraria o objetivo de custo
  zero). A proteção volumétrica básica (rede/transporte) já é coberta
  automaticamente e sem custo pelo **AWS Shield Standard**, incluído por
  padrão em todo recurso AWS — não requer nenhuma configuração
- **Throttling do API Gateway (sem custo adicional)** — o stage do HTTP
  API deve ter `rate limit`/`burst limit` configurados (5 req/s / 10 de
  rajada, suficiente para 2 usuários com folga), para que excesso de
  requisições receba `429` em vez de chegar à Lambda — limita tanto
  abuso quanto o pior cenário de custo por requisição
- Desenvolvimento e debug local **não mudam** — continuam via
  `dotnet run` direto no Windows; o build Docker/AOT só entra no
  momento de publicar uma nova versão para a AWS

## Requisitos de negócio / restrições

- **Custo**: usar apenas recursos dentro do free tier permanente da AWS
  sempre que possível (Lambda: 1M requisições + 400.000 GB-s de
  computação gratuitos por mês, para sempre). API Gateway HTTP API não
  tem free tier permanente nesta conta (já fora da janela de 12 meses),
  mas o custo por requisição é baixo o suficiente para ser desprezível
  no volume de uso de 2 pessoas — custo aceito explicitamente pelo
  usuário, diferente do "custo zero" estrito de outras features. Nenhum
  recurso com cobrança fixa mensal (ex.: NAT Gateway, WAF, Route53
  hosted zone) deve ser introduzido nesta feature.
- **Retenção de logs do CloudWatch limitada a 14 dias** — o log group
  criado automaticamente pela Lambda deve ter `retention_in_days = 14`
  (15 não é um valor aceito pelo CloudWatch Logs; 14 é o mais próximo)
  definido via Terraform (em vez do padrão "Never expire"), para não
  acumular armazenamento indefinidamente. Ingestão/armazenamento têm
  custo por GB fora do free tier de 12 meses desta conta, mas é
  desprezível no volume de uso de 2 pessoas — a retenção limitada evita
  crescimento sem limite ao longo dos anos, não elimina o custo por si
  só.
- **Native AOT obrigatório** — já é regra da constitution
  ("Buscar sempre o uso de Native AOT no .NET 10 para otimizar os cold
  starts do AWS Lambda")
- **Least privilege**: a role de execução da Lambda só pode ter permissão
  aos recursos que a aplicação realmente usa (tabela DynamoDB, os 3
  parâmetros do Parameter Store, as operações do Cognito já usadas por
  `CognitoAuthService`) — nada de permissões amplas (`*`)
- **Nenhuma mudança de contrato observável da API** — mesmos endpoints,
  mesmos request/response, mesmos códigos de status; a única mudança é
  *onde* a API roda e *qual URL* a expõe
- **CORS** restrito à origem do futuro frontend Angular (a origem exata
  — domínio ainda não definido — pode ficar como placeholder
  configurável, similar ao `callback_urls` do Cognito na FEAT-09)
- Nenhuma ação de infraestrutura (criação de recursos AWS, deploy) é
  executada sem aprovação explícita do usuário, comando a comando —
  mesma regra da FEAT-09
- Deploy continua **manual**, a partir da máquina do usuário — sem
  pipeline de CI/CD nesta feature (decisão já registrada em
  `backend/infra/CLAUDE.md` desde a FEAT-09)

## User Stories

**US1 — API acessível publicamente via HTTPS**
- Given a Lambda e o API Gateway provisionados
- When qualquer cliente HTTP faz uma requisição para a URL pública do
  API Gateway
- Then a requisição chega até a aplicação .NET rodando na Lambda, com o
  mesmo comportamento observável de hoje (rodando localmente)

**US2 — Autenticação continua sendo a proteção real**
- Given a API publicada
- When um cliente chama `/expenses/*` sem um JWT válido do Cognito
- Then recebe 401, exatamente como acontece hoje rodando localmente —
  não há nenhum controle de acesso adicional por IP

**US3 — Cold start reduzido via Native AOT**
- Given o pacote de deploy compilado em Native AOT
- When a Lambda é invocada após um período de inatividade (cold start)
- Then o tempo de inicialização é sensivelmente menor do que seria com
  o runtime JIT padrão do .NET (comparação qualitativa, sem SLA
  numérico definido nesta feature)

**US4 — Deploy reproduzível a partir do Windows**
- Given o Docker instalado na máquina do usuário
- When o usuário roda o processo de build/publish documentado
- Then o artefato Native AOT compatível com o runtime Lambda
  (`provided.al2023` ou equivalente) é gerado com sucesso, sem exigir
  uma máquina Linux dedicada

**US5 — Infraestrutura sob Terraform, com aprovação explícita**
- Given os novos recursos necessários (Lambda, API Gateway, IAM role,
  etc.)
- When esses recursos precisam ser criados ou alterados na AWS
- Then cada `terraform plan`/`apply` é apresentado ao usuário e só
  executado após aprovação explícita — nenhuma criação automática

**US6 — Nenhuma regressão de comportamento**
- Given a suíte de testes existente (unitários, componente, integração)
- When a aplicação passa a rodar via `Amazon.Lambda.AspNetCoreServer.Hosting`
  em vez de Kestrel puro
- Then todos os testes continuam passando sem alteração de
  comportamento de negócio

## Critérios de aceite

- [x] A API responde publicamente via a URL do API Gateway (HTTP API),
      com os mesmos endpoints e contratos de hoje
- [x] `POST /auth/register`, `POST /auth/login`, `GET /auth/me` e os
      endpoints de `/expenses/*` funcionam de ponta a ponta contra a
      Lambda publicada (mesmo teste manual usado na FEAT-09)
- [x] Requisições sem JWT válido a `/expenses/*` retornam 401 na Lambda
      publicada, igual ao comportamento local
- [x] O artefato de deploy é compilado em Native AOT (não JIT padrão)
- [x] A role de execução da Lambda tem apenas as permissões necessárias
      (DynamoDB da tabela `GastosApp`, os 3 parâmetros do Parameter
      Store, as operações do Cognito já usadas)
- [x] CORS configurado restringindo a origem (placeholder do futuro
      domínio do Angular, mesma lógica do `callback_urls` da FEAT-09)
- [x] Nenhum recurso com custo fixo mensal (WAF, NAT Gateway, Route53
      hosted zone) foi introduzido
- [x] O log group do CloudWatch da Lambda tem `retention_in_days = 14`
      definido via Terraform
- [x] O stage do API Gateway tem `throttling_rate_limit`/
      `throttling_burst_limit` configurados; uma rajada de requisições
      acima do limite recebe `429`
- [x] `terraform plan` final não acusa diferenças entre o código e os
      recursos reais criados
- [x] Suíte completa de testes (`dotnet test`) continua passando sem
      alteração após a mudança de hosting
- [x] Nenhum comando de `apply`/deploy foi executado sem aprovação
      explícita do usuário no momento da execução

## Status

Implementado. API publicada em `https://dhb1xc3bsi.execute-api.us-east-1.amazonaws.com/`
como Lambda Native AOT (`gastos-app-api`, `provided.al2023`, 256MB/10s)
atrás de um API Gateway HTTP API (`api-gateway.tf`), sem autorizador
JWT no Gateway. Validado manualmente de ponta a ponta: registro, login,
`/auth/me`, criação e listagem de despesas; 401 sem token; 429 sob
rajada (2 de 60 requisições simultâneas). Suíte completa
(`dotnet test`) passa: 176/176.

**Problemas reais encontrados e corrigidos durante a implementação**
(relevantes para qualquer trabalho futuro envolvendo Native AOT nesta
Lambda):

1. **`typeof(string?)` inválido em `[AsParameters]`** — bug conhecido do
   Request Delegate Generator do ASP.NET Core com propriedades nullable
   de tipo referência. Corrigido trocando `string?` por `string` com
   default `""` no `GetExpensesRequest` (FEAT-06), mapeando `""` → `null`
   no endpoint.
2. **Restore usando fallback do Windows dentro do container** — metadados
   gerados por uma sessão anterior do Visual Studio no host vazavam
   para o restore do Docker. Contornado com
   `-p:NuGetPackageFolders=/root/.nuget/packages` explícito no build.
3. **Incompatibilidade de glibc** — o binário AOT compilado na imagem
   oficial do SDK .NET (base Ubuntu) não roda no runtime
   `provided.al2023` da Lambda (Amazon Linux 2023, glibc mais antiga).
   Corrigido buildando numa imagem `amazonlinux:2023` (mesma base do
   runtime) em vez da imagem Microsoft.
4. **`dotnet` CLI precisa de ICU** — o Amazon Linux 2023 não tem
   `libicu` por padrão; corrigido com
   `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` no estágio de build.
5. **`System.Text.Json` reflection-based não funciona em AOT** — precisou
   de um `JsonSerializerContext` cobrindo todos os DTOs de request/response
   da API (`AppJsonSerializerContext`), incluindo trocar um tipo anônimo
   (`new { userId, email, name }`) por um record nomeado
   (`UserInfoResponse`), já que tipos anônimos não podem ser anotados
   para source generation.
6. **Serialização do próprio evento Lambda/API Gateway** — o
   `Amazon.Lambda.AspNetCoreServer.Hosting` também precisa de um
   serializer AOT-friendly para `APIGatewayHttpApiV2ProxyRequest`/
   `Response` (`LambdaEventJsonSerializerContext` +
   `SourceGeneratorLambdaJsonSerializer<T>`), distinto do contexto dos
   DTOs da aplicação.
7. **`Configure<T>(IConfiguration)`/`.Get<T>()` falham silenciosamente em
   AOT** — o `ConfigurationBinder` baseado em reflection não lançava
   nenhum erro, só deixava `CognitoOptions` inteiramente nulo dentro da
   Lambda (bug mais difícil de diagnosticar desta lista, já que não há
   exception nem warning de trimming visível em runtime). Corrigido
   substituindo tanto o binding de `CognitoOptions` quanto a leitura do
   Parameter Store (`Amazon.Extensions.Configuration.SystemsManager`,
   também baseada em reflection) por leitura manual explícita via
   `AWSSDK.SimpleSystemsManagement`, sem nenhum binder automático.

## Fora do escopo deste FEAT

- Domínio customizado / certificado ACM / Route53 (tem custo fixo
  mensal de hosted zone; URL padrão do API Gateway é suficiente por
  enquanto)
- Pipeline de CI/CD (GitHub Actions ou similar) para deploy automático
  — deploy continua manual, a partir da máquina do usuário
- Deploy do frontend Angular (feature futura separada, fora do escopo
  do backend)
- AWS WAF, rate limiting avançado, API keys/usage plans
- Múltiplos ambientes (staging/produção) — apenas um ambiente único por
  enquanto
- Observabilidade avançada (X-Ray, dashboards customizados) além do
  CloudWatch Logs padrão já emitido pelo Serilog
- Rollback automatizado / versionamento de Lambda com aliases — deploy
  substitui a versão publicada diretamente
